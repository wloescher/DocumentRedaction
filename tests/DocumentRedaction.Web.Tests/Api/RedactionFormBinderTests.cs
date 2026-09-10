using DocumentRedaction.Web.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace DocumentRedaction.Web.Tests.Api;

public class RedactionFormBinderTests
{
    private static FormCollection Form(Dictionary<string, StringValues> fields, IFormFile? file = null) =>
        new FormCollection(fields, file is null ? null : new FormFileCollection { file });

    [Fact]
    public void Reads_repeated_and_scalar_fields()
    {
        FormFile file = new(new MemoryStream([1]), 0, 1, "file", "a.txt");
        IFormCollection form = Form(new Dictionary<string, StringValues>
        {
            ["categories"] = new(["pii", "hipaa"]),
            ["excludedKinds"] = "Date",
            ["customTerms"] = new(["a", "b\nc"]),
            ["placeholderFormat"] = "<{0}>",
            ["customTermsAreCaseSensitive"] = "true",
        }, file);

        (IFormFile? boundFile, RedactionRequest request) = RedactionFormBinder.Read(form);

        Assert.Same(file, boundFile);
        Assert.Equal(["pii", "hipaa"], request.Categories);
        Assert.Equal(["Date"], request.ExcludedKinds);
        Assert.Equal(["a", "b\nc"], request.CustomTerms);
        Assert.Equal("<{0}>", request.PlaceholderFormat);
        Assert.True(request.CustomTermsAreCaseSensitive);
    }

    [Fact]
    public void Missing_fields_become_empty_or_null()
    {
        (IFormFile? file, RedactionRequest request) = RedactionFormBinder.Read(Form([]));

        Assert.Null(file);
        Assert.Empty(request.Categories);
        Assert.Empty(request.ExcludedKinds);
        Assert.Empty(request.CustomTerms);
        Assert.Null(request.PlaceholderFormat);
        Assert.False(request.CustomTermsAreCaseSensitive);
    }

    [Theory]
    [InlineData("false")]
    [InlineData("yes")]
    [InlineData("")]
    public void Non_true_flag_values_are_false(string value)
    {
        (_, RedactionRequest request) = RedactionFormBinder.Read(Form(new Dictionary<string, StringValues> { ["customTermsAreCaseSensitive"] = value }));
        Assert.False(request.CustomTermsAreCaseSensitive);
    }
}
