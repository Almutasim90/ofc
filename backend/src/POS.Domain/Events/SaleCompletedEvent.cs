namespace POS.Domain.Events;

public record SaleCompletedEvent(Guid SaleId, Guid BranchId, Guid CashierUserId, DateTime OccurredAt) : IDomainEvent;
