using Microsoft.Extensions.Logging;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Features.BoardColumns;

/// <summary>
/// Cấu hình cột board của một project (ADR-052).
///
/// <para>
/// 🔴 Lớp này là <b>người canh giữ bản sao <c>TaskItem.Category</c></b>. Cột đổi nhóm hay
/// task đổi cột đều phải đi qua đây; bỏ qua một nhánh là để bản sao trôi khỏi cột, và khi
/// đó mọi phép kiểm "task xong chưa" trong solution trả lời sai mà không có gì báo.
/// </para>
/// </summary>
public class BoardColumnService : IBoardColumnService
{
    private readonly IUnitOfWork _uow;
    private readonly IProjectAuthorizationService _authz;
    private readonly IActivityLogger _activityLog;
    private readonly ILogger<BoardColumnService> _logger;

    public BoardColumnService(
        IUnitOfWork uow, IProjectAuthorizationService authz,
        IActivityLogger activityLog, ILogger<BoardColumnService> logger)
    {
        _uow = uow;
        _authz = authz;
        _activityLog = activityLog;
        _logger = logger;
    }

    public async Task<IReadOnlyList<BoardColumnResponse>> ListAsync(
        Guid projectId, CancellationToken ct = default)
    {
        // View: ai đọc được project thì đọc được cấu hình cột của nó. Ẩn cột với Viewer sẽ
        // làm board của họ trống rỗng mà không có lý do nào giải thích được.
        await _authz.AuthorizeAsync(projectId, ProjectAction.View, ct);
        return await BuildResponsesAsync(projectId, ct);
    }

    public async Task<BoardColumnResponse> CreateAsync(
        Guid projectId, CreateBoardColumnRequest request, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(projectId, ProjectAction.ManageBoardColumns, ct);

        var existing = await _uow.BoardColumns.ListByProjectAsync(projectId, ct);
        var name = request.Name.Trim();

        // Kiểm ở đây thay vì để unique index ném: index trả về một DbUpdateException mà
        // middleware map thành 500, còn người dùng thì cần biết chính xác cái gì trùng.
        if (existing.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException($"Project đã có cột tên '{name}'.");

        var column = new BoardColumn
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = name,
            Color = request.Color.Trim(),
            Category = request.Category,
            // Thêm vào CUỐI dải. Chèn giữa sẽ phải dịch mọi cột phía sau, mà người dùng
            // muốn vị trí khác thì đã có thao tác sắp xếp lại.
            Order = existing.Count == 0 ? 0 : existing.Max(c => c.Order) + 1,
        };

        await _uow.BoardColumns.AddAsync(column, ct);

        _activityLog.Log(nameof(Project), projectId, ActivityAction.Updated,
            $"Thêm cột board '{column.Name}'");

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Thêm cột {ColumnId} '{Name}' vào project {ProjectId}",
            column.Id, column.Name, projectId);

