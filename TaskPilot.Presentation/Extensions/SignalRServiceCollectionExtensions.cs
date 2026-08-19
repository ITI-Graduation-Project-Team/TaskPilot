using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace TaskPilot.Presentation.Extensions;

public static class SignalRServiceCollectionExtensions
{
    public static ISignalRServerBuilder AddTaskPilotSignalR(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var signalR = services.AddSignalR();
        if (!configuration.GetValue<bool>("SignalR:RedisEnabled"))
        {
            return signalR;
        }

        var connectionString = configuration.GetConnectionString("SignalRRedis");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "SignalR Redis is enabled, but ConnectionStrings:SignalRRedis is missing.");
        }

        var channelPrefix = configuration["SignalR:ChannelPrefix"];
        if (string.IsNullOrWhiteSpace(channelPrefix))
        {
            throw new InvalidOperationException(
                "SignalR Redis is enabled, but SignalR:ChannelPrefix is missing.");
        }

        ConfigurationOptions redisConfiguration;
        try
        {
            redisConfiguration = ParseConfiguration(connectionString);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or UriFormatException)
        {
            throw new InvalidOperationException(
                "ConnectionStrings:SignalRRedis is not a valid Redis connection string.", exception);
        }

        redisConfiguration.AbortOnConnectFail = false;
        redisConfiguration.ClientName = $"TaskPilot-{Environment.MachineName}";
        redisConfiguration.ChannelPrefix = RedisChannel.Literal(channelPrefix);

        return signalR.AddStackExchangeRedis(options =>
        {
            options.Configuration = redisConfiguration;
        });
    }

    internal static ConfigurationOptions ParseConfiguration(string connectionString)
    {
        if (!connectionString.StartsWith("redis://", StringComparison.OrdinalIgnoreCase)
            && !connectionString.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase))
        {
            return ConfigurationOptions.Parse(connectionString);
        }

        var uri = new Uri(connectionString, UriKind.Absolute);
        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new FormatException("The Redis endpoint is missing.");
        }

        var useTls = uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase);
        var options = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            Ssl = useTls,
            SslHost = useTls ? uri.Host : null
        };
        options.EndPoints.Add(uri.Host, uri.IsDefaultPort ? 6379 : uri.Port);

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var credentials = uri.UserInfo.Split(':', 2);
            if (!string.IsNullOrWhiteSpace(credentials[0]))
            {
                options.User = Uri.UnescapeDataString(credentials[0]);
            }

            if (credentials.Length == 2)
            {
                options.Password = Uri.UnescapeDataString(credentials[1]);
            }
        }

        return options;
    }
}
