namespace POS.Application.Ofc;

public record TableDto(Guid Id, Guid BranchId, string Label, int? Capacity, bool IsActive);

public record CreateTableRequest(Guid BranchId, string Label, int? Capacity);

public record UpdateTableRequest(string Label, int? Capacity, bool IsActive);
