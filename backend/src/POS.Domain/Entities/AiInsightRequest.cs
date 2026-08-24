namespace POS.Domain.Entities;
public class AiInsightRequest { public Guid Id { get; set; } public Guid RequestedByUserId { get; set; } public Guid? BranchId { get; set; } public string RequestType { get; set; } = string.Empty; public DateTime CreatedAt { get; set; } public string ResultSummary { get; set; } = string.Empty; }
