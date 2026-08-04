# Chuẩn bị cho phiên Frontend kế tiếp

> Soạn ngày 2026-07-31, cuối phiên "Frontend — nền tảng".
> **Cập nhật 2026-08-03** cuối phiên "Backend hoàn chỉnh" — xem §0 trước.
> Đọc cùng `ARCHITECTURE.md` §6 và ADR-027 → ADR-042.

---

## 0. 🆕 Cập nhật 2026-08-03 (tối) — Lời mời + chi tiết Task + Notification bell đã xong

**Đọc mục này trước; §2, §3 và §4 bên dưới đã lỗi thời một phần.**

### Còn lại đúng ba nhóm, không nhóm nào bị chặn

| # | Việc | Tầng dữ liệu | Ghi chú |
|---|---|---|---|
| 1 | **Dashboard thống kê** | ✅ có sẵn (`useProjectStatistics`) | Phải `npm i recharts` — chưa cài. `byStatus`/`byPriority` đã zero-fill đủ mọi enum nên không cần tự vá cột thiếu |
| 2 | **Quên / đặt lại mật khẩu** | ✅ có sẵn (`forgotPassword`/`resetPassword`) | 2 trang. ⚠️ `forgot-password` LUÔN 204 → **một** thông điệp duy nhất (ADR-041) |
| 3 | **Nhóm Admin** (nhân sự · nhãn toàn cục · audit log) | ⚠️ **một nửa** | Audit log đã có `useSystemAuditLogs`; nhãn đã có `useCreateLabel/useUpdateLabel/useDeleteLabel`. **`AdminEmployees` chưa có gì** — phải tự viết |
| — | Search toàn cục | ❌ **chưa có API** | Xem cảnh báo `?search=` bên dưới |

**Hợp đồng `AdminEmployeesController` (đã tra sẵn, khỏi mở lại backend):**
`GET /admin/employees` — PagedRequest, `search` khớp `Name` **hoặc** `Email` và **thực sự
chạy** ở endpoint này; sort whitelist `name|email|role|locked`. Trả
`PagedResult<EmployeeAdminResponse>` = `{id, name, email, systemRole, isLocked, lockedAt|null,
lockReason|null, createdAt}`.
`POST /admin/employees/{id}/lock` body `{reason}` (bắt buộc, ≤256) → **204** ·
`POST /admin/employees/{id}/unlock` → **204** ·
`PUT /admin/employees/{id}/system-role` body `{role}` → **204**.
Tất cả gác bằng policy `require-system-admin` (403 với người khác).
Mã lỗi cần thông điệp riêng: **409** "đây là SystemAdmin hoạt động cuối cùng" · **409** khóa
tài khoản đã khóa / mở tài khoản không khóa · **400** tự khóa hoặc tự đổi vai trò của chính
mình · **404** không tìm thấy.

### 🪤 Bốn cái bẫy MỚI của phiên chi tiết Task

1. 🔴 **`PUT /tasks/{id}` là GHI ĐÈ TOÀN PHẦN, không phải PATCH** (ADR-044). Trường nào không
   gửi thì thành `null`. Lỗi này **đã sống thật**: form sửa task chưa bao giờ gửi
   `description` nên đổi tên task là xóa trắng mô tả. Nay mọi lệnh ghi của màn chi tiết đi
   qua đúng một trục `components/tasks/use-task-field-save.ts` — **đừng** thêm chỗ gọi
   `useUpdateTask` thứ hai vào màn đó.
2. 🔴 Trong trục đó, `patch.dueDate ?? current.dueDate` là **sai**. Hai trường nullable
   (`dueDate`, `description`) cần phân biệt "không truyền" (`undefined`) với "truyền `null`";
   dùng `??` thì thao tác **xóa** hạn/mô tả im lặng không có tác dụng.
3. 🔴 **Khóa nút lưu khi `detail.isFetching`**, trong đó có lượt tải lại sau 409. Bấm Lưu lúc
   đó là gửi lại đúng `rowVersion` đã chết → 409 vĩnh viễn.
4. **`?search=` là một lời hứa hão ở hầu hết endpoint.** `PagedRequest.Search` được model
   binder nhận ở **mọi** danh sách, nhưng chỉ `EmployeeRepository` và
   `NotificationRepository` thực sự dùng; project/task/sprint/comment/audit-log nhận rồi **bỏ
   qua im lặng**. Đây là lý do ô chọn task khi tạo liên kết lọc ở **client** (trần 100 task,
   `useProjectTaskOptions`).

### Đã hết hiệu lực — đừng làm lại

