using POS.Application.Common;
using POS.Application.Orders;
using POS.Domain.Constants;
using Xunit;

namespace POS.Application.Tests;

public class CarPickupTests
{
    [Fact]
    public void Disabled_branch_rejects_car_pickup() =>
        Assert.Throws<ValidationException>(() => RestaurantOrderService.ValidateCarPickup("1234", false));

    [Fact]
    public void Enabled_branch_requires_plate() =>
        Assert.Throws<ValidationException>(() => RestaurantOrderService.ValidateCarPickup(" ", true));

    [Fact]
    public void Enabled_branch_accepts_plate() =>
        RestaurantOrderService.ValidateCarPickup("OMAN 1234", true);

    [Fact]
    public void Plate_is_limited_to_database_length() =>
        Assert.Throws<ValidationException>(() => RestaurantOrderService.ValidateCarPickup(new string('1', 31), true));

    [Fact]
    public void Feature_key_has_one_canonical_value() => Assert.Equal("CAR_PICKUP", BranchFeatureKeys.CarPickup);
}
