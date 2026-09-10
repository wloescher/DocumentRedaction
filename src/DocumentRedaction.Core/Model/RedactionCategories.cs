namespace DocumentRedaction.Core.Model;

/// <summary>Display metadata for each selectable <see cref="RedactionCategory"/> flag.</summary>
/// <param name="Category">The single flag described.</param>
/// <param name="DisplayName">Short label for checkboxes and API output.</param>
/// <param name="Description">One sentence explaining what the category covers.</param>
public sealed record RedactionCategoryInfo(RedactionCategory Category, string DisplayName, string Description)
{
    public IEnumerable<InformationKindInfo> Kinds => InformationKinds.ForCategories(Category);
}

public static class RedactionCategories
{
    public static IReadOnlyList<RedactionCategoryInfo> All { get; } =
    [
        new(RedactionCategory.Pii, "PII", "Personally identifiable information: Social Security numbers, contact details, government IDs, addresses."),
        new(RedactionCategory.Hipaa, "HIPAA / PHI", "Protected health information: everything in PII plus medical record, health plan, provider and DEA numbers, and dates."),
        new(RedactionCategory.Financial, "Financial", "Payment and banking identifiers: card numbers, routing numbers, IBAN, SWIFT codes, account numbers."),
        new(RedactionCategory.Confidential, "Confidential", "Sentences marked confidential, proprietary, internal use only or trade secret."),
    ];

    public static RedactionCategoryInfo Get(RedactionCategory category) =>
        All.FirstOrDefault(info => info.Category == category)
        ?? throw new ArgumentOutOfRangeException(nameof(category), category, "Not a single known category.");

    /// <summary>Case-insensitive lookup by enum name ("pii", "Hipaa") or display name ("HIPAA / PHI").</summary>
    public static bool TryParse(string value, out RedactionCategory category)
    {
        foreach (RedactionCategoryInfo info in All)
        {
            if (string.Equals(value, info.Category.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, info.DisplayName, StringComparison.OrdinalIgnoreCase))
            {
                category = info.Category;
                return true;
            }
        }

        if (string.Equals(value, "all", StringComparison.OrdinalIgnoreCase))
        {
            category = RedactionCategory.All;
            return true;
        }

        category = RedactionCategory.None;
        return false;
    }
}
