using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskPilot.Presentation.Extensions;

namespace TaskPilot.Tests;

public sealed class SignalRRedisConfigurationTests
{
    [Fact]
    public void ParseConfiguration_ParsesRedissUriWithEscapedCredentials()
    {
        var options = SignalRServiceCollectionExtensions.ParseConfiguration(
            "rediss://default:p%40ss@example.redis.local:6379");

        var endpoint = Assert.IsType<DnsEndPoint>(Assert.Single(options.EndPoints));
        Assert.Equal("example.redis.local", endpoint.Host);
        Assert.Equal(6379, endpoint.Port);
        Assert.True(options.Ssl);
        Assert.Equal("example.redis.local", options.SslHost);
        Assert.Equal("default", options.User);
        Assert.Equal("p@ss", options.Password);
        Assert.False(options.AbortOnConnectFail);
    }

    [Fact]
    public void AddTaskPilotSignalR_RejectsEnabledBackplaneWithoutConnectionString()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SignalR:RedisEnabled"] = "true",
                ["SignalR:ChannelPrefix"] = "TaskPilot:Test"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddTaskPilotSignalR(configuration));

        Assert.Contains("ConnectionStrings:SignalRRedis is missing", exception.Message);
    }
}
