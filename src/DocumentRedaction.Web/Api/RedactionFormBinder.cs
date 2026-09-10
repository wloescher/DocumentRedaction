using Microsoft.Extensions.Primitives;

namespace DocumentRedaction.Web.Api;

/// <summary>
/// Reads the multipart form for the redact endpoints. Values may repeat ("categories=pii&amp;categories=hipaa")
/// or be comma-separated inside one value; both shapes reach <see cref="RedactionRequestParser"/> unchanged.
/// </summary>
public static class RedactionFormBinder
{
    public const string FileField = "file";
    public const string CategoriesField = "categories";
    public const string ExcludedKindsField = "excludedKinds";
    public const string CustomTermsField = "customTerms";
    public const string PlaceholderFormatField = "placeholderFormat";
    public const string CaseSensitiveField = "customTermsAreCaseSensitive";

    public static (IFormFile? File, RedactionRequest Request) Read(IFormCollection form)
    {
        ArgumentNullException.ThrowIfNull(form);

        return (
            form.Files.GetFile(FileField),
            new RedactionRequest(
                Values(form, CategoriesField),
                Values(form, ExcludedKindsField),
                Values(form, CustomTermsField),
                form.TryGetValue(PlaceholderFormatField, out StringValues format) ? format.ToString() : null,
                form.TryGetValue(CaseSensitiveField, out StringValues flag) && bool.TryParse(flag.ToString(), out bool sensitive) && sensitive));
    }

    private static List<string> Values(IFormCollection form, string field) =>
        form.TryGetValue(field, out StringValues values) ? values.Where(value => value is not null).Select(value => value!).ToList() : [];
}
