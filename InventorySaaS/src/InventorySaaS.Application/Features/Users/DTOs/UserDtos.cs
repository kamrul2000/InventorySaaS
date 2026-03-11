namespace InventorySaaS.Application.Features.Users.DTOs;

public record UserDto(
    Guid Id,
    string Email,
    string? FirstName,
    string? LastName,
    string? Phone,
    bool IsActive,
    List<string> Roles,
    DateTime CreatedAt);

public record CreateUserRequest(
    string Email,
    string FirstName,
    string LastName,
    string Password,
    string? Phone,
    List<string> Roles);

public record UpdateUserRequest(
    string? FirstName,
    string? LastName,
    string? Phone,
    bool? IsActive,
    List<string>? Roles);

public record InviteUserRequest(
    string Email,
    string FirstName,
    string LastName,
    List<string> Roles);
