using InventorySaaS.Application.Interfaces;

namespace InventorySaaS.Infrastructure.Services.Auth;

public class PasswordHasherService : IPasswordHasher
{
    public string Hash(string password) => PasswordHasher.Hash(password);
    public bool Verify(string password, string passwordHash) => PasswordHasher.Verify(password, passwordHash);
}
