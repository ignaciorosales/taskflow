using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using MongoDB.Driver;
using TaskFlow.Api.Common;
using TaskFlow.Application.Auth;
using TaskFlow.Identity.Api.Controllers;
using TaskFlow.Infrastructure;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

var jwt = JwtOptions.FromConfiguration(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration, jwt);
builder.Services.AddScoped<AuthService>();
builder.Services.AddTaskFlowApi(jwt.SigningKey(), jwt.Issuer, jwt.Audience);

// nginx is the only way in, so the client IP it forwards is the one to rate limit on.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthController.RateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

await MongoSetup.ConfigureAsync(app.Services.GetRequiredService<IMongoDatabase>());

app.UseForwardedHeaders();
app.UseTaskFlowApi();
app.UseRateLimiter();
app.MapTaskFlowApi();

app.Run();
