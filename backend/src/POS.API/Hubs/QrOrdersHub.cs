using Microsoft.AspNetCore.SignalR;
using POS.Application.QrOrdering;

namespace POS.API.Hubs;

public sealed class QrOrdersHub(QrOrderingService service) : Hub
{
    public async Task JoinSession(Guid sessionId, string accessToken)
    {
        await service.ValidateSessionAsync(sessionId, accessToken, Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, SessionGroup(sessionId));
    }

    public static string SessionGroup(Guid sessionId) => $"qr-session:{sessionId:N}";
}
