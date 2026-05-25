using BrainBackend.Hubs;
using BrainBackend.Services;

var builder = WebApplication.CreateBuilder(args);

// ──────────────────────────────────────────────
// 1. Services
// ──────────────────────────────────────────────
builder.Services.AddControllers();

// SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

// Chess services
builder.Services.AddSingleton<StockfishService>();
builder.Services.AddSingleton<FenCacheService>();
builder.Services.AddScoped<EvaluationService>();

// CORS
// Policy 1: "SignalRPolicy" — for SignalR hub (requires AllowCredentials + specific origin)
// Policy 2: "ApiPolicy" — for REST endpoints (allows any origin for Chrome Extension + chess.com)
builder.Services.AddCors(options =>
{
    // Allow list for SignalR WebSocket (must use AllowCredentials, cannot use *)
    var signalROrigins = builder.Configuration
        .GetValue("Cors:AllowedOrigins",
                  "http://localhost:3000,http://localhost:5173,http://localhost:5174,http://localhost:5175")!
        .Split(',', StringSplitOptions.RemoveEmptyEntries);

    options.AddPolicy("SignalRPolicy", policy =>
    {
        policy
            .WithOrigins(signalROrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });

    // Open CORS for REST API — Chrome Extension / chess.com content script calls us from chess.com origin
    options.AddPolicy("ApiPolicy", policy =>
    {
        policy
            .SetIsOriginAllowed(_ => true)  // Allow ANY origin
            .AllowAnyHeader()
            .AllowAnyMethod();
        // NOTE: AllowCredentials() intentionally omitted (incompatible with SetIsOriginAllowed)
    });
});

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// ──────────────────────────────────────────────
// 2. Build app
// ──────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

app.UseRouting();
// Apply ApiPolicy globally (REST endpoints)
app.UseCors("ApiPolicy");

// ──────────────────────────────────────────────
// 3. Endpoints
// ──────────────────────────────────────────────
app.MapControllers();
// SignalR hub uses stricter policy that requires AllowCredentials
app.MapHub<ChessHub>("/chessHub").RequireCors("SignalRPolicy");

// Health check root
app.MapGet("/", () => Results.Ok(new
{
    service = "Chess Brain Backend",
    version = "2.0",
    status = "running",
    endpoints = new[]
    {
        "POST /api/analysis",
        "GET  /api/analysis/status",
        "WS   /chessHub"
    }
}));

// ──────────────────────────────────────────────
// 4. Graceful shutdown — dispose Stockfish
// ──────────────────────────────────────────────
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    var stockfish = app.Services.GetService<StockfishService>();
    stockfish?.Dispose();
});

app.Run();
