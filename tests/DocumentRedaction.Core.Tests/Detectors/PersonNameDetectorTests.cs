using DocumentRedaction.Core.Detectors.Builtin;
using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Tests.Detectors;

public class PersonNameDetectorTests
{
    private readonly PersonNameDetector _detector = new();

    [Theory]
    [InlineData("Please contact Dr. Jane Smith for details.", "Jane Smith")]
    [InlineData("Mr Smith will attend.", "Smith")]
    [InlineData("Mrs. Mary Anne O'Brien-Jones Jr.", "Mary Anne O'Brien-Jones Jr.")]
    [InlineData("Prof. J. R. McDonald teaches.", "J. R. McDonald")]
    [InlineData("Ms. Smith's office called.", "Smith's")]
    [InlineData("Dr. José García and Dr. Müller", "José García", "Müller")]
    [InlineData("Dr. May Chen", "May Chen")]
    [InlineData("Sales Rep Jane Smith called.", "Jane Smith")]
    [InlineData("Dr. Anne Marie Van Der Berg", "Anne Marie Van Der Berg")]
    public void Finds_names_after_abbreviated_honorifics_and_keeps_the_honorific(string text, params string[] expected) =>
        DetectorAssert.Finds(_detector, text, expected);

    [Theory]
    [InlineData("Senator Robert Kennedy spoke.", "Robert Kennedy")]
    [InlineData("Captain James Cook sailed.", "James Cook")]
    [InlineData("Miss Jane Marple investigated.", "Jane Marple")]
    public void Spelled_out_titles_need_two_name_words(string text, string expected) =>
        DetectorAssert.Finds(_detector, text, expected);

    [Theory]
    [InlineData("Patient: Smith, John A.", "Smith, John A.")]
    [InlineData("Patient Name: Jane Smith Date: 01/02/2020", "Jane Smith")]
    [InlineData("Name: Jane Smith\nDOB: 01/02/1980", "Jane Smith")]
    [InlineData("Name:\tJane Smith", "Jane Smith")]
    [InlineData("Attn: Ludwig van Beethoven", "Ludwig van Beethoven")]
    [InlineData("Signed: Roberta Flack III", "Roberta Flack III")]
    [InlineData("Prepared by: Wei Zhang", "Wei Zhang")]
    [InlineData("insured - Kowalski, Anna", "Kowalski, Anna")]
    [InlineData("Patient: Smith, Please call.", "Smith")]
    [InlineData("Patient: Mrs Jane Smith", "Jane Smith")]
    [InlineData("Patient: Dr. Jane Smith", "Jane Smith")]
    public void Finds_names_after_form_labels_and_keeps_the_label(string text, params string[] expected) =>
        DetectorAssert.Finds(_detector, text, expected);

    [Theory]
    [InlineData("Dear John,", "John")]
    [InlineData("Dear Jane, Welcome aboard.", "Jane")]
    [InlineData("Dear John, Thanks for your letter.", "John")]
    [InlineData("Dear Mr. Smith,", "Smith")]
    [InlineData("Dear Mr Smith,", "Smith")]
    [InlineData("Dear Zoë,", "Zoë")]
    [InlineData("Sincerely,\nJane Smith", "Jane Smith")]
    [InlineData("Sincerely,\r\nJane Smith", "Jane Smith")]
    [InlineData("Best regards,\n\nMaria de la Cruz\nAccount Manager", "Maria de la Cruz")]
    [InlineData("Yours truly, Jane Smith", "Jane Smith")]
    public void Finds_names_after_salutations_and_sign_offs(string text, params string[] expected) =>
        DetectorAssert.Finds(_detector, text, expected);

    [Theory]
    [InlineData("Jane Smith reviewed the file.", "Jane Smith")]
    [InlineData("The file was reviewed by John Q. Public yesterday.", "John Q. Public")]
    [InlineData("Acme Corp hired Michael Smith-Jones.", "Michael Smith-Jones")]
    [InlineData("Acme Jane Smith", "Jane Smith")]
    [InlineData("see Mark Twain for details", "Mark Twain")]
    [InlineData("Interview with Grace Hopper Jr.", "Grace Hopper Jr.")]
    [InlineData("Emily Zhao and Carlos Ortiz attended.", "Emily Zhao", "Carlos Ortiz")]
    [InlineData("We met May Johnson today.", "May Johnson")]
    [InlineData("Signed off by Jane Smith", "Jane Smith")]
    [InlineData("Jane Smith May 3, 2020", "Jane Smith")]
    public void Finds_capitalised_pairs_led_by_a_common_given_name(string text, params string[] expected) =>
        DetectorAssert.Finds(_detector, text, expected);

