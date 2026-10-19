using NSubstitute;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Tasks;

namespace TaskFlow.Application.Tests.Tasks;

public class TaskServiceReadTests : TaskServiceTestBase
{
    [Fact]
    public async Task Get_returns_the_callers_task()
    {
        var task = StoredTask(UserId, Now.AddDays(1));

        var dto = (await Service.GetAsync(UserId, task.Id, default)).Value;

        Assert.Equal(task.Id, dto.Id);
        Assert.Equal("Buy milk", dto.Title);
        Assert.Equal("Semi-skimmed", dto.Description);
        Assert.Equal(TaskPriority.Low, dto.Priority);
        Assert.Equal(Now.AddDays(1), dto.DueDate);
        Assert.Equal(TaskItemStatus.Todo, dto.Status);
        Assert.Equal(Now, dto.CreatedAt);
        Assert.Null(dto.CompletedAt);
        Assert.False(dto.IsOverdue);
        Assert.False(dto.IsCompletedLate);
    }

    [Fact]
    public async Task Task_becomes_overdue_as_the_clock_passes_its_due_date()
    {
        var task = StoredTask(UserId, Now.AddHours(1));
        Time.Advance(TimeSpan.FromHours(2));

        Assert.True((await Service.GetAsync(UserId, task.Id, default)).Value.IsOverdue);
    }

    [Fact]
    public async Task Task_done_after_its_due_date_is_completed_late()
    {
        var task = StoredTask(UserId, Now.AddHours(1));
        task.ChangeStatus(TaskItemStatus.Done, Now.AddHours(2));

        var dto = (await Service.GetAsync(UserId, task.Id, default)).Value;

        Assert.Equal(Now.AddHours(2), dto.CompletedAt);
        Assert.True(dto.IsCompletedLate);
        Assert.False(dto.IsOverdue);
    }

    [Fact]
    public async Task List_only_asks_for_the_callers_tasks_and_maps_the_page()
    {
        var query = new TaskQuery(Status: TaskItemStatus.Todo, SortBy: TaskSortBy.DueDate, Page: 2, PageSize: 10);
        var task = TaskItem.Create(UserId, "Buy milk", null, null, Now.AddHours(1), Now).Value;
        Time.Advance(TimeSpan.FromHours(2));
        Repository.ListAsync(UserId, query, Now.AddHours(2), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<TaskItem>([task], 2, 10, 11));

        var result = await Service.ListAsync(UserId, query, default);

        var page = result.Value;
        Assert.Equal(2, page.Page);
        Assert.Equal(10, page.PageSize);
        Assert.Equal(11, page.TotalCount);
        var dto = Assert.Single(page.Items);
        Assert.Equal(task.Id, dto.Id);
        Assert.True(dto.IsOverdue);
        await Repository.Received(1).ListAsync(UserId, query, Now.AddHours(2), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_accepts_the_largest_page_size()
    {
        Repository.ListAsync(default, default!, default, default).ReturnsForAnyArgs(new PagedResult<TaskItem>([], 1, 100, 0));

        var result = await Service.ListAsync(UserId, new TaskQuery(PageSize: TaskQuery.MaxPageSize), default);

        Assert.True(result.IsSuccess);
    }

    public static TheoryData<TaskQuery, Domain.Common.Error> InvalidQueries => new()
    {
        { new TaskQuery(Page: 0), TaskQueryErrors.PageInvalid },
        { new TaskQuery(PageSize: 0), TaskQueryErrors.PageSizeInvalid },
        { new TaskQuery(PageSize: TaskQuery.MaxPageSize + 1), TaskQueryErrors.PageSizeInvalid },
        { new TaskQuery(Status: (TaskItemStatus)42), TaskQueryErrors.StatusInvalid },
        { new TaskQuery(Priority: (TaskPriority)42), TaskQueryErrors.PriorityInvalid },
        { new TaskQuery(SortBy: (TaskSortBy)42), TaskQueryErrors.SortByInvalid },
    };

    [Theory]
    [MemberData(nameof(InvalidQueries))]
    public async Task Invalid_query_is_rejected_without_reaching_the_repository(TaskQuery query, Domain.Common.Error expected)
    {
        var result = await Service.ListAsync(UserId, query, default);

        Assert.Equal([expected], result.Errors);
        await Repository.DidNotReceiveWithAnyArgs().ListAsync(default, default!, default, default);
    }

    [Fact]
    public async Task Every_invalid_query_field_is_reported()
    {
        var result = await Service.ListAsync(UserId, new TaskQuery(Page: 0, PageSize: 0), default);

        Assert.Equal([TaskQueryErrors.PageInvalid, TaskQueryErrors.PageSizeInvalid], result.Errors);
    }
}
