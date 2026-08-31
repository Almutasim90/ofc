namespace POS.Domain.Entities;
public class BranchSalesChannelAvailability{public Guid Id{get;set;}public Guid BranchId{get;set;}public Branch Branch{get;set;}=null!;public Guid SalesChannelId{get;set;}public SalesChannel SalesChannel{get;set;}=null!;public bool IsEnabled{get;set;}public bool RequiresPrepayment{get;set;}}
