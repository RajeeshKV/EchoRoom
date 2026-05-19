using Chat.Api.Cqrs;
using Chat.Api.DTOs;
using Chat.Api.Services;

namespace Chat.Api.Features.Chat.GetPublicMessages;

public class GetPublicMessagesQueryHandler(IMessageService messageService)
    : IQueryHandler<GetPublicMessagesQuery, IReadOnlyCollection<ChatMessageDto>>
{
    public Task<IReadOnlyCollection<ChatMessageDto>> HandleAsync(GetPublicMessagesQuery query, CancellationToken cancellationToken)
        => messageService.GetPublicMessagesAsync(query.Take, cancellationToken);
}
