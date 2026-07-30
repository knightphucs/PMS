using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Common.Models;
using PMS.Application.Features.Tasks;

namespace PMS.API.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly ITaskService _tasks;
    private readonly ITaskStatusTransitionService _transitions;
    private readonly ITaskAssignmentService _assignments;

    public TasksController(
        ITaskService tasks,
        ITaskStatusTransitionService transitions,
        ITaskAssignmentService assignments)
        => (_tasks, _transitions, _assignments) = (tasks, transitions, assignments);

    /// <summary>Tạo task; truyền ParentTaskId để tạo subtask (tối đa 1 cấp cha–con).</summary>
    [HttpPost("tasks")]
    [ProducesResponseType(typeof(TaskSummaryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaskSummaryResponse>> Create(
        CreateTaskRequest req, CancellationToken ct)
    {
        var created = await _tasks.CreateAsync(req, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpGet("projects/{projectId:guid}/tasks")]
    [ProducesResponseType(typeof(PagedResult<TaskSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<TaskSummaryResponse>>> GetByProject(
        Guid projectId, [FromQuery] PagedRequest request, CancellationToken ct)
        => Ok(await _tasks.GetByProjectAsync(projectId, request, ct));

    [HttpGet("tasks/{id:guid}")]
    [ProducesResponseType(typeof(TaskDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskDetailResponse>> GetById(Guid id, CancellationToken ct)
        => Ok(await _tasks.GetByIdAsync(id, ct));

    [HttpPut("tasks/{id:guid}")]
    [ProducesResponseType(typeof(TaskDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaskDetailResponse>> Update(
        Guid id, UpdateTaskRequest req, CancellationToken ct)
        => Ok(await _tasks.UpdateAsync(id, req, ct));

    /// <summary>Xóa task; bị chặn 409 nếu còn subtask chưa Done (ADR-018).</summary>
    [HttpDelete("tasks/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _tasks.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Đổi trạng thái theo Workflow Transition Rules. Được phép gọi: Assignee của task
    /// hoặc ProjectManager của project (ADR-017). Không cần RowVersion — chính state
    /// machine đã chặn hai người cùng chuyển tới một đích (ADR-021).
    /// </summary>
    [HttpPatch("tasks/{id:guid}/status")]
    [ProducesResponseType(typeof(TaskSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaskSummaryResponse>> ChangeStatus(
        Guid id, ChangeTaskStatusRequest req, CancellationToken ct)
        => Ok(await _transitions.ChangeStatusAsync(id, req, ct));

    /// <summary>Kéo task giữa Sprint và Backlog; SprintId = null nghĩa là về Backlog.</summary>
    [HttpPut("tasks/{id:guid}/sprint")]
    [ProducesResponseType(typeof(TaskSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskSummaryResponse>> MoveToSprint(
        Guid id, MoveTaskToSprintRequest req, CancellationToken ct)
        => Ok(await _tasks.MoveToSprintAsync(id, req, ct));

    [HttpGet("tasks/{id:guid}/assignees")]
    [ProducesResponseType(typeof(IReadOnlyList<TaskAssigneeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TaskAssigneeResponse>>> GetAssignees(
        Guid id, CancellationToken ct)
        => Ok(await _assignments.GetAssigneesAsync(id, ct));

    /// <summary>Gán người khác vào task — chỉ ProjectManager; target phải là thành viên đã Accepted.</summary>
    [HttpPost("tasks/{id:guid}/assignees")]
    [ProducesResponseType(typeof(TaskAssigneeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaskAssigneeResponse>> Assign(
        Guid id, AssignTaskRequest req, CancellationToken ct)
        => Ok(await _assignments.AssignAsync(id, req, ct));

    /// <summary>Tự nhận task — Member/PM, chỉ khi task đang ToDo.</summary>
    [HttpPost("tasks/{id:guid}/assignees/me")]
    [ProducesResponseType(typeof(TaskAssigneeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaskAssigneeResponse>> SelfAssign(Guid id, CancellationToken ct)
        => Ok(await _assignments.SelfAssignAsync(id, ct));

    /// <summary>Tự rút khỏi task (employeeId = chính mình), hoặc ProjectManager gỡ người khác.</summary>
    [HttpDelete("tasks/{id:guid}/assignees/{employeeId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unassign(Guid id, Guid employeeId, CancellationToken ct)
    {
        await _assignments.UnassignAsync(id, employeeId, ct);
        return NoContent();
    }
}
