using DocumentRedaction.Core.Redaction;
using DocumentRedaction.Documents.Processors;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentRedaction.Documents;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default text redactor, every document processor, the resolver and the service.
    /// <paramref name="limits"/> supplies the <see cref="DocumentLimits"/> from the container
    /// (typically bound options); null means the defaults. <paramref name="pdfLicense"/> supplies
    /// the <see cref="QuestPdfLicense"/> the same way; null means Community. Processors validate
    /// the limits when they are constructed, so a host that wants startup failure should validate
    /// its options at startup or resolve <see cref="IDocumentProcessorResolver"/> once after building.
    /// </summary>
    public static IServiceCollection AddDocumentRedaction(
        this IServiceCollection services,
        Func<IServiceProvider, DocumentLimits>? limits = null,
        Func<IServiceProvider, QuestPdfLicense>? pdfLicense = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(provider => limits?.Invoke(provider) ?? new DocumentLimits());
        services.AddSingleton<ITextRedactor>(_ => TextRedactor.CreateDefault());
        services.AddSingleton<IDocumentProcessor, TextDocumentProcessor>();
        services.AddSingleton<IDocumentProcessor, WordDocumentProcessor>();
        services.AddSingleton<IDocumentProcessor>(provider => new PdfDocumentProcessor(
            provider.GetRequiredService<ITextRedactor>(),
            provider.GetRequiredService<DocumentLimits>(),
            pdfLicense?.Invoke(provider) ?? QuestPdfLicense.Community));
        services.AddSingleton<IDocumentProcessorResolver, DocumentProcessorResolver>();
        services.AddSingleton<IDocumentRedactionService, DocumentRedactionService>();
        return services;
    }
}
