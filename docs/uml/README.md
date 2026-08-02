# UML — nguồn và cách sinh lại diagram

## Nguồn thật nằm ở đâu

| Thư mục | Nội dung |
|---|---|
| `seq-diagram/src/*.mmd` | Nguồn **Mermaid** của các sequence diagram |
| `src/*.puml` | Nguồn **PlantUML** của use case diagram |
| `seq-diagram/*.drawio`, `class-diagram.drawio`, `use-case-diagram.drawio`, `erd.drawio` | File draw.io — **sinh ra** từ nguồn ở trên (trừ 3 file ghi chú bên dưới) |
| `diagram_png/*.png` | Ảnh xuất ra để chèn vào báo cáo — luôn sinh lại từ `.drawio` |

Trước 2026-07-30, nguồn Mermaid/PlantUML chỉ nằm trong thuộc tính `mermaidData` /
`plantUmlData` nhúng bên trong XML của draw.io — bị escape hai lớp nên diff trên GitHub
là một dòng dài vô nghĩa, không ai review được thay đổi thật sự là gì. Nay tách ra thành
file text riêng.

## Sinh lại

Cần draw.io Desktop (đã có sẵn `-x/--export`, nhận cả input `.mmd`).

**Windows** — đường dẫn khác, cài bằng `winget install JGraph.Draw`:

```powershell
$DRAWIO = "$env:ProgramFiles\draw.io\draw.io.exe"
cd docs/uml

& $DRAWIO -x -f xml --no-sandbox `
  -o seq-diagram/seq-12-refresh-token.drawio `
     seq-diagram/src/seq-12-refresh-token.mmd

& $DRAWIO -x -f png -s 2 --no-sandbox `
  -o diagram_png/seq-12-refresh-token.png `
     seq-diagram/seq-12-refresh-token.drawio
```

**macOS:**

```bash
DRAWIO=/Applications/draw.io.app/Contents/MacOS/draw.io
cd docs/uml

# Mermaid -> .drawio (diagram còn sửa tay được, không phải ảnh tĩnh)
"$DRAWIO" -x -f xml --no-sandbox \
  -o seq-diagram/seq-03-change-status.drawio \
     seq-diagram/src/seq-03-change-status.mmd

# .drawio -> .png (scale 2x cho nét khi in báo cáo)
"$DRAWIO" -x -f png -s 2 --no-sandbox \
  -o diagram_png/seq-03-change-status.png \
     seq-diagram/seq-03-change-status.drawio
```

Đổi tên file cho các diagram khác. **Quy ước tên:** `seq-NN-<slug>` dùng chung cho cả
`.mmd`, `.drawio` và `.png`.

## Danh sách sequence diagram

| # | File | Luồng | Điểm nhấn nghiệp vụ |
|---|---|---|---|
| 01 | `seq-01-create-task` | PM tạo task | 403 nếu không phải PM; 1 lần `SaveChanges` |
| 02 | `seq-02-assign-task` | PM gán người vào task | 404 task; 403 nếu target không phải ProjectMember |
| 03 | `seq-03-change-status` | Đổi trạng thái task | ADR-017 (Assignee HOẶC PM), ADR-019 (404), blocker → 409, nhảy bước → 409 |
| 04 | `seq-04-invite-member` | PM mời thành viên | Email chưa có tài khoản → không tự tạo hộ |
| 05 | `seq-05-respond-invitation` | Chấp nhận / từ chối lời mời | `Pending → Accepted/Declined`, phản hồi lại → 409 |
| 06 | `seq-06-self-assign-task` | Member tự nhận task | Chỉ khi task đang `ToDo` → 409; Viewer → 403; báo cho PM |
| 07 | `seq-07-delete-task` | PM xóa task | ADR-018: còn subtask chưa `Done` → 409; cascade tường minh xuống subtask đã `Done` |
| 08 | `seq-08-move-task-sprint` | Kéo task Backlog ↔ Sprint | Sprint khác project → 400 |
| 09 | `seq-09-delete-sprint` | PM xóa sprint | ADR-020: task về Backlog, **không** bị xóa |
| 10 | `seq-10-read-notification` | Đọc / đánh dấu đã đọc thông báo | ADR-023: ngoại lệ hợp lệ của ADR-006/019, thông báo người khác → 404; đánh dấu lần hai vẫn 200 (idempotent); ADR-024: mark-all đi qua ChangeTracker, 1 `SaveChanges` |
| 11 | `seq-11-delete-comment` | Xóa comment trên task | ADR-026: ngoài project → 404, không phải tác giả và không phải PM → 403, xóa **cứng** + `ActivityLog` |
| 12 | `seq-12-refresh-token` | Cơ chế refresh token phía client | ADR-027/030: single-flight, hàng đợi request, nhánh reuse detection → `RevokeAllAsync` → đăng xuất sạch; refresh chủ động trước 60s. ⚠️ **mới chỉ có `.mmd`**, xem "Nợ còn lại" |

## Bẫy khi viết `.mmd`

**Không dùng `&lt;` / `&gt;` cho generic.** draw.io escape thêm một lớp nữa khi nhúng vào
XML (`&amp;lt;`), nên PNG hiện ra nguyên văn `PagedResult&lt;Notification&gt;` thay vì
`PagedResult<Notification>`. Dùng ngoặc vuông: `PagedResult[Notification]`. Phát hiện
2026-07-30 khi vẽ `seq-10` — các diagram trước đó tránh được vì không có generic nào.

## Nợ còn lại

- 🔴 **`seq-12-refresh-token` mới chỉ có `.mmd`, chưa có `.drawio` và `.png`.** Máy làm
  phiên 2026-07-31 là Windows và **không cài draw.io Desktop** (README trước đó chỉ ghi
  đường dẫn macOS — các phiên trước làm trên máy khác). Nguồn Mermaid đã viết xong và là
  thứ review được; chỉ cần chạy hai lệnh ở mục "Sinh lại" trên máy có draw.io là ra đủ.
  **Nhớ mở PNG ra xem thật** thay vì tin CLI báo thành công.
- `seq-04` và `seq-05` **không có** thuộc tính `mermaidData` nhúng, nên không trích ngược
  ra `.mmd` được. Hai diagram này vẫn đúng với code hiện tại nên chưa cần vẽ lại; khi nào
  phải sửa chúng thì viết `.mmd` mới rồi sinh lại như các file khác.
- `erd.drawio` chưa phản ánh `RowVersion` (ADR-016) và `Notifications.Type` đã đổi sang
  `nvarchar` (ADR-016). Không chặn code, chỉ lệch hình ảnh.
