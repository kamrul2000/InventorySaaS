using FluentAssertions;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Entities.Identity;
using InventorySaaS.Domain.Entities.Tenant;

namespace InventorySaaS.IntegrationTests;

public class DomainEntityTests
{
    [Fact]
    public void TenantInfo_ShouldSetDefaultValues()
    {
        var tenant = new TenantInfo
        {
            Name = "Test Company",
            Slug = "test-company"
        };

        tenant.Id.Should().NotBeEmpty();
        tenant.Status.Should().Be(TenantStatus.Active);
        tenant.Currency.Should().Be("USD");
        tenant.Timezone.Should().Be("UTC");
    }

    [Fact]
    public void ApplicationUser_ShouldComputeFullName()
    {
        var user = new ApplicationUser
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@test.com",
            NormalizedEmail = "JOHN@TEST.COM",
            PasswordHash = "hash"
        };

        user.FullName.Should().Be("John Doe");
    }

    [Fact]
    public void RefreshToken_IsActive_ShouldWorkCorrectly()
    {
        var activeToken = new RefreshToken
        {
            Token = "active-token",
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        activeToken.IsActive.Should().BeTrue();

        var expiredToken = new RefreshToken
        {
            Token = "expired-token",
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        };
        expiredToken.IsActive.Should().BeFalse();

        var revokedToken = new RefreshToken
        {
            Token = "revoked-token",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = DateTime.UtcNow
        };
        revokedToken.IsActive.Should().BeFalse();
    }

    [Fact]
    public void SubscriptionPlan_ShouldHoldPricingData()
    {
        var plan = new SubscriptionPlan
        {
            Name = "Professional",
            PlanType = SubscriptionPlanType.Professional,
            MonthlyPrice = 79.99m,
            AnnualPrice = 799.99m,
            MaxUsers = 50,
            MaxProducts = 10000
        };

        plan.MonthlyPrice.Should().Be(79.99m);
        plan.MaxUsers.Should().Be(50);
    }
}