- ~~"Lời mời của tôi" chưa có màn~~ → `app/(app)/invitations/page.tsx`
- ~~Notification không có gì ở FE~~ → `types/notification.ts` + `endpoints/notifications.ts`
  + `use-notifications.ts` + `notificationKeys`
- ~~`permissions.ts` thiếu hàm cho action mới~~ → đã thêm `canManageLabels`,
  `canManageTaskLinks`, `canUploadAttachment`, `canWatch`, `canDeleteAttachment`
- ~~Tạo subtask chưa có giao diện~~ → `TaskFormDialog` nhận prop `parentTaskId`

---

## 0b. Cập nhật 2026-08-03 (sáng) — backend đã xong

### Những gì đã thay đổi

| Câu hỏi treo ở §2 | Nay đã ra sao |
|---|---|
| 2.1 Mã task `PMS-12`? | ✅ **Xong.** Backend trả sẵn `code` (`"PMS-12"`) **và** `number` trên cả `TaskSummaryResponse` lẫn `TaskDetailResponse` |
| 2.2 `Description` cho task? | ✅ **Xong.** `TaskDetailResponse.description`, nullable |
| 2.3 Thư viện kéo–thả | ✅ Đã chọn `@dnd-kit`, đã dùng thật từ 2026-08-02 |
| 2.4 Dark mode | ✅ Xong 2026-08-02 |
| 2.5 Màu thương hiệu | ✅ Xong 2026-08-02 (xanh Jira) |
| 2.6 Hạ tầng test FE | ✅ Vitest, nay **79 test** |
| 2.7 Thứ tự làm | Đợt backend ở giữa **đã xong hết** — phần còn lại thuần frontend |

**§4 "Backend còn thiếu" nay RỖNG.** Cả bảy mục đều đã làm: Description + mã task, Watcher,
Label (kèm `color`), TaskLink (kèm guard vòng chặn), ActivityLog đọc, Dashboard API, job
task quá hạn. Cộng thêm hai thứ không có trong bảng đó: **Attachment** (tính năng mới) và
**reset password**.

### Tầng dữ liệu đã viết sẵn — ĐỪNG viết lại

Phiên 2026-08-03 đã dựng xong toàn bộ types + endpoints + hooks. Vào thẳng phần dựng màn:

| Nhóm | `types/` | `lib/api/endpoints/` | `lib/hooks/` |
|---|---|---|---|
| Nhãn | `label.ts` | `labels.ts` | `use-labels.ts` |
| Người theo dõi | `watcher.ts` | `watchers.ts` | `use-watchers.ts` |
| Liên kết task | `task-link.ts` | `task-links.ts` | `use-task-links.ts` |
| Đính kèm | `attachment.ts` | `attachments.ts` | `use-attachments.ts` |
| Comment | `comment.ts` | `comments.ts` | `use-comments.ts` |
| Lịch sử | `activity.ts` | `activity.ts` | `use-activity.ts` |
| Thống kê | `statistics.ts` | `statistics.ts` | `use-statistics.ts` |
| Quên mật khẩu | `auth.ts` *(mở rộng)* | `auth.ts` *(mở rộng)* | — |

Query key mới ở `lib/hooks/keys.ts`: `taskDetailKeys` (comments/attachments/watchers/links/
activity), `projectActivityKeys`, `statisticsKeys`, `projectAttachmentKeys`, `labelKeys`,
`systemAuditKeys`.

### 🪤 Sáu cái bẫy MỚI của phiên này

1. 🔴 **Mã task do backend ghép — đừng tự nối.** Dùng `task.code`, **không** dựng
   `` `${projectKey}-${number}` `` ở component. Hai nơi định dạng chắc chắn có lúc lệch
   (ADR-034).
2. 🔴 **`LinkType.IsBlockedBy` không bao giờ tồn tại trong DB**, nhưng **vẫn xuất hiện** ở
   hai chỗ hợp lệ: khi client gửi lên, và trong `TaskLinkResponse.linkType` khi xem từ đầu
   bị chặn. Backend chuẩn hóa mọi thứ về `Blocks` đảo chiều (ADR-038). Hệ quả cần xử lý ở
   UI: tạo `Blocks(A→B)` rồi tạo `IsBlockedBy(B→A)` nhận **409** — đúng, vì đó là cùng một
   sự thật.
3. 🔴 **`apiFetch` nay nhận `FormData`** và **cố tình không đặt `Content-Type`** (để trình
   duyệt tự sinh `boundary`). Đừng "sửa" lại bằng cách thêm header đó — có hai test Vitest
   khóa, đã mutation-test.
