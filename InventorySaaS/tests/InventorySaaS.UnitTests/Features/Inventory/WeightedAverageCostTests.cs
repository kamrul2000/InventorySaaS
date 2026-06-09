using FluentAssertions;
using InventorySaaS.Domain.Entities.Inventory;

namespace InventorySaaS.UnitTests.Features.Inventory;

/// <summary>
/// Tests for moving weighted-average costing on <see cref="InventoryBalance.ApplyInbound"/>.
/// Guards against the prior behaviour where inbound stock simply overwrote unit cost.
/// </summary>
public class WeightedAverageCostTests
{
    [Fact]
    public void ApplyInbound_OnEmptyBalance_UsesIncomingCost()
    {
        var balance = new InventoryBalance { QuantityOnHand = 0, UnitCost = 0 };

        balance.ApplyInbound(10, 5.00m);

        balance.QuantityOnHand.Should().Be(10);
        balance.UnitCost.Should().Be(5.00m);
    }

    [Fact]
    public void ApplyInbound_BlendsCost_AsWeightedAverage()
    {
        // 10 @ 5.00 = 50.00, then 10 @ 7.00 = 70.00 => 20 units, total 120.00 => 6.00 avg.
        var balance = new InventoryBalance { QuantityOnHand = 10, UnitCost = 5.00m };

        balance.ApplyInbound(10, 7.00m);

        balance.QuantityOnHand.Should().Be(20);
        balance.UnitCost.Should().Be(6.00m);
    }

    [Fact]
    public void ApplyInbound_WeightsByQuantity_NotJustAveragingPrices()
    {
        // 30 @ 2.00 = 60.00, then 10 @ 10.00 = 100.00 => 40 units, total 160.00 => 4.00 avg
        // (a naive (2+10)/2 = 6.00 would be wrong).
        var balance = new InventoryBalance { QuantityOnHand = 30, UnitCost = 2.00m };

        balance.ApplyInbound(10, 10.00m);

        balance.QuantityOnHand.Should().Be(40);
        balance.UnitCost.Should().Be(4.00m);
    }

    [Fact]
    public void ApplyInbound_DoesNotChangeCost_WhenIncomingCostMatches()
    {
        var balance = new InventoryBalance { QuantityOnHand = 5, UnitCost = 8.50m };

        balance.ApplyInbound(5, 8.50m);

        balance.QuantityOnHand.Should().Be(10);
        balance.UnitCost.Should().Be(8.50m);
    }
}
