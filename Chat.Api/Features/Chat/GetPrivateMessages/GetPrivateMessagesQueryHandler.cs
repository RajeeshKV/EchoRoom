using Chat.Api.Cqrs;
using Chat.Api.DTOs;
using Chat.Api.Services;

namespace Chat.Api.Features.Chat.GetPrivateMessages;

public class GetPrivateMessagesQueryHandler(IMessageService messageService)
    : IQueryHandler<GetPrivateMessagesQuery, IReadOnlyCollection<PrivateMessageDto>>
{
    public Task<IReadOnlyCollection<PrivateMessageDto>> HandleAsync(GetPrivateMessagesQuery query, CancellationToken cancellationToken)
        => messageService.GetPrivateMessagesAsync(query.CurrentUsername, query.OtherUsername, query.Take, cancellationToken);
}
