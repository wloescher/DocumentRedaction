using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Tests.Model;

public class RedactionReportTests
{
    [Fact]
    public void From_detections_counts_by_kind()
    {
        Detection[] detections =
        [
            new(InformationKind.EmailAddress, 0, 1, "a"),
            new(InformationKind.EmailAddress, 2, 1, "b"),
            new(InformationKind.Date, 4, 1, "c"),
        ];
        var report = RedactionReport.FromDetections(detections);
        Assert.Equal(2, report.CountsByKind[InformationKind.EmailAddress]);
        Assert.Equal(1, report.CountsByKind[InformationKind.Date]);
        Assert.Equal(3, report.Total);
    }

    [Fact]
    public void Merge_adds_counts()
    {
        var first = new RedactionReport(new Dictionary<InformationKind, int> { [InformationKind.Date] = 1 });
        var second = new RedactionReport(new Dictionary<InformationKind, int> { [InformationKind.Date] = 2, [InformationKind.Iban] = 1 });
        var merged = first.Merge(second);
        Assert.Equal(3, merged.CountsByKind[InformationKind.Date]);
        Assert.Equal(1, merged.CountsByKind[InformationKind.Iban]);
        Assert.Equal(4, merged.Total);
        Assert.Equal(1, first.Total);
    }

    [Fact]
    public void Empty_has_no_counts()
    {
        Assert.Equal(0, RedactionReport.Empty.Total);
        Assert.Equal(0, RedactionReport.Empty.Merge(RedactionReport.Empty).Total);
    }
}
