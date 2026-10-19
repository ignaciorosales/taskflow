using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace TaskFlow.Api.Common;

public static class CurrentUser
{
    /// <summary>The user id from the validated access token; only call this behind [Authorize].</summary>
    public static Guid UserId(this ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
