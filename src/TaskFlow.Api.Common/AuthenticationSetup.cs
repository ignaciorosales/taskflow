using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace TaskFlow.Api.Common;

public static class AuthenticationSetup
{
    /// <summary>Validates the access tokens the Identity API issues; both APIs share the same key.</summary>
    public static IServiceCollection AddTaskFlowAuthentication(
        this IServiceCollection services, SecurityKey signingKey, string issuer, string audience)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false; // keep "sub" as "sub"
            options.TokenValidationParameters = new TokenValidationParameters
            {
                IssuerSigningKey = signingKey,
                ValidIssuer = issuer,
                ValidAudience = audience,
                ClockSkew = TimeSpan.FromSeconds(30),
            };
        });
        services.AddAuthorization();
        return services;
    }
}
