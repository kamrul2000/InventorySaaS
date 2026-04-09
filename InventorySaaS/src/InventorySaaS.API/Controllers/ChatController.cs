using InventorySaaS.Application.Features.Chat.DTOs;
using InventorySaaS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventorySaaS.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "ViewerUp")]
public class ChatController : ControllerBase
{
    private readonly IAiChatService _chatService;

    public ChatController(IAiChatService chatService) => _chatService = chatService;

    [HttpPost]
    public async Task Chat([FromBody] ChatRequest request)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        await foreach (var chunk in _chatService.StreamResponseAsync(
            request.Message, request.History, HttpContext.RequestAborted))
        {
            var escaped = chunk.Replace("\n", "\\n").Replace("\r", "");
            await Response.WriteAsync($"data: {escaped}\n\n", HttpContext.RequestAborted);
            await Response.Body.FlushAsync(HttpContext.RequestAborted);
        }

        await Response.WriteAsync("data: [DONE]\n\n", HttpContext.RequestAborted);
        await Response.Body.FlushAsync(HttpContext.RequestAborted);
    }
}
