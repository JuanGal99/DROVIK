namespace Drovik.Messaging;

public sealed class RabbitMqOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Exchange { get; set; } = "drovik.events.v1";
    public string DeadLetterExchange { get; set; } = "drovik.events.dlx.v1";
    public int MaxAttempts { get; set; } = 5;
    public int PollSeconds { get; set; } = 2;
}
