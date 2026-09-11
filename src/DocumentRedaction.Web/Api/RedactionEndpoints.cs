using System.Text.Json;
using DocumentRedaction.Core.Model;
using DocumentRedaction.Documents;
using DocumentRedaction.Web.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace DocumentRedaction.Web.Api;

public static class RedactionEndpoints
{
    public const string ReportHeader = "X-Redaction-Report";
    public const string TotalHeader = "X-Redaction-Total";

    private static readonly JsonSerializerOptions HeaderJson = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapRedactionApi(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api").WithTags("Redaction");

        group.MapGet("/categories", GetCategories)
            .WithName("GetCategories")
            .WithSummary("Lists the redaction categories and the kinds of information each one covers.");

        group.MapPost("/redact", Redact)
            .WithName("RedactDocument")
            .WithSummary("Redacts an uploaded document and returns the redacted file.")
            .WithDescription("Multipart form: file (required), categories, excludedKinds, customTerms, placeholderFormat, customTermsAreCaseSensitive. Counts by kind are returned in the X-Redaction-Report header.")
            .DisableAntiforgery();

        group.MapPost("/redact/summary", Summarize)
            .WithName("SummarizeRedaction")
            .WithSummary("Redacts an uploaded document and returns only the counts, not the file.")
            .DisableAntiforgery();

        return endpoints;
    }

    private static Ok<IReadOnlyList<CategoryDto>> GetCategories() =>
        TypedResults.Ok<IReadOnlyList<CategoryDto>>(RedactionCategories.All.Select(CategoryDto.From).ToList());

    private static async Task<Results<FileContentHttpResult, ValidationProblem, ProblemHttpResult>> Redact(
        HttpContext httpContext,
        IDocumentRedactionService service,
        ProcessingEstimator estimator,
        IOptions<RedactionSettings> settings,
        CancellationToken cancellationToken)
    {
        RunOutcome outcome = await Run(httpContext.Request, service, estimator, settings.Value, cancellationToken);

        return outcome switch
        {
            { Document: { } document } => WithReportHeaders(httpContext, document),
            { Validation: { } problem } => problem,
            { Problem: { } problem } => problem,
            _ => throw new InvalidOperationException("Unexpected outcome."),
        };
    }

    private static async Task<Results<Ok<RedactionSummaryDto>, ValidationProblem, ProblemHttpResult>> Summarize(
        HttpContext httpContext,
        IDocumentRedactionService service,
        ProcessingEstimator estimator,
        IOptions<RedactionSettings> settings,
        CancellationToken cancellationToken)
    {
        RunOutcome outcome = await Run(httpContext.Request, service, estimator, settings.Value, cancellationToken);

        return outcome switch
        {
            { Document: { } document } => TypedResults.Ok(new RedactionSummaryDto(document.FileName, document.ContentType, document.Content.Length, ReportDto.From(document.Report, document.Warnings))),
            { Validation: { } problem } => problem,
            { Problem: { } problem } => problem,
            _ => throw new InvalidOperationException("Unexpected outcome."),
        };
    }

    /// <summary>Exactly one of the three is set.</summary>
    private sealed record RunOutcome(RedactedDocument? Document, ValidationProblem? Validation, ProblemHttpResult? Problem)
    {
        public static implicit operator RunOutcome(RedactedDocument document) => new(document, null, null);

        public static implicit operator RunOutcome(ValidationProblem problem) => new(null, problem, null);

        public static implicit operator RunOutcome(ProblemHttpResult problem) => new(null, null, problem);
    }

    /// <summary>Form reading, validation, size check, processing and error mapping shared by both endpoints.</summary>
    private static async Task<RunOutcome> Run(
        HttpRequest httpRequest,
        IDocumentRedactionService service,
        ProcessingEstimator estimator,
        RedactionSettings settings,
        CancellationToken cancellationToken)
    {
        if (!httpRequest.HasFormContentType)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [RedactionFormBinder.FileField] = ["Send the document as multipart/form-data."],
            });
        }

        IFormCollection form;
        try
        {
            form = await httpRequest.ReadFormAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidDataException || ex is BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge })
        {
            // The form reader and Kestrel both reject bodies over their limits before we can check the file length.
            return DocumentProblems.TooLarge(settings.MaxUploadBytes);
        }

        (IFormFile? file, RedactionRequest request) = RedactionFormBinder.Read(form);
        RedactionRequestParser.ParseResult parsed = RedactionRequestParser.Parse(request);
        Dictionary<string, string[]> errors = new(parsed.Errors, StringComparer.Ordinal);
        if (file is null || file.Length == 0)
        {
            errors[RedactionFormBinder.FileField] = ["A non-empty file is required."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        if (file!.Length > settings.MaxUploadBytes)
        {
            return DocumentProblems.TooLarge(settings.MaxUploadBytes);
        }

        try
        {
            await using Stream stream = file.OpenReadStream();
            return await estimator.MeasureAsync(file.Length, () => service.RedactAsync(stream, file.FileName, file.ContentType, parsed.Options!, cancellationToken));
        }
        catch (DocumentRedactionException ex)
        {
            return DocumentProblems.FromException(ex);
        }
    }

    private static FileContentHttpResult WithReportHeaders(HttpContext httpContext, RedactedDocument document)
    {
        httpContext.Response.Headers[ReportHeader] = JsonSerializer.Serialize(ReportDto.From(document.Report, document.Warnings), HeaderJson);
        httpContext.Response.Headers[TotalHeader] = document.Report.Total.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return TypedResults.Bytes(document.Content, document.ContentType, document.FileName);
    }
}
