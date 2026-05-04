using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Notifications.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Notification;
using InventorySaaS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Services;

public class NotificationService : INotificationService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public NotificationService(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<NotificationDto>> GetAllAsync(
        PaginationParams pagination,
        bool? unreadOnly,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;

        var query = _context.Notifications
            .Where(n => n.UserId == null || n.UserId == userId)
            .AsQueryable();

        if (unreadOnly == true)
            query = query.Where(n => !n.IsRead);

        query = query.OrderByDescending(n => n.CreatedAt);

        var projected = query.Select(n => new NotificationDto(
            n.Id,
            n.Type.ToString(),
            n.Title,
            n.Message,
            n.IsRead,
            n.ReadAt,
            n.ActionUrl,
            n.CreatedAt));

        return await PaginatedList<NotificationDto>.CreateAsync(
            projected, pagination.PageNumber, pagination.PageSize, cancellationToken);
    }

    public async Task MarkReadAsync(Guid id, CancellationToken cancellationToken)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(NotificationInfo), id);

        if (notification.IsRead) return;

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllReadAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;

        var unreadNotifications = await _context.Notifications
            .Where(n => !n.IsRead && (n.UserId == null || n.UserId == userId))
            .ToListAsync(cancellationToken);

        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
