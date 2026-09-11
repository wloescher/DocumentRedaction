using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Web.Api;

public sealed record KindDto(string Kind, string DisplayName, string PlaceholderLabel);

public sealed record CategoryDto(string Category, string DisplayName, string Description, IReadOnlyList<KindDto> Kinds)
{
    public static CategoryDto From(RedactionCategoryInfo info) => new(
        info.Category.ToString(),
        info.DisplayName,
        info.Description,
        info.Kinds.Select(kind => new KindDto(kind.Kind.ToString(), kind.DisplayName, kind.PlaceholderLabel)).ToList());
}

public sealed record ReportDto(int Total, IReadOnlyDictionary<string, int> CountsByKind, IReadOnlyList<string> Warnings)
{
    public static ReportDto From(RedactionReport report, IReadOnlyList<string> warnings) => new(
        report.Total,
        report.CountsByKind.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key.ToString(), pair => pair.Value, StringComparer.Ordinal),
        warnings);
}

public sealed record RedactionSummaryDto(string FileName, string ContentType, long SizeBytes, ReportDto Report);
