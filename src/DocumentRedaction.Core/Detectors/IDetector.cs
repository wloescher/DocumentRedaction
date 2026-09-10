using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Detectors;

/// <summary>
/// Finds one kind of sensitive information in text. Implementations must be stateless and
/// thread-safe; a single instance is shared across requests.
/// </summary>
public interface IDetector
{
    InformationKind Kind { get; }

    /// <summary>
    /// Returns every candidate span. Candidates may overlap each other or those of other
    /// detectors; the engine resolves overlaps afterwards.
    /// </summary>
    IEnumerable<Detection> Detect(string text, RedactionOptions options);
}
