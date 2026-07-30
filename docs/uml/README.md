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

Cần draw.io Desktop (đã có sẵn `-x/--export`, nhận cả input `.mmd`):

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

## Nợ còn lại

- `seq-04` và `seq-05` **không có** thuộc tính `mermaidData` nhúng, nên không trích ngược
  ra `.mmd` được. Hai diagram này vẫn đúng với code hiện tại nên chưa cần vẽ lại; khi nào
  phải sửa chúng thì viết `.mmd` mới rồi sinh lại như các file khác.
- `erd.drawio` chưa phản ánh `RowVersion` (ADR-016) và `Notifications.Type` đã đổi sang
  `nvarchar` (ADR-016). Không chặn code, chỉ lệch hình ảnh.
