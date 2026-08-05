using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Sprints",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Sprints",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Sprints_ProjectId_Status",
                table: "Sprints",
                columns: new[] { "ProjectId", "Status" });

            // ═══════════════════════════════════════════════════════════════════════════
            // 📌 CỐ Ý KHÔNG BACKFILL — mọi sprint đang có ở lại `Planned` (0).
            //
            // Nghe có vẻ sai với một sprint mà hôm nay nằm trong khoảng ngày của nó: nhìn
            // vào thì rõ ràng "đang chạy". Nhưng `Status` trả lời câu hỏi *đội đã bấm bắt
            // đầu chưa*, và câu trả lời thật là **chưa** — tính năng này vừa mới có.
            //
            // Hai phương án backfill đều tệ hơn:
            //
            //  • Đặt `Active` cho sprint có ngày phủ hôm nay: dữ liệu cũ không bảo đảm mỗi
            //    project chỉ có một sprint như vậy, nên nó phá ngay bất biến "tối đa MỘT
            //    sprint đang chạy" mà `StartAsync` phải giữ — và phá bằng dữ liệu, thứ mà
            //    không lệnh kiểm nào ở tầng code bắt được.
            //
            //  • Đặt `Completed` cho sprint đã qua ngày kết thúc: phải bịa một `CompletedAt`
            //    chưa từng xảy ra. Mà `CompletedAt` chính là mốc velocity đo theo — bịa nó
            //    là làm hỏng đúng con số mà tính năng này sinh ra để tạo.
            //
            // Hệ quả chấp nhận được: người dùng bấm "Bắt đầu" một lần cho sprint hiện tại.
            // Một thao tác thật, thay cho một lịch sử giả.
            // ═══════════════════════════════════════════════════════════════════════════
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sprints_ProjectId_Status",
                table: "Sprints");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Sprints");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Sprints");
        }
    }
}
