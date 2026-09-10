using System.Text;
using DocumentRedaction.Core.Detectors;
using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Redaction;

/// <summary>Default <see cref="ITextRedactor"/> that runs a fixed set of detectors.</summary>
public sealed class TextRedactor : ITextRedactor
{
    private readonly Dictionary<InformationKind, IDetector> _detectors;

    public TextRedactor(IEnumerable<IDetector> detectors)
    {
        _detectors = detectors.ToDictionary(detector => detector.Kind);
    }

    public static TextRedactor CreateDefault()
    {
        IReadOnlyList<IDetector> detectors = DetectorRegistry.CreateDefaultDetectors();
        DetectorRegistry.EnsureComplete(detectors);
        return new TextRedactor(detectors);
    }

    public IReadOnlyList<Detection> Detect(string text, RedactionOptions options)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(options);

        if (text.Length == 0)
        {
            return [];
        }

        IEnumerable<Detection> candidates = options.EffectiveKinds()
            .Where(_detectors.ContainsKey)
            .SelectMany(kind => _detectors[kind].Detect(text, options));

        return DetectionResolver.Resolve(candidates);
    }

    public TextRedactionResult Redact(string text, RedactionOptions options)
    {
        IReadOnlyList<Detection> detections = Detect(text, options);
        return new TextRedactionResult(
            Apply(text, detections, options),
            detections,
            RedactionReport.FromDetections(detections));
    }

    public string Apply(string text, IReadOnlyList<Detection> detections, RedactionOptions options)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(detections);
        ArgumentNullException.ThrowIfNull(options);

        if (detections.Count == 0)
        {
            return text;
        }

        StringBuilder builder = new(text.Length);
        int cursor = 0;
        foreach (Detection detection in detections)
        {
            if (detection.Start < cursor || detection.End > text.Length)
            {
                throw new ArgumentException("Detections must be non-overlapping, ordered, and within the text.", nameof(detections));
            }

            builder.Append(text, cursor, detection.Start - cursor);
            builder.Append(options.PlaceholderFor(detection.Kind));
            cursor = detection.End;
        }

        builder.Append(text, cursor, text.Length - cursor);
        return builder.ToString();
    }
}
