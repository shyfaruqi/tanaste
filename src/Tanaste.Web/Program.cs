using MudBlazor.Services;
using Tanaste.Web.Components;
using Tanaste.Web.Services.Integration;
using Tanaste.Web.Services.Theming;

var builder = WebApplication.CreateBuilder(args);

// ── Blazor ────────────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── MudBlazor ─────────────────────────────────────────────────────────────────
builder.Services.AddMudServices();

// ── Theming ───────────────────────────────────────────────────────────────────
// Singleton: one theme instance shared across all connections; toggle is per-connection
// (components hold their own _isDark flag synced via ThemeService.OnThemeChanged).
builder.Services.AddSingleton<ThemeService>();

// ── Tanaste API HTTP Client ───────────────────────────────────────────────────
var apiBase = builder.Configuration["TanasteApi:BaseUrl"] ?? "http://localhost:61495";
var apiKey  = builder.Configuration["TanasteApi:ApiKey"]  ?? string.Empty;

builder.Services.AddHttpClient<TanasteApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBase);
    if (!string.IsNullOrWhiteSpace(apiKey))
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
});

builder.Services.AddScoped<ITanasteApiClient, TanasteApiClient>();

// ── State + Orchestration (scoped = one per SignalR circuit) ──────────────────
builder.Services.AddScoped<UniverseStateContainer>();
builder.Services.AddScoped<UIOrchestratorService>();

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
