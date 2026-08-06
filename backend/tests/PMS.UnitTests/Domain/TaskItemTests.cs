using PMS.Domain.Common;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using Shouldly;

namespace PMS.UnitTests.Domain;

public class TaskItemTests
{
    // ---------- Chuyển cột (ADR-052 thay thế ma trận chuyển trạng thái của ADR-021) ----------

    // 🗑️ `CanTransitionTo_phu_het_16_cap_trang_thai` đã XÓA cùng ADR-052, không phải vì nó
    // hỏng mà vì thứ nó khóa đã không còn tồn tại. Ma trận sáu-cặp-hợp-lệ là luật đúng khi
    // hệ thống sở hữu bốn trạng thái; với cột do NGƯỜI DÙNG tạo thì không còn cơ sở nào để
    // nói cặp nào hợp lệ — hệ thống không biết "Chờ QA" đứng trước hay sau "Đang sửa".
    //
    // Hệ quả đáng nhớ nhất: **kéo thẻ về đúng cột nó đang đứng nay là hợp lệ**, trước đây là
    // 409. Xem test ngay dưới.

    [Fact]
    public void MoveTo_dat_ca_BoardColumnId_va_Category_trong_mot_lan()
    {
        var task = NewTask();
        var column = ColumnFor(task, "Chờ QA", StatusCategory.InProgress);

        task.MoveTo(column);

        task.BoardColumnId.ShouldBe(column.Id);
        // 🔴 Bất biến quan trọng nhất của ADR-052: bản sao Category trên task phải đi cùng
        // cột. Lệch là mọi phép kiểm "task xong chưa" trong solution trả lời sai, im lặng.
        task.Category.ShouldBe(StatusCategory.InProgress);
    }

    [Fact]
    public void MoveTo_ve_dung_cot_dang_dung_khong_con_la_loi()
    {
        var task = NewTask();
        var column = ColumnFor(task, "Cần làm", StatusCategory.ToDo);
        task.MoveTo(column);

        Should.NotThrow(() => task.MoveTo(column));

        task.BoardColumnId.ShouldBe(column.Id);
    }