4. 🔴 **Link tải file KHÔNG dùng được với `<a href>`.** Endpoint download cần header
   `Authorization`, mà thẻ `<a>` không gắn được, và access token nằm trong bộ nhớ chứ không
   phải cookie (ADR-027). Dùng `downloadAttachment()` rồi tạo object URL từ `Blob`.
5. **`forgot-password` LUÔN trả 204**, kể cả email không tồn tại (ADR-041). UI phải hiện
   đúng một thông điệp cho mọi trường hợp — hiện "email không tồn tại" là dựng lại đúng
   kênh dò tài khoản mà backend vừa bịt.
6. **`Viewer` theo dõi được task.** Đây là ngoại lệ ghi duy nhất của vai trò đó (ADR-036),
   nên đừng ẩn nút Theo dõi bằng một phép kiểm `role !== 'Viewer'` chung chung.

### Bốn mã lỗi của upload cần thông điệp riêng

`400` tên file có ý đồ / đuôi kép / nội dung không khớp đuôi đã khai · `413` quá lớn ·
`415` định dạng không hỗ trợ · `403` Viewer. Bốn nguyên nhân này có bốn hành động khắc phục
khác hẳn nhau, gộp thành "Tải file thất bại" là bỏ phí toàn bộ công backend đã bỏ ra để
phân biệt chúng.

### Quyền: hai thay đổi

- **`Member` nay xem được thống kê** (ADR-039) — trước chỉ PM + Viewer.
- **`SystemAdmin` KHÔNG có đặc quyền nghiệp vụ nào** (ADR-042). Đừng dựng UI kiểu "admin
  xem được mọi project" — họ nhận 404 y hệt người ngoài. Màn admin chỉ có: quản lý nhân sự,
  nhãn toàn cục, và `GET /admin/audit-logs`.

⚠️ `lib/tasks/permissions.ts` **chưa** có hàm cho các action mới (`canManageLabels`,
`canManageTaskLinks`, `canUploadAttachment`, `canWatch`). Bổ sung theo đúng khuôn có sẵn khi
dựng màn chi tiết Task — đừng đoán quyền từ mã lỗi.

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

## 2. ~~Quyết định cần chốt TRƯỚC khi code~~ — ✅ đã chốt và làm xong cả bảy

> ⚠️ **Mục này giữ lại làm hồ sơ thiết kế** (hữu ích cho chương "Phân tích thiết kế" của
> báo cáo), **không còn là việc cần quyết**. Bảng đối chiếu trạng thái ở §0.

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
| **Task detail** | ✅ *(2026-08-03)* | đủ cả `description`, `code`, Label, Watcher, TaskLink, Activity, **Attachment** |
| **Comment trên task** | ✅ | nhớ ADR-026: sửa = chỉ tác giả, xóa = tác giả hoặc PM |
| **Notification bell** | ✅ | dùng `relatedEntityKind` + `relatedEntityId` để điều hướng (ADR-025) |
| **Admin: nhân sự** | ✅ | khóa / mở / đổi SystemRole |
| **Admin: nhãn toàn cục + audit log** | ✅ *(2026-08-03)* | `PUT/DELETE /labels/{id}` và `GET /admin/audit-logs`, đều chỉ SystemAdmin |
| **Dashboard thống kê** | ✅ *(2026-08-03)* | `GET /projects/{id}/statistics` — `byStatus`/`byPriority` đã zero-fill đủ mọi giá trị enum |
| **Đính kèm file** | ✅ *(2026-08-03)* | Task và Project. ⚠️ tải về phải qua `downloadAttachment()`, không dùng `<a href>` — bọc sẵn ở `lib/attachments/download.ts` |
| **Quên/đặt lại mật khẩu** | ✅ *(2026-08-03)* | `forgot-password` luôn 204 — UI chỉ được hiện một thông điệp duy nhất |
| **Search toàn cục** | ⬜ | ⚠️ **KHÔNG có API.** `?search=` được binder nhận ở mọi danh sách nhưng chỉ `Employee` và `Notification` dùng tới; chỗ khác nhận rồi bỏ qua im lặng |

> 📌 Cột "Màn hình" ở bảng trên nói API có sẵn hay chưa, **không** nói màn đã dựng hay chưa.
> Tính tới cuối 2026-08-03 đã dựng: Project (list/detail/4 tab), Board, Backlog, Sprint,
> Thành viên, đăng nhập/đăng ký, **Lời mời**, **Thông báo (bell + trang)**, **chi tiết Task**.
> Chưa dựng: Dashboard, Quên/đặt lại mật khẩu, nhóm Admin, Search toàn cục.

