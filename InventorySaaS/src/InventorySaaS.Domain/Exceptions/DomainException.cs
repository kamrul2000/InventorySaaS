namespace InventorySaaS.Domain.Exceptions;

public class DomainException(string message) : Exception(message);

public class NotFoundException(string entityName, object key)
    : DomainException($"Entity '{entityName}' with key '{key}' was not found.");

public class ConflictException(string message) : DomainException(message);

public class ForbiddenAccessException(string message = "Access denied.")
    : DomainException(message);

public class BadRequestException(string message) : DomainException(message);
