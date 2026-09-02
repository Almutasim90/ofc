using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using POS.Application.Abstractions;
using POS.Domain.Constants;

namespace POS.API.Hubs;

[Authorize]
public sealed class RestaurantOrdersHub(ICurrentUserService currentUser) : Hub
{
    public async Task JoinBranch(Guid branchId)
    {
        if (!currentUser.Permissions.Contains(PermissionKeys.OrdersCreate))
            throw new HubException("You do not have permission to view restaurant orders.");
        if (!currentUser.BypassBranchFilter && currentUser.BranchId != branchId)
            throw new HubException("You do not have access to this branch.");
        await Groups.AddToGroupAsync(Context.ConnectionId, BranchGroup(branchId));
    }

    public static string BranchGroup(Guid branchId) => $"branch:{branchId:N}";
}
