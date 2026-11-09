using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Drovik.Foundation.Tests;

public class GatewayProxyTests
{
    [Theory]
    [InlineData("/api/v1/auth/login", "identity")]
    [InlineData("/api/v1/users", "identity")]
    [InlineData("/api/v1/vehicles", "fleet")]
    [InlineData("/api/v1/drivers", "fleet")]
    [InlineData("/api/v1/centers", "fleet")]
    [InlineData("/api/v1/shipments", "shipment")]
    [InlineData("/api/v1/customers", "shipment")]
    [InlineData("/api/v1/simulations/run-1/start", "simulation")]
    [InlineData("/hubs/operations/negotiate", "simulation")]
    [InlineData("/api/v1/incidents", "incident")]
    public async Task Proxy_forwards_request_to_owning_service(string requestPath, string clusterId)
    {
        await using var destination = new StubDestination();
        var destinationAddress = await destination.StartAsync();

        using var factory = new WebApplicationFactory<Drovik.Gateway.Api.GatewayAssemblyMarker>().WithWebHostBuilder(webHost =>
            webHost.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"ReverseProxy:Clusters:{clusterId}:Destinations:local:Address"] = destinationAddress
                })));

        using var client = factory.CreateClient();
        using var response = await client.GetAsync(requestPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var echoedPath = await response.Content.ReadFromJsonAsync<string>();
        Assert.Equal(requestPath, echoedPath);
    }

    [Theory]
    [InlineData("/api/v1/unknown-resource")]
    [InlineData("/api/v1/reservations")]
    public async Task Paths_outside_the_browser_contract_are_not_forwarded(string path)
    {
        using var factory = new WebApplicationFactory<Drovik.Gateway.Api.GatewayAssemblyMarker>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cors_allows_credentials_only_for_the_local_angular_origin()
    {
        using var factory = new WebApplicationFactory<Drovik.Gateway.Api.GatewayAssemblyMarker>();
        using var client = factory.CreateClient();
        using var allowedRequest = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/refresh");
        allowedRequest.Headers.Add("Origin", "http://localhost:4200");
        allowedRequest.Headers.Add("Access-Control-Request-Method", "POST");
        allowedRequest.Headers.Add("Access-Control-Request-Headers", "content-type,x-csrf-token");
        using var allowedResponse = await client.SendAsync(allowedRequest);

        Assert.Equal("http://localhost:4200", allowedResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains("true", allowedResponse.Headers.GetValues("Access-Control-Allow-Credentials"));

        using var rejectedRequest = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/refresh");
        rejectedRequest.Headers.Add("Origin", "http://evil.example");
        rejectedRequest.Headers.Add("Access-Control-Request-Method", "POST");
        using var rejectedResponse = await client.SendAsync(rejectedRequest);

        Assert.False(rejectedResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private sealed class StubDestination : IAsyncDisposable
    {
        private readonly WebApplication _application;

        public StubDestination()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            _application = builder.Build();
            _application.MapGet("/{**catch-all}", (HttpRequest request) => Results.Json(request.Path.Value));
        }

        public async Task<string> StartAsync()
        {
            await _application.StartAsync();
            var server = _application.Services.GetRequiredService<IServer>();
            return server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        }

        public async ValueTask DisposeAsync()
        {
            await _application.DisposeAsync();
        }
    }
}
