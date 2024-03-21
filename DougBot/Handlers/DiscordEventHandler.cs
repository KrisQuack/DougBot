using Discord;
using Discord.WebSocket;
using DougBot.Discord.Notifications;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace DougBot.Handlers;

public class DiscordEventHandler
{
    private readonly CancellationToken _cancellationToken;

    private readonly DiscordSocketClient _client;
    private readonly IServiceScopeFactory _serviceScope;
    private bool _firstReady = true;

    public DiscordEventHandler(DiscordSocketClient client, IServiceScopeFactory serviceScope)
    {
        _client = client;
        _serviceScope = serviceScope;
        _cancellationToken = new CancellationTokenSource().Token;
    }

    private IMediator Mediator
    {
        get
        {
            var scope = _serviceScope.CreateScope();
            return scope.ServiceProvider.GetRequiredService<IMediator>();
        }
    }

    public Task StartAsync()
    {
        _client.Ready += OnReadyAsync;
        _client.UserJoined += OnUserJoinedAsync;
        _client.UserLeft += OnUserLeftAsync;
        _client.GuildMemberUpdated += OnGuildMemberUpdatedAsync;
        _client.MessageReceived += OnMessageReceivedAsync;
        _client.MessageDeleted += OnMessageDeletedAsync;
        _client.MessageUpdated += OnMessageUpdatedAsync;
        _client.ThreadCreated += OnThreadCreatedAsync;

        return Task.CompletedTask;
    }

    private Task OnReadyAsync()
    {
        // Set status message
        _ = _client.SetGameAsync("DM me for Mod support", type: ActivityType.CustomStatus);
        if (_firstReady)
        {
            _firstReady = false;
            return Mediator.Publish(new ReadyNotification(_client), _cancellationToken);
        }

        return Task.CompletedTask;
    }

    private Task OnUserJoinedAsync(SocketGuildUser arg)
    {
        return Mediator.Publish(new UserJoinedNotification(arg), _cancellationToken);
    }

    private Task OnUserLeftAsync(SocketGuild arg1, SocketUser arg2)
    {
        return Mediator.Publish(new UserLeftNotification(arg1, arg2), _cancellationToken);
    }

    private Task OnGuildMemberUpdatedAsync(Cacheable<SocketGuildUser, ulong> arg1, SocketGuildUser arg2)
    {
        return Mediator.Publish(new GuildMemberUpdatedNotification(arg1, arg2), _cancellationToken);
    }

    private Task OnMessageReceivedAsync(SocketMessage arg)
    {
        return Mediator.Publish(new MessageReceivedNotification(arg, _client), _cancellationToken);
    }

    private Task OnMessageDeletedAsync(Cacheable<IMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2)
    {
        return Mediator.Publish(new MessageDeletedNotification(arg1, arg2), _cancellationToken);
    }

    private Task OnMessageUpdatedAsync(Cacheable<IMessage, ulong> arg1, SocketMessage arg2, ISocketMessageChannel arg3)
    {
        return Mediator.Publish(new MessageUpdatedNotification(arg1, arg2, arg3), _cancellationToken);
    }

    private Task OnThreadCreatedAsync(SocketThreadChannel arg)
    {
        return Mediator.Publish(new ThreadCreatedNotification(arg), _cancellationToken);
    }
}