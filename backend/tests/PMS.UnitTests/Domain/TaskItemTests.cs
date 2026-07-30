using PMS.Domain.Common;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using Shouldly;

namespace PMS.UnitTests.Domain;

public class TaskItemTests
{
    // ---------- Workflow Transition Rules ----------

    [Theory]
    // 6 chuyển đổi hợp lệ duy nhất theo §5 ARCHITECTURE
    [InlineData(Status.ToDo,       Status.InProgress, true)]
    [InlineData(Status.InProgress, Status.Review,     true)]
    [InlineData(Status.InProgress, Status.ToDo,       true)]   // lùi lại
    [InlineData(Status.Review,     Status.Done,       true)]
    [InlineData(Status.Review,     Status.InProgress, true)]   // bị reject
    [InlineData(Status.Done,       Status.Review,     true)]   // mở lại
    // nhảy bước
    [InlineData(Status.ToDo,       Status.Review,     false)]
    [InlineData(Status.ToDo,       Status.Done,       false)]
    [InlineData(Status.InProgress, Status.Done,       false)]
    [InlineData(Status.Done,       Status.ToDo,       false)]
    [InlineData(Status.Done,       Status.InProgress, false)]
    [InlineData(Status.Review,     Status.ToDo,       false)]
    // đứng yên cũng không hợp lệ — nhờ đó 2 người cùng bấm 1 đích thì người sau bị chặn
    [InlineData(Status.ToDo,       Status.ToDo,       false)]
    [InlineData(Status.InProgress, Status.InProgress, false)]
    [InlineData(Status.Review,     Status.Review,     false)]
    [InlineData(Status.Done,       Status.Done,       false)]
    public void CanTransitionTo_phu_het_16_cap_trang_thai(Status from, Status to, bool expected)
        => TaskAt(from).CanTransitionTo(to).ShouldBe(expected);

    [Fact]
    public void ChangeStatus_hop_le_thi_doi_trang_thai()
    {
        var task = TaskAt(Status.InProgress);

        task.ChangeStatus(Status.Review);

        task.Status.ShouldBe(Status.Review);
    }

    [Fact]
    public void ChangeStatus_nhay_buoc_nem_DomainException_va_giu_nguyen_trang_thai()
    {
        var task = TaskAt(Status.ToDo);

        Should.Throw<DomainException>(() => task.ChangeStatus(Status.Done));

        task.Status.ShouldBe(Status.ToDo);
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

        Advance(parent.Subtasks.First(), Status.Done);

        parent.SubtaskProgress.ShouldBe(33.33m);       // 1/3
    }

    [Fact]
    public void SubtaskProgress_moi_subtask_Done_thi_bang_100_nhung_task_cha_van_chua_Done()
    {
        // Chốt hành vi Jira: task cha KHÔNG tự động đóng (§5 ARCHITECTURE)
        var parent = NewTask();
        parent.AddSubtask(new TaskItem { Id = Guid.NewGuid(), Name = "Sub" });
        Advance(parent.Subtasks.Single(), Status.Done);

        parent.SubtaskProgress.ShouldBe(100m);
        parent.Status.ShouldBe(Status.ToDo);
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
        Advance(task, Status.Done);

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

    /// <summary>Status là private set + có state machine nên phải đi đúng đường, không set tắt được.</summary>
    private static void Advance(TaskItem task, Status target)
    {
        Status[] path = target switch
        {
            Status.ToDo       => [],
            Status.InProgress => [Status.InProgress],
            Status.Review     => [Status.InProgress, Status.Review],
            Status.Done       => [Status.InProgress, Status.Review, Status.Done],
            _ => throw new ArgumentOutOfRangeException(nameof(target))
        };
        foreach (var step in path) task.ChangeStatus(step);
    }

    private static TaskItem TaskAt(Status status)
    {
        var task = NewTask();
        Advance(task, status);
        return task;
    }
}