    [Fact]
    public void MoveTo_cot_cua_project_khac_nem_DomainException()
    {
        var task = NewTask();
        var foreign = new BoardColumn
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),          // project KHÁC
            Name = "Cột lạ",
            Category = StatusCategory.ToDo,
        };

        Should.Throw<DomainException>(() => task.MoveTo(foreign));
    }

    // ---------- Subtask ----------

    [Fact]
    public void AddSubtask_gan_ParentTaskId_va_keo_theo_ProjectId_cua_cha()
    {
        var parent = NewTask();
        var child = new TaskItem { Id = Guid.NewGuid(), Name = "Subtask" };

        parent.AddSubtask(child);

        child.ParentTaskId.ShouldBe(parent.Id);
        child.ProjectId.ShouldBe(parent.ProjectId);
        child.IsSubtask.ShouldBeTrue();
        parent.Subtasks.ShouldHaveSingleItem();
    }

    [Fact]
    public void AddSubtask_tren_1_subtask_nem_DomainException_chu_khong_phai_InvalidOperation()
    {
        // DomainException -> 409; InvalidOperationException sẽ rơi vào catch-all -> 500 (ADR-011)
        var parent = NewTask();
        var child = new TaskItem { Id = Guid.NewGuid(), Name = "Subtask" };
        parent.AddSubtask(child);

        var grandchild = new TaskItem { Id = Guid.NewGuid(), Name = "Subtask của subtask" };

        Should.Throw<DomainException>(() => child.AddSubtask(grandchild));
    }

    [Fact]
    public void SubtaskProgress_khong_co_subtask_thi_bang_0()
        => NewTask().SubtaskProgress.ShouldBe(0m);

    [Fact]
    public void SubtaskProgress_lam_tron_2_chu_so_thap_phan()
    {
        var parent = NewTask();
        for (var i = 0; i < 3; i++)
            parent.AddSubtask(new TaskItem { Id = Guid.NewGuid(), Name = $"Sub {i}" });

        Advance(parent.Subtasks.First(), StatusCategory.Done);

        parent.SubtaskProgress.ShouldBe(33.33m);       // 1/3
    }

    [Fact]
    public void SubtaskProgress_moi_subtask_Done_thi_bang_100_nhung_task_cha_van_chua_Done()
    {
        // Chốt hành vi Jira: task cha KHÔNG tự động đóng (§5 ARCHITECTURE)
        var parent = NewTask();
        parent.AddSubtask(new TaskItem { Id = Guid.NewGuid(), Name = "Sub" });
        Advance(parent.Subtasks.Single(), StatusCategory.Done);

        parent.SubtaskProgress.ShouldBe(100m);
        parent.Category.ShouldBe(StatusCategory.ToDo);
    }

    // ---------- Assignment ----------

    [Fact]
    public void AddAssignee_sinh_Id_khac_Guid_Empty_cho_tung_ban_ghi()
    {
        // Id phải sinh phía app: ApplyIdNeverGenerated() tắt sinh Id ở DB, để mặc định
        // Guid.Empty thì bản ghi thứ hai vi phạm khóa chính.
        var task = NewTask();

        task.AddAssignee(NewEmployee(), RoleInTask.Owner);
        task.AddAssignee(NewEmployee(), RoleInTask.Contributor);

        task.Assignments.Count.ShouldBe(2);
        task.Assignments.ShouldAllBe(a => a.Id != Guid.Empty);
        task.Assignments.Select(a => a.Id).Distinct().Count().ShouldBe(2);

        // Navigation phải sẵn sàng ngay: caller map bản ghi vừa tạo ra DTO (cần
        // Employee.Name) trước khi có lần load lại nào.
        task.Assignments.ShouldAllBe(a => a.Employee != null);
    }

    [Fact]
    public void AddAssignee_goi_2_lan_cung_1_nguoi_thi_khong_tao_ban_ghi_trung()
    {
        var task = NewTask();
        var employee = NewEmployee();

        task.AddAssignee(employee, RoleInTask.Owner);
        task.AddAssignee(employee, RoleInTask.Contributor);

        var assignment = task.Assignments.ShouldHaveSingleItem();
        assignment.RoleInTask.ShouldBe(RoleInTask.Owner);   // lần gọi đầu thắng
    }

    [Fact]
    public void RemoveAssignee_tra_false_khi_nguoi_do_von_khong_duoc_gan()
    {
        var task = NewTask();

        task.RemoveAssignee(Guid.NewGuid()).ShouldBeFalse();
    }

    [Fact]
    public void RemoveAssignee_tra_true_va_go_dung_nguoi()
    {
        var task = NewTask();
        var giu_lai = NewEmployee();
        var go_ra = NewEmployee();
        task.AddAssignee(giu_lai, RoleInTask.Owner);
        task.AddAssignee(go_ra, RoleInTask.Contributor);

        task.RemoveAssignee(go_ra.Id).ShouldBeTrue();

        task.Assignments.ShouldHaveSingleItem().EmployeeId.ShouldBe(giu_lai.Id);
    }

    [Fact]
    public void LinkTo_sinh_Id_khac_Guid_Empty()
    {
        var source = NewTask();
        var target = NewTask();

        source.LinkTo(target, LinkType.Blocks);

        var link = source.OutgoingLinks.ShouldHaveSingleItem();
        link.Id.ShouldNotBe(Guid.Empty);
        link.SourceTaskId.ShouldBe(source.Id);
        link.TargetTaskId.ShouldBe(target.Id);
    }

    // ---------- IsOverdue ----------

    [Fact]
    public void IsOverdue_true_khi_qua_han_va_chua_Done()
        => NewTask(dueDate: DateTime.UtcNow.AddDays(-1)).IsOverdue.ShouldBeTrue();

    [Fact]
    public void IsOverdue_false_khi_da_Done_du_qua_han()
    {
        var task = NewTask(dueDate: DateTime.UtcNow.AddDays(-1));
        Advance(task, StatusCategory.Done);

        task.IsOverdue.ShouldBeFalse();
    }

    [Fact]
    public void IsOverdue_false_khi_khong_co_DueDate()
        => NewTask().IsOverdue.ShouldBeFalse();

    [Fact]
    public void IsOverdue_false_khi_han_con_trong_tuong_lai()
        => NewTask(dueDate: DateTime.UtcNow.AddDays(1)).IsOverdue.ShouldBeFalse();

    // ---------- helpers ----------

    private static TaskItem NewTask(DateTime? dueDate = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Task test",
        ProjectId = Guid.NewGuid(),
        ReporterId = Guid.NewGuid(),
        DueDate = dueDate
    };

    private static Employee NewEmployee() => new()
    {
        Id = Guid.NewGuid(), Name = "Nhân sự", Email = $"{Guid.NewGuid():N}@pms.test"
    };

    /// <summary>
    /// Cột giả cho project của <paramref name="task"/>. Sau ADR-052 không còn state machine
    /// nên không phải "đi đúng đường" nữa — task đặt thẳng vào cột nào cũng được.
    /// </summary>
    private static BoardColumn ColumnFor(TaskItem task, string name, StatusCategory category)
        => new()
        {
            Id = Guid.NewGuid(),
            ProjectId = task.ProjectId,
            Name = name,
            Category = category,
        };

    private static void Advance(TaskItem task, StatusCategory category)
        => task.MoveTo(ColumnFor(task, category.ToString(), category));

    private static TaskItem TaskAt(StatusCategory category)
    {
        var task = NewTask();
        Advance(task, category);
        return task;
    }
}
