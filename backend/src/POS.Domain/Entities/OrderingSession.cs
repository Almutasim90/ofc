namespace POS.Domain.Entities;
public static class OrderingSessionStatuses{public const string Open="Open",Closed="Closed";}
public class OrderingSession{public Guid Id{get;set;}public Guid OrderingPointId{get;set;}public OrderingPoint OrderingPoint{get;set;}=null!;public string Status{get;set;}=OrderingSessionStatuses.Open;public DateTime OpenedAt{get;set;}public DateTime? ClosedAt{get;set;}}
