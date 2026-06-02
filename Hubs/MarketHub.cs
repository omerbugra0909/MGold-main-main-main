using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MGold.Domain.Constants;

namespace MGold.Hubs;

[Authorize(Roles = RoleConstants.AuthenticatedPortalRoles)]
public class MarketHub : Hub
{
}
