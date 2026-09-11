using DocumentRedaction.Core.Model;
using DocumentRedaction.Core.Redaction;
using DocumentRedaction.Documents.Processors;
using DocumentRedaction.Documents.Processors.Pdf;
using DocumentRedaction.Tests.Fixtures;
using QuestPDF.Infrastructure;

namespace DocumentRedaction.Documents.Tests.Processors.Pdf;

/// <summary>QuestPDF's settings are process-wide, so nothing else may run while these assert them.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class QuestPdfGlobalSettings
{
    public const string Name = "QuestPDF global settings";
}

[Collection(QuestPdfGlobalSettings.Name)]
public class QuestPdfSettingsTests : IDisposable
{
    private static readonly RedactionOptions Options = new() { Categories = RedactionCategory.Pii };

    public void Dispose()
    {
        QuestPdfSettings.Apply(QuestPdfLicense.Community);
        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData(QuestPdfLicense.Community, LicenseType.Community)]
    [InlineData(QuestPdfLicense.Professional, LicenseType.Professional)]
    [InlineData(QuestPdfLicense.Enterprise, LicenseType.Enterprise)]
    public void Each_license_maps_onto_the_QuestPDF_tier(QuestPdfLicense license, LicenseType expected) =>
        Assert.Equal(expected, QuestPdfSettings.ToLicenseType(license));

    [Fact]
    public void Apply_writes_the_license_and_relaxes_the_glyph_check()
    {
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = true;
        QuestPdfSettings.Apply(QuestPdfLicense.Professional);

        Assert.Equal(LicenseType.Professional, QuestPDF.Settings.License);
        Assert.False(QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable);
    }

    [Fact]
    public void Constructing_a_processor_applies_its_license()
    {
        _ = new PdfDocumentProcessor(TextRedactor.CreateDefault(), license: QuestPdfLicense.Enterprise);
        Assert.Equal(LicenseType.Enterprise, QuestPDF.Settings.License);

        _ = new PdfDocumentProcessor(TextRedactor.CreateDefault());
        Assert.Equal(LicenseType.Community, QuestPDF.Settings.License);
    }

    [Fact]
    public void Undefined_license_is_rejected_before_it_reaches_the_library()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => QuestPdfSettings.ToLicenseType((QuestPdfLicense)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfDocumentProcessor(TextRedactor.CreateDefault(), license: (QuestPdfLicense)99));
    }

    [Fact]
    public void Processor_built_with_a_paid_tier_still_renders()
    {
        // The tier only changes the library's licensing check; output is identical.
        PdfDocumentProcessor processor = new(TextRedactor.CreateDefault(), license: QuestPdfLicense.Enterprise);
        ProcessedDocument result = processor.Redact(PdfFixture.Build(["SSN 123-45-6789"]), Options);
        Assert.Equal(1, result.Report.Total);
    }
}
