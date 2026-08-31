using POS.Domain.Constants;
using Xunit;

namespace POS.Application.Tests;

public class RestaurantPermissionTests
{
    [Fact]
    public void Sprint_one_permissions_are_registered_and_unique()
    {
        string[] required =
        [
            "orders.create",
            "orders.cancel",
            "combos.manage",
            "modifiers.manage",
            "tables.manage",
            "printing.manage",
            "channels.manage",
            "closedOrders.edit",
            "debtPayments.approve",
            "orders.transfer",
        ];

        Assert.All(required, key => Assert.Contains(key, PermissionKeys.All));
        Assert.Equal(PermissionKeys.All.Count, PermissionKeys.All.Distinct(StringComparer.Ordinal).Count());
    }
}
