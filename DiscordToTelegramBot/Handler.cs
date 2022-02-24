using DSharpPlus;
using DSharpPlus.Entities;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace DiscordToTelegramBot;

public class Handler
{

    public Dictionary<int, DiscordMessage> MessageCache { get; set; } = new Dictionary<int, DiscordMessage>();

    public DiscordClient DiscordClient { get; set; }
    public async Task AsyncUpdateHandler(ITelegramBotClient client, Update update, CancellationToken token)
    {
        var handler = new Handler();
        new Thread(() => { HandleUpdateAsync(client, update, token).GetAwaiter().GetResult(); }).Start();
    }

    public async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken token)
    {
        if (update.Type == UpdateType.ChannelPost)
        {
            var message = update.ChannelPost;

            var messageBuilder = new DiscordMessageBuilder();

            if (!string.IsNullOrEmpty(message!.Text))
            {
                messageBuilder.WithContent(message.Text);

                if (message.Entities is not null)
                {
                    var messageEntity = message.Entities.FirstOrDefault();
                    if (messageEntity!.Type == MessageEntityType.TextLink)
                    {
                        messageBuilder.Content += $"\nURL: {messageEntity.Url}";
                    }
                }
            }

            if (message.Photo is not null)
            {
                var photo = message.Photo.Last();

                var file = await client.GetFileAsync(photo.FileId);


                var memoryStream = new MemoryStream();

                await client.DownloadFileAsync(file.FilePath, memoryStream, token);
                memoryStream.Seek(0, SeekOrigin.Begin);

                messageBuilder.WithFile(Path.GetFileName(file.FilePath), memoryStream, false);

                if (!string.IsNullOrEmpty(message.Caption))
                    messageBuilder.WithContent(message!.Caption);

                if (message.CaptionEntities is not null)
                {
                    var messageEntity = message.Entities.FirstOrDefault();
                    if (messageEntity!.Type == MessageEntityType.TextLink)
                    {
                        messageBuilder.Content += $"\nURL: {messageEntity.Url}";
                    }
                }
            }

            if (message.Document is not null)
            {
                var memoryStream = new MemoryStream();
                var file = await client.GetInfoAndDownloadFileAsync(message.Document.FileId, memoryStream);

                memoryStream.Seek(0, SeekOrigin.Begin);

                messageBuilder.WithFile(Path.GetFileName(file.FilePath), memoryStream, false);

                if (message.CaptionEntities is not null)
                {
                    var messageEntity = message.Entities.FirstOrDefault();
                    if (messageEntity!.Type == MessageEntityType.TextLink)
                    {
                        messageBuilder.Content += $"\nURL: {messageEntity.Url}";
                    }
                }
            }

            if (message.Poll is not null)
            {
                var poll = message.Poll;

                messageBuilder.WithContent($"Poll:\nOptions: {String.Join("\n", poll.Options.Select(a => a.Text))}");
                messageBuilder.AddComponents(new DiscordLinkButtonComponent($"https://t.me/c/{message.Chat.Id}/{message.MessageId}", "Проголосовать", false, new DiscordComponentEmoji("✈️")));
            }

            if (!string.IsNullOrEmpty(update!.ChannelPost!.AuthorSignature))
                messageBuilder.WithContent($"{messageBuilder.Content}\n\nПост от **{update!.ChannelPost!.AuthorSignature}**");


            var channel = await DiscordClient.GetChannelAsync(ulong.Parse(Environment.GetEnvironmentVariable("POST_CHANNEL_ID")!));

            var discordMessage = await channel.SendMessageAsync(messageBuilder);

            if (message.ForwardFromChat is not null)
            {
                messageBuilder.AddComponents(new DiscordLinkButtonComponent($"https://t.me/c/{message.ForwardFromChat.Id}/{message.ForwardFromMessageId}", "Источник", false, new DiscordComponentEmoji("✈️")));
            }

            await discordMessage.CreateThreadAsync("Обсуждение", AutoArchiveDuration.Hour, "Auto-post creation....");

            MessageCache.Add(message.MessageId, discordMessage);
        }

        if (update.Type == UpdateType.EditedChannelPost)
        {
            var post = update.EditedChannelPost;



            if (!MessageCache.TryGetValue(post!.MessageId, out var message))
                return;

            var messageBuilder = new DiscordMessageBuilder();


            if (!string.IsNullOrEmpty(post.Text))
            {

                if (post.Entities is not null)
                {
                    var messageEntity = post!.Entities!.First();
                    if (messageEntity!.Type == MessageEntityType.TextLink)
                    {
                        messageBuilder.Content += $"\nURL: {messageEntity.Url}";
                    }
                }
                messageBuilder.WithContent($"{post.Text}\n\nПост от **{post!.AuthorSignature}**");
            }

            if (!string.IsNullOrEmpty(post.Caption))
            {
                messageBuilder.WithContent($"{post.Caption}\n\nПост от **{post!.AuthorSignature}**");
                if (post.CaptionEntities is not null)
                {
                    var messageEntity = post!.CaptionEntities!.First();
                    if (messageEntity!.Type == MessageEntityType.TextLink)
                    {
                        messageBuilder.Content += $"\nURL: {messageEntity.Url}";
                    }
                }

                await message.ModifyAsync(messageBuilder);
            }

        }
    }


    public async Task HandleError(ITelegramBotClient client, Exception exception, CancellationToken token)
    {
        Console.WriteLine($"Error\n{exception.Message}\n{exception.StackTrace}");
    }
}