---

## 4. ~~Backend còn thiếu~~ — ✅ ĐÃ XONG TOÀN BỘ 2026-08-03

> ⚠️ **Bảng dưới đây giữ lại làm hồ sơ, KHÔNG còn là việc cần làm.** Cả bảy mục đã hoàn
> thành trong phiên 2026-08-03, cộng thêm Attachment và reset password vốn không có trong
> bảng. Xem §0 để biết trạng thái thật.

<details>
<summary>Bảng gốc (đã hoàn thành hết)</summary>


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

</details>

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
- **Nhảy bước** `ToDo → Done` → **409**.
- ⚠️ **ĐÍNH CHÍNH 2026-08-02:** dòng trên trước đây viết *"Chỉ cho thả sang cột kề"* — **SAI**,
  và cài theo nó là ship bug theo cả hai chiều. Đã đối chiếu `TaskItem.CanTransitionTo`
  (`PMS.Domain/Entities/TaskItem.cs:77-86`), state machine thật có **đúng sáu** bước:
  ```
  ToDo → InProgress          InProgress → Review        Review → Done
  InProgress → ToDo          Review → InProgress        Done → Review
  ```
  Nghĩa là `Done → Review` và `Review → InProgress` là **bước lùi HỢP LỆ** (task bị
  trả về sửa), còn `ToDo → Review` **trông như cột kề nhưng KHÔNG hợp lệ**.
  Bản sao phía frontend nằm ở `lib/tasks/status-transitions.ts`, có test khóa lại đúng
  hai phản ví dụ này.
- Task đang bị `Blocks` chặn cũng → 409, **nhưng chỉ khi đích là `InProgress`** —
  `TaskStatusTransitionService` chỉ gọi `EnsureNotBlockedAsync` cho nhánh đó. Mọi cột
  khác đều đoán trước được hoàn toàn ở client.
- `PATCH /tasks/{id}/status` và `PUT /tasks/{id}/sprint` **KHÔNG** cần `RowVersion` (ADR-021); `PUT /tasks/{id}` thì **bắt buộc**.
- Board luôn trả **đủ 4 cột** kể cả cột rỗng.
- Board **không có `sprintId`** KHÔNG phải "board của backlog": backend rơi xuống
  `GetRootTasksByProjectAsync`, tức **tất cả** task gốc kể cả task thuộc sprint khác.
  Nhãn đúng cho lựa chọn đó là **"Tất cả task"**.

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

**Thêm sau phiên 2026-08-03 (chi tiết Task):**
- 🔴 **`PUT /tasks/{id}` ghi đè TOÀN PHẦN.** Xem §0. Đây là lớp lỗi mà §15 đã đặt tên:
  build sạch, test xanh, tài liệu ✅ — và vẫn sai, vì **chưa có ai gọi tới** đường đó.
- 🔴 **Intercepting route: tiền tố là `(.)`, không phải `(..)`**, và `@modal/default.tsx` là
  **bắt buộc** (thiếu nó thì *board* trả 404, không phải slot rỗng). Đừng gom 4 tab vào route
  group `(tabs)/` — `useSelectedLayoutSegment()` sẽ trả `'(tabs)'` và tab mất trạng thái
  active. Lý do đầy đủ + bằng chứng đọc từ mã nguồn Next 15.5.22 ở **ADR-043**.
- 🔴 **TanStack v5: `gcTime` hiệu lực là giá trị LỚN NHẤT trong các observer đang mount.**
  `useTask` cố ý đặt `staleTime/gcTime: 0` vì nó là nguồn duy nhất của `rowVersion`; thêm một
  observer thứ hai lên cùng khóa mà quên lặp lại hai tùy chọn đó là dựng lại đúng bug
  `rowVersion` cũ → 409 vĩnh viễn. Xem `useTaskCached` (dùng cho breadcrumb).
- **`DateTime.MinValue` (`0001-01-01`) có thật trong DB** và hiển thị ra `01/01/1`. Nguyên
  nhân giống ADR-033: migration thêm cột `CreatedAt` với `defaultValue`, nên mọi hàng cũ mang
  mốc canh. `lib/format.ts` nay chặn ở tầng hiển thị (`isSentinelDate`, ngưỡng năm 1900) —
  nhưng nhớ là **dữ liệu vẫn sai**, chỉ có phần hiện ra là đúng.
