using DiscordToTelegramBot.Database;
using DiscordToTelegramBot.Database.Tables;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using File = System.IO.File;

namespace DiscordToTelegramBot;

public class Handler : IHandler, IServiceScope
{

    public Dictionary<int, DiscordMessage> MessageCache { get; set; } = new Dictionary<int, DiscordMessage>();
    public DiscordClient DiscordClient { get; set; } = null!;

    public ITelegramBotClient TelegramBotClient { get; set; }

    public async Task AsyncUpdateHandler(ITelegramBotClient client, Update update, CancellationToken token)
    {
        await HandleUpdateAsync(client, update, token);
    }
    
    private IServiceScope _services { get; }

    public Handler(IServiceProvider services)
    {
        ServiceProvider = services;
        _services = services.CreateScope();
    }

    public ApplicationContext GetContext()
    {
        var scope = ServiceProvider.CreateScope();

        return scope.ServiceProvider.GetRequiredService<ApplicationContext>();
    }

    public async Task HandleDiscordMessage(DiscordClient sender, MessageCreateEventArgs args)
    {
        var message = args.Message;
        
        if (message.Channel.Type != ChannelType.PublicThread && message.Channel.Type != ChannelType.PrivateThread && message.Channel.Type != ChannelType.NewsThread)
            return;
        
        if (message.Author.IsBot)
            return;
        
        
        var thread = args.Guild.Threads.FirstOrDefault(t => t.Key == message.Channel.Id).Value;

        var context = GetContext();

        var messageInDb = await context.Messages.FirstOrDefaultAsync(m => m.ThreadId == thread.Id);
        
        if (messageInDb is null)
            return;
        
        var channel = await TelegramBotClient.GetChatAsync(messageInDb.TelegramChannelId);
        var chat = await TelegramBotClient.GetChatAsync(channel.LinkedChatId!);

        await TelegramBotClient.SendTextMessageAsync(chat.Id, $"[Discord]{message.Author.Username}\n\n{message.Content}",
            replyToMessageId: messageInDb.MessageIdInChat);
    }
    public async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken token)
    {

        var context = GetContext();
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

            if (message.Video is not null)
            {
                var memoryStream = new MemoryStream();
                var file = await client.GetInfoAndDownloadFileAsync(message.Video.FileId, memoryStream);

                memoryStream.Seek(0, SeekOrigin.Begin);

                messageBuilder.WithFile(Path.GetFileName(file.FilePath), memoryStream, false);

                if (message.CaptionEntities is not null)
                {

                    foreach(var entity in message.Entities) {
                        if (entity.Type == MessageEntityType.TextLink)
                        {
                            messageBuilder.Content += $"\nURL: {entity.Url}";
                        }
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
            
            
            if (message.ForwardFromChat is not null)
            {

                if (message.ForwardFromChat.Username is not null)
                {
                    messageBuilder.AddComponents(new DiscordLinkButtonComponent($"https://t.me/{message.ForwardFromChat.Username}/{message.ForwardFromMessageId}", "Источник", false, new DiscordComponentEmoji("✈️")));
                }
                else
                {
                    messageBuilder.AddComponents(new DiscordLinkButtonComponent($"https://t.me/c/{message.ForwardFromChat.Id}/{message.ForwardFromMessageId}", "Источник (приватный)", false, new DiscordComponentEmoji("✈️")));
                }
            }


            var channel = await DiscordClient.GetChannelAsync(ulong.Parse(Environment.GetEnvironmentVariable("POST_CHANNEL_ID")!));

            var discordMessage = await channel.SendMessageAsync(messageBuilder);



            var thread = await discordMessage.CreateThreadAsync("Обсуждение", AutoArchiveDuration.Hour, "Auto-post creation....");

            await context.AddAsync(new DatabaseMessages
            {
                DiscordId = discordMessage.Id,
                TelegramId = message.MessageId,
                DiscordChannelId = channel.Id,
                TelegramChannelId = message.Chat.Id,
                ThreadId = thread.Id
            });
            
            DiscordClient.Logger.Log(LogLevel.Information, new EventId(999, "Telegram"), "Processed message: {Message}", message.MessageId);
            await context.SaveChangesAsync();
        }

        if (update.Type == UpdateType.EditedChannelPost)
        {
            var post = update.EditedChannelPost;


            var messageInDatabase = await context.Messages.FirstOrDefaultAsync(m => m.TelegramId == post!.MessageId, cancellationToken: token);
            if (messageInDatabase is null)
                return;

            var channel = await DiscordClient.GetChannelAsync(messageInDatabase.DiscordChannelId);

            var message = await channel.GetMessageAsync(messageInDatabase.DiscordId);
            
            var messageBuilder = new DiscordMessageBuilder();


            if (!string.IsNullOrEmpty(post!.Text))
            {

                if (post.Entities is not null)
                {
                    var messageEntity = post!.Entities!.First();
                    if (messageEntity.Type == MessageEntityType.TextLink)
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

        if (update.Type == UpdateType.Message)
        {
            var message = update.Message;

            
            if (message.ReplyToMessage is null)
                return;

            var chat = await client.GetChatAsync(message.ReplyToMessage.Chat.Id, token);
            
            var messageInDb = await context.Messages.FirstOrDefaultAsync(m => m.TelegramId == message.ReplyToMessage.ForwardFromMessageId, token);
    
            if (messageInDb is null)
                return;

            if (messageInDb.MessageIdInChat == 0)
            {
                messageInDb.MessageIdInChat = message.MessageId;

                await context.SaveChangesAsync(token);     
            }
            var channel = await DiscordClient.GetChannelAsync(messageInDb.DiscordChannelId);

            var discordMessage = await channel.GetMessageAsync(messageInDb.DiscordId);

            var thread = channel.Guild.Threads.FirstOrDefault(t => t.Value.Id == messageInDb.ThreadId).Value;
            

            var embed = new DiscordEmbedBuilder()
                .WithAuthor(message.From.FirstName,
                    message.From.Username is not null ? $"https://t.me/{message.From.Username}" : null);
            var builder = new DiscordMessageBuilder();

            if (message.Text is not null)
            {
                embed.WithDescription(message.Text);
            }else if (message.Caption is not null || message.Photo is not null || message.Audio is not null|| message.Video is not null || message.Voice is not null)
            {
                embed.WithDescription(message.Caption);

                if (message.Photo is not null)
                {
                    var stream = await GetFileStream(client, message.Photo.Last().FileId);
                    var file = await client.GetFileAsync(message.Photo.Last().FileId);
                    embed.WithImageUrl($"attachment://{Path.GetFileName(file.FilePath)}");
                    builder.WithFile(Path.GetFileName(file.FilePath), stream);
                }
                
                if (message.Video is not null)
                {
                    var stream = await GetFileStream(client, message.Video.FileId);
                    var file = await client.GetFileAsync(message.Video.FileId, token);
                    builder.WithFile(Path.GetFileName(file.FilePath), stream);
                }
                
                if (message.Voice is not null)
                {
                    var stream = await GetFileStream(client, message.Voice.FileId);
                    var file = await client.GetFileAsync(message.Voice.FileId, token);
                    builder.WithFile(Path.GetFileName(file.FilePath), stream);
                }
                
                if (message.Audio is not null)
                {
                    var stream = await GetFileStream(client, message.Audio.FileId);
                    var file = await client.GetFileAsync(message.Audio.FileId, token);
                    builder.WithFile(Path.GetFileName(file.FilePath), stream);
                }
            }

            embed.WithColor(DiscordColor.Aquamarine);
            await thread.SendMessageAsync(builder.WithEmbed(embed));

        }
    }

    public async Task<MemoryStream> GetFileStream(ITelegramBotClient client, string fileId)
    {
        var stream = new MemoryStream();

        await client.GetInfoAndDownloadFileAsync(fileId, stream);
        stream.Seek(0, SeekOrigin.Begin);

        return stream;
    }


    public async Task HandleError(ITelegramBotClient client, Exception exception, CancellationToken token)
    {
        Console.WriteLine($"Error\n{exception.Message}\n{exception.StackTrace}");
    }

    public void Dispose()
    {
        _services.Dispose();
    }


    public IServiceProvider ServiceProvider { get; }
}


