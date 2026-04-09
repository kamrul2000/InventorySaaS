using InventorySaaS.Application.Features.Chat.DTOs;

namespace InventorySaaS.Application.Interfaces;

public interface IAiChatService
{
    IAsyncEnumerable<string> StreamResponseAsync(
        string userMessage,
        List<ChatMessageDto> conversationHistory,
        CancellationToken cancellationToken = default);
}
