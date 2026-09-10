using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DocumentRedaction.Tests.Fixtures;
using DocumentRedaction.Web.Api;
using Microsoft.AspNetCore.Mvc;

namespace DocumentRedaction.Web.Tests.Api;

public class RedactionApiTests : IClassFixture<RedactionApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;

    public RedactionApiTests(RedactionApiFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    private static byte[] Text(string value) => Encoding.UTF8.GetBytes(value);

    private Task<HttpResponseMessage> PostAsync(MultipartFormDataContent form, string path = "/api/redact") =>
        _client.PostAsync(new Uri(path, UriKind.Relative), form, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Categories_lists_every_category_with_kinds()
    {
        List<CategoryDto>? categories = await _client.GetFromJsonAsync<List<CategoryDto>>("/api/categories", Json, TestContext.Current.CancellationToken);

        Assert.NotNull(categories);
        Assert.Equal(["Pii", "Hipaa", "Financial", "Confidential"], categories.Select(c => c.Category));
        CategoryDto hipaa = categories.Single(c => c.Category == "Hipaa");
        Assert.Contains(hipaa.Kinds, k => k.Kind == "SocialSecurityNumber" && k.PlaceholderLabel == "SSN");
        Assert.Contains(hipaa.Kinds, k => k.Kind == "Date");
    }

    [Fact]
    public async Task Redacts_text_file_and_reports_in_headers()
    {
        using HttpResponseMessage response = await PostAsync(RedactionApiFixture.Form(Text("SSN 123-45-6789 mail a@b.co")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("input-redacted.txt", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Equal("SSN [REDACTED-SSN] mail [REDACTED-EMAIL]", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("2", response.Headers.GetValues(RedactionEndpoints.TotalHeader).Single());

        ReportDto? report = JsonSerializer.Deserialize<ReportDto>(response.Headers.GetValues(RedactionEndpoints.ReportHeader).Single(), Json);
        Assert.NotNull(report);
        Assert.Equal(2, report.Total);
        Assert.Equal(1, report.CountsByKind["SocialSecurityNumber"]);
        Assert.Equal(1, report.CountsByKind["EmailAddress"]);
    }

    [Fact]
    public async Task Redacts_word_file()
    {
        byte[] input = WordFixture.WithParagraphs("card 4111 1111 1111 1111");
        using HttpResponseMessage response = await PostAsync(RedactionApiFixture.Form(input, "memo.docx", "application/octet-stream", ["financial"]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", response.Content.Headers.ContentType?.MediaType);
        byte[] output = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["card [REDACTED-CREDIT-CARD]"], WordFixture.ReadParagraphs(output));
    }

    [Fact]
    public async Task Redacts_pdf_file()
    {
        byte[] input = PdfFixture.Build(["Phone (555) 123-4567 today"]);
        using HttpResponseMessage response = await PostAsync(RedactionApiFixture.Form(input, "scan.pdf", "application/pdf"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        byte[] output = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        Assert.Contains("[REDACTED-PHONE]", PdfFixture.ReadPageTexts(output)[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Summary_returns_counts_without_the_file()
    {
        using HttpResponseMessage response = await PostAsync(RedactionApiFixture.Form(Text("a@b.co and c@d.co")), "/api/redact/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        RedactionSummaryDto? summary = await response.Content.ReadFromJsonAsync<RedactionSummaryDto>(Json, TestContext.Current.CancellationToken);
        Assert.NotNull(summary);
        Assert.Equal("input-redacted.txt", summary.FileName);
        Assert.Equal(2, summary.Report.Total);
        Assert.Equal(2, summary.Report.CountsByKind["EmailAddress"]);
        Assert.True(summary.SizeBytes > 0);
    }

    [Fact]
    public async Task Custom_terms_excluded_kinds_and_placeholder_are_honoured()
    {
        MultipartFormDataContent form = RedactionApiFixture.Form(
            Text("Jane Doe born 01/02/1980, SSN 123-45-6789"),
            categories: ["hipaa"],
            excludedKinds: ["date"],
            customTerms: ["Jane Doe"],
            placeholderFormat: "<{0}>");
        using HttpResponseMessage response = await PostAsync(form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("<CUSTOM> born 01/02/1980, SSN <SSN>", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Comma_separated_categories_are_accepted()
    {
        using HttpResponseMessage response = await PostAsync(RedactionApiFixture.Form(Text("a@b.co 4111111111111111"), categories: ["pii,financial"]));
        Assert.Equal("[REDACTED-EMAIL] [REDACTED-CREDIT-CARD]", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Missing_file_is_a_validation_problem()
    {
        using HttpResponseMessage response = await PostAsync(RedactionApiFixture.Form(null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ValidationProblemDetails? problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Json, TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.Contains("file", problem.Errors.Keys);
    }

    [Fact]
    public async Task Non_form_body_is_a_validation_problem()
    {
        using StringContent body = new("{}", Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await _client.PostAsync(new Uri("/api/redact", UriKind.Relative), body, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Empty_file_is_a_validation_problem()
    {
        using HttpResponseMessage response = await PostAsync(RedactionApiFixture.Form([]));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_category_and_kind_are_reported_together()
    {
        using HttpResponseMessage response = await PostAsync(RedactionApiFixture.Form(Text("x"), categories: ["secret"], excludedKinds: ["Nope"]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ValidationProblemDetails? problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Json, TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.Contains("Categories", problem.Errors.Keys);
        Assert.Contains("ExcludedKinds", problem.Errors.Keys);
    }

    [Fact]
    public async Task No_categories_and_no_terms_is_rejected()
    {
        using HttpResponseMessage response = await PostAsync(RedactionApiFixture.Form(Text("x"), categories: []));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_placeholder_format_is_rejected()
    {
        using HttpResponseMessage response = await PostAsync(RedactionApiFixture.Form(Text("x"), placeholderFormat: "{0"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unsupported_extension_is_415()
    {
        using HttpResponseMessage response = await PostAsync(RedactionApiFixture.Form(Text("x"), "sheet.xlsx", "application/octet-stream"));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Json, TestContext.Current.CancellationToken);
        Assert.Contains(".docx", problem?.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Corrupt_document_is_422()
    {
        using HttpResponseMessage response = await PostAsync(RedactionApiFixture.Form([1, 2, 3], "broken.docx", "application/octet-stream"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Pdf_without_text_is_422_with_ocr_hint()
    {
        using HttpResponseMessage response = await PostAsync(RedactionApiFixture.Form(PdfFixture.BlankPage(), "scan.pdf", "application/pdf"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Json, TestContext.Current.CancellationToken);
        Assert.Contains("OCR", problem?.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Oversized_upload_is_413()
    {
        byte[] big = new byte[RedactionApiFixture.MaxUploadBytes + 1];
        Array.Fill(big, (byte)'a');
        using HttpResponseMessage response = await PostAsync(RedactionApiFixture.Form(big));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Upload_beyond_the_form_body_limit_is_still_413()
    {
        // Larger than MaxUploadBytes plus the 64 KB slack, so the multipart reader itself rejects it.
        byte[] huge = new byte[RedactionApiFixture.MaxUploadBytes + 128 * 1024];
        Array.Fill(huge, (byte)'a');
        using HttpResponseMessage response = await PostAsync(RedactionApiFixture.Form(huge));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task OpenApi_document_describes_the_endpoints()
    {
        string json = await _client.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        Assert.Contains("/api/redact", json, StringComparison.Ordinal);
        Assert.Contains("/api/categories", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Home_page_renders()
    {
        using HttpResponseMessage response = await _client.GetAsync("/", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Redact a document", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }
}
