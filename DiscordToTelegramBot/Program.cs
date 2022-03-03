

using DiscordToTelegramBot;
using DiscordToTelegramBot.Database;
using DSharpPlus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Extensions.Polling;

await using var services = new ServiceCollection()
    .AddDbContext<ApplicationContext>()
    .AddSingleton<IHandler, Handler>()
    .BuildServiceProvider();


TelegramBotClient telegramClient = new TelegramBotClient(token: Environment.GetEnvironmentVariable("TG_TOKEN"));

var client = new DiscordClient(new DiscordConfiguration
{
    Token = Environment.GetEnvironmentVariable("DISCORD_TOKEN"),
    Intents = DiscordIntents.AllUnprivileged
});


client.Ready += async (client, args) =>
{
    client.Logger.Log(LogLevel.Information, new EventId(999, "Telegram"), "Connecting telegram");

    await using (var scope = services.CreateAsyncScope())
    {
        var service = scope.ServiceProvider.GetRequiredService<IHandler>();
        service.DiscordClient = client;
        service.TelegramBotClient = telegramClient;
        telegramClient.StartReceiving(service.AsyncUpdateHandler, service.HandleError, new ReceiverOptions { }, CancellationToken.None);

        var me = await telegramClient.GetMeAsync();

        client.MessageCreated += service.HandleDiscordMessage;
        client.Logger.Log(LogLevel.Information, new EventId(999, "Telegram"),"Telegram bot @{Username} ready to accept updates", me.Username);
    }
};

await client.ConnectAsync();

await Task.Delay(-1);