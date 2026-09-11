using DocumentRedaction.Documents;
using DocumentRedaction.Web;
using DocumentRedaction.Web.Api;
using DocumentRedaction.Web.Components;
using DocumentRedaction.Web.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Everything reads the settings through IOptions so hosts that add configuration late (tests) are honoured.
builder.Services.AddOptions<RedactionSettings>()
    .BindConfiguration(RedactionSettings.SectionName)
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<RedactionSettings>, RedactionSettingsValidator>();

// Uploads are buffered in memory and never written to disk or logs; the size limit bounds that memory.
builder.Services.AddOptions<KestrelServerOptions>()
    .Configure<IOptions<RedactionSettings>>((kestrel, settings) => kestrel.Limits.MaxRequestBodySize = settings.Value.RequestBodyLimit);
builder.Services.AddOptions<FormOptions>()
    .Configure<IOptions<RedactionSettings>>((form, settings) => form.MultipartBodyLengthLimit = settings.Value.RequestBodyLimit);

builder.Services.AddDocumentRedaction(provider => provider.GetRequiredService<IOptions<RedactionSettings>>().Value.Limits);
builder.Services.AddSingleton<ProcessingEstimator>();
builder.Services.AddOpenApi();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapOpenApi();
app.MapRedactionApi();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

/// <summary>Exposed so integration tests can host the application with WebApplicationFactory.</summary>
public partial class Program
{
}