    [Theory]
    [InlineData("")]
    [InlineData("Mark your calendar for the review.")]
    [InlineData("May Revenue grew by 5%.")]
    [InlineData("Bill Payment is due on Monday.")]
    [InlineData("Total Bill Amount: 45.00")]
    [InlineData("- Will Update the plan")]
    [InlineData("Grace Period ends in March.")]
    [InlineData("Dear Sir or Madam,")]
    [InlineData("Dear Hiring Manager,")]
    [InlineData("Dear Jr.")]
    [InlineData("Signed: III")]
    [InlineData("Client: Acme Corporation")]
    [InlineData("Name: N/A")]
    [InlineData("Jane\nSmith")]
    [InlineData("Smith")]
    [InlineData("lives at 123 John Street")]
    [InlineData("lives at 45 Grace Ln")]
    [InlineData("the Victoria Station concourse")]
    [InlineData("JANE SMITH")]
    [InlineData("jane smith")]
    [InlineData("Dr. McDONALD")]
    [InlineData("Zhang Wei attended")]
    [InlineData("The General Ledger balance")]
    [InlineData("General Motors Company")]
    [InlineData("General Counsel reviewed it")]
    [InlineData("Employee Handbook")]
    [InlineData("Patient Portal login")]
    [InlineData("Physician Assistant")]
    [InlineData("Signature Required")]
    [InlineData("the client Acme signed")]
    [InlineData("Morgan Stanley and Dean Foods confirmed.")]
    [InlineData("we called Jordan Retail")]
    [InlineData("Ruby Tuesday catered.")]
    public void Rejects(string text) => DetectorAssert.FindsNothing(_detector, text);

    [Fact]
    public void Ambiguous_given_names_count_mid_sentence_but_not_at_a_sentence_start()
    {
        DetectorAssert.Finds(_detector, "We met Mark Twain there.", "Mark Twain");
        DetectorAssert.FindsNothing(_detector, "Mark Twain wrote it.");
        DetectorAssert.FindsNothing(_detector, "Sold. Bill Gates bought it.");
        DetectorAssert.FindsNothing(_detector, "Notes:\nBill Gates bought it.");
        DetectorAssert.Finds(_detector, "Notes:\nJohn Gates bought it.", "John Gates");
    }

    [Fact]
    public void Name_at_the_very_end_of_the_text_is_found() =>
        DetectorAssert.Finds(_detector, "Approved by: Jane Smith", "Jane Smith");

    [Fact]
    public void A_month_in_the_middle_of_a_name_ends_it()
    {
        // Documented miss: the month rule cuts the run, and a lone word before a cut is not accepted.
        DetectorAssert.FindsNothing(_detector, "Jane May Smith reviewed it.");
        DetectorAssert.FindsNothing(_detector, "Dr. Jane May Smith");
    }

    [Fact]
    public void Names_longer_than_five_words_are_truncated()
    {
        // The run is capped at five tokens; the remainder is a documented leak.
        DetectorAssert.Finds(_detector, "Dr. Anne Marie Louise Van Der Berg", "Anne Marie Louise Van Der");
    }

    [Theory]
    [InlineData("", 0, true)]
    [InlineData("Jane", 0, true)]
    [InlineData("Hello. Jane", 7, true)]
    [InlineData("Hello.  Jane", 8, true)]
    [InlineData("List:\n\tJane", 7, true)]
    [InlineData("• Jane", 2, true)]
    [InlineData("met Jane", 4, false)]
    [InlineData("Dr. Jane", 4, true)]
    [InlineData("a@b.co Jane", 7, false)]
    public void Sentence_start_is_the_text_start_a_line_break_or_a_terminator(string text, int index, bool expected) =>
        Assert.Equal(expected, PersonNameDetector.IsSentenceStart(text, index));

    [Fact]
    public void Name_list_loads_from_the_assembly_with_the_ambiguous_section_inside_it()
    {
        Assert.Contains("Jane", FirstNames.All);
        Assert.Contains("Mark", FirstNames.All);
        Assert.Contains("Mark", FirstNames.Ambiguous);
        Assert.Contains("May", FirstNames.Ambiguous);
        Assert.DoesNotContain("Jane", FirstNames.Ambiguous);
        Assert.True(FirstNames.All.Count > 500);
        Assert.True(FirstNames.Ambiguous.Count < FirstNames.All.Count / 4);
        Assert.All(FirstNames.All, name => Assert.True(char.IsUpper(name[0]) && name.Trim() == name, name));
    }

    [Fact]
    public void Kind_is_person_name() => Assert.Equal(InformationKind.PersonName, _detector.Kind);
}
