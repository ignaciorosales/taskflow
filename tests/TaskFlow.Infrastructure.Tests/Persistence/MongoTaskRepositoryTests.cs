using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Tasks;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Tests.Persistence;

public class MongoTaskRepositoryTests(MongoFixture mongo) : IClassFixture<MongoFixture>
{
    private static readonly DateTimeOffset T0 = new(2030, 1, 1, 9, 0, 0, TimeSpan.Zero);
    private readonly Guid _owner = Guid.NewGuid();

    private async Task<MongoTaskRepository> RepositoryAsync() => new(await mongo.NewDatabaseAsync());

    private static TaskItem NewTask(
        Guid owner, string title, TaskPriority? priority = null, DateTimeOffset? dueDate = null, DateTimeOffset? createdAt = null)
        => TaskItem.Create(owner, title, null, priority, dueDate, createdAt ?? T0).Value;

    private static async Task AddAllAsync(MongoTaskRepository repository, params TaskItem[] tasks)
    {
        foreach (var task in tasks)
            await repository.AddAsync(task, default);
    }

    private async Task<string[]> TitlesAsync(MongoTaskRepository repository, TaskQuery query, DateTimeOffset? now = null)
        => [.. (await repository.ListAsync(_owner, query, now ?? T0, default)).Items.Select(t => t.Title)];

    [Fact]
    public async Task Added_task_comes_back_with_every_field()
    {
        var repository = await RepositoryAsync();
        var task = TaskItem.Create(_owner, "Buy milk", "Semi-skimmed", TaskPriority.High, T0.AddDays(1), T0).Value;
        task.ChangeStatus(TaskItemStatus.Done, T0.AddHours(2));

        await repository.AddAsync(task, default);
        var loaded = await repository.GetByIdAsync(task.Id, default);

        Assert.NotNull(loaded);
        Assert.Equal(task.Id, loaded.Id);
        Assert.Equal(_owner, loaded.OwnerId);
        Assert.Equal("Buy milk", loaded.Title);
        Assert.Equal("Semi-skimmed", loaded.Description);
        Assert.Equal(TaskPriority.High, loaded.Priority);
        Assert.Equal(T0.AddDays(1), loaded.DueDate);
        Assert.Equal(TaskItemStatus.Done, loaded.Status);
        Assert.Equal(T0, loaded.CreatedAt);
        Assert.Equal(T0.AddHours(2), loaded.CompletedAt);
        Assert.Null(loaded.DeletedAt);
    }

