using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.JsonSerializerOptions.Converters.Add(new OffsetRequiredConverter());
            })
            .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = FieldErrorsResponse);
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

    // A JSON body that doesn't bind reports "$.dueDate" plus a knock-on "request is required";
    // keep only the field, named the way every other validation error names it.
    private static IActionResult FieldErrorsResponse(ActionContext context)
    {
        var bodyFailed = context.ModelState.Keys.Any(k => k.StartsWith('$'));
        var fields = new ModelStateDictionary();
        foreach (var (key, entry) in context.ModelState)
            if (!bodyFailed || key.StartsWith('$'))
                foreach (var error in entry.Errors)
                    fields.AddModelError(key.StartsWith("$.") ? key[2..] : key, error.ErrorMessage);

        var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var details = factory.CreateValidationProblemDetails(context.HttpContext, fields);
        return new BadRequestObjectResult(details) { ContentTypes = { "application/problem+json" } };
    }

    // Times are instants in UTC; a value without Z or ±hh:mm would be read in the server's own time zone.
    private sealed class OffsetRequiredConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.TryGetDateTime(out var value) && value.Kind != DateTimeKind.Unspecified
                ? reader.GetDateTimeOffset()
                : throw new JsonException("Dates must be ISO 8601 with a UTC offset, such as 2030-01-15T10:00:00Z.");

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
            => writer.WriteStringValue(value);
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
