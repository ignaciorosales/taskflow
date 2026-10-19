using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using TaskFlow.Domain.Users;
using TaskFlow.Infrastructure.Security;
using Testcontainers.MongoDb;

namespace TaskFlow.Tasks.Api.Tests;

/// <summary>The real Tasks API on a real Mongo container, one per test class.</summary>
public sealed class TasksApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string JwtKey = "integration-test-signing-key-at-least-32-bytes";
    public static readonly JwtOptions Jwt = new(JwtKey, "taskflow", "taskflow");

    private readonly MongoDbContainer _mongo = new MongoDbBuilder("mongo:8.0").Build();

    public Task InitializeAsync() => _mongo.StartAsync();

    Task IAsyncLifetime.DisposeAsync() => Task.WhenAll(DisposeAsync().AsTask(), _mongo.DisposeAsync().AsTask());

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Mongo:ConnectionString", _mongo.GetConnectionString());
        builder.UseSetting("Mongo:Database", "taskflow_tests");
        builder.UseSetting("Jwt:Key", JwtKey);
    }

    /// <summary>A client signed in as a brand-new user, with a token minted exactly like the Identity API does.</summary>
    public HttpClient ClientForNewUser(DateTimeOffset? tokenExpiresAt = null, JwtOptions? signWith = null)
        => this.ClientFor(NewUser(), tokenExpiresAt, signWith);

    public static User NewUser() => User.Create(Email.Create($"user-{Guid.NewGuid():N}@example.com").Value, "hash", DateTimeOffset.UtcNow);
}

public sealed record TaskResponse(
    Guid Id,
    string Title,
    string? Description,
    string Priority,
    DateTimeOffset? DueDate,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    bool IsOverdue,
    bool IsCompletedLate);

public sealed record PageResponse(TaskResponse[] Items, int Page, int PageSize, long TotalCount);

public static class TasksClientExtensions
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static HttpClient ClientFor(
        this WebApplicationFactory<Program> factory, User user, DateTimeOffset? tokenExpiresAt = null, JwtOptions? signWith = null)
    {
        var expiresAt = tokenExpiresAt ?? DateTimeOffset.UtcNow.AddMinutes(15);
        var token = new JwtTokenService(signWith ?? TasksApiFactory.Jwt).CreateAccessToken(user, expiresAt.AddMinutes(-15), expiresAt);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static Task<HttpResponseMessage> CreateTaskAsync(this HttpClient client, object body)
        => client.PostAsJsonAsync("/api/tasks", body);

    public static async Task<TaskResponse> CreatedTaskAsync(this HttpClient client, string title = "Buy milk", object? body = null)
    {
        var response = await client.CreateTaskAsync(body ?? new { title });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskResponse>(Json))!;
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<T>(Json))!;
}