- **Base UI `Popover`** (`components/ui/popover.tsx`, thêm ở phiên này) đặt cùng khuôn với
  `dropdown-menu.tsx`. Dùng nó cho nội dung tự do (danh sách thông báo, ô chọn nhãn); dùng
  `DropdownMenu` cho **menu lệnh** thật — `Menu` của Base UI quản lý roving-tabindex nên
  nhồi form/danh sách cuộn vào đó sẽ tranh focus.
- **Ghi chú môi trường, không phải lỗi ứng dụng:** công cụ trình duyệt của phiên 2026-08-03
  không bắn được sự kiện chuột tổng hợp tới `<button>` React thường (Base UI trigger và thẻ
  `<a>` thì được). Mọi thao tác kiểm chứng phải gọi `.click()`/`requestSubmit()` bằng JS.
  Nếu phiên sau gặp "bấm nút không có gì xảy ra" khi tự động hóa, **kiểm bằng `.click()`
  trước khi kết luận ứng dụng hỏng** — và vì lý do này, kéo–thả bằng bàn phím vẫn còn là nợ.

**Thêm sau phiên 2026-08-02:**
- **`SelectValue` của Base UI hiện GIÁ TRỊ THÔ**, không phải nhãn của `SelectItem`. Phải
  truyền hàm định dạng: `<SelectValue>{(v) => NHAN[v]}</SelectValue>`. Không làm thì ô
  chọn hiện `"Member"` thay vì `"Thành viên"`, hoặc nguyên một guid.
- **`onValueChange` của Base UI `Select` có thể trả `null`** khi bỏ chọn — kiểu là
  `string | null`, phải xử lý cả hai.
- **Đừng chạy `npm run build` khi `npm run dev` đang chạy.** Cả hai cùng ghi vào `.next`
  và làm hỏng nó: trang trả 500 kèm `ENOENT ... _buildManifest.js.tmp.*`. Phải `rm -rf
  .next` rồi khởi động lại.
- **`vitest.config` phải đặt tên `.mts`.** `package.json` không có `"type": "module"` nên
  Vitest nạp file `.ts` bằng `require()` và chết `ERR_REQUIRE_ESM` ở phụ thuộc `std-env`.
- **Vitest không đọc `.env.local`** mà `lib/api/config.ts` thì NÉM ngay lúc import nếu
  thiếu `NEXT_PUBLIC_API_BASE_URL` → phải set qua `test.env` trong config.
- **`new Response('', { status: 204 })` NÉM** — body phải là `null` cho 204/205/304.
- **`mockResolvedValue(response)` chỉ dùng được MỘT lần**: body của `Response` chỉ đọc
  được một lượt, lời gọi thứ hai chết với "Body is unusable". Dùng `mockImplementation`
  để dựng `Response` mới mỗi lần.
- **Đo tương phản màu thì đừng bóc `getComputedStyle().color` bằng regex** — Chrome trả
  `lab()`/`oklch()`, không phải `rgb()`. Vẽ lên canvas 1×1 rồi đọc pixel, và nhớ **xếp
  chồng cả các lớp nền bán trong suốt** (`bg-muted/30`, `bg-primary/5`) chứ không lấy lớp
  đầu tiên gặp được.
- Ngày dạng ngắn: **`Intl.DateTimeFormat('vi-VN', { day, month })` trả về dấu GẠCH NGANG**
  (`29-07`) trong khi mẫu đủ ba thành phần dùng gạch chéo (`12/08/2026`). Ghép hai cái
  trong một khoảng ngày ra `29-07 – 12/08/2026`, trông hệt như lỗi. Tự ghép chuỗi.
- 🔴 **Class `.variable` của `next/font` phải đặt trên `<html>`, KHÔNG phải `<body>`.**
  `globals.css` `@apply font-sans` ở tầng `html`; đặt biến ở `<body>` thì `<html>` không
  thấy biến → font-family không hợp lệ → rơi về **Times New Roman**, `<body>` thừa kế
  luôn. Không lỗi, không cảnh báo. Bug này sống từ phiên dựng scaffold tới 2026-08-02.
  Kiểm bằng `getComputedStyle(document.documentElement).fontFamily`, đừng tin mắt.
- **Đổi font thì phải kiểm bộ `vietnamese`** trong
  `next/dist/compiled/@next/font/dist/google/font-data.json`. Thiếu glyph thì trình duyệt
  âm thầm rơi về font dự phòng ở đúng vài ký tự có dấu — rất khó thấy. Cách kiểm bằng số:
  đo bề rộng ký tự bằng canvas với `font: '400 40px "Tên Font", monospace'` rồi so với
  `'400 40px monospace'`; trùng nhau nghĩa là glyph bị thiếu.

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
