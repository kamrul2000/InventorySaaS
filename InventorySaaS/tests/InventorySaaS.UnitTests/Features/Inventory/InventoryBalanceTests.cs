using FluentAssertions;
using InventorySaaS.Domain.Entities.Inventory;

namespace InventorySaaS.UnitTests.Features.Inventory;

public class InventoryBalanceTests
{
    [Fact]
    public void QuantityAvailable_ShouldBeCalculatedCorrectly()
    {
        var balance = new InventoryBalance
        {
            QuantityOnHand = 100,
            QuantityReserved = 25
        };

        balance.QuantityAvailable.Should().Be(75);
    }

    [Fact]
    public void QuantityAvailable_ShouldBeZero_WhenAllReserved()
    {
        var balance = new InventoryBalance
        {
            QuantityOnHand = 50,
            QuantityReserved = 50
        };

        balance.QuantityAvailable.Should().Be(0);
    }

    [Fact]
    public void InventoryTransaction_ShouldInitializeWithDefaults()
    {
        var transaction = new InventoryTransaction();

        transaction.Id.Should().NotBeEmpty();
        transaction.TransactionDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
