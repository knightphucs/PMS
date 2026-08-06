using System.Data;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Features.Sprints;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.DbTechniques;

/// <summary>
/// Bốn kỹ thuật DB của migration <c>AddReportingDbObjects</c> — hạng mục 12 "Áp kỹ thuật
/// DB" của lộ trình (§1 ARCHITECTURE.md). Test bằng SQL thô qua chính connection của
/// <see cref="PMS.Infrastructure.Persistence.PmsDbContext"/> vì cả bốn đối tượng là view/
/// stored procedure/trigger/constraint — EF không có API gõ kiểu cho việc gọi chúng.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class DbTechniquesTests : IntegrationTestBase
{
    public DbTechniquesTests(PmsWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task INDEX_IX_TaskAssignments_EmployeeId_ton_tai_va_la_covering_index_cho_TaskId()
    {
        var (hasIndex, includesTaskId) = await WithDbAsync(async db =>
        {
            await using var cmd = CreateCommand(db, @"
                SELECT
                    CASE WHEN EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE name = 'IX_TaskAssignments_EmployeeId'
                          AND object_id = OBJECT_ID('TaskAssignments')
                    ) THEN 1 ELSE 0 END AS HasIndex,
                    CASE WHEN EXISTS (
                        SELECT 1
                        FROM sys.index_columns ic
                        JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                        JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                        WHERE i.name = 'IX_TaskAssignments_EmployeeId'
                          AND i.object_id = OBJECT_ID('TaskAssignments')
                          AND c.name = 'TaskId'
                          AND ic.is_included_column = 1
                    ) THEN 1 ELSE 0 END AS IncludesTaskId;");

            await using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();
            return (reader.GetInt32(0) == 1, reader.GetInt32(1) == 1);
        });

        hasIndex.ShouldBeTrue();
        includesTaskId.ShouldBeTrue();
    }

    [Fact]
    public async Task CONSTRAINT_chan_Sprint_EndDate_truoc_StartDate_ngay_ca_khi_ghi_bang_SQL_tho_bo_qua_domain()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        // Cố tình đi vòng qua SprintValidators + Sprint domain bằng SQL thô qua ADO trực
        // tiếp (không qua SaveChanges của EF) — mô phỏng một script/migration ghi thẳng
        // xuống DB. Đây là đúng thứ mà validator KHÔNG chặn được, chỉ constraint mới.
        var ex = await Should.ThrowAsync<DbException>(() => WithDbAsync(async db =>
        {
            await using var cmd = CreateCommand(db, @"
                INSERT INTO Sprints (Id, ProjectId, Name, Goal, StartDate, EndDate, Status, IsDeleted)
                VALUES (@id, @projectId, N'Sprint sai ngày', N'', @start, @end, 0, 0);",
                ("@id", Guid.NewGuid()),
                ("@projectId", projectId),
                ("@start", DateTime.UtcNow),
                ("@end", DateTime.UtcNow.AddDays(-1)));   // EndDate TRƯỚC StartDate

            await cmd.ExecuteNonQueryAsync();
        }));

        ex.Message.ShouldContain("CK_Sprints_EndDate_After_StartDate");
    }

    [Fact]
    public async Task TRIGGER_duy_tri_Projects_TaskCount_qua_tao_va_xoa_mem_task()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        (await TaskCountAsync(projectId)).ShouldBe(0);

        var taskId = await CreateTaskAsync(pm.Client, projectId, "Task 1");
        await CreateTaskAsync(pm.Client, projectId, "Task 2");

        // Trigger chạy AFTER INSERT — không cần đợi round-trip nào khác để thấy hiệu lực.
        (await TaskCountAsync(projectId)).ShouldBe(2);

        var del = await pm.Client.DeleteAsync($"/api/v1/tasks/{taskId}");
        del.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Xóa task đi qua ApplySoftDelete() (UPDATE IsDeleted=1, KHÔNG phải DELETE thật) —
        // đây chính là lý do trigger phải đọc `inserted`/`deleted` theo IsDeleted thay vì
        // chỉ đếm sự kiện DELETE. Không xử lý đúng nhánh này thì bộ đếm chỉ tăng, không
        // bao giờ giảm.
        (await TaskCountAsync(projectId)).ShouldBe(1);
    }

    [Fact]
    public async Task TRIGGER_khong_doi_gia_tri_khi_UPDATE_khong_dung_toi_IsDeleted()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId, "Tên cũ");
        (await TaskCountAsync(projectId)).ShouldBe(1);

        // Một UPDATE trên Tasks không đụng IsDeleted (đổi mô tả trực tiếp bằng SQL, tương
        // đương PUT /tasks/{id}) — trigger phải triệt tiêu +1/-1 và không đổi TaskCount.
        await WithDbAsync(async db =>
        {
            await using var cmd = CreateCommand(db,
                "UPDATE Tasks SET Description = N'Đổi mô tả' WHERE Id = @id;", ("@id", taskId));
            await cmd.ExecuteNonQueryAsync();
        });

        (await TaskCountAsync(projectId)).ShouldBe(1);
    }

    [Fact]
    public async Task VIEW_vw_SprintVelocity_chi_tinh_sprint_da_DONG_SO_va_dem_dung_so_task_Done()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var sprintId = await CreateSprintAsync(pm.Client, projectId, "Sprint velocity");

        var doneTask = await CreateTaskAsync(pm.Client, projectId, "Xong", sprintId: sprintId);
        await CreateTaskAsync(pm.Client, projectId, "Chưa xong", sprintId: sprintId);
        await MoveToColumnAsync(pm.Client, doneTask, targetOrder: 3);   // "Hoàn thành" = Done

        // Sprint CHƯA đóng sổ -> view không được liệt kê, dù đã có task Done.
        (await VelocityRowAsync(sprintId)).ShouldBeNull();

        var start = await pm.Client.PostAsync($"/api/v1/sprints/{sprintId}/start", null);
        start.StatusCode.ShouldBe(HttpStatusCode.OK);

        // TargetSprintId = null nghĩa là đẩy task CHƯA XONG về Backlog (ADR-050) — task đó
        // rời khỏi sprint (SprintId = null) chứ không ở lại. Nên sau khi đóng, sprint chỉ
        // còn đúng task đã Done: view phải phản ánh đúng hiện trạng, không phải một bản chụp
        // lịch sử "từng có 2 task".
        var complete = await pm.Client.PostAsJsonAsync(
            $"/api/v1/sprints/{sprintId}/complete", new CompleteSprintRequest(null));
        complete.StatusCode.ShouldBe(HttpStatusCode.OK);

        var row = await VelocityRowAsync(sprintId);
        row.ShouldNotBeNull();
        row!.Value.Total.ShouldBe(1);
        row.Value.Done.ShouldBe(1);
        row.Value.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task STORED_PROCEDURE_sp_GetProjectBacklogInsight_dem_dung_qua_han_sap_den_han_va_khong_han()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        await CreateTaskAsync(pm.Client, projectId, "Quá hạn", dueDate: DateTime.UtcNow.AddDays(-3));
        await CreateTaskAsync(pm.Client, projectId, "Sắp đến hạn", dueDate: DateTime.UtcNow.AddDays(2));
        await CreateTaskAsync(pm.Client, projectId, "Không hạn");

        // Task đã Done không được tính vào backlog — kiểm bằng cách chuyển một task quá hạn
        // sang cột Done rồi xác nhận nó KHÔNG còn nằm trong "Quá hạn".
        var doneOverdue = await CreateTaskAsync(
            pm.Client, projectId, "Quá hạn nhưng đã xong", dueDate: DateTime.UtcNow.AddDays(-10));
        await MoveToColumnAsync(pm.Client, doneOverdue, targetOrder: 3);

        var (total, overdue, dueSoon, noDueDate) = await WithDbAsync(async db =>
        {
            await using var cmd = CreateCommand(db,
                "EXEC sp_GetProjectBacklogInsight @ProjectId = @projectId, @DueSoonHorizonDays = @horizon;",
                ("@projectId", projectId), ("@horizon", 7));

            await using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();
            return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3));
        });

        total.ShouldBe(3);       // ba task chưa Done — task đã Done không tính
        overdue.ShouldBe(1);
        dueSoon.ShouldBe(1);
        noDueDate.ShouldBe(1);
    }

    [Fact]
    public async Task STORED_PROCEDURE_sp_GetProjectBacklogByPriority_gom_dung_theo_Priority()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        await CreateTaskAsync(pm.Client, projectId, "Việc gấp 1", priority: PMS.Domain.Enums.Priority.High);
        await CreateTaskAsync(pm.Client, projectId, "Việc gấp 2", priority: PMS.Domain.Enums.Priority.High);
        await CreateTaskAsync(pm.Client, projectId, "Việc thường", priority: PMS.Domain.Enums.Priority.Medium);

        var counts = await WithDbAsync(async db =>
        {
            await using var cmd = CreateCommand(db,
                "EXEC sp_GetProjectBacklogByPriority @ProjectId = @projectId;",
                ("@projectId", projectId));

            var result = new Dictionary<int, int>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result[reader.GetInt32(0)] = reader.GetInt32(1);
            return result;
        });

        counts[(int)PMS.Domain.Enums.Priority.High].ShouldBe(2);
        counts[(int)PMS.Domain.Enums.Priority.Medium].ShouldBe(1);
    }

    // ---------- helpers ----------

    private Task<int> TaskCountAsync(Guid projectId)
        => WithDbAsync(db => db.Projects
            .Where(p => p.Id == projectId).Select(p => p.TaskCount).SingleAsync());

    private async Task<(int Total, int Done, DateTime? CompletedAt)?> VelocityRowAsync(Guid sprintId)
        => await WithDbAsync(async db =>
        {
            await using var cmd = CreateCommand(db,
                "SELECT TotalTasks, DoneTasks, CompletedAt FROM vw_SprintVelocity WHERE SprintId = @id;",
                ("@id", sprintId));

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return ((int, int, DateTime?)?)null;

            return (reader.GetInt32(0), reader.GetInt32(1),
                reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2));
        });

    private static DbCommand CreateCommand(
        PMS.Infrastructure.Persistence.PmsDbContext db, string sql, params (string Name, object Value)[] parameters)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
        }
        return cmd;
    }
}
