using System.Text.Json.Serialization;
using Admission.Api.Data;
using Admission.Api.Endpoints;
using Admission.Api.Errors;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("The ConnectionStrings__Default environment variable is required.");

var corsOrigin = builder.Configuration["CORS_ORIGIN"];
corsOrigin = string.IsNullOrWhiteSpace(corsOrigin) ? "http://localhost:3000" : corsOrigin.Trim().TrimEnd('/');

var holdDurationSetting = builder.Configuration["HOLD_DURATION_SECONDS"];
var holdDurationSeconds = 600;
if (!string.IsNullOrWhiteSpace(holdDurationSetting)
    && (!int.TryParse(holdDurationSetting, out holdDurationSeconds) || holdDurationSeconds <= 0))
{
    // A clean exit code 1; an unhandled exception here makes the container exit with 139.
    Console.Error.WriteLine(
        $"Configuration error: HOLD_DURATION_SECONDS must be a positive whole number of seconds, but was '{holdDurationSetting}'.");
    Environment.ExitCode = 1;
    return;
}

builder.Services.AddSingleton(new HoldSettings(holdDurationSeconds));

builder.Services.AddDbContext<AdmissionDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(corsOrigin)
    .WithMethods("GET", "POST", "DELETE")
    .WithHeaders("Content-Type", "X-Student-Id")));

// Without this, a binding failure in production ends as a bare 400 with no body.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

// The web defaults would otherwise accept "120" as the number 120.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    options.SerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
});

var app = builder.Build();

await DatabaseMigrator.MigrateAsync(app.Services);

// CORS stays outermost so that error responses are readable by the browser too.
app.UseCors();
app.UseMiddleware<ApiErrorMiddleware>();

app.MapGet("/", () => "Hello World!");
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapClassEndpoints();
app.MapHoldEndpoints();

app.Run();
