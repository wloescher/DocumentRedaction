using System.Text;
using DocumentRedaction.Core.Model;
using DocumentRedaction.Core.Redaction;
using DocumentRedaction.Documents.Processors;

namespace DocumentRedaction.Documents.Tests.Processors;

public class TextDocumentProcessorTests
{
    private readonly TextDocumentProcessor _processor = new(TextRedactor.CreateDefault());
    private readonly RedactionOptions _options = new();

    private string Redact(byte[] input, out ProcessedDocument processed)
    {
        processed = _processor.Redact(input, _options);
        return Encoding.UTF8.GetString(processed.Content.Span);
    }

    [Fact]
    public void Redacts_and_reports()
    {
        string output = Redact(Encoding.UTF8.GetBytes("SSN 123-45-6789, mail a@b.co"), out ProcessedDocument processed);
        Assert.Equal("SSN [REDACTED-SSN], mail [REDACTED-EMAIL]", output);
        Assert.Equal(2, processed.Report.Total);
    }

    [Fact]
    public void Preserves_crlf_line_endings()
    {
        string output = Redact(Encoding.UTF8.GetBytes("line one\r\nSSN 123-45-6789\r\nline three\n"), out _);
        Assert.Equal("line one\r\nSSN [REDACTED-SSN]\r\nline three\n", output);
    }

    [Fact]
    public void Keeps_utf8_bom_when_present()
    {
        byte[] input = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("a@b.co")];
        ProcessedDocument processed = _processor.Redact(input, _options);
        Assert.Equal(Encoding.UTF8.GetPreamble(), processed.Content[..3].ToArray());
        Assert.Equal("[REDACTED-EMAIL]", Encoding.UTF8.GetString(processed.Content.Span[3..]));
    }

    [Fact]
    public void Adds_no_bom_when_input_had_none()
    {
        ProcessedDocument processed = _processor.Redact(Encoding.UTF8.GetBytes("a@b.co"), _options);
        Assert.Equal((byte)'[', processed.Content.Span[0]);
    }

    [Fact]
    public void Round_trips_utf16_input()
    {
        Encoding utf16 = Encoding.Unicode;
        byte[] input = [.. utf16.GetPreamble(), .. utf16.GetBytes("mail a@b.co")];
        ProcessedDocument processed = _processor.Redact(input, _options);
        Assert.Equal(utf16.GetPreamble(), processed.Content[..2].ToArray());
        Assert.Equal("mail [REDACTED-EMAIL]", utf16.GetString(processed.Content.Span[2..]));
    }

    [Fact]
    public void Preserves_non_ascii_text()
    {
        string output = Redact(Encoding.UTF8.GetBytes("Zoë – café ✓ a@b.co"), out _);
        Assert.Equal("Zoë – café ✓ [REDACTED-EMAIL]", output);
    }

    [Fact]
    public void Empty_input_gives_empty_output()
    {
        ProcessedDocument processed = _processor.Redact(Array.Empty<byte>(), _options);
        Assert.True(processed.Content.IsEmpty);
        Assert.Equal(0, processed.Report.Total);
    }

    [Fact]
    public void Honours_cancellation()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => _processor.Redact(Encoding.UTF8.GetBytes("x"), _options, cts.Token));
    }

    [Fact]
    public void Declares_formats()
    {
        Assert.Equal(DocumentFormat.PlainText, _processor.Format);
        Assert.Contains(".txt", _processor.Extensions);
        Assert.Contains("text/plain", _processor.ContentTypes);
        Assert.Equal(".txt", _processor.OutputExtension);
    }
}
