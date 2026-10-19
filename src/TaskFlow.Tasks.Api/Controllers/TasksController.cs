using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Common;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Tasks;

namespace TaskFlow.Tasks.Api.Controllers;

public sealed record ChangeStatusRequest(TaskItemStatus Status);

[ApiController]
[Authorize]
[Route("api/tasks")]
public sealed class TasksController(TaskService tasks) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] TaskQuery query, CancellationToken cancellationToken)
    {
        var result = await tasks.ListAsync(User.UserId(), query, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Errors);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await tasks.GetAsync(User.UserId(), id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Errors);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var result = await tasks.CreateAsync(User.UserId(), request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value)
            : this.ToProblem(result.Errors);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        var result = await tasks.UpdateAsync(User.UserId(), id, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Errors);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, ChangeStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await tasks.ChangeStatusAsync(User.UserId(), id, request.Status, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Errors);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await tasks.DeleteAsync(User.UserId(), id, cancellationToken);
        return result.IsSuccess ? NoContent() : this.ToProblem(result.Errors);
    }
}
