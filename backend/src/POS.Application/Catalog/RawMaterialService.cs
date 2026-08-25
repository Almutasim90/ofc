using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.Catalog;

public class RawMaterialService(IAppDbContext db)
{
    public async Task<List<RawMaterialDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.RawMaterials
            .OrderBy(m => m.NameEn)
            .Select(m => new RawMaterialDto(m.Id, m.NameAr, m.NameEn, m.Unit, m.MeasurementType))
            .ToListAsync(cancellationToken);
    }

    public async Task<RawMaterialDto> CreateAsync(CreateRawMaterialRequest request, CancellationToken cancellationToken = default)
    {
        var material = new RawMaterial
        {
            Id = Guid.NewGuid(),
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            Unit = request.Unit,
            MeasurementType = request.MeasurementType,
        };

        db.RawMaterials.Add(material);
        await db.SaveChangesAsync(cancellationToken);

        return new RawMaterialDto(material.Id, material.NameAr, material.NameEn, material.Unit, material.MeasurementType);
    }

    public async Task<RawMaterialDto> UpdateAsync(Guid id, UpdateRawMaterialRequest request, CancellationToken cancellationToken = default)
    {
        var material = await db.RawMaterials.FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Raw material '{id}' not found.");

        material.NameAr = request.NameAr;
        material.NameEn = request.NameEn;
        var hasMovements = await db.StockAdjustments.AnyAsync(x => x.RawMaterialId == id, cancellationToken)
            || await db.ProductRecipes.AnyAsync(x => x.RawMaterialId == id, cancellationToken);
        if (hasMovements && (!material.Unit.Equals(request.Unit, StringComparison.OrdinalIgnoreCase)
            || !material.MeasurementType.Equals(request.MeasurementType, StringComparison.OrdinalIgnoreCase)))
            throw new ValidationException("The measurement unit cannot be changed after inventory movements or recipes exist.");
        material.Unit = request.Unit;
        material.MeasurementType = request.MeasurementType;

        await db.SaveChangesAsync(cancellationToken);

        return new RawMaterialDto(material.Id, material.NameAr, material.NameEn, material.Unit, material.MeasurementType);
    }
}
