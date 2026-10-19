using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace TaskFlow.Api.Common;

/// <summary>What both APIs share: controllers, ProblemDetails, JWT auth, health and OpenAPI docs.</summary>
public static class ApiSetup
{
    public static IServiceCollection AddTaskFlowApi(this IServiceCollection services, SecurityKey signingKey, string issuer, string audience)
    {
        services.AddControllers()
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddProblemDetails();
        services.AddExceptionHandler<UnexpectedExceptionHandler>();
        services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecurityTransformer>());

        // Validates the access tokens the Identity API issues; both APIs share the same key.
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

    public static WebApplication UseTaskFlowApi(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    public static WebApplication MapTaskFlowApi(this WebApplication app)
    {
        app.MapControllers();
        app.MapHealthChecks("/health");
        app.MapOpenApi();
        app.MapScalarApiReference();
        return app;
    }

    private sealed class BearerSecurityTransformer : IOpenApiDocumentTransformer
    {
        public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "The access token from /api/auth/login or /api/auth/register.",
            };
            document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }];
            return Task.CompletedTask;
        }
    }
}
