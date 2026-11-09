using Drovik.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Drovik.Observability;

public static class ObservabilityExtensions
{
    private static readonly PathString HealthPath = new("/health");

    /// <summary>
    /// JSON logs carrying the trace id, plus traces and metrics. The traces always exist so logs can be correlated;
    /// they only leave the process when <c>Observability:OtlpEndpoint</c> is set.
    /// </summary>
    public static void AddDrovikObservability(this WebApplicationBuilder builder, string serviceName)
    {
        builder.Services.AddSerilog((_, logger) => logger
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("service", serviceName)
            .WriteTo.Console(new CompactJsonFormatter()), preserveStaticLogger: true);

        var endpoint = builder.Configuration["Observability:OtlpEndpoint"];
        var exporting = !string.IsNullOrWhiteSpace(endpoint);
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation(options => options.Filter = context => !IsHealthCheck(context))
                    .AddHttpClientInstrumentation()
                    .AddSource(MessagingTelemetry.Name);
                if (exporting) tracing.AddOtlpExporter(options => options.Endpoint = new Uri(endpoint!));
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter(MessagingTelemetry.Name);
                if (exporting) metrics.AddOtlpExporter(options => options.Endpoint = new Uri(endpoint!));
            });
    }

    public static void UseDrovikRequestLogging(this WebApplication app) =>
        app.UseSerilogRequestLogging(options =>
        {
            // Without this it writes to Serilog's static logger, which each host deliberately leaves unconfigured.
            options.Logger = app.Services.GetRequiredService<Serilog.ILogger>();
            options.GetLevel = (context, _, exception) =>
                exception is not null || context.Response.StatusCode >= StatusCodes.Status500InternalServerError ? LogEventLevel.Error
                : IsHealthCheck(context) ? LogEventLevel.Verbose
                : LogEventLevel.Information;
        });

    private static bool IsHealthCheck(HttpContext context) => context.Request.Path.StartsWithSegments(HealthPath);
}
