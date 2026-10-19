using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Infrastructure.Security;

namespace TaskFlow.Tasks.Api.Tests;

public class TasksSecurityTests(TasksApiFactory factory) : IClassFixture<TasksApiFactory>
{
    private readonly HttpClient _alice = factory.ClientForNewUser();
    private readonly HttpClient _bob = factory.ClientForNewUser();

    public static TheoryData<string, string> Endpoints => new()
    {
        { "GET", "/api/tasks" },
        { "POST", "/api/tasks" },
        { "GET", $"/api/tasks/{Guid.NewGuid()}" },
        { "PUT", $"/api/tasks/{Guid.NewGuid()}" },
        { "PATCH", $"/api/tasks/{Guid.NewGuid()}/status" },
        { "DELETE", $"/api/tasks/{Guid.NewGuid()}" },
    };

    private static HttpRequestMessage Request(string method, string url)
        => new(new HttpMethod(method), url) { Content = JsonContent.Create(new { title = "x", status = "Done" }) };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Every_endpoint_needs_a_token(string method, string url)
    {
        var response = await factory.CreateClient().SendAsync(Request(method, url));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Expired_token_is_a_401()
    {
        var client = factory.ClientForNewUser(tokenExpiresAt: DateTimeOffset.UtcNow.AddMinutes(-5));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tasks")).StatusCode);
    }

    [Fact]
    public async Task Token_signed_with_another_key_is_a_401()
    {
        var forged = factory.ClientForNewUser(signWith: TasksApiFactory.Jwt with { Key = "an-attacker-key-that-is-also-32-bytes-long" });

        Assert.Equal(HttpStatusCode.Unauthorized, (await forged.GetAsync("/api/tasks")).StatusCode);
    }

    [Fact]
    public async Task Someone_elses_task_doesnt_show_up_in_my_list()
    {
        await _alice.CreatedTaskAsync("Alice's task");

        var page = await (await _bob.GetAsync("/api/tasks")).ReadAsync<PageResponse>();

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task Someone_elses_task_gives_the_same_404_as_one_that_doesnt_exist()
    {
        var alices = await _alice.CreatedTaskAsync();

        var theirs = await _bob.GetAsync($"/api/tasks/{alices.Id}");
        var missing = await _bob.GetAsync($"/api/tasks/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, theirs.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var (a, b) = (await theirs.ReadAsync<ProblemDetails>(), await missing.ReadAsync<ProblemDetails>());
        Assert.Equal((a.Title, a.Detail, a.Type), (b.Title, b.Detail, b.Type));
    }

    [Fact]
    public async Task I_cant_edit_change_or_delete_someone_elses_task_and_it_stays_intact()
    {
        var alices = await _alice.CreatedTaskAsync("Alice's task");

        var edit = await _bob.PutAsJsonAsync($"/api/tasks/{alices.Id}", new { title = "Hacked", priority = "High" });
        var status = await _bob.PatchAsJsonAsync($"/api/tasks/{alices.Id}/status", new { status = "Done" });
        var delete = await _bob.DeleteAsync($"/api/tasks/{alices.Id}");

        Assert.All(new[] { edit, status, delete }, r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
        var after = await (await _alice.GetAsync($"/api/tasks/{alices.Id}")).ReadAsync<TaskResponse>();
        Assert.Equal(alices, after);
    }

    [Fact]
    public async Task Owner_comes_from_the_token_never_from_the_body()
    {
        var bob = TasksApiFactory.NewUser();
        var bobClient = factory.ClientFor(bob);

        var created = await _alice.CreatedTaskAsync(body: new { title = "Mine", ownerId = bob.Id });

        Assert.Equal(HttpStatusCode.NotFound, (await bobClient.GetAsync($"/api/tasks/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _alice.GetAsync($"/api/tasks/{created.Id}")).StatusCode);
    }
}
