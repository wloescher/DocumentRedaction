using System.Collections.Frozen;

namespace DocumentRedaction.Core.Detectors.Builtin;

/// <summary>
/// The curated given-name list embedded as <c>GivenNames.txt</c>. <see cref="All"/> is every
/// name the list rule accepts; <see cref="Ambiguous"/> is the section after the
/// <c>## ambiguous</c> marker, names that double as an ordinary word or month ("Mark", "May",
/// "Grace") and are therefore trusted only mid-sentence. Reading both sets from one file keeps
/// the second a subset of the first by construction.
/// </summary>
internal static class FirstNames
{
    private const string ResourceName = "GivenNames.txt";
    private const string AmbiguousMarker = "## ambiguous";

    public static IReadOnlySet<string> All { get; }

    public static IReadOnlySet<string> Ambiguous { get; }

    static FirstNames()
    {
        (All, Ambiguous) = Load();
    }

    private static (FrozenSet<string> All, FrozenSet<string> Ambiguous) Load()
    {
        using Stream stream = typeof(FirstNames).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' is missing.");
        using StreamReader reader = new(stream);

        HashSet<string> all = new(StringComparer.Ordinal);
        HashSet<string> ambiguous = new(StringComparer.Ordinal);
        bool inAmbiguousSection = false;
        while (reader.ReadLine() is { } line)
        {
            string name = line.Trim();
            if (name.Equals(AmbiguousMarker, StringComparison.Ordinal))
            {
                inAmbiguousSection = true;
            }
            else if (name.Length > 0 && !name.StartsWith('#'))
            {
                all.Add(name);
                if (inAmbiguousSection)
                {
                    ambiguous.Add(name);
                }
            }
        }

        return (all.ToFrozenSet(StringComparer.Ordinal), ambiguous.ToFrozenSet(StringComparer.Ordinal));
    }
}
