using Yarp.ReverseProxy;

var builder = WebApplication.CreateBuilder(args);
var allowedOrigins = (builder.Configuration["Security:AllowedOrigins"] ?? "http://localhost:4200")
    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
builder.Services.AddCors(options => options.AddPolicy("frontend", policy =>
    policy.WithOrigins(allowedOrigins).WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
        .WithHeaders("Authorization", "Content-Type", "X-CSRF-TOKEN").AllowCredentials()));
builder.Services.AddHealthChecks();
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();
app.UseCors("frontend");
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapReverseProxy();
app.Run();

public partial class Program { }
