namespace POS.Application.Printing;

public record PrinterConfigDto(Guid Id, Guid BranchId, string NameAr, string NameEn, string IpAddress, int Port, bool IsDefault, bool IsActive);
public record SavePrinterConfigRequest(Guid BranchId, string NameAr, string NameEn, string IpAddress, int Port = 9100, bool IsDefault = false, bool IsActive = true);
public record PrinterSectionDto(Guid Id, Guid BranchId, string NameAr, string NameEn, Guid? PrinterConfigId);
public record SavePrinterSectionRequest(Guid BranchId, string NameAr, string NameEn, Guid? PrinterConfigId);
public record PrinterTestRequest(string Text);
public record PrintJob(string IpAddress, int Port, byte[] Payload, string Kind);
