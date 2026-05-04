using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Notifications.DTOs;

namespace InventorySaaS.Application.Services;

public interface INotificationService
{
    Task<PaginatedList<NotificationDto>> GetAllAsync(
        PaginationParams pagination,
        bool? unreadOnly,
        CancellationToken cancellationToken);

    Task MarkReadAsync(Guid id, CancellationToken cancellationToken);
    Task MarkAllReadAsync(CancellationToken cancellationToken);
}
