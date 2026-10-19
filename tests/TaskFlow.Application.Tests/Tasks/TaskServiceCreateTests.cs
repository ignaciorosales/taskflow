using NSubstitute;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Tasks;

namespace TaskFlow.Application.Tests.Tasks;

public class TaskServiceCreateTests : TaskServiceTestBase
{
    [Fact]
    public async Task Create_stores_a_new_task_owned_by_the_caller()
    {
        var result = await Service.CreateAsync(UserId, new("  Buy milk ", null, null, Now.AddDays(1)), default);

        Assert.True(result.IsSuccess);
        var dto = result.Value;
        Assert.Equal("Buy milk", dto.Title);
        Assert.Equal(TaskItemStatus.Todo, dto.Status);
        Assert.Equal(TaskPriority.Medium, dto.Priority);
        Assert.Equal(Now, dto.CreatedAt);
        Assert.Equal(Now.AddDays(1), dto.DueDate);
        await Repository.Received(1).AddAsync(
            Arg.Is<TaskItem>(t => t.Id == dto.Id && t.OwnerId == UserId && t.Title == "Buy milk"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Due_date_is_checked_against_the_clock_from_TimeProvider()
    {
        // A day before the fake clock, but still in the future for the real one.
        var result = await Service.CreateAsync(UserId, new("Buy milk", null, null, Now.AddDays(-1)), default);

        Assert.Equal([TaskErrors.DueDateInPast], result.Errors);
    }

    [Fact]
    public async Task Invalid_request_returns_every_domain_error_and_stores_nothing()
    {
        var result = await Service.CreateAsync(UserId, new(" ", new string('d', 1001), null, null), default);

        Assert.Equal([TaskErrors.TitleRequired, TaskErrors.DescriptionTooLong], result.Errors);
        await Repository.DidNotReceive().AddAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>());
    }
}
