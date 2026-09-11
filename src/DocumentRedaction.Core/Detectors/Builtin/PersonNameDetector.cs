using System.Collections.Frozen;
using System.Text.RegularExpressions;
using DocumentRedaction.Core.Model;
using DocumentRedaction.Core.Redaction;

namespace DocumentRedaction.Core.Detectors.Builtin;

/// <summary>
/// Heuristic person-name detection without a language model. Five rules propose candidates and
/// the engine keeps the longest where they overlap:
/// <list type="bullet">
/// <item>an abbreviated honorific ("Dr. Jane Smith", "Mr Smith");</item>
/// <item>a spelled-out title, which needs at least two name words so "General Ledger" and
/// "Captain America" are left alone ("Captain James Cook");</item>
/// <item>a form label followed by a colon or dash ("Patient: Smith, John A.", "Attn: Jane Smith");</item>
/// <item>a salutation or sign-off ("Dear John,", "Sincerely,\nJane Smith");</item>
/// <item>a capitalised pair whose first word is a common given name ("Jane Smith reviewed").</item>
/// </list>
/// Honorifics and labels stay in the document; only the name is reported. Name words are
/// Unicode-aware ("José García", "Müller") and never cross a line break. A name ends before the
/// first word that ends one (Date, Street, Inc, a month, a pronoun) and cannot start with a form
/// of address, a title or a suffix ("Dear Sir", "Dear Mr. Smith" reports only "Smith"). Given
/// names that are also ordinary words ("Mark", "May") count only mid-sentence, so "Mark your
/// calendar" is left alone.
/// </summary>
public sealed partial class PersonNameDetector : IDetector
{
    private static readonly string[] Connectives =
    [
        "A", "An", "The", "This", "That", "These", "Those", "I", "You", "We", "They", "He", "She", "It",
        "My", "Your", "Our", "Their", "His", "Her", "Its", "And", "Or", "But", "If", "Then", "So", "As", "At",
        "By", "For", "From", "In", "Of", "On", "To", "With", "Please", "Thank", "Thanks", "Re", "Welcome",
    ];

    private static readonly string[] FormsOfAddress =
    [
        "Sir", "Madam", "Sirs", "Team", "All", "Colleagues", "Customer", "Valued", "Hiring", "Manager", "Members",
        "Friend", "Friends", "Parent", "Guardian", "Resident", "Occupant", "Homeowner", "Applicant", "Candidate",
    ];

    private static readonly string[] Honorifics =
    [
        "Mr", "Mrs", "Ms", "Miss", "Mx", "Dr", "Prof", "Professor", "Rev", "Reverend", "Hon", "Sen", "Senator",
        "Rep", "Representative", "Capt", "Captain", "Col", "Colonel", "Lt", "Lieutenant", "Sgt", "Sergeant",
        "Gen", "General", "Fr", "Father", "Sister", "Brother", "Judge", "Justice",
    ];

    private static readonly string[] SuffixWords = ["Jr", "Jr.", "Sr", "Sr.", "II", "III", "IV"];

    private static readonly string[] OrganisationAndDocumentWords =
    [
        "Inc", "LLC", "LLP", "Ltd", "Corp", "Corporation", "Company", "Co", "Group", "Partners", "Associates",
        "Bank", "Hospital", "Clinic", "Medical", "Center", "Centre", "University", "College", "School",
        "Department", "Office", "Foundation", "Institute", "Trust", "Services", "Systems", "Solutions",
        "Foods", "Motors", "Retail", "Holdings", "Industries", "Technologies", "Enterprises", "Brands", "Stores",
        "Markets", "Airlines", "Energy", "Capital", "Insurance", "Pharmaceuticals", "Media", "Press", "Publishing",
        "Studios", "Records", "Labs", "Software", "Consulting", "Logistics", "Manufacturing", "Realty", "Properties",
        "County", "City", "State", "Station", "Report", "Invoice", "Account", "Payment", "Total", "Date", "Name",
        "Patient", "Phone", "Email", "Address", "Signature", "Title", "Subject", "Section", "Page", "Table",
        "Figure", "Amount", "Balance", "Due", "Number", "No", "Summary", "Statement", "Details", "Information",
        "Form", "Type", "Status", "Code", "ID", "Period", "Notice", "Policy", "Plan", "Order", "Reference", "Ref",
    ];

    private static readonly string[] MonthsAndWeekdays =
    [
        "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December",
        "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday",
    ];

