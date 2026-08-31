using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.Printing;

public class PrinterAdminService(IAppDbContext db, IRawPrinterClient printer)
{
    public Task<List<PrinterConfigDto>> GetConfigsAsync(Guid branchId, CancellationToken ct = default) => db.PrinterConfigs.Where(x => x.BranchId == branchId).OrderByDescending(x => x.IsDefault).ThenBy(x => x.NameEn).Select(x => new PrinterConfigDto(x.Id, x.BranchId, x.NameAr, x.NameEn, x.IpAddress, x.Port, x.IsDefault, x.IsActive)).ToListAsync(ct);
    public Task<List<PrinterSectionDto>> GetSectionsAsync(Guid branchId, CancellationToken ct = default) => db.PrinterSections.Where(x => x.BranchId == branchId).OrderBy(x => x.NameEn).Select(x => new PrinterSectionDto(x.Id, x.BranchId, x.NameAr, x.NameEn, x.PrinterConfigId)).ToListAsync(ct);

    public async Task<PrinterConfigDto> SaveConfigAsync(Guid? id, SavePrinterConfigRequest request, CancellationToken ct = default)
    {
        Validate(request.NameAr, request.NameEn); if (string.IsNullOrWhiteSpace(request.IpAddress)) throw new ValidationException("Printer address is required."); if (request.Port is < 1 or > 65535) throw new ValidationException("Printer port must be between 1 and 65535.");
        if (!await db.Branches.AnyAsync(x => x.Id == request.BranchId, ct)) throw new NotFoundException("Branch not found.");
        var row = id is null ? new PrinterConfig { Id = Guid.NewGuid() } : await db.PrinterConfigs.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Printer not found.");
        if (id is null) db.PrinterConfigs.Add(row); if (request.IsDefault) foreach (var old in await db.PrinterConfigs.Where(x => x.BranchId == request.BranchId && x.Id != row.Id && x.IsDefault).ToListAsync(ct)) old.IsDefault = false;
        row.BranchId = request.BranchId; row.NameAr = request.NameAr.Trim(); row.NameEn = request.NameEn.Trim(); row.IpAddress = request.IpAddress.Trim(); row.Port = request.Port; row.IsDefault = request.IsDefault; row.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct); return new(row.Id, row.BranchId, row.NameAr, row.NameEn, row.IpAddress, row.Port, row.IsDefault, row.IsActive);
    }

    public async Task<PrinterSectionDto> SaveSectionAsync(Guid? id, SavePrinterSectionRequest request, CancellationToken ct = default)
    {
        Validate(request.NameAr, request.NameEn); if (!await db.Branches.AnyAsync(x => x.Id == request.BranchId, ct)) throw new NotFoundException("Branch not found.");
        if (request.PrinterConfigId is not null && !await db.PrinterConfigs.AnyAsync(x => x.Id == request.PrinterConfigId && x.BranchId == request.BranchId, ct)) throw new ValidationException("Printer must belong to the same branch.");
        var row = id is null ? new PrinterSection { Id = Guid.NewGuid() } : await db.PrinterSections.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Printer section not found.");
        if (id is null) db.PrinterSections.Add(row); row.BranchId = request.BranchId; row.NameAr = request.NameAr.Trim(); row.NameEn = request.NameEn.Trim(); row.PrinterConfigId = request.PrinterConfigId;
        await db.SaveChangesAsync(ct); return new(row.Id, row.BranchId, row.NameAr, row.NameEn, row.PrinterConfigId);
    }

    public async Task TestAsync(Guid id, string text, CancellationToken ct = default)
    {
        var config = await db.PrinterConfigs.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct) ?? throw new NotFoundException("Active printer not found.");
        await printer.SendAsync(config.IpAddress, config.Port, EscPosDocument.Text(string.IsNullOrWhiteSpace(text) ? "OFC printer test" : text.Trim()), ct);
    }
    private static void Validate(string ar, string en) { if (string.IsNullOrWhiteSpace(ar) || string.IsNullOrWhiteSpace(en)) throw new ValidationException("Arabic and English names are required."); }
}
