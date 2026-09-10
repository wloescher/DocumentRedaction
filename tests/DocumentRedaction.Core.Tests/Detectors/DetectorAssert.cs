using DocumentRedaction.Core.Detectors;
using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Tests.Detectors;

internal static class DetectorAssert
{
    public static IReadOnlyList<Detection> Run(IDetector detector, string text, RedactionOptions? options = null) =>
        detector.Detect(text, options ?? new RedactionOptions()).ToList();

    /// <summary>Asserts the detector reports exactly these spans, in order, and that each span's offsets agree with its text.</summary>
    public static void Finds(IDetector detector, string text, params string[] expected)
    {
        IReadOnlyList<Detection> found = Run(detector, text);
        Assert.Equal(expected, found.Select(detection => detection.Text).ToArray());
        Assert.All(found, detection =>
        {
            Assert.Equal(detector.Kind, detection.Kind);
            Assert.Equal(detection.Text, text.Substring(detection.Start, detection.Length));
        });
    }

    public static void FindsNothing(IDetector detector, string text) => Assert.Empty(Run(detector, text));
}
