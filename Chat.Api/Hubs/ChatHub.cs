using System.Security.Claims;
using Chat.Api.DTOs;
using Chat.Api.Helpers;
using Chat.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Chat.Api.Hubs;

[Authorize]
public class ChatHub(
    IUserConnectionService userConnectionService,
    IMessageService messageService,
    IMessagePersistenceQueue messagePersistenceQueue,
    IRateLimitService rateLimitService,
    IBlockedIpService blockedIpService) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var username = GetUsername();
        if (string.IsNullOrWhiteSpace(username))
        {
            Context.Abort();
            return;
        }

        var previousConnectionId = await userConnectionService.RegisterConnectionAsync(username, Context.ConnectionId, Context.ConnectionAborted);
        await userConnectionService.TouchUserAsync(username, Context.ConnectionAborted);

        await Groups.AddToGroupAsync(Context.ConnectionId, userConnectionService.GlobalRoomName, Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, GetUserPrivateGroup(username), Context.ConnectionAborted);

        if (!string.IsNullOrWhiteSpace(previousConnectionId) && previousConnectionId != Context.ConnectionId)
        {
            await Clients.Client(previousConnectionId).SendAsync("SessionReplaced", "You have signed in from another tab or device.", Context.ConnectionAborted);
        }

        await Clients.Group(userConnectionService.GlobalRoomName).SendAsync("UserJoined", username, Context.ConnectionAborted);
        await Clients.Caller.SendAsync("PublicRoomHistory", await messageService.GetPublicMessagesAsync(50, Context.ConnectionAborted), Context.ConnectionAborted);
        await BroadcastActiveUsersAsync();

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var username = GetUsername();
        await userConnectionService.RemoveConnectionAsync(Context.ConnectionId, Context.ConnectionAborted);

        if (!string.IsNullOrWhiteSpace(username))
        {
            await Clients.Group(userConnectionService.GlobalRoomName).SendAsync("UserLeft", username, Context.ConnectionAborted);
            await BroadcastActiveUsersAsync();
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(string message)
        => await SendRichMessage(new SendChatMessageRequest { Message = message });

    public async Task SendRichMessage(SendChatMessageRequest request)
    {
        var username = GetUsername();
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new HubException("Unauthorized.");
        }

        var decision = await EvaluateRateLimitAsync(username);
        if (!decision.IsAllowed)
        {
            throw new HubException(decision.Message ?? "Rate limited.");
        }

        if (decision.ShouldWarn)
        {
            await Clients.Caller.SendAsync("RateLimitWarning", decision.Message, Context.ConnectionAborted);
        }

        var preparedMessage = await messageService.PreparePublicMessageAsync(username, request, Context.ConnectionAborted);
        await userConnectionService.TouchUserAsync(username, Context.ConnectionAborted);
        await Clients.Group(userConnectionService.GlobalRoomName).SendAsync("ReceiveMessage", preparedMessage.Message, Context.ConnectionAborted);
        await messagePersistenceQueue.QueueAsync(preparedMessage.PersistenceItem, Context.ConnectionAborted);
    }

    public async Task JoinPrivateRoom(string otherUsername)
    {
        var username = GetUsername();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(otherUsername))
        {
            throw new HubException("A username is required.");
        }

        await userConnectionService.TouchUserAsync(username, Context.ConnectionAborted);
        var roomKey = userConnectionService.GetPrivateRoomKey(username, otherUsername);
        await Groups.AddToGroupAsync(Context.ConnectionId, roomKey, Context.ConnectionAborted);

        var history = await messageService.GetPrivateMessagesAsync(username, otherUsername, 50, Context.ConnectionAborted);
        await Clients.Caller.SendAsync("PrivateRoomHistory", history, Context.ConnectionAborted);
    }

    public async Task SendPrivateMessage(string receiverUsername, string message)
        => await SendPrivateRichMessage(receiverUsername, new SendChatMessageRequest { Message = message });

    public async Task SendPrivateRichMessage(string receiverUsername, SendChatMessageRequest request)
    {
        var username = GetUsername();
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new HubException("Unauthorized.");
        }

        if (string.Equals(username, receiverUsername, StringComparison.OrdinalIgnoreCase))
        {
            throw new HubException("You cannot send a private message to yourself.");
        }

        var receiverConnectionId = await userConnectionService.GetConnectionIdAsync(receiverUsername, Context.ConnectionAborted);
        if (string.IsNullOrWhiteSpace(receiverConnectionId))
        {
            throw new HubException("That user is not online.");
        }

        var decision = await EvaluateRateLimitAsync(username);
        if (!decision.IsAllowed)
        {
            throw new HubException(decision.Message ?? "Rate limited.");
        }

        if (decision.ShouldWarn)
        {
            await Clients.Caller.SendAsync("RateLimitWarning", decision.Message, Context.ConnectionAborted);
        }

        var preparedMessage = await messageService.PreparePrivateMessageAsync(username, receiverUsername, request, Context.ConnectionAborted);
        await userConnectionService.TouchUserAsync(username, Context.ConnectionAborted);

        await Groups.AddToGroupAsync(receiverConnectionId, preparedMessage.Message.RoomKey, Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, preparedMessage.Message.RoomKey, Context.ConnectionAborted);
        await Clients.Group(preparedMessage.Message.RoomKey).SendAsync("ReceivePrivateMessage", preparedMessage.Message, Context.ConnectionAborted);
        await messagePersistenceQueue.QueueAsync(preparedMessage.PersistenceItem, Context.ConnectionAborted);
    }

    public async Task Typing(string? targetUsername = null)
    {
        var username = GetUsername();
        if (string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        await userConnectionService.TouchUserAsync(username, Context.ConnectionAborted);

        if (string.IsNullOrWhiteSpace(targetUsername))
        {
            await Clients.OthersInGroup(userConnectionService.GlobalRoomName).SendAsync("UserTyping", username, Context.ConnectionAborted);
            return;
        }

        var roomKey = userConnectionService.GetPrivateRoomKey(username, targetUsername);
        await Clients.OthersInGroup(roomKey).SendAsync("UserTypingPrivate", username, targetUsername, Context.ConnectionAborted);
    }

    public async Task Heartbeat()
    {
        var username = GetUsername();
        if (string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        await userConnectionService.TouchUserAsync(username, Context.ConnectionAborted);
    }

    private async Task<RateLimitDecision> EvaluateRateLimitAsync(string username)
    {
        var ipHash = IpHelper.HashIp(IpHelper.GetIpAddress(Context.GetHttpContext()!));
        if (await blockedIpService.IsBlockedAsync(ipHash, Context.ConnectionAborted))
        {
            return new RateLimitDecision(false, false, true, "Your IP is temporarily blocked.", null, null);
        }

        return await rateLimitService.EvaluateAsync(username, ipHash, Context.ConnectionAborted);
    }

    private async Task BroadcastActiveUsersAsync()
    {
        var activeUsers = await userConnectionService.GetActiveUsersAsync(Context.ConnectionAborted);
        await Clients.All.SendAsync("ActiveUsersUpdated", activeUsers, Context.ConnectionAborted);
    }

    private string? GetUsername()
    {
        return Context.User?.FindFirstValue(ClaimTypes.Name)
            ?? Context.User?.FindFirstValue("unique_name")
            ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub");
    }

    private static string GetUserPrivateGroup(string username) => $"user:{username}";
}
