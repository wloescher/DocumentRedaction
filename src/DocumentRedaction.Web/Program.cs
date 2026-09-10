using DocumentRedaction.Documents;
using DocumentRedaction.Web;
using DocumentRedaction.Web.Api;
using DocumentRedaction.Web.Components;
using DocumentRedaction.Web.Services;
using Microsoft.AspNetCore.Http.Features;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<RedactionSettings>(builder.Configuration.GetSection(RedactionSettings.SectionName));
RedactionSettings settings = builder.Configuration.GetSection(RedactionSettings.SectionName).Get<RedactionSettings>() ?? new RedactionSettings();

// Uploads are buffered in memory and never written to disk or logs; the size limit bounds that memory.
long requestLimit = settings.MaxUploadBytes + 64 * 1024;
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = requestLimit);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = requestLimit);

builder.Services.AddDocumentRedaction();
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
