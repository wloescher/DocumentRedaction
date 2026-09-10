using DocumentRedaction.Core.Detectors;
using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Tests.Detectors;

public class DetectorRegistryTests
{
    [Fact]
    public void Default_detectors_cover_every_kind()
    {
        var detectors = DetectorRegistry.CreateDefaultDetectors();
        DetectorRegistry.EnsureComplete(detectors);
        Assert.Equal(Enum.GetValues<InformationKind>().Order(), detectors.Select(d => d.Kind).Order());
    }

    [Fact]
    public void Missing_detector_is_reported()
    {
        var incomplete = DetectorRegistry.CreateDefaultDetectors().Where(d => d.Kind != InformationKind.Iban).ToList();
        var ex = Assert.Throws<InvalidOperationException>(() => DetectorRegistry.EnsureComplete(incomplete));
        Assert.Contains("Iban", ex.Message, StringComparison.Ordinal);
    }
}
