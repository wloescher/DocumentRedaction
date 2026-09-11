using DocumentRedaction.Core.Redaction;
using DocumentRedaction.Documents.Processors;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentRedaction.Documents;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default text redactor, every document processor, the resolver and the service.
    /// <paramref name="limits"/> supplies the <see cref="DocumentLimits"/> from the container
    /// (typically bound options); null means the defaults. Processors validate the limits when
    /// they are constructed, so a host that wants startup failure should validate its options at
    /// startup or resolve <see cref="IDocumentProcessorResolver"/> once after building.
    /// </summary>
    public static IServiceCollection AddDocumentRedaction(this IServiceCollection services, Func<IServiceProvider, DocumentLimits>? limits = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(provider => limits?.Invoke(provider) ?? new DocumentLimits());
        services.AddSingleton<ITextRedactor>(_ => TextRedactor.CreateDefault());
        services.AddSingleton<IDocumentProcessor, TextDocumentProcessor>();
        services.AddSingleton<IDocumentProcessor, WordDocumentProcessor>();
        services.AddSingleton<IDocumentProcessor, PdfDocumentProcessor>();
        services.AddSingleton<IDocumentProcessorResolver, DocumentProcessorResolver>();
        services.AddSingleton<IDocumentRedactionService, DocumentRedactionService>();
        return services;
    }
}
