namespace InventorySaaS.Application.Features.Chat.DTOs;

public record ChatMessageDto(string Role, string Content);

public record ChatRequest(string Message, List<ChatMessageDto> History);
