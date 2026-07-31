# Chuẩn bị cho phiên Frontend kế tiếp

> Soạn ngày 2026-07-31, cuối phiên "Frontend — nền tảng".
> Đọc cùng `ARCHITECTURE.md` §6 và ADR-027 → ADR-032.

Mục tiêu phiên sau: **từ một màn hình lên một sản phẩm trông chuyên nghiệp**. Tài liệu này
gom ba thứ: những gì còn thiếu, những quyết định phải chốt trước khi gõ dòng code nào, và
kế hoạch cụ thể để giao diện đạt mức Jira/Trello thay vì mức "template shadcn".

---

## 1. Trạng thái hiện tại — nói thẳng

**Đã có:** đăng ký/đăng nhập/giữ phiên/đăng xuất, route guard, danh sách Project + CRUD
đầy đủ (kể cả hai luồng 409), khung ứng dụng hai cột.

**Thực tế nhìn vào:** sau đăng nhập chỉ có **một màn hình**. Toàn bộ Task, Sprint, Board,
Backlog, Comment, Notification, Dashboard — đều chưa có màn nào, dù **API của phần lớn
trong số đó đã sẵn sàng từ lâu**.

Giá trị phiên vừa rồi nằm ở tầng không nhìn thấy (cookie auth, API client, single-flight
refresh). Đó là đầu tư đúng, nhưng nó có nghĩa là **phiên sau phải trả bằng màn hình**.

---

## 2. Quyết định cần chốt TRƯỚC khi code

Bảy câu. Bốn câu đầu đắt nếu đổi sau; ba câu cuối rẻ hơn nhưng vẫn nên quyết sớm.

### 2.1 🔴 Thêm mã task dạng `PMS-12` không? (cần sửa backend + migration)

Hiện `TaskItem` **chỉ có `Guid`**. Không có mã ngắn đọc được.

Đây là **thứ đơn lẻ có ảnh hưởng thị giác lớn nhất** tới cảm giác "giống Jira". Mã task
xuất hiện ở mọi chỗ: thẻ Kanban, tiêu đề trang, breadcrumb, comment ("liên quan PMS-12"),
URL. Không có nó thì mọi tham chiếu tới task đều phải dùng tên dài hoặc UUID.

**Chi phí thật:** thêm cột `Number int` trên `TaskItem` + `TaskCounter` trên `Project`
(hoặc `MAX(Number)+1` trong transaction), một migration, sửa `TaskService.CreateAsync`,
thêm vào 2 DTO. ⚠️ Cần khóa chống trùng số khi hai người tạo task cùng lúc — đây là chỗ
`IUnitOfWork.ExecuteInTransactionAsync` (lâu nay chưa có caller nào) cuối cùng có việc thật.

**Khuyến nghị: CÓ.** Làm sớm khi bảng `Tasks` còn ít dữ liệu; để sau thì phải backfill.

### 2.2 🔴 Thêm `Description` cho task không? (cần sửa backend + migration)

`TaskItem` **không có trường mô tả**. `CreateTaskRequest` cũng không.

Màn chi tiết task mà không có mô tả thì chỉ còn tên + vài badge — không dùng được thật.
Project đã có `Description`, Task không có là thiếu nhất quán.

**Khuyến nghị: CÓ.** Gom chung một migration với 2.1.

### 2.3 Thư viện kéo–thả cho Kanban

| | `@dnd-kit` | `@hello-pangea/dnd` |
|---|---|---|
| Bảo trì | tích cực | bản fork của `react-beautiful-dnd` (đã ngừng) |
| React 19 | hỗ trợ | cần kiểm tra |
| Bàn phím / a11y | tốt sẵn | tốt sẵn |
| Code cho Kanban | nhiều hơn | ít hơn, API hợp board |

**Khuyến nghị: `@dnd-kit`.** Đồ án còn dài, chọn thư viện đang được bảo trì và chắc chắn
chạy với React 19 quan trọng hơn tiết kiệm vài chục dòng.

