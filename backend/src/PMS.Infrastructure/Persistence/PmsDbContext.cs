using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PMS.Domain.Common;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence;

public class PmsDbContext : DbContext
{
    public PmsDbContext(DbContextOptions<PmsDbContext> options) : base(options){}

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Sprint> Sprints => Set<Sprint>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<BoardColumn> BoardColumns => Set<BoardColumn>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();
    public DbSet<Watcher> Watchers => Set<Watcher>();
    public DbSet<TaskLink> TaskLinks => Set<TaskLink>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<ProjectTaskCounter> ProjectTaskCounters => Set<ProjectTaskCounter>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<ProjectInvitation> ProjectInvitations => Set<ProjectInvitation>();

    // Phân quyền tầng 1 (ADR-045). Hai bảng này được seed bằng HasData nên có mặt ngay sau
    // `Migrate()` — kể cả ở môi trường Testing, nơi DbSeeder không bao giờ chạy.
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PmsDbContext).Assembly);

        ApplySoftDeleteQueryFilter(modelBuilder);
        ApplyIdNeverGenerated(modelBuilder);
        ApplyUtcDateTimeKind(modelBuilder);
    }

    /// <summary>
    /// Đóng dấu lại <see cref="DateTimeKind.Utc"/> cho MỌI cột <c>DateTime</c> lúc ĐỌC ra.
    ///
    /// <para>
    /// 🔴 Vì sao cần: kiểu <c>datetime2</c> của SQL Server <b>không lưu Kind</b>. Ghi một
    /// <c>DateTime</c> có <c>Kind = Utc</c> xuống rồi đọc lên thì nhận lại
    /// <c>Kind = Unspecified</c>, và <c>System.Text.Json</c> serialize giá trị Unspecified
    /// <b>không kèm hậu tố</b> — ra <c>"2026-08-04T14:15:06"</c> thay vì
    /// <c>"2026-08-04T14:15:06Z"</c>.
    /// </para>
    /// <para>
    /// Hệ quả ở trình duyệt: <c>new Date("...T14:15:06")</c> hiểu là giờ ĐỊA PHƯƠNG, nên
    /// <b>mọi mốc thời gian trong ứng dụng lệch đúng bằng chênh múi giờ</b>. Ở Việt Nam
    /// (UTC+7), một thao tác vừa xảy ra hiện thành "7 giờ trước". Lỗi này chạm tới bình luận,
    /// thông báo, nhật ký hoạt động, hạn hoàn thành — tức gần như mọi màn hình — mà build vẫn
    /// sạch, test vẫn xanh, vì không test nào so chuỗi JSON thô của một mốc thời gian.
    /// </para>
    /// <para>
    /// 📌 Chỉ chuyển đổi chiều ĐỌC; chiều ghi giữ nguyên giá trị. Toàn bộ hệ thống đã dùng
    /// <c>DateTime.UtcNow</c> nên dữ liệu trong cột vốn là UTC — ở đây chỉ khôi phục lại
    /// thông tin Kind mà tầng lưu trữ đánh rơi, không dịch chuyển thời điểm nào cả.
    /// </para>
    /// <para>
    /// Cách đã loại: cấu hình <c>JsonSerializerOptions</c> ở tầng API. Nó chỉ vá được đường
    /// đi ra HTTP, trong khi cùng một giá trị Unspecified còn chảy vào so sánh nghiệp vụ
    /// (<c>IsOverdue</c>, <c>DueDate &lt; UtcNow</c>) và vào <c>DueDateNotifier</c> — sửa ở
    /// tầng đọc là sửa một lần cho mọi người tiêu thụ.
    /// </para>
    /// </summary>
    private static void ApplyUtcDateTimeKind(ModelBuilder modelBuilder)
    {
        var toUtc = new ValueConverter<DateTime, DateTime>(
            write => write,
            read => DateTime.SpecifyKind(read, DateTimeKind.Utc));

        var toUtcNullable = new ValueConverter<DateTime?, DateTime?>(
            write => write,
            read => read.HasValue ? DateTime.SpecifyKind(read.Value, DateTimeKind.Utc) : read);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                    property.SetValueConverter(toUtc);
                else if (property.ClrType == typeof(DateTime?))
                    property.SetValueConverter(toUtcNullable);
            }
        }
    }

    private static void ApplyIdNeverGenerated(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            modelBuilder.Entity(entityType.ClrType)
                        .Property(nameof(BaseEntity.Id))
                        .ValueGeneratedNever();
        }
    }

    private static void ApplySoftDeleteQueryFilter(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var body = Expression.Not(Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted)));

            modelBuilder.Entity(entityType.ClrType)
                        .HasQueryFilter(Expression.Lambda(body, parameter));
        }
    }

    private void ApplySoftDelete()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State != EntityState.Deleted) continue;

            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAt = now;
        }
    }

    private void ApplyAuditFields()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.CreatedAt == default) entry.Entity.CreatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        // Phải gọi đủ cả hai và đúng thứ tự như bản async, nếu không bản ghi lưu qua
        // đường đồng bộ sẽ thiếu CreatedAt/UpdatedAt (ADR-014).
        ApplySoftDelete();
        ApplyAuditFields();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplySoftDelete();
        ApplyAuditFields();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}