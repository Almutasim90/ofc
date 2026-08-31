namespace POS.Domain.Entities;
public class OrderEditLog { public Guid Id{get;set;} public Guid OrderId{get;set;} public RestaurantOrder Order{get;set;}=null!; public Guid UserId{get;set;} public string EditType{get;set;}=string.Empty; public string? Notes{get;set;} public decimal AmountDelta{get;set;} public DateTime CreatedAt{get;set;} }
