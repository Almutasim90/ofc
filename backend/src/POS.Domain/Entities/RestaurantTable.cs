namespace POS.Domain.Entities;

public class RestaurantTable
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public string Label { get; set; } = string.Empty;
    public int? Capacity { get; set; }
    public Guid? FloorId { get; set; }
    public RestaurantFloor? Floor { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public string Shape { get; set; } = RestaurantTableShapes.Rectangle;
    public bool IsActive { get; set; } = true;
}

public static class RestaurantTableShapes
{
    public const string Rectangle = "Rectangle";
    public const string Round = "Round";
    public static readonly IReadOnlySet<string> All = new HashSet<string> { Rectangle, Round };
}
