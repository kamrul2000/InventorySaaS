using FluentAssertions;
using InventorySaaS.Infrastructure.Services.Auth;

namespace InventorySaaS.UnitTests.Features.Auth;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_ShouldReturnNonEmptyString()
    {
        var hash = PasswordHasher.Hash("TestPassword123");
        hash.Should().NotBeNullOrEmpty();
        hash.Should().Contain(".");
    }

    [Fact]
    public void Verify_ShouldReturnTrue_ForCorrectPassword()
    {
        var password = "TestPassword123";
        var hash = PasswordHasher.Hash(password);
        PasswordHasher.Verify(password, hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_ShouldReturnFalse_ForIncorrectPassword()
    {
        var hash = PasswordHasher.Hash("CorrectPassword");
        PasswordHasher.Verify("WrongPassword", hash).Should().BeFalse();
    }

    [Fact]
    public void Hash_ShouldProduceDifferentHashes_ForSamePassword()
    {
        var hash1 = PasswordHasher.Hash("SamePassword");
        var hash2 = PasswordHasher.Hash("SamePassword");
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Verify_ShouldReturnFalse_ForInvalidHashFormat()
    {
        PasswordHasher.Verify("password", "invalidhash").Should().BeFalse();
    }
}