### 2.4 Dark mode — làm bây giờ hay để §14 Nhóm C?

`globals.css` do shadcn sinh **đã có sẵn đầy đủ biến CSS cho dark**. Chi phí thực tế chỉ
là: một `ThemeProvider`, một nút chuyển, và rà lại vài chỗ tôi lỡ hardcode màu
(`bg-amber-50`, `text-slate-700`… trong `status-badge.tsx` và cảnh báo 409).

**Khuyến nghị: LÀM NGAY.** Càng nhiều màn hình thì rà lại càng đắt, mà đây là tín hiệu
"chuyên nghiệp" rõ nhất với chi phí thấp nhất. §14 xếp nó vào Nhóm C khi chưa biết
shadcn cho sẵn hạ tầng.

### 2.5 Màu thương hiệu

Hiện đang dùng nguyên bảng màu trung tính mặc định của shadcn — đó là lý do lớn khiến giao
diện trông như template chưa hoàn thiện. Cần **một** màu nhấn dùng nhất quán cho nút chính,
trạng thái active, và link.

**Khuyến nghị:** chọn một màu (xanh dương đậm kiểu Jira, hoặc tím kiểu Linear) rồi đặt vào
`--primary` trong `globals.css`. Một dòng, đổi toàn bộ cảm giác.

### 2.6 Hạ tầng test frontend — vẫn treo từ phiên trước

Chưa có gì. Phiên vừa rồi kiểm single-flight bằng harness biên dịch tay rồi xóa.

**Khuyến nghị:** thêm **Vitest** (không Playwright). Chỉ để test tầng `lib/api/` — nơi
logic thật sự khó và không nhìn thấy được. Component test chưa cần.

### 2.7 Thứ tự làm

**Khuyến nghị:** Project detail (khung tab) → Board Kanban → Backlog + Sprint → *(chèn một
đợt backend ngắn cho 2.1/2.2 + Label/Watcher/ActivityLog)* → Task detail → Notification →
Dashboard.

Lý do: Board **hoàn toàn không bị chặn** và là màn hình bán được nhất. Task detail thì bị
chặn một phần (xem §4), nên để sau đợt backend.

---

## 3. Bản đồ màn hình → API

✅ = API đã sẵn sàng, làm được ngay · ⚠️ = làm được một phần · ⬜ = bị chặn

| Màn hình | API | Ghi chú |
|---|---|---|
| **Project detail** (khung tab: Board / Backlog / Sprint / Thành viên) | ✅ | `GET /projects/{id}` đã trả cả `members` |
| **Board Kanban** | ✅ | `GET /projects/{id}/board?sprintId=`, `PATCH /tasks/{id}/status` |
| **Backlog** | ✅ | `GET /projects/{id}/backlog`, `PUT /tasks/{id}/sprint` |
| **Sprint CRUD** | ✅ | 5 endpoint đủ; `SprintResponse` có `IsActive` + `TaskCount` |
| **Quản lý thành viên** | ✅ | mời / đổi role / gỡ / accept / decline |
| **Lời mời của tôi** | ✅ | `GET /projects/invitations` |
| **Task: tạo / sửa / đổi status / gán người** | ✅ | ⚠️ `PUT /tasks/{id}` cần `RowVersion`, `PATCH status` thì **không** |
| **Task detail** | ⚠️ | thiếu Description (2.2), Label, Watcher, TaskLink, Activity |
| **Comment trên task** | ✅ | nhớ ADR-026: sửa = chỉ tác giả, xóa = tác giả hoặc PM |
| **Notification bell** | ✅ | dùng `relatedEntityKind` + `relatedEntityId` để điều hướng (ADR-025) |
| **Admin: nhân sự** | ✅ | khóa / mở / đổi SystemRole |
| **Dashboard thống kê** | ⬜ | chưa có API nào |
| **Search toàn cục** | ⬜ | chỉ có `search` theo tên trong từng danh sách |

