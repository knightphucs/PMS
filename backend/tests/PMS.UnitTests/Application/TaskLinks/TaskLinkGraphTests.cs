using PMS.Application.Common.Interfaces;
using PMS.Application.Features.TaskLinks;
using PMS.Domain.Enums;
using Shouldly;
using Xunit;

namespace PMS.UnitTests.Application.TaskLinks;

/// <summary>
/// Hai luật thuần hàm của ADR-038. Test ở đây chứ không qua service vì chúng không cần DB —
/// và vì đây là chỗ dễ sai nhất: cả hai đều là loại lỗi KHÔNG lộ ra khi thử tay một lần.
/// </summary>
public class TaskLinkGraphTests
{
    private static readonly Guid A = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid B = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid C = new("33333333-3333-3333-3333-333333333333");

    // ---------- Chuẩn hóa: điều làm cho unique index thực sự kín ----------

    [Fact]
    public void IsBlockedBy_bi_quy_ve_Blocks_dao_chieu()
    {
        // Blocks(A,B) và IsBlockedBy(B,A) là CÙNG một sự thật. Không chuẩn hóa thì unique
        // index (Source, Target, LinkType) lưu cả hai và task detail hiện trùng hai lần.
        var fromBlocks = TaskLinkGraph.Canonicalize(A, B, LinkType.Blocks);
        var fromIsBlockedBy = TaskLinkGraph.Canonicalize(B, A, LinkType.IsBlockedBy);

        fromIsBlockedBy.ShouldBe(fromBlocks);
        fromBlocks.Type.ShouldBe(LinkType.Blocks);   // IsBlockedBy KHÔNG BAO GIỜ được lưu
    }

    [Theory]
    [InlineData(LinkType.RelatesTo)]
    [InlineData(LinkType.Duplicates)]
    public void Loai_doi_xung_sap_cap_theo_thu_tu_on_dinh(LinkType type)
    {
        TaskLinkGraph.Canonicalize(A, B, type)
            .ShouldBe(TaskLinkGraph.Canonicalize(B, A, type));
    }

    // ---------- Diễn giải hướng theo người xem ----------

    [Fact]
    public void Hang_Blocks_doc_nguoc_lai_thanh_IsBlockedBy_khi_xem_tu_dau_kia()
    {
        // Cùng một hàng Blocks(A,B): A thấy "tôi chặn B", B thấy "tôi bị A chặn".
        var fromA = TaskLinkGraph.ViewFrom(A, A, B, LinkType.Blocks);
        fromA.Displayed.ShouldBe(LinkType.Blocks);
        fromA.RelatedTaskId.ShouldBe(B);

        var fromB = TaskLinkGraph.ViewFrom(B, A, B, LinkType.Blocks);
        fromB.Displayed.ShouldBe(LinkType.IsBlockedBy);
        fromB.RelatedTaskId.ShouldBe(A);
    }

    [Fact]
    public void Loai_doi_xung_hien_nguyen_trang_o_ca_hai_phia()
    {
        TaskLinkGraph.ViewFrom(A, A, B, LinkType.RelatesTo).Displayed.ShouldBe(LinkType.RelatesTo);
        TaskLinkGraph.ViewFrom(B, A, B, LinkType.RelatesTo).Displayed.ShouldBe(LinkType.RelatesTo);
    }

    // ---------- Dò chu trình ----------

    [Fact]
    public void Phat_hien_duoc_chu_trinh_GIAN_TIEP_qua_nhieu_buoc()
    {
        // A -> B -> C. Thêm C -> A sẽ khóa chết cả ba: guard phải thấy đường đi A..->C.
        List<BlockingEdge> edges = [new(A, B), new(B, C)];

        TaskLinkGraph.HasPath(edges, A, C).ShouldBeTrue();    // thêm C->A là tạo vòng
        TaskLinkGraph.HasPath(edges, C, A).ShouldBeFalse();   // thêm A->C thì không
    }

    [Fact]
    public void Do_thi_co_san_vong_khong_lam_treo_thuat_toan()
    {
        // Race giữa hai insert đồng thời vẫn có thể tạo ra vòng trong DB. Khi đó guard
        // phải TRẢ VỀ được, không phải lặp vô hạn — đây là lý do dùng HashSet visited.
        List<BlockingEdge> edges = [new(A, B), new(B, A)];

        TaskLinkGraph.HasPath(edges, A, C).ShouldBeFalse();
    }

    [Fact]
    public void Do_thi_rong_thi_khong_co_duong_di_nao()
        => TaskLinkGraph.HasPath([], A, B).ShouldBeFalse();
}
