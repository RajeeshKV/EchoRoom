using Chat.Api.Cqrs;
using Chat.Api.DTOs;

namespace Chat.Api.Features.Chat.GetPublicMessages;

public sealed record GetPublicMessagesQuery(int Take) : IQuery<IReadOnlyCollection<ChatMessageDto>>;