---

## 4. Backend còn thiếu, xếp theo mức độ chặn frontend

| # | Việc | Chặn gì | Quy mô |
|---|---|---|---|
| 1 | `Description` + mã `PMS-12` cho task (§2.1, §2.2) | Task detail dùng được thật | Nhỏ, 1 migration |
| 2 | **Watcher API** | nút Watch/Unwatch. ⚠️ `Watcher` **không** kế thừa `BaseEntity` (khóa kép) nên `IRepository<T>` không phục vụ được | Nhỏ |
| 3 | **Label API** | chip nhãn màu trên thẻ Kanban — ảnh hưởng thị giác lớn | Nhỏ |
| 4 | **TaskLink API** | tab "Linked issues". ⚠️ cần guard chống link vòng | Vừa |
| 5 | **ActivityLog đọc** | tab "Lịch sử". `IActivityLogger` đã ghi đủ, chưa endpoint nào đọc | Nhỏ |
| 6 | **Dashboard API** | toàn bộ màn thống kê. `ProjectAction.ViewStatistics` có sẵn chưa ai dùng | Vừa |
| 7 | Job task quá hạn → `DueSoon` | không chặn UI | Nhỏ |

Mục 2–5 gom được vào **một đợt backend ngắn**, nên chèn vào giữa như §2.7 đề xuất.

---

## 5. Làm sao để trông chuyên nghiệp — cụ thể, không nói chung chung

Khoảng cách giữa hiện tại và Jira/Trello **không** nằm ở "thêm CSS cho đẹp". Nó nằm ở
những chi tiết cụ thể sau:

### 5.1 Thứ tạo ra khác biệt lớn nhất

| Việc | Vì sao |
|---|---|
| **Mã task `PMS-12`** ở mọi nơi | Dấu hiệu nhận dạng số một của công cụ theo dõi việc |
| **Avatar người dùng** (chữ cái đầu, có màu ổn định theo id) | Làm sản phẩm có "người" trong đó; thẻ Kanban không avatar trông như danh sách chết |
| **Icon + màu cho Priority** (mũi tên lên/xuống kiểu Jira) | Quét mắt nhanh hơn nhiều so với chữ |
| **Chip nhãn có màu** | Cần Label API (§4 mục 3) |
| **Breadcrumb** `Dự án › Hệ thống kho › Board` | Ứng dụng nhiều tầng mà không có breadcrumb thì luôn thấy lạc |
| **Một màu nhấn nhất quán** | §2.5 |

### 5.2 Mật độ và nhịp

Jira/Linear **dày**, không thoáng. Hiện app đang thoáng sai chỗ.

- Chiều cao dòng bảng ~40px, không phải 56px
- Định nghĩa thang cỡ chữ rõ ràng thay vì rải `text-sm` khắp nơi
- Thẻ Kanban: mã task + tên + hàng dưới cùng (avatar, priority, nhãn)
- Cột Board có **số lượng task** trên đầu cột

### 5.3 Tương tác khiến app "sống"

- **Cập nhật lạc quan khi kéo–thả**: thẻ phải nhảy cột **ngay**, rồi mới gọi API; hỏng thì trả về chỗ cũ. Chờ round-trip mới di chuyển là cảm giác chậm chạp điển hình của app sinh viên.
- **Sửa tại chỗ**: bấm vào tên task là sửa được, không phải mở dialog
- **Bảng lệnh `Cmd+K`** — rẻ (`cmdk` đi kèm shadcn) và là tín hiệu "công cụ pro" mạnh
- **Phím tắt**: `c` tạo task, `/` focus ô tìm kiếm
- **Skeleton đúng hình dạng nội dung thật**, không phải khung xám chung chung

### 5.4 Nợ kỹ thuật thị giác cần dọn

