//--------------------------------------------------------------------------------
// Configure builder
//--------------------------------------------------------------------------------
Directory.SetCurrentDirectory(AppContext.BaseDirectory);
var builder = WebApplication.CreateBuilder(args);

// Logging
builder.ConfigureLogging();

// Http
builder.ConfigureHttp();
// API
builder.ConfigureApi();
// Authentication
builder.ConfigureAuthentication();
// Rate limit
builder.ConfigureRateLimiter();
// Compress
builder.ConfigureCompression();
// OpenApi
builder.ConfigureOpenApi();

// Blazor
builder.ConfigureBlazor();

// Health
builder.ConfigureHealth();
// Telemetry
builder.ConfigureTelemetry();

// Components
builder.ConfigureComponents();

//--------------------------------------------------------------------------------
// Configure the HTTP request pipeline
//--------------------------------------------------------------------------------
var app = builder.Build();

// Startup information
app.LogStartupInformation();

// Forwarded headers
app.UseForwardedHeaders();

// Security headers
app.UseSecurityHeaders();

// Error handler
app.UseErrorHandler();

// Kitchen (WebAssembly)
app.UseKitchenFiles();

// Routing
app.UseRouting();

// Compression
app.UseCompression();

// HTTP log
app.UseHttpLog();

// Authentication
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();

// End point
app.MapEndpoints();

// Initialize
await app.InitializeApplicationAsync();

// Run
await app.RunAsync();

[ExcludeFromCodeCoverage]
public partial class Program;
