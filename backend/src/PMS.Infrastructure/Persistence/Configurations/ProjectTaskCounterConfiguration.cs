using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Configurations;

public class ProjectTaskCounterConfiguration : IEntityTypeConfiguration<ProjectTaskCounter>
{
    public void Configure(EntityTypeBuilder<ProjectTaskCounter> builder)
    {
        builder.ToTable("ProjectTaskCounters");

        // Khóa chính LÀ ProjectId — quan hệ 1-1 với Project, không có cột Id riêng.
        // Tiền lệ: WatcherConfiguration cũng khai khóa thẳng trên FK.
        builder.HasKey(c => c.ProjectId);

        builder.Property(c => c.NextNumber).IsRequired().HasDefaultValue(0);

        builder.HasOne(c => c.Project)
               .WithOne()
               .HasForeignKey<ProjectTaskCounter>(c => c.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);

        // Query filter khớp với filter của Project. Không có nó, EF cảnh báo
        // PossibleIncorrectRequiredNavigationWithQueryFilterInteraction ở mỗi lần dựng model
        // (Project có filter, đầu bắt buộc của quan hệ thì không).
        //
        // Thêm filter KHÔNG làm mất dữ liệu bộ đếm khi project bị xóa mềm: filter chỉ ảnh
        // hưởng đường đọc bằng LINQ, mà đường đọc duy nhất của bảng này —
        // ProjectTaskCounterRepository.NextNumberAsync — là SQL thô nên không đi qua filter.
        // Hàng vẫn nằm nguyên trong DB, nên khôi phục project sẽ đánh số tiếp chứ không
        // quay về 1 và tạo ra hai task cùng mã.
        builder.HasQueryFilter(c => !c.Project.IsDeleted);
    }
}
