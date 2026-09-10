using DocumentRedaction.Core.Detectors.Builtin;
using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Tests.Detectors;

public class CustomTermDetectorTests
{
    private readonly CustomTermDetector _detector = new();

    private static RedactionOptions With(params string[] terms) => new() { CustomTerms = terms };

    [Fact]
    public void No_terms_means_no_detections() =>
        Assert.Empty(DetectorAssert.Run(_detector, "John Smith", new RedactionOptions()));

    [Fact]
    public void Blank_terms_are_ignored() =>
        Assert.Empty(DetectorAssert.Run(_detector, "John Smith", With("", "   ")));

    [Fact]
    public void Matches_case_insensitively_by_default()
    {
        var found = DetectorAssert.Run(_detector, "JOHN SMITH and john smith", With("John Smith"));
        Assert.Equal(["JOHN SMITH", "john smith"], found.Select(d => d.Text));
    }

    [Fact]
    public void Honours_case_sensitivity()
    {
        var options = new RedactionOptions { CustomTerms = ["John"], CustomTermsAreCaseSensitive = true };
        var found = DetectorAssert.Run(_detector, "john John JOHN", options);
        Assert.Equal(["John"], found.Select(d => d.Text));
    }

    [Fact]
    public void Respects_word_boundaries() =>
        Assert.Empty(DetectorAssert.Run(_detector, "Annual report", With("Ann")));

    [Fact]
    public void Longest_term_wins_at_same_position()
    {
        var found = DetectorAssert.Run(_detector, "New York City", With("New York", "New York City"));
        Assert.Equal(["New York City"], found.Select(d => d.Text));
    }

    [Fact]
    public void Tolerates_flexible_whitespace_inside_phrases()
    {
        var found = DetectorAssert.Run(_detector, "Project  Falcon", With("Project Falcon"));
        Assert.Equal(["Project  Falcon"], found.Select(d => d.Text));
    }

    [Fact]
    public void Escapes_regex_metacharacters()
    {
        var found = DetectorAssert.Run(_detector, "cost $1.5M (est.)", With("$1.5M (est.)"));
        Assert.Equal(["$1.5M (est.)"], found.Select(d => d.Text));
    }

    [Fact]
    public void Terms_with_symbol_edges_need_no_word_boundary()
    {
        var found = DetectorAssert.Run(_detector, "issue#42.", With("#42"));
        Assert.Equal(["#42"], found.Select(d => d.Text));
    }

    [Fact]
    public void Repeated_calls_with_same_options_reuse_results()
    {
        var options = With("Acme");
        Assert.Single(DetectorAssert.Run(_detector, "Acme one", options));
        Assert.Single(DetectorAssert.Run(_detector, "Acme two", options));
    }

    [Fact]
    public void Duplicate_terms_are_deduplicated()
    {
        Assert.Equal(@"\bAcme\b", CustomTermDetector.BuildPattern(["Acme", "Acme"]));
    }
}
