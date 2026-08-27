from pathlib import Path
import re,json,subprocess
for name in ['backend/src/POS.Application/Sales/SaleService.cs','frontend/src/components/Layout.tsx','frontend/src/pages/CashierPage.tsx']:
 p=Path(name);s=p.read_text();s=re.sub(r'<<<<<<< HEAD\n(.*?)=======\n.*?>>>>>>> origin/main\n',lambda m:m[1],s,flags=re.S)
 if 'SaleService' in name:
  s=s.replace('sale.Id, sale.BranchId, sale.ChannelId','sale.Id, sale.SaleNumber, sale.BranchId, sale.ChannelId')
  at=s.index('    private static string NormalizeDiscountType')
  s=s[:at]+'''    public async Task<IReadOnlyList<SaleDto>> ListForShiftAsync(Guid shiftId, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("Missing user context.");
        var shift = await db.Shifts.AsNoTracking().FirstOrDefaultAsync(s => s.Id == shiftId, cancellationToken)
            ?? throw new NotFoundException("Shift not found.");
        EnsureBranchScope(shift.BranchId);
        if (currentUser.RoleName == RoleNames.Cashier && shift.CashierUserId != userId)
            throw new ForbiddenException("You do not have access to this shift.");
        var rows = await db.Sales.AsNoTracking().Include(s => s.Items).Include(s => s.Shift)
            .Where(s => s.ShiftId == shiftId && s.Status == SaleStatus.Completed)
            .OrderByDescending(s => s.CreatedAt).ToListAsync(cancellationToken);
        return rows.Select(ToDto).ToList();
    }

'''+s[at:]
 if 'Layout' in name:
  s=s.replace('>{children}</main>{bottomNavigation}', ">{pathname === '/shift' && <Breadcrumb items={breadcrumbTrail} />}{children}</main>{bottomNavigation}")
  s=s.replace('p-4 lg:p-6">{children}</main>', 'p-4 lg:p-6"><Breadcrumb items={breadcrumbTrail} />{children}</main>')
 if 'CashierPage' in name:
  s=s.replace('  const { user, hasPermission } = useAuth()','  const { user, hasPermission } = useAuth()\n  const toast = useToast()')
  s=s.replace('            <button type="button" className="text-on-primary min-h-14 border-0 bg-primary" onClick={() => setSuccessSale(null)}>', '            <button type="button" className="min-h-14" onClick={() => window.print()}>{t(\'receipt.print\')}</button>\n            <button type="button" className="text-on-primary min-h-14 border-0 bg-primary" onClick={() => setSuccessSale(null)}>')
 p.write_text(s)
for lang in ['ar','en']:
 name=f'frontend/src/locales/{lang}.json';ours=json.loads(subprocess.check_output(['git','show','HEAD:'+name]).decode('utf-8'));s=subprocess.check_output(['git','show','origin/main:'+name]).decode('utf-8');s=s.replace('"cashier": {','"cashier": {\n    "mixed": '+json.dumps(ours['cashier']['mixed'],ensure_ascii=False)+',',1);s=s.replace('"permissionKeys": {','"permissionKeys": {\n    "sales.edit": '+json.dumps(ours['permissionKeys']['sales.edit'],ensure_ascii=False)+',',1);pos=s.rfind('}');orders=json.dumps(ours['orders'],ensure_ascii=False,indent=2);orders='\n'.join('  '+l if i else '  "orders": '+l for i,l in enumerate(orders.splitlines()));s=s[:pos].rstrip()+',\n'+orders+'\n}\n';json.loads(s);Path(name).write_text(s)
p=Path('backend/src/POS.API/Controllers/SalesController.cs');s=p.read_text().replace('[HttpGet]\n    [RequirePermission(PermissionKeys.SalesEdit)]','[HttpGet("editable")]\n    [RequirePermission(PermissionKeys.SalesEdit)]');p.write_text(s)
p=Path('frontend/src/components/SavedOrders.tsx');s=p.read_text().replace('/api/sales?branchId=', '/api/sales/editable?branchId=').replace('#{sale.id.slice(0, 8)}','#{sale.saleNumber}');p.write_text(s)
p=Path('frontend/src/components/Receipt.tsx');s=p.read_text().replace("sale.paymentMethod === 'Cash' ? t('cashier.cash') : t('cashier.card')", "t(`cashier.${sale.paymentMethod.toLowerCase()}`)").replace('      </div>\n    </div>','        {sale.paymentMethod === \'Mixed\' && <><div><span>{t(\'cashier.cash\')}</span><Money value={sale.cashAmount} /></div><div><span>{t(\'cashier.card\')}</span><Money value={sale.cardAmount} /></div></>}\n      </div>\n    </div>');p.write_text(s)
p=Path('frontend/src/pages/ShiftPage.tsx');s=p.read_text();s=s.replace("method === 'Cash' ? t('cashier.cash') : t('cashier.card')", "t(`cashier.${method.toLowerCase()}`)");p.write_text(s)
