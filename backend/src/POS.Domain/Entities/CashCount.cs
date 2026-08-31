namespace POS.Domain.Entities;
public class CashCount{public Guid Id{get;set;}public Guid CashShiftId{get;set;}public CashShift CashShift{get;set;}=null!;public decimal DenominationValue{get;set;}public string DenominationType{get;set;}=string.Empty;public int CountedQty{get;set;}public DateTime CreatedAt{get;set;}}
