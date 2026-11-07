using Yarp.ReverseProxy;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks();
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapReverseProxy();
app.Run();

public partial class Program { }
