using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Notifications.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Notifications.Queries;

public record GetNotificationsQuery(PaginationParams Pagination, bool? UnreadOnly = null) : IRequest<Result<PaginatedList<NotificationDto>>>;

public class GetNotificationsQueryHandler : IRequestHandler<GetNotificationsQuery, Result<PaginatedList<NotificationDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetNotificationsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PaginatedList<NotificationDto>>> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;

        var query = _context.Notifications
            .Where(n => n.UserId == null || n.UserId == userId)
            .AsQueryable();

        if (request.UnreadOnly == true)
            query = query.Where(n => !n.IsRead);

        query = query.OrderByDescending(n => n.CreatedAt);

        var projectedQuery = query.Select(n => new NotificationDto(
            n.Id,
            n.Type.ToString(),
            n.Title,
            n.Message,
            n.IsRead,
            n.ReadAt,
            n.ActionUrl,
            n.CreatedAt));

        var result = await PaginatedList<NotificationDto>.CreateAsync(
            projectedQuery,
            request.Pagination.PageNumber,
            request.Pagination.PageSize,
            cancellationToken);

        return Result<PaginatedList<NotificationDto>>.Success(result);
    }
}
