using DocumentRedaction.Core.Detectors.Builtin;

namespace DocumentRedaction.Core.Tests.Detectors;

public class ConfidentialStatementDetectorTests
{
    private readonly ConfidentialStatementDetector _detector = new();

    [Theory]
    [InlineData("Intro. This document is confidential. Next.", "This document is confidential.")]
    [InlineData("CONFIDENTIAL - Internal Use Only", "CONFIDENTIAL - Internal Use Only")]
    [InlineData("Line one\nContains trade secrets\nLine three", "Contains trade secrets")]
    [InlineData("Is it proprietary? Yes.", "Is it proprietary?")]
    [InlineData("Do not distribute!", "Do not distribute!")]
    [InlineData("Attorney-client privileged material follows", "Attorney-client privileged material follows")]
    public void Widens_to_sentence(string text, string expected) => DetectorAssert.Finds(_detector, text, expected);

    [Fact]
    public void Two_markers_in_one_sentence_give_one_detection() =>
        DetectorAssert.Finds(_detector, "This proprietary and confidential text. Fine.", "This proprietary and confidential text.");

    [Fact]
    public void Separate_sentences_give_separate_detections() =>
        DetectorAssert.Finds(_detector, "Confidential. Public. Proprietary.", "Confidential.", "Proprietary.");

    [Theory]
    [InlineData("Intro. Confidential: mail a@b.co or pay 3.5 now. Next.", "Confidential: mail a@b.co or pay 3.5 now.")]
    [InlineData("Confidential!Next", "Confidential!Next")]
    public void Terminators_inside_tokens_do_not_end_the_sentence(string text, string expected) => DetectorAssert.Finds(_detector, text, expected);

    [Theory]
    [InlineData("Confidence is high.")]
    [InlineData("The trade was secret.")]
    [InlineData("nothing here")]
    public void Rejects(string text) => DetectorAssert.FindsNothing(_detector, text);
}
