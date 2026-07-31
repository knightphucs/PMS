using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Interfaces;
using PMS.Application.Common.Models;
using PMS.Application.Features.Notifications;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using Shouldly;
using Xunit;

namespace PMS.UnitTests.Application.Notifications;

public class NotificationFeedServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly INotificationRepository _repo = Substitute.For<INotificationRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly NotificationFeedService _sut;

    public NotificationFeedServiceTests()
    {
        _uow.Notifications.Returns(_repo);
        _currentUser.EmployeeId.Returns(_userId);

        _sut = new NotificationFeedService(
            _uow, _currentUser,
            new NotificationMapper(),
            NullLogger<NotificationFeedService>.Instance);
    }

    // ---------- ADR-023: chỉ đọc thông báo của chính mình ----------

    [Fact]
    public async Task GetMineAsync_luon_loc_theo_EmployeeId_lay_tu_ICurrentUserService()
    {
        _repo.GetPagedForRecipientAsync(
                _userId, null, Arg.Any<PagedRequest>(), Arg.Any<CancellationToken>())
             .Returns(Empty());

        await _sut.GetMineAsync(null, new PagedRequest());

        // Không có overload nào nhận id người nhận từ ngoài vào, nên chỉ cần khẳng định
        // service truyền đúng id của người đang đăng nhập.
        await _repo.Received(1).GetPagedForRecipientAsync(
            _userId, null, Arg.Any<PagedRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Danh_dau_da_doc_thong_bao_cua_nguoi_khac_thi_404()
    {
        // Repository lọc theo người nhận ngay trong query nên trả null — service không
        // phân biệt được "không tồn tại" với "của người khác", đúng chủ ý (OWASP API1:2023).
        var id = Guid.NewGuid();
        _repo.GetForRecipientAsync(id, _userId, Arg.Any<CancellationToken>())
             .Returns((Notification?)null);

        var ex = await Should.ThrowAsync<NotFoundException>(() => _sut.MarkAsReadAsync(id));

        ex.StatusCode.ShouldBe(404);
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Danh_dau_da_doc_lan_dau_thi_luu_mot_lan()
    {
        var notification = New();
        _repo.GetForRecipientAsync(notification.Id, _userId, Arg.Any<CancellationToken>())
             .Returns(notification);

        var result = await _sut.MarkAsReadAsync(notification.Id);

        result.IsRead.ShouldBeTrue();
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Danh_dau_da_doc_lan_hai_van_tra_ve_binh_thuong_va_khong_luu_lai()
    {
        // Idempotent (ADR-023): không 409, và không phát sinh UPDATE vô nghĩa.
        var notification = New();
        notification.MarkAsRead();
        _repo.GetForRecipientAsync(notification.Id, _userId, Arg.Any<CancellationToken>())
             .Returns(notification);

        var result = await _sut.MarkAsReadAsync(notification.Id);

        result.IsRead.ShouldBeTrue();
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---------- ADR-007 + ADR-024 ----------

    [Fact]
    public async Task MarkAllAsRead_sua_nhieu_ban_ghi_nhung_chi_mot_lan_SaveChangesAsync()
    {
        var unread = new[] { New(), New(), New() };
        _repo.GetUnreadForRecipientAsync(_userId, Arg.Any<CancellationToken>()).Returns(unread);

        var result = await _sut.MarkAllAsReadAsync();

        result.MarkedCount.ShouldBe(3);
        unread.ShouldAllBe(n => n.IsRead);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkAllAsRead_nap_qua_repository_co_tracking_chu_khong_bulk_update()
    {
        // ADR-024: nếu ai đó đổi sang ExecuteUpdateAsync thì service không còn gọi
        // GetUnreadForRecipientAsync nữa và test này đỏ — đó là chốt chặn duy nhất cho
        // quyết định "không bulk update", vì bulk update vẫn cho ra kết quả đúng về
        // IsRead và chỉ âm thầm làm mất UpdatedAt.
        _repo.GetUnreadForRecipientAsync(_userId, Arg.Any<CancellationToken>())
             .Returns([New()]);

        await _sut.MarkAllAsReadAsync();

        await _repo.Received(1).GetUnreadForRecipientAsync(_userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkAllAsRead_khong_co_gi_chua_doc_thi_khong_goi_SaveChanges()
    {
        _repo.GetUnreadForRecipientAsync(_userId, Arg.Any<CancellationToken>()).Returns([]);

        (await _sut.MarkAllAsReadAsync()).MarkedCount.ShouldBe(0);

        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Dem_chua_doc_loc_theo_dung_nguoi_dang_dang_nhap()
    {
        _repo.CountUnreadAsync(_userId, Arg.Any<CancellationToken>()).Returns(7);

        (await _sut.GetUnreadCountAsync()).UnreadCount.ShouldBe(7);
    }

    [Fact]
    public async Task Chua_dang_nhap_thi_401_chu_khong_tra_ve_thong_bao_cua_ai_ca()
    {
        _currentUser.EmployeeId.Returns((Guid?)null);

        await Should.ThrowAsync<UnauthorizedException>(
            () => _sut.GetMineAsync(null, new PagedRequest()));
        await Should.ThrowAsync<UnauthorizedException>(() => _sut.GetUnreadCountAsync());
        await Should.ThrowAsync<UnauthorizedException>(() => _sut.MarkAllAsReadAsync());
    }

    // ---------- ADR-025 ----------

    [Fact]
    public async Task Response_mang_theo_RelatedEntityKind_de_frontend_dieu_huong()
    {
        var notification = New();
        notification.Type = NotificationType.TaskAssigned;
        _repo.GetForRecipientAsync(notification.Id, _userId, Arg.Any<CancellationToken>())
             .Returns(notification);

        var result = await _sut.MarkAsReadAsync(notification.Id);

        result.RelatedEntityKind.ShouldBe(RelatedEntityKind.Task);
        result.RelatedEntityId.ShouldBe(notification.RelatedEntityId);
    }

    // ---------- helpers ----------

    private Notification New() => new()
    {
        Id = Guid.NewGuid(),
        EmployeeId = _userId,
        Type = NotificationType.TaskAssigned,
        Content = "Bạn được gán vào task 'Dựng API'",
        RelatedEntityId = Guid.NewGuid()
    };

    private static PagedResult<Notification> Empty() => new()
    {
        Items = [], TotalCount = 0, Page = 1, PageSize = 20
    };
}