- `status-badge.tsx` và cảnh báo 409 trong `edit-project-dialog.tsx` đang **hardcode màu** (`bg-amber-50`, `text-slate-700`). Phải chuyển sang biến CSS trước khi làm dark mode, nếu không dark mode sẽ vỡ ở đúng những chỗ đó.
- Thanh header trên cùng gần như trống — cho breadcrumb vào đó.
- Chỉ có `sonner` toast; chưa có mẫu xử lý lỗi thống nhất cho thao tác nền.

---

## 6. Bẫy đã biết — mang sang phiên sau

Đây là kiến thức đã trả giá, đừng phát hiện lại.

**Kanban:**
- Thả thẻ về **đúng cột nó đang đứng** → **409** (state machine từ chối "đứng yên"). Kéo–thả phải **chặn trước**, không được để bắn request rồi hiện toast đỏ.
- **Nhảy bước** `ToDo → Done` → **409**. Chỉ cho thả sang cột kề.
- Task đang bị `Blocks` chặn cũng → 409.
- `PATCH /tasks/{id}/status` và `PUT /tasks/{id}/sprint` **KHÔNG** cần `RowVersion` (ADR-021); `PUT /tasks/{id}` thì **bắt buộc**.
- Board luôn trả **đủ 4 cột** kể cả cột rỗng.

**Chung:**
- 404 ≠ "không có quyền". Người ngoài project nhận 404 **cố ý** (ADR-006/019).
- 403 chỉ khi **đã là thành viên** nhưng role không đủ.
- Ẩn/hiện nút theo `roleInProject`, **đừng đoán từ mã lỗi**. Danh sách project nay đã trả sẵn trường này (ADR-032) — endpoint khác có thể chưa, cần thì bổ sung theo đúng khuôn đó.
- Trường nullable gửi `null` thật, không gửi chuỗi `"string"`.
- `IsOverdue`, `SubtaskProgress` là **giá trị tính sẵn**, đừng tính lại.
- Comment: sửa = **chỉ tác giả** (PM cũng không), xóa = tác giả **hoặc** PM (ADR-026).
- **Đừng thêm `middleware.ts`** để chặn route — cookie `Path=/api/v1/auth` nên nó không đọc được (ADR-028).
- **Đừng gọi `performRefresh()` trực tiếp** — chỉ `refreshAccessToken()` (ADR-030).
- Tailwind v4 **đã bỏ** `max-w-screen-*`. Class không tồn tại thì im lặng không có tác dụng, không hề báo lỗi.
- `components/ui/*` do shadcn sinh — chỉnh style ở **nơi dùng**, đừng sửa file đó (sẽ bị ghi đè).

---

## 7. Môi trường — chạy lại được ngay

```powershell
dotnet dev-certs https --trust     # một lần cho mỗi máy
mkcert -install                    # một lần cho mỗi máy
```

```powershell
$env:PMS_TEST_DB = "Server=localhost;Database=PmsTestDb;Integrated Security=True;TrustServerCertificate=True;Encrypt=False"
```
*(Máy dùng Windows Authentication cần biến này, nếu không toàn bộ 133 integration test đỏ
với `Login failed for user 'sa'` — trông như code hỏng nhưng là môi trường.)*

Backend: `cd backend/src/PMS.API && dotnet run --launch-profile https` → `https://localhost:7264`
Frontend: `cd frontend && npm run dev` → `https://localhost:3000`

**Còn nợ về kiểm chứng:** browser trong phiên vừa rồi không điều hướng được tới bất kỳ
origin nào, nên **chưa thao tác thật trên UI lần nào**. Phiên sau kiểm sớm: giữ phiên khi
F5, guard chuyển hướng không nháy, và kịch bản single-flight trên Network tab.

**Còn nợ về diagram:** `seq-12-refresh-token` mới có `.mmd`, chưa sinh `.drawio`/`.png` vì
máy không cài draw.io Desktop. Lệnh có sẵn ở `docs/uml/README.md` (nay đã có nhánh Windows).
