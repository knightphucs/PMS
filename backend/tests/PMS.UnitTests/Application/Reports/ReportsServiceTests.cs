using NSubstitute;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Interfaces;
using PMS.Application.Features.Reports;
using PMS.Domain.Enums;
using Shouldly;
using Xunit;

namespace PMS.UnitTests.Application.Reports;

public class ReportsServiceTests
{
    private readonly IProjectStatisticsRepository _stats = Substitute.For<IProjectStatisticsRepository>();
    private readonly IProjectAuthorizationService _authz = Substitute.For<IProjectAuthorizationService>();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly ReportsService _sut;

    public ReportsServiceTests() => _sut = new ReportsService(_stats, _authz);

    [Fact]
    public async Task GetBacklogInsightAsync_kiem_quyen_ViewStatistics_truoc_khi_hoi_repository()
    {
        _stats.GetBacklogInsightAsync(_projectId, 7, Arg.Any<CancellationToken>())
            .Returns(new BacklogInsightTally(0, 0, 0, 0));
        _stats.GetBacklogByPriorityAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<PriorityTally>)[]);

        await _sut.GetBacklogInsightAsync(_projectId, 7);

        await _authz.Received(1).AuthorizeAsync(
            _projectId, ProjectAction.ViewStatistics, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetBacklogInsightAsync_horizon_khong_duong_nem_loi_va_KHONG_hoi_repository()
    {
        await Should.ThrowAsync<BusinessRuleException>(
            () => _sut.GetBacklogInsightAsync(_projectId, 0));

        await _stats.DidNotReceive().GetBacklogInsightAsync(
            Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetBacklogInsightAsync_zero_fill_du_ca_nam_muc_Priority()
    {
        _stats.GetBacklogInsightAsync(_projectId, 7, Arg.Any<CancellationToken>())
            .Returns(new BacklogInsightTally(TotalOpen: 3, Overdue: 1, DueSoon: 1, NoDueDate: 1));
        // Repository chỉ trả về mức có task — Low hoàn toàn vắng mặt.
        _stats.GetBacklogByPriorityAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<PriorityTally>)[new PriorityTally(Priority.High, 3)]);

        var result = await _sut.GetBacklogInsightAsync(_projectId, 7);

        result.ByPriority.Count.ShouldBe(Enum.GetValues<Priority>().Length);
        result.ByPriority.Single(p => p.Priority == Priority.Low).Count.ShouldBe(0);
        result.ByPriority.Single(p => p.Priority == Priority.High).Count.ShouldBe(3);
        result.TotalOpen.ShouldBe(3);
    }

    [Fact]
    public async Task GetVelocityAsync_chua_co_sprint_dong_so_nao_tra_rong_va_AverageVelocity_bang_0()
    {
        _stats.TallyVelocityAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<SprintVelocityTally>)[]);

        var result = await _sut.GetVelocityAsync(_projectId);

        result.Sprints.ShouldBeEmpty();
        result.AverageVelocity.ShouldBe(0m);
    }

    [Fact]
    public async Task GetVelocityAsync_tinh_dung_trung_binh_DoneCount_qua_cac_sprint_da_dong()
    {
        var completedAt1 = DateTime.UtcNow.AddDays(-14);
        var completedAt2 = DateTime.UtcNow.AddDays(-1);

        _stats.TallyVelocityAsync(_projectId, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<SprintVelocityTally>)
        [
            new SprintVelocityTally(Guid.NewGuid(), "Sprint 1", completedAt1, Total: 10, Done: 8),
            new SprintVelocityTally(Guid.NewGuid(), "Sprint 2", completedAt2, Total: 6, Done: 4),
        ]);

        var result = await _sut.GetVelocityAsync(_projectId);

        result.Sprints.Count.ShouldBe(2);
        result.AverageVelocity.ShouldBe(6m);   // (8 + 4) / 2
        result.Sprints[0].DoneCount.ShouldBe(8);
        result.Sprints[0].TotalCount.ShouldBe(10);
    }

    [Fact]
    public async Task GetTimelineAsync_kiem_quyen_ViewStatistics_truoc_khi_hoi_repository()
    {
        _stats.TallyTimelineAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<SprintTimelineTally>)[]);

        await _sut.GetTimelineAsync(_projectId);

        await _authz.Received(1).AuthorizeAsync(
            _projectId, ProjectAction.ViewStatistics, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTimelineAsync_giu_nguyen_ca_sprint_Planned_chua_dong_so()
    {
        var start = DateTime.UtcNow.AddDays(7);
        var end = DateTime.UtcNow.AddDays(21);

        _stats.TallyTimelineAsync(_projectId, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<SprintTimelineTally>)
        [
            new SprintTimelineTally(
                Guid.NewGuid(), "Sprint tương lai", SprintStatus.Planned,
                start, end, CompletedAt: null, Total: 0, Done: 0),
        ]);

        var result = await _sut.GetTimelineAsync(_projectId);

        var point = result.Sprints.ShouldHaveSingleItem();
        point.Status.ShouldBe(SprintStatus.Planned);
        point.CompletedAt.ShouldBeNull();
        point.IsOverdue.ShouldBeFalse();
    }

    /// <summary>
    /// Bẫy thật (2026-08-06): "quá hạn" KHÔNG suy được từ so sánh <c>StartDate</c> — một
    /// sprint <c>Active</c> mà <c>StartDate</c> còn ở TƯƠNG LAI (bấm Start sớm hơn kế
    /// hoạch) không phải quá hạn, dù về hình thức nó cũng "chưa tới ngày bắt đầu" giống
    /// Planned. Chỉ <c>EndDate</c> đã qua mới tính.
    /// </summary>
    [Fact]
    public async Task GetTimelineAsync_Active_chay_som_hon_ke_hoach_KHONG_tinh_la_qua_han()
    {
        var start = DateTime.UtcNow.AddDays(5);   // StartDate còn ở tương lai
        var end = DateTime.UtcNow.AddDays(14);

        _stats.TallyTimelineAsync(_projectId, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<SprintTimelineTally>)
        [
            new SprintTimelineTally(
                Guid.NewGuid(), "Chạy sớm", SprintStatus.Active,
                start, end, CompletedAt: null, Total: 0, Done: 0),
        ]);

        var result = await _sut.GetTimelineAsync(_projectId);

        result.Sprints.ShouldHaveSingleItem().IsOverdue.ShouldBeFalse();
    }

    [Fact]
    public async Task GetTimelineAsync_Active_da_qua_EndDate_thi_IsOverdue_true()
    {
        var start = DateTime.UtcNow.AddDays(-10);
        var end = DateTime.UtcNow.AddDays(-1);   // đã qua

        _stats.TallyTimelineAsync(_projectId, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<SprintTimelineTally>)
        [
            new SprintTimelineTally(
                Guid.NewGuid(), "Quá hạn", SprintStatus.Active,
                start, end, CompletedAt: null, Total: 3, Done: 1),
        ]);

        var result = await _sut.GetTimelineAsync(_projectId);

        result.Sprints.ShouldHaveSingleItem().IsOverdue.ShouldBeTrue();
    }
}
