using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;
using TaskFlow.Application.Auth;
using TaskFlow.Application.Tasks;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Security;

namespace TaskFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, JwtOptions jwt)
    {
        var connectionString = configuration["Mongo:ConnectionString"]
            ?? throw new InvalidOperationException("Mongo:ConnectionString must be set (environment variable Mongo__ConnectionString).");

        services.AddSingleton<IMongoClient>(new MongoClient(connectionString));
        services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(configuration["Mongo:Database"] ?? "taskflow"));

        services.AddSingleton<IUserRepository, MongoUserRepository>();
        services.AddSingleton<ITaskRepository, MongoTaskRepository>();
        services.AddSingleton<IRefreshTokenRepository, MongoRefreshTokenRepository>();
        services.AddSingleton<ILoginAttemptRepository, MongoLoginAttemptRepository>();

        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
        services.AddSingleton(jwt);
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton(TimeProvider.System);

        services.AddHealthChecks().AddCheck<MongoHealthCheck>("mongo");
        return services;
    }

    private sealed class MongoHealthCheck(IMongoDatabase database) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
                return HealthCheckResult.Healthy();
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Mongo is unreachable.", ex);
            }
        }
    }
}