    [Fact]
    public async Task Unknown_id_finds_nothing()
    {
        Assert.Null(await (await RepositoryAsync()).GetByIdAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Get_by_id_leaves_ownership_and_deletion_to_the_service()
    {
        var repository = await RepositoryAsync();
        var someoneElses = NewTask(Guid.NewGuid(), "Theirs");
        var deleted = NewTask(_owner, "Deleted");
        deleted.Delete(T0);
        await AddAllAsync(repository, someoneElses, deleted);

        Assert.NotNull(await repository.GetByIdAsync(someoneElses.Id, default));
        Assert.Equal(T0, (await repository.GetByIdAsync(deleted.Id, default))!.DeletedAt);
    }

    [Fact]
    public async Task Update_saves_the_changes()
    {
        var repository = await RepositoryAsync();
        var task = NewTask(_owner, "Buy milk");
        await repository.AddAsync(task, default);

        task.Edit("Buy bread", "Wholegrain", TaskPriority.Low, T0.AddDays(3), T0);
        task.ChangeStatus(TaskItemStatus.InProgress, T0);
        await repository.UpdateAsync(task, default);
        var loaded = await repository.GetByIdAsync(task.Id, default);

        Assert.Equal("Buy bread", loaded!.Title);
        Assert.Equal("Wholegrain", loaded.Description);
        Assert.Equal(TaskPriority.Low, loaded.Priority);
        Assert.Equal(T0.AddDays(3), loaded.DueDate);
        Assert.Equal(TaskItemStatus.InProgress, loaded.Status);
    }

    [Fact]
    public async Task List_returns_only_the_owners_tasks_that_arent_deleted()
    {
        var repository = await RepositoryAsync();
        var deleted = NewTask(_owner, "Deleted");
        await AddAllAsync(repository, NewTask(_owner, "Mine"), deleted, NewTask(Guid.NewGuid(), "Theirs"));
        deleted.Delete(T0);
        await repository.UpdateAsync(deleted, default);

        var page = await repository.ListAsync(_owner, new TaskQuery(), T0, default);

        Assert.Equal(["Mine"], page.Items.Select(t => t.Title));
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task Default_order_is_newest_first_and_can_be_reversed()
    {
        var repository = await RepositoryAsync();
        await AddAllAsync(repository,
            NewTask(_owner, "Old", createdAt: T0),
            NewTask(_owner, "New", createdAt: T0.AddMinutes(2)),
            NewTask(_owner, "Middle", createdAt: T0.AddMinutes(1)));

        Assert.Equal(["New", "Middle", "Old"], await TitlesAsync(repository, new TaskQuery()));
        Assert.Equal(["Old", "Middle", "New"], await TitlesAsync(repository, new TaskQuery(Descending: false)));
    }

    [Fact]
    public async Task Filters_by_status()
    {
        var repository = await RepositoryAsync();
        var inProgress = NewTask(_owner, "In progress");
        inProgress.ChangeStatus(TaskItemStatus.InProgress, T0);
        var done = NewTask(_owner, "Done");
        done.ChangeStatus(TaskItemStatus.Done, T0);
        await AddAllAsync(repository, NewTask(_owner, "To do"), inProgress, done);

        Assert.Equal(["In progress"], await TitlesAsync(repository, new TaskQuery(Status: TaskItemStatus.InProgress)));
    }

    [Fact]
    public async Task Filters_by_priority_and_combines_filters()
    {
        var repository = await RepositoryAsync();
        var highDone = NewTask(_owner, "High done", TaskPriority.High);
        highDone.ChangeStatus(TaskItemStatus.Done, T0);
        await AddAllAsync(repository, NewTask(_owner, "High", TaskPriority.High), highDone, NewTask(_owner, "Low", TaskPriority.Low));

        Assert.Equal(["High"], await TitlesAsync(repository, new TaskQuery(Status: TaskItemStatus.Todo, Priority: TaskPriority.High)));
    }

    [Fact]
    public async Task Overdue_filter_keeps_open_tasks_whose_due_date_has_passed()
    {
        var repository = await RepositoryAsync();
        var now = T0.AddHours(3);
        var done = NewTask(_owner, "Done late", dueDate: T0.AddHours(1));
        done.ChangeStatus(TaskItemStatus.Done, now);
        await AddAllAsync(repository,
            NewTask(_owner, "Overdue", dueDate: T0.AddHours(1)),
            done,
            NewTask(_owner, "Due exactly now", dueDate: now),
            NewTask(_owner, "Due later", dueDate: T0.AddHours(5)),
            NewTask(_owner, "No due date"));

        Assert.Equal(["Overdue"], await TitlesAsync(repository, new TaskQuery(OverdueOnly: true), now));
    }

    [Fact]
    public async Task Sorting_by_due_date_puts_tasks_without_one_last_in_both_directions()
    {
        var repository = await RepositoryAsync();
        await AddAllAsync(repository,
            NewTask(_owner, "Later", dueDate: T0.AddHours(3)),
            NewTask(_owner, "None"),
            NewTask(_owner, "Sooner", dueDate: T0.AddHours(1)));

        Assert.Equal(["Sooner", "Later", "None"], await TitlesAsync(repository, new TaskQuery(SortBy: TaskSortBy.DueDate, Descending: false)));
        Assert.Equal(["Later", "Sooner", "None"], await TitlesAsync(repository, new TaskQuery(SortBy: TaskSortBy.DueDate, Descending: true)));
    }

    [Fact]
    public async Task Sorting_by_priority_follows_importance_not_alphabetical_order()
    {
        var repository = await RepositoryAsync();
        await AddAllAsync(repository,
            NewTask(_owner, "Low", TaskPriority.Low),
            NewTask(_owner, "High", TaskPriority.High),
            NewTask(_owner, "Medium", TaskPriority.Medium));

        Assert.Equal(["High", "Medium", "Low"], await TitlesAsync(repository, new TaskQuery(SortBy: TaskSortBy.Priority, Descending: true)));
        Assert.Equal(["Low", "Medium", "High"], await TitlesAsync(repository, new TaskQuery(SortBy: TaskSortBy.Priority, Descending: false)));
    }

    [Fact]
    public async Task Pages_through_the_results_counting_the_whole_filtered_set()
    {
        var repository = await RepositoryAsync();
        await AddAllAsync(repository, [.. Enumerable.Range(1, 5).Select(i => NewTask(_owner, $"Task {i}", createdAt: T0.AddMinutes(i)))]);
        await repository.AddAsync(NewTask(Guid.NewGuid(), "Theirs"), default);

        var page = await repository.ListAsync(_owner, new TaskQuery(Page: 2, PageSize: 2), T0, default);

        Assert.Equal(["Task 3", "Task 2"], page.Items.Select(t => t.Title));
        Assert.Equal(2, page.Page);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(5, page.TotalCount);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(int.MaxValue)] // (page - 1) * pageSize no longer fits an int
    public async Task A_page_past_the_end_is_empty_however_far_it_is(int pageNumber)
    {
        var repository = await RepositoryAsync();
        await AddAllAsync(repository, [.. Enumerable.Range(1, 5).Select(i => NewTask(_owner, $"Task {i}"))]);

        var page = await repository.ListAsync(_owner, new TaskQuery(Page: pageNumber, PageSize: TaskQuery.MaxPageSize), T0, default);

        Assert.Empty(page.Items);
        Assert.Equal(pageNumber, page.Page);
        Assert.Equal(5, page.TotalCount);
    }

    [Fact]
    public async Task Paging_through_ties_returns_every_task_exactly_once()
    {
        var repository = await RepositoryAsync();
        var tasks = Enumerable.Range(1, 5).Select(i => NewTask(_owner, $"Task {i}", TaskPriority.Medium)).ToArray();
        await AddAllAsync(repository, tasks);

        var seen = new List<Guid>();
        for (var pageNumber = 1; pageNumber <= 3; pageNumber++)
        {
            var page = await repository.ListAsync(_owner, new TaskQuery(SortBy: TaskSortBy.Priority, Page: pageNumber, PageSize: 2), T0, default);
            seen.AddRange(page.Items.Select(t => t.Id));
        }

        Assert.Equal(tasks.Select(t => t.Id).Order(), seen.Order());
    }
}
