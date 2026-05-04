using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Users.DTOs;

namespace InventorySaaS.Application.Services;

public interface IUserService
{
    Task<PaginatedList<UserDto>> GetAllAsync(PaginationParams pagination, CancellationToken cancellationToken);
    Task<UserDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);
    Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken);
    Task InviteAsync(InviteUserRequest request, CancellationToken cancellationToken);
}
