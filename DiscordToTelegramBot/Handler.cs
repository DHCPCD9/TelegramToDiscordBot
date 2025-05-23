﻿using DiscordToTelegramBot.Database;
using DiscordToTelegramBot.Database.Tables;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
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

    public async Task HandleDiscordMessage(DiscordClient sender, MessageCreatedEventArgs args)
    {
        var message = args.Message;

        if (message.Channel.Type != DiscordChannelType.PublicThread && message.Channel.Type != DiscordChannelType.PrivateThread &&
            message.Channel.Type != DiscordChannelType.NewsThread)
            return;

        if (message.Author.IsBot)
            return;


        var thread = args.Guild.Threads.FirstOrDefault(t => t.Key == message.Channel.Id).Value;

        var context = GetContext();

        var messageInDb = await context.Messages.FirstOrDefaultAsync(m => m.ThreadId == thread.Id);

        if (messageInDb is null)
            return;

        var channel = await TelegramBotClient.GetChat(messageInDb.TelegramChannelId);
        var chat = await TelegramBotClient.GetChat(channel.LinkedChatId!);

        await TelegramBotClient.SendMessage(chat.Id,
            $"[Discord]{message.Author.Username}\n\n{message.Content}",
            replyParameters: new ReplyParameters
            {
                MessageId = messageInDb.MessageIdInChat
            });
    }

    public async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken token)
    {
        var context = GetContext();
        if (update.Type == UpdateType.ChannelPost)
        {
            var message = update.ChannelPost;

            var messageBuilder = new DiscordMessageBuilder();

            if (message.Text is not null)
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


            if (message.NewChatPhoto is not null)
            {
                messageBuilder.WithContent("Channel icon updated.");

                var stream = new MemoryStream();


                var file = await client.GetInfoAndDownloadFile(message.NewChatPhoto.Last().FileId, stream, token);
                stream.Seek(0, SeekOrigin.Begin);
                messageBuilder.AddFile(Path.GetFileName(file.FilePath), stream);
            }

            if (message.Photo is not null)
            {
                var photo = message.Photo.Last();

                var file = await client.GetFile(photo.FileId);


                var memoryStream = new MemoryStream();

                await client.DownloadFile(file.FilePath, memoryStream, token);
                memoryStream.Seek(0, SeekOrigin.Begin);

                messageBuilder.AddFile(Path.GetFileName(file.FilePath), memoryStream, false);

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
                var file = await client.GetInfoAndDownloadFile(message.Video.FileId, memoryStream);

                memoryStream.Seek(0, SeekOrigin.Begin);

                if (memoryStream.Length < 1024 * 8)
                {
                    messageBuilder.AddFile(Path.GetFileName(file.FilePath), memoryStream, false);
                }


                if (message.CaptionEntities is not null)
                {
                    if (message.Entities is not null)
                    {
                        foreach (var entity in message.Entities)
                        {
                            if (entity.Type == MessageEntityType.TextLink)
                            {
                                messageBuilder.Content += $"\nURL: {entity.Url}";
                            }
                        }
                    }
                }
            }

            if (message.Poll is not null)
            {
                var poll = message.Poll;

                messageBuilder.WithContent($"Poll:\nOptions: {String.Join("\n", poll.Options.Select(a => a.Text))}");
                messageBuilder.AddComponents(new DiscordLinkButtonComponent(
                    $"https://t.me/c/{message.Chat.Id}/{message.MessageId}", "Vote", false,
                    new DiscordComponentEmoji("✈️")));
            }

            if (!string.IsNullOrEmpty(update!.ChannelPost!.AuthorSignature))
                messageBuilder.WithContent(
                    $"{messageBuilder.Content}\n\nPost from **{update!.ChannelPost!.AuthorSignature}**");


            if (message.ForwardFromChat is not null)
            {
                if (message.ForwardFromChat.Username is not null)
                {
                    messageBuilder.AddComponents(new DiscordLinkButtonComponent(
                        $"https://t.me/{message.ForwardFromChat.Username}/{message.ForwardFromMessageId}", "Источник",
                        false, new DiscordComponentEmoji("✈️")));
                }
                else
                {
                    messageBuilder.AddComponents(new DiscordLinkButtonComponent(
                        $"https://t.me/c/{message.ForwardFromChat.Id}/{message.ForwardFromMessageId}",
                        "Источник (приватный)", false, new DiscordComponentEmoji("✈️")));
                }

                messageBuilder.WithContent($"Forwarded from {message.ForwardFromChat.Title}\n" +
                                           messageBuilder.Content);
            }

            if (message.Sticker is not null)
            {
                var sticker = message.Sticker;
                var stream = new MemoryStream();

                var file = await client.GetInfoAndDownloadFile(sticker.FileId, stream);
                stream.Seek(0, SeekOrigin.Begin);

                var pngStream = new MemoryStream();
                using (var image = Image.Load(stream))
                {
                    image.SaveAsPng(pngStream);
                }

                pngStream.Seek(0, SeekOrigin.Begin);
            }


            var channel =
                await DiscordClient.GetChannelAsync(
                    ulong.Parse(Environment.GetEnvironmentVariable("POST_CHANNEL_ID")!));

            var discordMessage = await channel.SendMessageAsync(messageBuilder);


            var thread =
                await discordMessage.CreateThreadAsync("Discussion", DiscordAutoArchiveDuration.Hour,
                    "Auto-post creation....");

            await context.AddAsync(new DatabaseMessages
            {
                DiscordId = discordMessage.Id,
                TelegramId = message.MessageId,
                DiscordChannelId = channel.Id,
                TelegramChannelId = message.Chat.Id,
                ThreadId = thread.Id
            });

            DiscordClient.Logger.Log(LogLevel.Information, new EventId(999, "Telegram"), "Processed message: {Message}",
                message.MessageId);
            await context.SaveChangesAsync();
        }

        if (update.Type == UpdateType.EditedChannelPost)
        {
            var post = update.EditedChannelPost;


            var messageInDatabase =
                await context.Messages.FirstOrDefaultAsync(m => m.TelegramId == post!.MessageId,
                    cancellationToken: token);
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

                messageBuilder.WithContent($"{post.Text}\n\nPost from **{post!.AuthorSignature}**");
            }

            if (!string.IsNullOrEmpty(post.Caption))
            {
                messageBuilder.WithContent($"{post.Caption}\n\nPost from **{post!.AuthorSignature}**");
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


            if (message.ForwardFromChat is not null)
            {
                var dbMessage =
                    await context.Messages.FirstOrDefaultAsync(m => m.TelegramId == message.ForwardFromMessageId,
                        token);

                if (dbMessage is not null)
                {
                    dbMessage.MessageIdInChat = message.MessageId;

                    await context.SaveChangesAsync(token);
                }

                await context.SaveChangesAsync(token);
            }


            if (message.ReplyToMessage is null)
                return;


            var chat = await client.GetChat(message.ReplyToMessage.Chat.Id, token);

            var messageInDb =
                await context.Messages.FirstOrDefaultAsync(
                    m => m.TelegramId == message.ReplyToMessage.ForwardFromMessageId, token);

            if (messageInDb is null)
                return;


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
            }
            else if (message.Caption is not null || message.Photo is not null || message.Audio is not null ||
                     message.Video is not null || message.Voice is not null)
            {
                embed.WithDescription(message.Caption);

                if (message.Photo is not null)
                {
                    var stream = await GetFileStream(client, message.Photo.Last().FileId);
                    var file = await client.GetFile(message.Photo.Last().FileId, cancellationToken: token);
                    embed.WithImageUrl($"attachment://{Path.GetFileName(file.FilePath)}");
                    builder.AddFile(Path.GetFileName(file.FilePath) ?? "photo.png", stream);
                }

                if (message.Video is not null)
                {
                    var stream = await GetFileStream(client, message.Video.FileId);
                    var file = await client.GetFile(message.Video.FileId, token);
                    builder.AddFile(Path.GetFileName(file.FilePath) ?? "video.mp4", stream);
                }

                if (message.Voice is not null)
                {
                    var stream = await GetFileStream(client, message.Voice.FileId);
                    var file = await client.GetFile(message.Voice.FileId, token);
                    builder.AddFile(Path.GetFileName(file.FilePath) ?? "audio.mp3", stream);
                }

                if (message.Audio is not null)
                {
                    var stream = await GetFileStream(client, message.Audio.FileId);
                    var file = await client.GetFile(message.Audio.FileId, token);
                    builder.AddFile(Path.GetFileName(file.FilePath) ?? "audio.mp3", stream);
                }
            }

            embed.WithColor(DiscordColor.Aquamarine);
            await thread.SendMessageAsync(builder.AddEmbed(embed));
        }
    }

    public async Task<MemoryStream> GetFileStream(ITelegramBotClient client, string fileId)
    {
        var stream = new MemoryStream();

        await client.GetInfoAndDownloadFile(fileId, stream);
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
