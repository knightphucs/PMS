using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Authorization;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Admin;

/// <summary>
/// Bảo vệ chống trôi giữa danh mục trong CODE (<see cref="SystemPermissions"/>) và danh mục
/// trong DB (seed bằng <c>HasData</c>) — ADR-045.
/// <para>
/// Chạy trên database chỉ được dựng bằng <c>Migrate()</c>, tức đúng môi trường quan trọng:
/// <c>DbSeeder</c> KHÔNG chạy ở đây (nó nằm trong nhánh <c>IsDevelopment()</c> và còn
/// early-return khi DB đã có Employee). Nếu ai đó chuyển seed permission sang <c>DbSeeder</c>
/// thì cả file này đỏ ngay — cùng với hàng chục test khác, vì <c>projects:create</c> biến mất.
/// </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class PermissionSeedTests : IntegrationTestBase
{
    public PermissionSeedTests(PmsWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Bang_Permission_khop_chinh_xac_voi_danh_muc_trong_code()
    {
        var inDb = await WithDbAsync(db => db.Permissions
            .AsNoTracking().Select(p => p.Code).ToListAsync());

        // So hai chiều: thiếu hàng thì policy 403 nhầm, thừa hàng thì có quyền cấp được mà
        // không endpoint nào dùng — cả hai đều là trôi lệch cần thấy ngay.
        inDb.ShouldBe(SystemPermissions.All, ignoreOrder: true);
    }

    [Fact]
    public async Task Moi_quyen_deu_co_mo_ta_khong_rong()
    {
        // Mô tả là nhãn hiện cạnh ô tích ở /admin/roles. Rỗng thì màn quản trị thành một ma
        // trận toàn mã kỹ thuật, và người quản trị không biết mình đang cấp cái gì.
        var descriptions = await WithDbAsync(db => db.Permissions
            .AsNoTracking().Select(p => p.Description).ToListAsync());

        descriptions.Count.ShouldBe(SystemPermissions.All.Count);
        descriptions.ShouldAllBe(d => !string.IsNullOrWhiteSpace(d));
    }

    [Fact]
    public async Task SystemAdmin_duoc_cap_du_ca_nam_quyen()
    {
        var codes = await GrantsForAsync(SystemRole.SystemAdmin);
        codes.ShouldBe(SystemPermissions.All, ignoreOrder: true);
    }

    [Fact]
    public async Task User_thuong_chi_co_projects_create()
    {
        // 🔴 Nếu dòng seed này biến mất thì gần như TOÀN BỘ suite tích hợp đỏ, vì hầu hết test
        // class đều gọi IntegrationTestBase.CreateProjectAsync bằng một user thường. Đó là
        // chủ ý: nó biến một hồi quy phân quyền thành thứ không thể bỏ qua.
        var codes = await GrantsForAsync(SystemRole.User);
        codes.ShouldBe([SystemPermissions.ProjectsCreate]);
    }

    [Fact]
    public async Task Khong_vai_tro_nao_duoc_cap_ma_ngoai_danh_muc()
    {
        var granted = await WithDbAsync(db => db.RolePermissions
            .AsNoTracking().Select(rp => rp.PermissionCode).Distinct().ToListAsync());

        granted.ShouldAllBe(c => SystemPermissions.All.Contains(c));
    }

    private Task<List<string>> GrantsForAsync(SystemRole role)
        => WithDbAsync(db => db.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.SystemRole == role)
            .Select(rp => rp.PermissionCode)
            .ToListAsync());
}