    /// <summary>A candidate whose first word is one of these is not a name at all.</summary>
    private static readonly FrozenSet<string> NeverStartsAName =
        Connectives.Concat(FormsOfAddress).Concat(Honorifics).Concat(SuffixWords).ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The name stops before the first of these; street suffixes come from the address detector.</summary>
    private static readonly FrozenSet<string> EndsAName =
        Connectives.Concat(OrganisationAndDocumentWords).Concat(MonthsAndWeekdays)
            .Concat(StreetAddressDetector.SuffixAlternation.Split('|'))
            .ToFrozenSet(StringComparer.Ordinal);

    public InformationKind Kind => InformationKind.PersonName;

    /// <summary>
    /// The rules overlap by design ("Patient: Dr. Jane Smith" is proposed three times), so the
    /// detector collapses them to the longest span per name the same way the engine would; its
    /// output is then one detection per name, which keeps it testable on its own.
    /// </summary>
    public IEnumerable<Detection> Detect(string text, RedactionOptions options)
    {
        List<Detection> candidates = [];
        CollectAnchored(AbbreviatedHonorificRegex(), text, minimumWords: 1, candidates);
        CollectAnchored(SpelledOutTitleRegex(), text, minimumWords: 2, candidates);
        CollectAnchored(FormLabelRegex(), text, minimumWords: 1, candidates);
        CollectAnchored(SalutationRegex(), text, minimumWords: 1, candidates);
        CollectListed(CapitalisedSequenceRegex(), text, candidates);
        return DetectionResolver.Resolve(candidates);
    }

    /// <summary>Runs that follow an honorific, title, label or salutation: the name starts at the run's first word.</summary>
    private void CollectAnchored(Regex regex, string text, int minimumWords, List<Detection> candidates)
    {
        foreach (Match match in regex.Matches(text))
        {
            Group value = match.Groups[RegexDetector.ValueGroup];
            List<(int Start, int Length)> words = Words(text, value.Index, value.Length);
            if (NeverStartsAName.Contains(Word(text, words[0])))
            {
                continue;
            }

            int end = EndBeforeStopWord(text, words, 0);
            bool cut = end < words.Count;
            if (cut && end == 1 && text[words[0].Start + words[0].Length - 1] == ',')
            {
                // "Smith, Please call" is the comma form with prose after it: the name is the part before the comma.
                candidates.Add(Candidate(text, words[0].Start, words[0].Length - 1));
                continue;
            }

            // One word is a name only when nothing was cut after it: "Dear John" yes, "Client: Acme Corporation" no.
            if (end < minimumWords || (end == 1 && cut))
            {
                continue;
            }

            candidates.Add(Candidate(text, words[0].Start, words[end - 1].Start + words[end - 1].Length - words[0].Start));
        }
    }

    /// <summary>
    /// Capitalised runs anywhere: the name starts at the first word on the given-name list (so
    /// "Acme Jane Smith" yields "Jane Smith") and needs a second word. A listed name that is also
    /// an ordinary word is skipped at a sentence start.
    /// </summary>
    private void CollectListed(Regex regex, string text, List<Detection> candidates)
    {
        foreach (Match match in regex.Matches(text))
        {
            Group value = match.Groups[RegexDetector.ValueGroup];
            List<(int Start, int Length)> words = Words(text, value.Index, value.Length);
            int first = words.FindIndex(word => FirstNames.All.Contains(Word(text, word)));
            if (first < 0)
            {
                continue;
            }

            if (FirstNames.Ambiguous.Contains(Word(text, words[first])) && IsSentenceStart(text, words[first].Start))
            {
                continue;
            }

            int end = EndBeforeStopWord(text, words, first);
            if (end - first < 2)
            {
                continue;
            }

            candidates.Add(Candidate(text, words[first].Start, words[end - 1].Start + words[end - 1].Length - words[first].Start));
        }
    }

    private Detection Candidate(string text, int start, int length) =>
        new(Kind, start, length, text.Substring(start, length));

    /// <summary>Index of the first word at or after <paramref name="first"/> + 1 that ends a name, or the word count.</summary>
    private static int EndBeforeStopWord(string text, List<(int Start, int Length)> words, int first)
    {
        int end = first + 1;
        while (end < words.Count && !EndsAName.Contains(Word(text, words[end])))
        {
            end++;
        }

        return end;
    }

    /// <summary>The word without a trailing comma, so "Smith," in "Smith, John" is checked as "Smith".</summary>
    private static string Word(string text, (int Start, int Length) word) =>
        text.Substring(word.Start, word.Length).TrimEnd(',');

