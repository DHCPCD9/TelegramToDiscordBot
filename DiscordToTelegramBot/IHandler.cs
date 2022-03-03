using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.EventArgs;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace DiscordToTelegramBot
{
    public interface IHandler
    {
        public DiscordClient DiscordClient { get; set; }
        
        public ITelegramBotClient TelegramBotClient { get; set; }

        public Task HandleDiscordMessage(DiscordClient sender, MessageCreateEventArgs args);
        public Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken token);

        public Task HandleError(ITelegramBotClient client, Exception exception, CancellationToken token);

        public Task AsyncUpdateHandler(ITelegramBotClient client, Update update, CancellationToken token);
    }
}
