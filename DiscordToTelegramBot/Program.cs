

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

var builder = DiscordClientBuilder.CreateDefault(Environment.GetEnvironmentVariable("DISCORD_TOKEN"),
    DiscordIntents.AllUnprivileged);

await using (var scope = services.CreateAsyncScope())
{
    var service = scope.ServiceProvider.GetRequiredService<IHandler>();
    service.TelegramBotClient = telegramClient;



    builder.ConfigureEventHandlers(b => b.HandleSessionCreated(async (client, args) =>
    {
        service.DiscordClient = client;

        client.Logger.Log(LogLevel.Information, new EventId(999, "Telegram"), "Connecting telegram");
        
        
        var me = await telegramClient.GetMe();
        telegramClient.StartReceiving(service.AsyncUpdateHandler, service.HandleError, new ReceiverOptions { }, CancellationToken.None);

        client.Logger.Log(LogLevel.Information, new EventId(999, "Telegram"),"Telegram bot @{Username} ready to accept updates", me.Username);
    }).HandleMessageCreated(service.HandleDiscordMessage));

}

var client = builder.Build();
await client.ConnectAsync();

await Task.Delay(-1);