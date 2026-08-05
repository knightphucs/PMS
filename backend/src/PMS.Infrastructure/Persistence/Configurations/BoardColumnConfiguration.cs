using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Configurations;

public class BoardColumnConfiguration : IEntityTypeConfiguration<BoardColumn>
{
    public void Configure(EntityTypeBuilder<BoardColumn> builder)
    {
        builder.ToTable("BoardColumns");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).IsRequired().HasMaxLength(50);
        builder.Property(c => c.Color).IsRequired().HasMaxLength(7);
        builder.Property(c => c.Order).IsRequired();
        builder.Property(c => c.Category).IsRequired();

        // Đọc board là "lấy mọi cột của project này theo thứ tự" — index ghép phục vụ đúng
        // hình dạng đó. KHÔNG unique trên (ProjectId, Order): đổi thứ tự cột là ghi lại cả
        // dải, và bước trung gian của thao tác đó chắc chắn có hai cột trùng Order.
        builder.HasIndex(c => new { c.ProjectId, c.Order });

        // Tên cột không trùng trong cùng một project. Hai cột cùng tên thì ô chọn "đẩy task
        // sang cột nào" trở thành một câu hỏi không trả lời được.
        builder.HasIndex(c => new { c.ProjectId, c.Name }).IsUnique();

        builder.HasOne(c => c.Project)
               .WithMany(p => p.BoardColumns)
               .HasForeignKey(c => c.ProjectId)
               // 🔴 `ClientNoAction` — đã thử SAI hai lần trước khi tới đây, ghi lại cả hai
               // để không ai đi lại.
               //
               // Bối cảnh: project trong hệ thống này **chỉ xóa mềm**. `ApplySoftDelete` đổi
               // Deleted->Modified, nhưng CHỈ cho entity cài `ISoftDeletable`. `BoardColumn`
               // không cài (nó không có vòng đời xóa mềm riêng — người dùng xóa cột là xóa
               // thật, và có luồng chọn cột đích cho task).
               //
               //  • `Cascade` (thử đầu): EF đánh dấu luôn các cột là Deleted → DELETE thật →
               //    FK `Restrict` từ `Tasks` chặn ở tầng DB → **500**.
               //  • `Restrict` (thử hai): EF không xóa được mà cũng không set null được FK
               //    bắt buộc, nên ném *"The association between entity types 'Project' and
               //    'BoardColumn' has been severed"* → vẫn **500**.
               //
               // `ClientNoAction` nói đúng ý định: **EF đừng làm gì cả với cột khi project
               // bị đánh dấu xóa.** Xóa mềm chỉ là một lệnh UPDATE trên hàng Projects, cột
               // không liên quan và phải nằm yên. Ở tầng DB, FK vẫn tồn tại và vẫn chặn một
               // lần xóa cứng bằng SQL tay — đó là điều mong muốn.
               //
               // ⚠️ Cũng KHÔNG giải bằng cách cho `BoardColumn` cài `ISoftDeletable`: khi đó
               // `Remove()` ở luồng xóa cột của người dùng biến thành xóa mềm, và unique
               // index `(ProjectId, Name)` sẽ chặn việc tạo lại một cột trùng tên với cột đã
               // xóa. Đổi một lỗi lấy một lỗi khác khó thấy hơn.
               //
               // Chỉ integration test `Cascade_xuong_task_va_sprint...` bắt được lớp lỗi này
               // — xóa project là đường ít ai đi lúc phát triển, và unit test thì không có
               // DB thật để FK lên tiếng.
               .OnDelete(DeleteBehavior.ClientNoAction);

        // 🔴 Phải soi gương query filter của `Project`, dù `BoardColumn` KHÔNG phải
        // `ISoftDeletable`.
        //
        // `ApplySoftDeleteQueryFilter` trong PmsDbContext chỉ gắn filter cho entity có cài
        // `ISoftDeletable`, nên cột không được lọc — trong khi `Project` (đầu BẮT BUỘC của
        // quan hệ) thì có. EF cảnh báo đúng: một project đã xóa mềm vẫn để lại cột "sống",
        // và mọi truy vấn nào đi từ cột ngược về project sẽ thấy một navigation bắt buộc
        // trỏ tới thứ đã bị lọc mất.
        //
        // Không giải bằng cách cho `BoardColumn` cài `ISoftDeletable`: cột không có vòng đời
        // xóa mềm riêng, nó sống chết theo project. Điều kiện dưới đây nói đúng điều đó.
        builder.HasQueryFilter(c => !c.Project.IsDeleted);
    }
}
