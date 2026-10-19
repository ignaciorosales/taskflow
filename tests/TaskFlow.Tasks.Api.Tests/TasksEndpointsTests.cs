using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace TaskFlow.Tasks.Api.Tests;

public class TasksEndpointsTests(TasksApiFactory factory) : IClassFixture<TasksApiFactory>
{
    private readonly HttpClient _client = factory.ClientForNewUser();

    [Fact]
    public async Task Create_returns_201_with_its_location_and_the_new_task()
    {
        var dueDate = DateTimeOffset.UtcNow.AddDays(1);

        var response = await _client.CreateTaskAsync(new { title = "  Buy milk ", description = "Semi-skimmed", priority = "High", dueDate });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var task = await response.ReadAsync<TaskResponse>();
        Assert.Equal($"/api/tasks/{task.Id}", response.Headers.Location!.AbsolutePath);
        Assert.Equal("Buy milk", task.Title);
        Assert.Equal("Semi-skimmed", task.Description);
        Assert.Equal("High", task.Priority);
        Assert.Equal("Todo", task.Status);
        Assert.Equal(dueDate.ToUnixTimeMilliseconds(), task.DueDate!.Value.ToUnixTimeMilliseconds());
        Assert.False(task.IsOverdue);
    }

    [Fact]
    public async Task Priority_defaults_to_medium()
    {
        Assert.Equal("Medium", (await _client.CreatedTaskAsync()).Priority);
    }

    [Fact]
    public async Task Created_task_can_be_read_back()
    {
        var created = await _client.CreatedTaskAsync("Buy milk");

        var response = await _client.GetAsync($"/api/tasks/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created, await response.ReadAsync<TaskResponse>());
    }

    [Fact]
    public async Task Invalid_task_returns_400_with_errors_per_field()
    {
        var response = await _client.CreateTaskAsync(new { title = " ", dueDate = DateTimeOffset.UtcNow.AddMinutes(-1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadAsync<ValidationProblemDetails>();
        Assert.Equal(["Title is required."], problem.Errors["title"]);
        Assert.Equal(["Due date can't be in the past."], problem.Errors["dueDate"]);
    }

    [Fact]
    public async Task Unknown_priority_name_is_a_400()
    {
        var response = await _client.CreateTaskAsync(new { title = "Buy milk", priority = "Urgent" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_returns_my_tasks_a_page_at_a_time()
    {
        foreach (var title in new[] { "One", "Two", "Three" })
            await _client.CreatedTaskAsync(title);

        var page = await (await _client.GetAsync("/api/tasks?pageSize=2")).ReadAsync<PageResponse>();

        Assert.Equal(["Three", "Two"], page.Items.Select(t => t.Title));
        Assert.Equal(1, page.Page);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(3, page.TotalCount);
    }

    [Fact]
    public async Task List_filters_and_sorts_from_the_query_string()
    {
        var low = await _client.CreatedTaskAsync(body: new { title = "Low", priority = "Low" });
        var high = await _client.CreatedTaskAsync(body: new { title = "High", priority = "High" });
        await _client.CreatedTaskAsync(body: new { title = "Not started", priority = "High" });
        foreach (var task in new[] { low, high })
            await _client.PatchAsJsonAsync($"/api/tasks/{task.Id}/status", new { status = "InProgress" });

        var page = await (await _client.GetAsync("/api/tasks?status=InProgress&sortBy=Priority&descending=true")).ReadAsync<PageResponse>();

        Assert.Equal(["High", "Low"], page.Items.Select(t => t.Title));
    }

    [Theory]
    [InlineData("pageSize=500", "pageSize")]
    [InlineData("page=0", "page")]
    public async Task Invalid_query_returns_400_with_the_offending_field(string query, string field)
    {
        var response = await _client.GetAsync($"/api/tasks?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(field, (await response.ReadAsync<ValidationProblemDetails>()).Errors.Keys);
    }

    [Fact]
    public async Task Update_returns_200_with_the_changes()
    {
        var created = await _client.CreatedTaskAsync("Buy milk");

        var response = await _client.PutAsJsonAsync($"/api/tasks/{created.Id}", new { title = "Buy bread", priority = "Low" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadAsync<TaskResponse>();
        Assert.Equal("Buy bread", updated.Title);
        Assert.Equal("Low", updated.Priority);
    }

    [Fact]
    public async Task Status_change_returns_200_and_records_completion()
    {
        var created = await _client.CreatedTaskAsync();

        var response = await _client.PatchAsJsonAsync($"/api/tasks/{created.Id}/status", new { status = "Done" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var done = await response.ReadAsync<TaskResponse>();
        Assert.Equal("Done", done.Status);
        Assert.NotNull(done.CompletedAt);
    }

    [Fact]
    public async Task Unknown_status_name_is_a_400()
    {
        var created = await _client.CreatedTaskAsync();

        var response = await _client.PatchAsJsonAsync($"/api/tasks/{created.Id}/status", new { status = "Paused" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_returns_204_and_the_task_is_gone()
    {
        var created = await _client.CreatedTaskAsync();

        var response = await _client.DeleteAsync($"/api/tasks/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/tasks/{created.Id}")).StatusCode);
        Assert.Equal(0, (await (await _client.GetAsync("/api/tasks")).ReadAsync<PageResponse>()).TotalCount);
    }

    [Fact]
    public async Task Health_is_public_and_healthy()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OpenApi_document_describes_the_endpoints_and_bearer_auth()
    {
        var response = await factory.CreateClient().GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"/api/tasks\"", document);
        Assert.Contains("\"/api/tasks/{id}\"", document);
        Assert.Contains("\"bearer\"", document, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scalar_ui_is_served()
    {
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync("/scalar/v1")).StatusCode);
    }
}
