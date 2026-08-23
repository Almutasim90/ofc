using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Constants;
using POS.Domain.Entities;

namespace POS.Application.Shifts;

public class VoidService(IAppDbContext db, ICurrentUserService currentUser)
{
    public async Task<VoidRequestDto> VoidAsync(Guid saleId, VoidSaleRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ValidationException("A reason is required to void a sale.");

        var userId = currentUser.UserId ?? throw new UnauthorizedException("Missing user context.");
        var sale = await db.Sales.Include(s => s.Items).Include(s => s.Shift).Include(s => s.VoidRequest)
            .FirstOrDefaultAsync(s => s.Id == saleId, cancellationToken)
            ?? throw new NotFoundException("Sale not found.");
        if (sale.Status != SaleStatus.Completed || sale.VoidRequest is not null)
            throw new ValidationException("This sale has already been voided.");
        if (sale.Shift.Status != ShiftStatus.Open)
            throw new ValidationException("Sales from a closed shift are immutable.");

        var productIds = sale.Items.Select(i => i.ProductId).Distinct().ToList();
        var recipes = await db.ProductRecipes
            .Where(r => r.BranchId == sale.BranchId && productIds.Contains(r.ProductId)).ToListAsync(cancellationToken);
        var restoreByMaterial = new Dictionary<Guid, decimal>();
        foreach (var item in sale.Items)
        foreach (var recipe in recipes.Where(r => r.ProductId == item.ProductId))
            restoreByMaterial[recipe.RawMaterialId] =
                restoreByMaterial.GetValueOrDefault(recipe.RawMaterialId) + recipe.QuantityRequired * item.Quantity;

        if (restoreByMaterial.Count > 0)
        {
            var materialIds = restoreByMaterial.Keys.ToList();
            var stocks = await db.BranchRawMaterialStocks
                .Where(s => s.BranchId == sale.BranchId && materialIds.Contains(s.RawMaterialId)).ToListAsync(cancellationToken);
            foreach (var (materialId, quantity) in restoreByMaterial)
            {
                var stock = stocks.FirstOrDefault(s => s.RawMaterialId == materialId);
                if (stock is null)
                {
                    stock = new BranchRawMaterialStock { BranchId = sale.BranchId, RawMaterialId = materialId };
                    db.BranchRawMaterialStocks.Add(stock);
                }
                stock.CurrentQuantity += quantity;
            }
        }

        sale.Status = SaleStatus.Voided;
        var voidRequest = new VoidRequest
        {
            Id = Guid.NewGuid(), SaleId = sale.Id, RequestedByUserId = userId, ApprovedByUserId = userId,
            Reason = request.Reason.Trim(), CreatedAt = DateTime.UtcNow,
        };
        db.VoidRequests.Add(voidRequest);
        await db.SaveChangesAsync(cancellationToken);
        return new VoidRequestDto(voidRequest.Id, voidRequest.SaleId, voidRequest.RequestedByUserId,
            voidRequest.Reason, voidRequest.ApprovedByUserId, voidRequest.CreatedAt);
    }
}
