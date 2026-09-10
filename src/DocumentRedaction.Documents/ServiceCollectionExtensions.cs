using DocumentRedaction.Core.Redaction;
using DocumentRedaction.Documents.Processors;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentRedaction.Documents;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the default text redactor, every document processor, the resolver and the service.</summary>
    public static IServiceCollection AddDocumentRedaction(this IServiceCollection services)
    {
        services.AddSingleton<ITextRedactor>(_ => TextRedactor.CreateDefault());
        services.AddSingleton<IDocumentProcessor, TextDocumentProcessor>();
        services.AddSingleton<IDocumentProcessor, WordDocumentProcessor>();
        services.AddSingleton<IDocumentProcessor, PdfDocumentProcessor>();
        services.AddSingleton<IDocumentProcessorResolver, DocumentProcessorResolver>();
        services.AddSingleton<IDocumentRedactionService, DocumentRedactionService>();
        return services;
    }
}
