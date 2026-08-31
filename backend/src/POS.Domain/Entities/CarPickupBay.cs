namespace POS.Domain.Entities;
public class CarPickupBay{public Guid Id{get;set;}public Guid BranchId{get;set;}public Branch Branch{get;set;}=null!;public string BayLabel{get;set;}=string.Empty;public bool IsActive{get;set;}=true;}