        return new BoardColumnResponse(
            column.Id, column.Name, column.Color, column.Order, column.Category, 0);
    }

    public async Task<BoardColumnResponse> UpdateAsync(
        Guid columnId, UpdateBoardColumnRequest request, CancellationToken ct = default)
    {
        var column = await RequireColumnAsync(columnId, ct);
        await _authz.AuthorizeAsync(column.ProjectId, ProjectAction.ManageBoardColumns, ct);

        var siblings = await _uow.BoardColumns.ListByProjectAsync(column.ProjectId, ct);
        var name = request.Name.Trim();

        if (siblings.Any(c => c.Id != columnId
                           && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException($"Project đã có cột tên '{name}'.");

        var categoryChanged = column.Category != request.Category;

        column.Name = name;
        column.Color = request.Color.Trim();
        column.Category = request.Category;

        _uow.BoardColumns.Update(column);
        await _uow.SaveChangesAsync(ct);

        // 🔴 BẮT BUỘC, và phải chạy SAU khi cột đã lưu. Bản sao `TaskItem.Category` không
        // tự theo cột — bỏ bước này thì một cột vừa đổi từ InProgress sang Done vẫn để lại
        // hàng chục task mang nhóm cũ, và chúng sẽ bị đếm là "chưa xong" mãi mãi.
        if (categoryChanged)
        {
            var affected = await _uow.BoardColumns.SyncTaskCategoriesAsync(column, ct);
            _logger.LogInformation(
                "Cột {ColumnId} đổi nhóm sang {Category}, đồng bộ {Affected} task",
                columnId, column.Category, affected);
        }

        _activityLog.Log(nameof(Project), column.ProjectId, ActivityAction.Updated,
            $"Sửa cột board '{column.Name}'");
        await _uow.SaveChangesAsync(ct);

        var counts = await _uow.BoardColumns.CountTasksByColumnAsync(column.ProjectId, ct);

        return new BoardColumnResponse(
            column.Id, column.Name, column.Color, column.Order, column.Category,
            counts.GetValueOrDefault(column.Id));
    }

    public async Task DeleteAsync(
        Guid columnId, DeleteBoardColumnRequest request, CancellationToken ct = default)
    {
        var column = await RequireColumnAsync(columnId, ct);
        await _authz.AuthorizeAsync(column.ProjectId, ProjectAction.ManageBoardColumns, ct);

        var siblings = await _uow.BoardColumns.ListByProjectAsync(column.ProjectId, ct);

        // Project phải luôn còn ít nhất một cột: task mới cần một cột để đứng, nên project
        // không cột là project không tạo được task nào — và lỗi đó chỉ lộ ra ở lần tạo task
        // kế tiếp chứ không phải ngay lúc xóa.
        if (siblings.Count <= 1)
            throw new ConflictException(
                "Không thể xóa cột cuối cùng — board phải luôn còn ít nhất một cột.");

        var counts = await _uow.BoardColumns.CountTasksByColumnAsync(column.ProjectId, ct);
        var taskCount = counts.GetValueOrDefault(columnId);

        if (taskCount > 0)
        {
            if (request.TargetColumnId is not { } targetId)
                throw new BusinessRuleException(
                    $"Cột '{column.Name}' còn {taskCount} task. Hãy chọn cột để chuyển chúng sang.");

            if (targetId == columnId)
                throw new BusinessRuleException("Cột đích phải khác cột đang xóa.");

            var target = siblings.FirstOrDefault(c => c.Id == targetId)
                ?? throw new NotFoundException(nameof(BoardColumn), targetId);

            // Một lệnh UPDATE hàng loạt, đặt cả BoardColumnId lẫn Category — xem chú thích
            // ở `MoveAllTasksAsync` về việc nó KHÔNG đi qua ChangeTracker.
            var moved = await _uow.BoardColumns.MoveAllTasksAsync(columnId, target, ct);

            _logger.LogInformation("Chuyển {Moved} task từ cột {From} sang {To}",
                moved, columnId, targetId);
        }

        _uow.BoardColumns.Remove(column);

        _activityLog.Log(nameof(Project), column.ProjectId, ActivityAction.Updated,
            taskCount > 0
                ? $"Xóa cột board '{column.Name}', chuyển {taskCount} task sang cột khác"
                : $"Xóa cột board '{column.Name}'");

        await _uow.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<BoardColumnResponse>> ReorderAsync(
        Guid projectId, ReorderBoardColumnsRequest request, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(projectId, ProjectAction.ManageBoardColumns, ct);

        var columns = await _uow.BoardColumns.ListByProjectAsync(projectId, ct);
        var ids = request.OrderedColumnIds;

        // Đòi ĐỦ và ĐÚNG tập cột hiện có. Nhận danh sách thiếu thì những cột không được
        // nhắc tới sẽ giữ Order cũ và trộn lẫn vào dải mới một cách không đoán được — hỏng
        // im lặng, và chỉ lộ ra khi ai đó nhìn board thấy thứ tự lạ.
        if (ids.Count != columns.Count || ids.Distinct().Count() != ids.Count
            || ids.Any(id => columns.All(c => c.Id != id)))
            throw new BusinessRuleException(
                "Danh sách sắp xếp phải chứa đúng một lần mỗi cột hiện có của project.");

        for (var i = 0; i < ids.Count; i++)
        {
            var column = columns.First(c => c.Id == ids[i]);
            if (column.Order == i) continue;

            column.Order = i;
            _uow.BoardColumns.Update(column);
        }

        await _uow.SaveChangesAsync(ct);

        return await BuildResponsesAsync(projectId, ct);
    }

    private async Task<BoardColumn> RequireColumnAsync(Guid columnId, CancellationToken ct)
        => await _uow.BoardColumns.GetByIdAsync(columnId, ct)
           ?? throw new NotFoundException(nameof(BoardColumn), columnId);

    private async Task<IReadOnlyList<BoardColumnResponse>> BuildResponsesAsync(
        Guid projectId, CancellationToken ct)
    {
        var columns = await _uow.BoardColumns.ListByProjectAsync(projectId, ct);
        var counts = await _uow.BoardColumns.CountTasksByColumnAsync(projectId, ct);

        return columns
            .Select(c => new BoardColumnResponse(
                c.Id, c.Name, c.Color, c.Order, c.Category, counts.GetValueOrDefault(c.Id)))
            .ToList();
    }
}
