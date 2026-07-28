using PMS.Domain.Common;
using PMS.Domain.Entities;
using Shouldly;

namespace PMS.UnitTests.Domain;

public class SoftDeletableContractTests
{
    /// <summary>
    /// ARCHITECTURE.md §5 tuyên bố Project/Task/Sprint dùng soft delete. Toàn bộ cơ chế —
    /// Global Query Filter và ApplySoftDelete() — đều dựa vào phép kiểm tra
    /// IsAssignableFrom(ISoftDeletable). Thiếu interface thì cả hai âm thầm bỏ qua entity
    /// đó và nó bị XÓA CỨNG. Có cột IsDeleted trong DB là không đủ.
    /// </summary>
    [Theory]
    [InlineData(typeof(Project))]
    [InlineData(typeof(TaskItem))]
    [InlineData(typeof(Sprint))]
    public void Entity_duoc_khai_bao_soft_delete_phai_implement_ISoftDeletable(Type entityType)
        => typeof(ISoftDeletable).IsAssignableFrom(entityType).ShouldBeTrue();
}