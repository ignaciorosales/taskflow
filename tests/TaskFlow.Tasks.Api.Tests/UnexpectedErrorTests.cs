using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TaskFlow.Application.Tasks;

namespace TaskFlow.Tasks.Api.Tests;

public class UnexpectedErrorTests(TasksApiFactory factory) : IClassFixture<TasksApiFactory>
{
    [Fact]
    public async Task Unexpected_failure_is_a_500_problem_that_leaks_no_internals()
    {
        var repository = Substitute.For<ITaskRepository>();
        repository.ListAsync(default, default!, default, default)
            .ThrowsAsyncForAnyArgs(new InvalidOperationException("connection string mongodb://secret@db"));
        var user = TasksApiFactory.NewUser();
        var client = factory
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton(repository)))
            .ClientFor(user);

        var response = await client.GetAsync("/api/tasks");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret", body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.NotNull((await response.ReadAsync<ProblemDetails>()).Title);
    }
}