    private static List<(int Start, int Length)> Words(string text, int start, int length)
    {
        List<(int Start, int Length)> words = [];
        foreach (ValueMatch word in WordRegex().EnumerateMatches(text.AsSpan(start, length)))
        {
            words.Add((start + word.Index, word.Length));
        }

        return words;
    }

    /// <summary>
    /// True when nothing but spaces separates <paramref name="index"/> from the start of the
    /// text, a line break, a sentence terminator, a colon, or an opening quote or bracket.
    /// </summary>
    internal static bool IsSentenceStart(string text, int index)
    {
        int i = index - 1;
        while (i >= 0 && text[i] is ' ' or '\t')
        {
            i--;
        }

        return i < 0 || text[i] is '\n' or '\r' or '.' or '!' or '?' or ':' or ';' or '"' or '“' or '(' or '[' or '•' or '-' or '–' or '—';
    }

    // One capitalised word in any script, allowing an inner capital after "Mc" or "O'": "Jane", "José", "McDonald", "O'Brien".
    private const string WordPattern = @"\p{Lu}[\p{Ll}'’]+(?:\p{Lu}[\p{Ll}'’]+)?";

    // A name token: a word, a hyphenated pair ("Smith-Jones"), an initial "J." or a suffix.
    // Suffixes come first so "Jr." keeps its period instead of matching as the word "Jr".
    private const string Token = @"(?:Jr\.?|Sr\.?|I{2,3}|IV|" + WordPattern + @"(?:-" + WordPattern + @")*|\p{Lu}\.)";

    // Lower-case particles that sit inside a name: "Ludwig van Beethoven", "Maria de la Cruz".
    private const string Particle = @"(?:(?:van|von|de|del|della|der|den|da|di|du|la|le|bin|ibn|al|el)[ \t]+)";

    // Up to five tokens joined by spaces or tabs only, so a name never spans a line break.
    private const string NameRun = @"(?<value>" + Token + @"(?:[ \t]+" + Particle + @"*" + Token + @"){0,4})";

    // "Smith, John" or "Smith, John A." as written on forms.
    private const string LastFirst = @"(?<value>" + WordPattern + @",[ \t]+" + WordPattern + @"(?:[ \t]+\p{Lu}\.)?)";

    // The run must end at a non-letter so "José" is never matched as "Jos" and "McDONALD" not as "Mc".
    private const string NotInsideAWord = @"(?![\p{L}\p{M}'’-])";

    [GeneratedRegex(
        @"\b(?:Mr|Mrs|Ms|Mx|Dr|Prof|Rev|Hon|Sen|Rep|Capt|Col|Lt|Sgt|Gen|Fr)\.?[ \t]+" + NameRun + NotInsideAWord,
        RegexOptions.ExplicitCapture)]
    private static partial Regex AbbreviatedHonorificRegex();

    [GeneratedRegex(
        @"\b(?:Miss|Professor|Reverend|Senator|Representative|Captain|Colonel|Lieutenant|Sergeant|General|Father|Sister|Brother|Judge|Justice)[ \t]+" + NameRun + NotInsideAWord,
        RegexOptions.ExplicitCapture)]
    private static partial Regex SpelledOutTitleRegex();

    [GeneratedRegex(
        @"\b(?i:patient(?:[ \t]+name)?|name|attn|attention|employee|client|guarantor|insured|subscriber|physician|provider|signed|signature|prepared[ \t]+by|approved[ \t]+by|reviewed[ \t]+by|submitted[ \t]+by)" +
        @"[ \t]*[:\-–]\s*(?:" + LastFirst + "|" + NameRun + ")" + NotInsideAWord,
        RegexOptions.ExplicitCapture)]
    private static partial Regex FormLabelRegex();

    [GeneratedRegex(
        @"\b(?i:dear|sincerely|(?:best|kind|warm)[ \t]+regards|regards|yours[ \t]+(?:truly|sincerely|faithfully)|cordially|respectfully)" +
        @"(?:[ \t]*,\s*|\s+)" + NameRun + NotInsideAWord,
        RegexOptions.ExplicitCapture)]
    private static partial Regex SalutationRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{M}'’-])" + NameRun + NotInsideAWord, RegexOptions.ExplicitCapture)]
    private static partial Regex CapitalisedSequenceRegex();

    [GeneratedRegex(@"\S+")]
    private static partial Regex WordRegex();
}
