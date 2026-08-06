# ARCHITECTURE.md
## Hệ thống Quản lý Dự án & Task (Project Management System)

> Tài liệu này ghi lại các quyết định kiến trúc (Architecture Decisions) của dự án.
> Mục đích: đảm bảo tính nhất quán xuyên suốt quá trình phát triển, và làm tài liệu
> tham chiếu cho báo cáo thực tập tốt nghiệp.
>
> Cập nhật lần cuối: **2026-08-05** (phiên **cột board tuỳ biến + vòng đời Sprint + Việc của tôi** — ADR-050/052/053)

> ## 🧭 Bắt đầu phiên mới ở đây
> **Trạng thái: KHÔNG CÒN MÀN HÌNH NÀO ⬜ trong lộ trình ban đầu.** **480 test backend
> (223 unit + 257 integration) + 69 test frontend**, build 0 warning **và có
> `TreatWarningsAsErrors`** nên con số đó là một điều kiện chứ không còn là một quan sát.
>
> ⚠️ **Hai con số test GIẢM so với bản trước (489 → 480 backend, 89 → 69 frontend) và đó là
> ĐÚNG, không phải mất test.** ADR-052 gỡ ma trận chuyển trạng thái, nên ~29 test khóa một
> luật **không còn tồn tại** đã bị xóa cùng thứ chúng bảo vệ. Ba trong số đó bị **đảo chiều**
> chứ không xóa (409 → 200) — xem bảng trong ADR-052. Đừng "khôi phục" chúng.
>
> Phiên 2026-08-05 làm ba việc lớn: **cột board tuỳ biến theo từng project** (ADR-052 — thay
> đổi lớn nhất dự án tính tới nay), **vòng đời Sprint** (ADR-050, mở khóa velocity), và
> **"Việc của tôi" xuyên dự án** (ADR-053).
>
> ### 🔑 Sáu điều phải biết trước khi đụng vào code
> - **Phân quyền tầng 1 nay là DỮ LIỆU, không phải code** (ADR-045). Năm mã trong danh mục
>   **ĐÓNG** ở `SystemPermissions.cs`, lưu ở hai bảng seed bằng `HasData`, sửa được ở
>   `/admin/roles`. Tên policy **chính là** mã quyền; `require-system-admin` và
>   `can-create-project` đã **xóa hẳn**. Tầng 2 (`ProjectPermissions`) **không đổi một dòng**
>   và không bao giờ được đưa vào token.
> - 🔴 **Thêm quyền mới cần ĐỦ BA bước**: `const` trong `SystemPermissions` → `HasData` trong
>   `PermissionConfiguration` → `dotnet ef migrations add`. Quên bước 3 thì
>   `has-pending-model-changes` đỏ; quên bước 2 thì test khóa danh mục đỏ. **Hàng permission
>   là SCHEMA, không phải data** — đừng chuyển sang `DbSeeder`, nó không chạy ở môi trường
>   test và cả suite sẽ đỏ.
> - 🔴 **Mọi cột `DateTime` có `ValueConverter`** đóng dấu `Kind=Utc` lúc đọc (ADR-046b). Hệ
>   quả bắt buộc nhớ: **EF KHÔNG dịch được `.Date` trên cột đã chuyển đổi** — nó ném lúc chạy
>   thành HTTP 500. Lọc theo ngày phải so thẳng với mốc nửa đêm (`DueDate < today`).
> - 🔴 **`min-width:auto` của grid/flex item** là lớp lỗi bố cục đã cắn **năm lần** (dialog
>   Task tràn chữ, board lệch 8px, thống kê lệch 104px, và 2026-08-05 thêm hai lần: board
>   cuộn ngang, hàng nút `PageHeader` ba nút ở 375px). Và **`break-words` KHÔNG sửa được
>   nó**: `overflow-wrap` cho phép ngắt để khỏi tràn nhưng **không làm giảm min-content**.
>   Cách sửa gốc là `min-w-0` (+ `grid-cols-[minmax(0,1fr)]` khi con lại là grid item, +
>   `flex-wrap` khi là hàng nút).
> - 🔴 **Trạng thái task KHÔNG còn là enum** (ADR-052). Nó là một **cột** thuộc project, mang
>   `columnId`/`name`/`color` do người dùng đặt. Mọi phép kiểm "task xong chưa" phải đọc
>   `category` (`ToDo`/`InProgress`/`Done`), **không so tên cột và không so id với hằng nào**.
>   `STATUS_TONE` ở frontend nay CHỈ dùng cho trạng thái **project**; task dùng
>   `TaskStatusChip`.
> - 🔴 **`TaskItem.Category` là bản sao CÓ CHỦ ĐÍCH của `BoardColumn.Category`** — xem ADR-052
>   để biết vì sao chấp nhận dữ liệu trùng. Người ghi duy nhất là `TaskItem.MoveTo`; đổi nhóm
>   của một cột **bắt buộc** gọi `SyncTaskCategoriesAsync` cho mọi task trong cột đó.
>
> ### ➡️ Phiên tiếp theo — hai hạng mục, không cái nào chặn cái nào
> 1. **Nhóm báo cáo kiểu Jira** — backlog insight, velocity, report, timeline.
>    ✅ **Velocity nay ĐÃ mở khóa**: `Sprint.CompletedAt` là mốc đo (ADR-050 đã cài đặt
>    2026-08-05). Sidebar đã có sẵn nhóm **LẬP KẾ HOẠCH** để thêm mục "Báo cáo" (ADR-051).
>    ⚠️ Đọc ADR-052 trước khi tính toán bất cứ thứ gì theo trạng thái: **cột là dữ liệu của
>    từng project**, nên biểu đồ phải gom theo `columnId`/`category`, không theo một enum
>    cố định — và số cột khác nhau giữa các project.
> 2. **Áp kỹ thuật DB** — trigger, stored procedure, view, index. ⚠️ **Không có giao diện
>    nào** — đừng kỳ vọng nó lấp chỗ trên sidebar. *(Xa hơn: Elasticsearch cho Search toàn
>    cục, Redis cho cache + rate limit phân tán.)*
>
> Còn lại, nhỏ hơn: **đường GHI cho hồ sơ cá nhân** (`PUT /employees/me` + đổi mật khẩu khi
> đã đăng nhập) — đọc **ADR-049** trước, vấn đề không nằm ở endpoint mà ở chỗ `/auth/me`
> dựng DTO từ claim. Và **SignalR** (§6).
>
> ### 🪤 Ba cái bẫy mới, đã trả giá — đừng phát hiện lại
> - **`AuthController.Me()` dựng DTO từ CLAIM chứ không đọc DB.** Thêm trường vào
>   `EmployeeDto` mà chỉ nối dây ở `AuthService` thì `/auth/login` và `/auth/me` trả **hai
>   câu trả lời khác nhau**, và không gì bắt được lúc biên dịch.
> - **`#pragma warning disable` phải nằm TRƯỚC attribute** — span chẩn đoán bắt đầu ở danh
>   sách attribute, đặt xen giữa làm 15 cảnh báo quay lại.
> - **Lưu quyền ở `/admin/roles` tự đăng xuất chính admin đang bấm.** Đúng hợp đồng bảo mật
>   (thu hồi mọi phiên của vai trò đó), banner đã nói rõ — đừng "sửa".
>
> ### 📌 Ba đính chính với tài liệu cũ
> - **`?search=` KHÔNG phải "chỉ Employee + Notification"** — 5/6 repository vẫn luôn lọc
>   thật; chỉ `ActivityLogRepository` là không, và nay đã sửa. Điều còn đúng: nó chỉ lọc
>   **một trường** mỗi endpoint nên không thay được search toàn cục.
> - **Không thiếu 7 index khóa ngoại** — đã kiểm `sys.indexes` trên DB thật, 6/7 đã có do EF
>   tự sinh theo quy ước. Chỉ index ghép `(DueDate, Status)` là thật sự thiếu.
> - **`GET /projects/{id}/statistics` từng hỏng 500 ở MỌI lần gọi** suốt từ ngày viết
>   (2026-08-03) tới 2026-08-04, trong khi tài liệu ghi ✅ — vì chưa có test nào gọi tới.
>
> ### 🔑 Ba điều của phiên chi tiết-Task vẫn còn hiệu lực
> - **`PUT /tasks/{id}` là GHI ĐÈ TOÀN PHẦN, không phải PATCH** (ADR-044). Trường nào không
>   gửi thì thành `null`. Mọi lệnh ghi của màn chi tiết đi qua đúng một trục `useTaskFieldSave`.
> - **Chi tiết Task có hai vỏ** (dialog chặn route + trang thật) dùng chung một
>   `TaskDetailContent` — ADR-043. Tiền tố intercepting route là **`(.)`**, và
>   `@modal/default.tsx` là **bắt buộc** (thiếu nó thì *board* 404).
>   🆕 Từ 2026-08-05 hai vỏ **khác nhau về bố cục** (ADR-051): ở trang thật, khối
>   `Bình luận | Lịch sử` xuống dưới hai cột và lấy trọn bề ngang. Chung *nội dung*, không
>   chung *bố cục* — `variant` là thứ quyết định, đừng gộp lại cho "nhất quán".
> - **Đừng gom các tab vào route group `(tabs)/`** — `useSelectedLayoutSegment()` sẽ trả
>   `'(tabs)'` và thanh tab mất trạng thái active, hỏng im lặng. Áp cho cả `/admin`.
>
> ⚠️ Trước khi làm Kanban: `docs/frontend-next-session.md` §6 có khối "Bẫy đã biết", trong đó
> **ba mục đầu đã bị gạch ngang** vì ADR-052 gỡ ma trận chuyển trạng thái. Đọc phần gạch để
> hiểu code cũ, nhưng **đừng cài theo**.
>
> ### ⚠️ TRƯỚC KHI CHẠY LẦN ĐẦU TRÊN MÁY MỚI — hai lệnh bắt buộc
> Cả hai đều ghi vào kho chứng chỉ tin cậy của Windows nên **phải tự chạy tay** trong
> terminal có quyền, không tự động hóa được:
> ```powershell
> dotnet dev-certs https --trust     # để trình duyệt tin https://localhost:7264
> mkcert -install                    # để `next dev --experimental-https` sinh được cert
> ```
> **Vì sao bắt buộc, không phải tùy chọn:** refresh token đi trong cookie `SameSite=Strict`
> (ADR-027). Theo luật *schemeful same-site*, `http://localhost` và `https://localhost` là
> **hai site khác nhau** — chạy Next trên http thì cookie không bao giờ được gửi và toàn
> bộ luồng refresh hỏng **im lặng** (401 mà không có gì chỉ ra nguyên nhân).
>
> **Bốn điều về Kanban — đọc trước khi đụng vào việc chuyển cột của task:**
>
> 🔴 **KHÔNG CÒN state machine.** Khối này trước 2026-08-05 dạy một ma trận sáu-cặp-hợp-lệ
> (`Done → Review` hợp lệ, `ToDo → Review` thì không). **ADR-052 đã gỡ nó cùng với enum
> `Status`.** `TaskItem.CanTransitionTo`, `ALLOWED_TRANSITIONS`, `canTransition` đều **không
> còn tồn tại**. Đừng đi tìm, và đừng dựng lại.
>
> - **Mọi cột đều là đích hợp lệ**, kể cả "nhảy bước". Kéo thẻ về đúng cột nó đang đứng nay
>   trả **200** (no-op) chứ không 409 — vẫn nên chặn ở client để khỏi bắn request thừa.
> - Guard **duy nhất** còn lại: task đang bị `Blocks`/`IsBlockedBy` chặn thì không vào được
>   cột thuộc **nhóm `InProgress`** → **409**. Điều kiện là `category`, **không phải tên cột**.
> - `PATCH /tasks/{id}/status` và `PUT /tasks/{id}/sprint` **KHÔNG** cần `RowVersion`
>   (ADR-021), nhưng `PUT /tasks/{id}` thì **bắt buộc**.
>   ⚠️ Thân request nay là `{ targetColumnId }` (Guid), **không phải** `{ target }` (enum).
> - Board luôn trả **đủ MỌI cột của project** kể cả cột rỗng, sắp sẵn theo `order` — không
>   phải tự dựng cột thiếu và không phải tự sắp xếp. **Số cột không cố định 4.**
>
> **Đọc theo thứ tự:**
> 1. `docs/frontend-next-session.md` §6 — danh sách bẫy đã trả giá, gồm cả bẫy của
>    Base UI, Vitest và đo tương phản màu
> 2. §6 (Kiến trúc Frontend) — hiện trạng, không còn là dự kiến
> 3. §15 **ADR-027 → ADR-032** — sáu quyết định của phiên 2026-07-31
> 4. `frontend/lib/api/` — tầng API client; đọc `refresh.ts` trước tiên
> 5. `docs/uml/seq-diagram/src/seq-12-refresh-token.mmd` — cơ chế refresh có nhánh
>
> **Khuôn mẫu để copy khi làm màn hình mới** (cập nhật 2026-08-02):
> - Trang trong tab dự án: `app/(app)/projects/[id]/sprints/page.tsx` — ngắn nhất, đủ cả
>   bốn trạng thái (skeleton / empty / error / data) và gác quyền theo vai trò.
> - Bảng có phân trang: `app/(app)/projects/page.tsx` + `lib/hooks/use-projects.ts`.
> - Form có `RowVersion` (ADR-016): `components/tasks/task-form-dialog.tsx`.
> - Primitive dùng chung ở `components/common/` — đừng chép lại EmptyState/QueryError/
>   PageHeader/ConfirmDialog lần nữa.
>
> ⚠️ **Quyền: đọc `lib/tasks/permissions.ts`, đừng tự suy từ mã lỗi.** `roleInProject`
> chỉ có ở `GET /projects` (danh sách), **KHÔNG** có ở `GET /projects/{id}` — muốn biết
> vai trò của mình trên trang chi tiết thì tự tìm mình trong `members[]`, và chỉ tính khi
> `invitationStatus === 'Accepted'` (dùng sẵn `useMyProjectRole`).
>
> ⚠️ **Đừng thêm `middleware.ts` để chặn route.** Cookie có `Path=/api/v1/auth` nên
> middleware **không đọc được** và sẽ đá cả người đã đăng nhập về `/login`. Guard nằm ở
> `components/auth/auth-guard.tsx` — lý do đầy đủ ở ADR-028.
>
> **Nếu phải làm backend:** khuôn mẫu gần nhất vẫn là `PMS.Application/Features/Comments/`.
> ⚠️ **`Watcher` không kế thừa `BaseEntity`** (khóa kép `TaskId + EmployeeId`) nên
> `ApplyAuditFields()` không đóng dấu `CreatedAt`, và `IRepository<T> where T : BaseEntity`
> không phục vụ được nó — phải xử lý riêng, đừng mất thời gian phát hiện lại.

---

## 1. Tổng quan dự án

**Mô tả:** Hệ thống quản lý dự án và task, cho phép nhóm và các thành viên phân công
công việc, giám sát đầu việc rõ ràng trực quan, theo dõi timeline và hiện trạng của
các task và dự án. Tương tự phiên bản thu nhỏ của Jira/Trello.

**Mục đích sử dụng:** Đồ án tốt nghiệp / báo cáo thực tập tốt nghiệp.

**Yêu cầu bắt buộc từ đề bài:**
- Sử dụng kỹ thuật lập trình hướng đối tượng (OOP)
- Xây dựng cơ sở dữ liệu quan hệ để mapping các đối tượng một cách logic

### Tiến độ triển khai theo module

> 📌 Bảng này chỉ nói trạng thái **code** (có Service/Controller dùng được qua API hay
> chưa), không phải trạng thái thiết kế — mọi module đều đã có quyết định thiết kế đầy
> đủ trong tài liệu này dù chưa code. ✅ = đã có API dùng được và có test. ⬜ = mới có
> entity/schema (chưa có Service/Controller), hoặc chưa bắt đầu.

| Module | Trạng thái | Ghi chú |
|---|---|---|
| Domain layer (Entity, Enum) | ✅ | Toàn bộ entity ở §5 đã có trong `PMS.Domain` |
| Auth (Register/Login/Refresh/Logout/Me) | ✅ | Chi tiết xem bảng ở §10 — chỉ còn Reset Password ⬜, khóa/mở tài khoản đã xong |
| Project (CRUD + phân quyền 2 tầng + soft delete + optimistic concurrency) | ✅ | Có Unit + Integration Test. `RowVersion` đã wire đầy đủ qua DTO (không chỉ có ở schema) — xem ADR-016 |
| Project — quản lý thành viên (mời/accept/decline/đổi role/gỡ) | ✅ | `ProjectMemberService`/`ProjectMembersController`, có Integration Test (seq-04/05) — *bảng này từng ghi ⬜ dù đã code xong từ commit `ca8ff0b`, đã sửa lại 2026-07-29* |
| Sprint (CRUD + Backlog ↔ Sprint + **vòng đời**) | ✅ | `SprintService`/`SprintsController`, có Unit + Integration Test. Xóa sprint đẩy task về Backlog (ADR-020). 🆕 2026-08-05: `Status` + `CompletedAt`, start/complete, **đóng sprint HỎI task chưa xong đi đâu** (ADR-050) |
| Task (CRUD + Subtask + optimistic concurrency) | ✅ | `TaskService`/`TasksController`. `RowVersion` wire đầy đủ qua DTO, đóng lại "giới hạn đã biết" của ADR-016 |
| Task — chuyển cột | ✅ | `TaskStatusTransitionService`, quyền theo ADR-017 (Assignee HOẶC PM), chặn task đang bị `Blocks`/`IsBlockedBy`. ⚠️ 2026-08-05: **ma trận chuyển trạng thái đã GỠ** (ADR-052 thay ADR-021) — mọi cột đều tới thẳng được, guard duy nhất còn lại là nhóm `InProgress` khi task bị chặn |
| Task — giao việc (gán/tự nhận/gỡ) | ✅ | `TaskAssignmentService`, đúng bảng "Quy tắc gán việc" ở §5 và seq-02 |
| Board (Kanban) + Backlog | ✅ | `GET /projects/{id}/board?sprintId=` và `/backlog`; board luôn trả đủ **mọi cột của project** kể cả cột rỗng. ⚠️ Số cột **không còn cố định 4** kể từ ADR-052 |
| Comment — API | ✅ | `CommentService`/`CommentsController`, có Unit + Integration Test. Quyền theo ADR-026: viết = PM/Member, sửa = chỉ tác giả, xóa = tác giả hoặc PM. Xóa cứng |
| Watcher / Label / TaskLink — API | ✅ | Xong 2026-08-03. `Label` thêm `Color` + phân quyền theo bán kính ảnh hưởng (ADR-037); `Watcher` có repository riêng vì không phải `BaseEntity` (ADR-036); `TaskLink` chuẩn hóa lúc ghi + guard chu trình (ADR-038) |
| Task — `Description` + mã `PMS-12` | ✅ | Xong 2026-08-03. Bảng đếm riêng `ProjectTaskCounters` (ADR-033), mã ghép ở Mapper (ADR-034). Migration có backfill, đã kiểm tay trên DB có dữ liệu |
| Attachment (đính kèm file cho Task/Project) | ✅ | Mới 2026-08-03. `IFileStorage` + `LocalFileStorage`, whitelist 9 bước gồm magic number, tải về `octet-stream` + `nosniff` (ADR-035). 15 integration test |
| Task — người đảm nhận trên thẻ board/backlog | ✅ | `TaskSummaryResponse.Assignees` (thêm 2026-08-02, **không migration**). Đồng thời sửa bug im lặng: ba query board/backlog thiếu `Include` nên `SubtaskProgress` LUÔN trả 0 |
| Notification — API đọc | ✅ | `NotificationFeedService`/`NotificationsController` — danh sách có phân trang, đếm chưa đọc, đánh dấu một/tất cả. Ngoại lệ hợp lệ của ADR-006/019 — xem ADR-023 |
| Activity Log — API đọc | ✅ | Xong 2026-08-03. `GET /tasks/{id}/activity` + `/projects/{id}/activity`. **Đồng thời sửa một lỗ hổng có sẵn:** `ProjectService` trước đó KHÔNG ghi `ActivityLog` dòng nào — tạo/sửa/xóa project đều không sinh log |
| Nhật ký cấp hệ thống (SystemAdmin) | ✅ | `GET /admin/audit-logs`, `entityType` cố định ở server — nay là `Employee`/`Label`/**`RolePermission`** (ADR-045). `?search=` **thật sự lọc** từ 2026-08-04; trước đó bị nhận rồi bỏ qua im lặng (ADR-046) |
| Employee management (ngoài Auth) | ✅ | `AdminEmployeesController` — khóa/mở tài khoản, cấp `SystemAdmin` — *bảng này từng ghi ⬜ dù đã code xong, đã sửa lại 2026-07-29* |
| Thống kê / Dashboard — API | ✅ | `GET /projects/{id}/statistics` — tổng hợp trong SQL, zero-fill đủ mọi giá trị enum, `Member` cũng xem được (ADR-039). ⚠️ **Endpoint này trả 500 ở MỌI lần gọi từ ngày viết (2026-08-03) tới 2026-08-04** vì không có test nào chạm tới — xem ADR-046 |
| Background job task quá hạn → `DueSoon` | ✅ | Xong 2026-08-03. `IDueDateNotifier` (nghiệp vụ, test được) + `DueDateNotificationWorker` (timer). Khử trùng lặp theo ngày UTC, không đăng ký ở môi trường Testing (ADR-040) |
| Reset password | ✅ | Xong 2026-08-03 (ADR-041). `IEmailSender` giả lập cho Dev — không còn bị chặn bởi email service |
| Frontend — nền tảng (scaffold, API client, Auth, Project CRUD) | ✅ | Next 15 + Tailwind 4 + shadcn/ui + TanStack Query + Zustand. Tầng API client xử lý JWT, single-flight refresh (ADR-030) và cả 4 hình dạng lỗi của backend. Đăng ký/đăng nhập/giữ phiên khi F5/đăng xuất + route guard. Project: danh sách phân trang + tìm kiếm + tạo + sửa (round-trip `RowVersion`, xử lý 409 bằng tải lại) + xóa (xử lý 409 còn task chưa xong) |
| Frontend — Project detail (tab) + Thành viên + Sprint | ✅ | `app/(app)/projects/[id]/` với tab là segment định tuyến thật. Mời/đổi vai trò/gỡ/rời dự án; Sprint CRUD đầy đủ |
| Frontend — Board/Backlog Kanban | ✅ | `@dnd-kit` + cập nhật lạc quan. Ba bẫy 409 chặn bằng **cấu trúc** (`useDroppable disabled`) nên không request nào được tạo ra — đã kiểm chứng trên trình duyệt |
| Frontend — Task CRUD + giao việc | ✅ | Tạo/sửa (trọn luồng `RowVersion` 409 của ADR-016)/xóa, gán–tự nhận–gỡ người |
| Frontend — dark mode + màu thương hiệu | ✅ | Xanh Jira trên `--primary`, ThemeProvider + nút chuyển, breadcrumb. Đã đo tương phản WCAG AA trên cả 5 màn × 2 chế độ |
| Frontend — hạ tầng test | ✅ | Vitest cho `lib/api/` + logic thuần của Kanban — 76 test |
| Frontend — "Lời mời của tôi" | ✅ | Xong 2026-08-03. `app/(app)/invitations/`. **Luồng mời nay đã khép kín**: PM mời → người được mời thấy badge ở sidebar → chấp nhận → vào thẳng board |
| Frontend — Notification bell + trang thông báo | ✅ | Xong 2026-08-03. Tầng dữ liệu notification viết mới trong phiên này (types/endpoints/hook/keys). Điều hướng bằng cặp `(relatedEntityKind, relatedEntityId)` (ADR-025), kèm trang phân giải `/tasks/{id}` vì DTO thông báo không mang `projectId` |
| Frontend — **chi tiết Task** (7 khối) | ✅ | Xong 2026-08-03. Hai vỏ dùng chung một nội dung: dialog chặn route + trang thật (ADR-043). Mô tả sửa tại chỗ, subtask + tạo subtask, đính kèm (4 mã lỗi riêng), liên kết, người theo dõi, nhãn, `Bình luận \| Lịch sử` |
| Frontend — Dashboard (Recharts) | ✅ | Xong 2026-08-04. Tab thứ 5 của dự án. Màu chia ba nhóm theo VIỆC (trạng thái / tuần tự / phân đoạn), chỉ nhóm phải-phân-biệt mới chạy validator — ADR-047. Thẻ số và thanh mức cố ý KHÔNG phải biểu đồ |
| Frontend — Quên/đặt lại mật khẩu | ✅ | Xong 2026-08-04. `forgot-password` hiện **một thông điệp duy nhất** cho mọi kết quả (ADR-041) — đã kiểm bằng email thật và email bịa, ra cùng một chữ |
| Frontend — Admin (nhân sự / nhãn / audit log / **phân quyền**) | ✅ | Xong 2026-08-04. **Bốn** màn dưới `/admin`, gác bằng PERMISSION chứ không bằng `systemRole` (ADR-045). Tầng dữ liệu `AdminEmployees` viết mới trong phiên này |
| **Authorization — claim `permission` lưu DB** | ✅ | Mới 2026-08-04 (ADR-045). Hai bảng `Permission` + `RolePermission` seed bằng `HasData`, policy đăng ký bằng vòng lặp trên danh mục ĐÓNG, quản trị ở `/admin/roles`. Tầng 2 không sửa một dòng |
| **Frontend — nhóm Admin (4 màn)** | ✅ | Mới 2026-08-04. Nhân sự · Phân quyền · Nhãn toàn cục · Nhật ký hệ thống |
| **Frontend — ba tính năng ADR-048 + ba lỗ hổng UI** | ✅ | Mới 2026-08-05. Nút đổi `Project.Status` (PM-only) · ô gợi ý nhân sự khi mời · @mention (`reconcileMentions` + 6 test) · "Mở trang riêng" điều hướng cứng · `/profile` chỉ đọc (ADR-049) · sidebar theo ngữ cảnh dự án. **Kèm sửa lỗi có sẵn: `UserMenu` sập khi mở** |
| **Cột board tuỳ biến (ADR-052)** | ✅ | Mới 2026-08-05. `BoardColumns` theo từng project + `StatusCategory` ĐÓNG. Thêm/sửa/xóa/đổi thứ tự, thu cột, xóa bắt buộc chọn cột đích. **Gỡ ma trận chuyển trạng thái** — thay thế ADR-021 |
| **Vòng đời Sprint (ADR-050)** | ✅ | Mới 2026-08-05. `Sprint.Status` + `CompletedAt` + migration. Start (tối đa MỘT Active/project) · preview · complete **hỏi task chưa xong đi đâu**. Tab Sprint kiểu Jira: thu/mở, dòng task inline. **Mở khóa velocity** cho nhóm báo cáo |
| **"Việc của tôi" (ADR-053)** | ✅ | Mới 2026-08-05. `GET /tasks/my` — endpoint xuyên dự án đầu tiên. Trang `/my-work` gom theo dự án, đổi view được |
| **Đường ghi hồ sơ cá nhân (ADR-054)** | ✅ | Mới 2026-08-06. `PUT /auth/profile` + `POST /auth/change-password`, cả hai phát lại token qua `IssueSession`. Đổi mật khẩu thu hồi phiên KHÁC, giữ phiên hiện tại. Frontend: sửa tên tại chỗ + dialog đổi mật khẩu ở `/profile` |
| **Kỹ thuật DB: index/view/2 SP/trigger/constraint (ADR-055)** | ✅ | Mới 2026-08-06. Migration `AddReportingDbObjects`. Kèm sửa lỗi có sẵn dạng mới: thêm trigger vào `Tasks` làm MỌI ghi qua EF 500 cho tới khi khai `HasTrigger` |
| **Nhóm báo cáo: backlog insight + velocity + timeline (ADR-056)** | ✅ | Mới 2026-08-06. `GET /projects/{id}/reports/{backlog-insight,velocity,timeline}`. Ba tab/route riêng trên FE (không còn dồn vào một tab "Báo cáo") |
| Real-time (SignalR) | ⬜ | Có chủ đích — chỉ làm sau khi core CRUD ổn định (xem §6) |

### Lộ trình các phiên tiếp theo

> Sắp theo thứ tự phụ thuộc và giá trị, không phải theo độ khó. Cập nhật 2026-08-03 (phiên
> Backend hoàn chỉnh). **Toàn bộ hạng mục backend đã xong** — mọi việc còn lại là frontend.

| # | Hạng mục | Trạng thái | Ghi chú |
|---|---|---|---|
| ~~1~~ | ~~Frontend — Project detail + Board/Backlog Kanban + Sprint~~ | ✅ 2026-08-02 | 6 màn hình, kéo–thả cập nhật lạc quan, dark mode |
| ~~1b~~ | ~~`Description` + mã task `PMS-12`~~ | ✅ 2026-08-03 | ADR-033/034. Bảng đếm riêng, backfill đã kiểm tay |
| ~~2~~ | ~~Watcher + Label + TaskLink API~~ | ✅ 2026-08-03 | ADR-036/037/038 |
| ~~3~~ | ~~Background job task quá hạn~~ | ✅ 2026-08-03 | ADR-040. `GetOverdueAsync` không tái dùng được — cần bản có `Include` |
| ~~4~~ | ~~Activity Log API đọc~~ | ✅ 2026-08-03 | Kèm sửa lỗi `ProjectService` không ghi log dòng nào |
| ~~5~~ | ~~Dashboard thống kê — **API**~~ | ✅ 2026-08-03 | ADR-039. Màn hình Recharts vẫn còn |
| ~~6~~ | ~~Reset password~~ | ✅ 2026-08-03 | ADR-041 |
| — | **Attachment** (ngoài kế hoạch ban đầu) | ✅ 2026-08-03 | ADR-035. Chuyển từ §14 Nhóm B lên core theo yêu cầu |
| — | **Chốt vai trò `SystemAdmin`** | ✅ 2026-08-03 | ADR-042 |
| ~~7~~ | ~~Frontend — Lời mời + chi tiết Task + Notification bell~~ | ✅ 2026-08-03 | ADR-043/044. Kèm sửa lỗi mất mô tả ở `PUT /tasks/{id}` |
| ~~8~~ | ~~Frontend — Dashboard (Recharts) + Quên/đặt lại mật khẩu~~ | ✅ 2026-08-04 | ADR-047 |
| ~~9~~ | ~~Frontend — nhóm Admin~~ | ✅ 2026-08-04 | **Bốn** màn, không phải ba: thêm màn Phân quyền |
| ~~A~~ | ~~Authorization — claim kiểu Permission~~ | ✅ 2026-08-04 | ADR-045. Đã chốt cả bốn điểm căng trước khi gõ code |
| — | **Vá nợ backend + lệch múi giờ** | ✅ 2026-08-04 | ADR-046 / 046b. Kèm sửa endpoint thống kê hỏng 500 từ ngày viết |
| — | **Backend tầng 3 — ba trên bốn** | ✅ 2026-08-04 | ADR-048. `Project.Status` có đường ghi · `GET /employees?search=` · @mention (server lọc id, đã mutation test) |
| ~~9b~~ | ~~Frontend — ba tính năng ADR-048 + ba lỗ hổng UI~~ | ✅ 2026-08-05 | Cả sáu: `Project.Status` có nút, ô tra nhân viên khi mời, @mention (kèm `reconcileMentions` + 6 test), nút "Mở trang riêng" điều hướng cứng, `/profile` chỉ đọc (**ADR-049**), sidebar theo ngữ cảnh dự án. **Kèm sửa một lỗi có sẵn: `UserMenu` SẬP khi mở** — xem ADR-049 |
| ~~10~~ | ~~Vòng đời Sprint~~ | ✅ 2026-08-05 | **ADR-050 đã cài đặt.** `Sprint.Status {Planned/Active/Completed}` + `CompletedAt` + migration · `POST /sprints/{id}/start` (tối đa MỘT sprint Active mỗi project) · `/completion-preview` · `/complete` **hỏi task chưa xong đi đâu**. UI: tab Sprint kiểu Jira, thu/mở, dòng task inline, dialog đóng sprint |
| ~~—~~ | ~~**Cột board tuỳ biến**~~ | ✅ 2026-08-05 | **ADR-052** — ngoài kế hoạch ban đầu, theo yêu cầu. Thêm/sửa/xóa/đổi thứ tự cột, thu cột, xóa cột bắt buộc chọn cột đích. **Thay thế ADR-021** (gỡ ma trận chuyển trạng thái) |
| ~~—~~ | ~~**"Việc của tôi" xuyên dự án**~~ | ✅ 2026-08-05 | **ADR-053** — `GET /tasks/my`, trang `/my-work` gom theo dự án, đổi được view (theo dự án / theo hạn) |
| ~~—~~ | ~~**Đường ghi hồ sơ cá nhân**~~ | ✅ 2026-08-06 | **ADR-054** — `PUT /auth/profile` + `POST /auth/change-password`, phát lại token, `/profile` hết chỉ-đọc |
| ~~11~~ | ~~**Nhóm báo cáo kiểu Jira**~~ | ✅ 2026-08-06 | **ADR-056** — backlog insight + velocity + timeline, cả ba xong. FE tách thành ba tab/route riêng thay vì dồn chung |
| ~~12~~ | ~~**Áp kỹ thuật DB**~~ | ✅ 2026-08-06 | **ADR-055** — index · view · 2 stored procedure · trigger · CHECK constraint, migration `AddReportingDbObjects` |
| 13 | **Real-time (SignalR)** | ⬜ | Theo §6, chỉ làm sau khi core CRUD **và** frontend đã ổn định. Cố ý KHÔNG làm ở phiên 2026-08-06 |
| 14 | **Elasticsearch + Redis** | ⬜ | Định hướng xa. Elasticsearch là lời giải thật cho "Search toàn cục"; Redis cho cache + rate limit phân tán |

#### ✅ Hạng mục A đã chốt và đã làm — xem ADR-045

Khối "bốn điểm căng phải chốt trước khi gõ code" từng nằm ở đây đã hoàn thành nhiệm vụ của
nó: cả bốn câu hỏi đều được trả lời **trước** khi viết dòng code đầu tiên, và câu trả lời
nằm trong ADR-045 (§15). Tóm tắt để khỏi phải lật:

| | Đã chốt |
|---|---|
| (a) Quyền project-scoped vào claim? | **KHÔNG.** Mô hình lai — tầng 2 giữ nguyên đọc DB mỗi request, không sửa một dòng |
| (b) Đặt tên & thay policy? | `resource:action`, 5 mã, nguồn là hai **bảng DB** seed bằng `HasData`; tên policy == mã quyền; hai policy cũ xóa hẳn |
| (c) Chống god mode? | Danh mục **ĐÓNG** + `SystemPermissionsCatalogTests` với 4 phép kiểm độc lập, đã mutation test |
| (d) Bản sao ở frontend? | `lib/tasks/permissions.ts` **không đổi** (tầng 2); tầng 1 là file mới đọc `EmployeeDto.permissions`. FE không giải mã JWT |

**Tiến độ, nói thẳng (cập nhật 2026-08-03):** rủi ro của phiên trước — "frontend bị backend
chặn" — **đã gỡ hết**. Không còn một màn hình nào phải chờ API.

Rủi ro nay đổi hình dạng lần nữa, và lần này nó **chỉ còn một chiều**: toàn bộ giá trị còn
lại nằm ở frontend, và khối lượng đó không nhỏ — bốn tới năm màn hình, trong đó **chi tiết
Task** là màn phức tạp nhất của cả sản phẩm (mô tả, nhãn, người theo dõi, liên kết, lịch sử,
comment, đính kèm — bảy khối trên một trang).

**Ba thứ phiên này phát hiện mà không ai đi tìm** — đều là code đã build sạch, test xanh,
tài liệu ghi ✅, nhưng sai:
1. **`ProjectService` không ghi `ActivityLog` dòng nào.** Tạo/sửa/xóa project đều không sinh
   log. Chỉ lộ ra khi dựng màn "lịch sử project" và thấy nó trống rỗng.
2. **`ValidationFilter` không bao giờ chạy cho upload** — nó tra `IValidator<IFormFile>`,
   thứ không tồn tại. Thiết kế dựa vào FluentValidation cho whitelist file sẽ để ngỏ toàn bộ
   cửa **mà vẫn trông như đã khóa**.
3. **§10 mô tả một quyền mà code chưa từng có** (SystemAdmin read-only toàn hệ thống) — lần
   thứ tư dự án gặp hình dạng lỗi này, nhưng là lần đầu **code mới là bản đúng**.

Cả ba đều thuộc đúng một lớp mà §15 đã đặt tên từ 2026-07-30: *"build sạch, test xanh, ADR
ghi ✅" vẫn có thể là ba lời khai sai cùng lúc.* Điểm chung của chúng: thứ cần kiểm chứng
chưa có ai gọi tới.

**Khoản nợ kiểm chứng của phiên này:** backfill trong migration **không** được test nào chạm
(factory luôn `EnsureDeleted` + `Migrate` trên DB rỗng), nên đã kiểm **bằng tay** trên một
database tạm dựng đúng hình dạng dữ liệu cũ — gồm cả hai task có `CreatedAt` giống hệt nhau
và một task đã xóa mềm. Kết quả đúng như thiết kế.

### Việc còn dang dở — đọc trước khi bắt đầu phiên mới

> Liệt kê thẳng, không giấu. Mục đích: phiên sau **không phải dò lại**, và không viết lại
> thứ đã có. Cập nhật 2026-08-03 (phiên chi tiết Task).

#### A. Code ĐÃ VIẾT nhưng chưa có màn nào dùng

Không phải code chết cần xóa — chúng đúng, có test ở chỗ cần thiết, và là thứ màn hình
tương ứng sẽ cần ngay. Nhưng phải biết là **đang có sẵn** để khỏi viết lại:

| Thứ đã có | Ở đâu | Màn hình sẽ dùng |
|---|---|---|
| ~~`useProjectStatistics`~~ | — | ✅ đã dùng ở tab **Thống kê** (2026-08-04) |
| ~~`forgotPassword` / `resetPassword`~~ | — | ✅ đã dùng ở hai màn mật khẩu (2026-08-04) |
| ~~`useSystemAuditLogs`~~ | — | ✅ đã dùng ở **/admin/audit-logs** (2026-08-04) |
| ~~`useCreateLabel` / `useUpdateLabel` / `useDeleteLabel`~~ | — | ✅ đã dùng ở **/admin/labels** (2026-08-04) |
| `useProjectActivity` | `lib/hooks/use-activity.ts` | Tab **lịch sử của project** (chi tiết Task đã dùng bản của task, chưa ai dùng bản project) |
| `useProjectAttachments`, `useUploadProjectAttachment` | `lib/hooks/use-attachments.ts` | Đính kèm ở cấp **project** (cấp task đã dùng rồi) |
| `getCurrentUser` (`GET /auth/me`) | `lib/api/endpoints/auth.ts` | Chưa ai gọi — phiên khôi phục bằng `/auth/refresh`. ⚠️ Nếu về sau dùng tới thì nhớ nó dựng DTO **từ claim**, không đọc DB (ADR-045) |
| `listProjectTasks` (phân trang, `sortBy` name/priority/status) | `endpoints/tasks.ts` | Màn **danh sách task** dạng bảng. Nay đã có một người dùng: `useProjectTaskOptions` (ô chọn task khi tạo liên kết) |
| `mayFailUnpredictably(status)` | `lib/tasks/status-transitions.ts` | Đã dùng ở `task-status-control.tsx` để soạn thông điệp riêng cho nước đi có thể 409 |

✅ **`AdminEmployees` nay đã có đủ tầng dữ liệu** (viết mới 2026-08-04): `types/admin.ts`,
`lib/api/endpoints/admin.ts`, `lib/hooks/use-admin.ts`, `adminEmployeeKeys` +
`rolePermissionKeys` trong `keys.ts`. Đã **kiểm chứng trên trình duyệt** rằng `search` ở
`GET /admin/employees` thật sự chạy (tìm ra 1 trong 9 tài khoản bằng một phần email) — nó là
một trong hai endpoint hiếm hoi như vậy, đừng đem khuôn này áp sang project/task/sprint.

#### B. Màn hình chưa làm — không màn nào bị chặn

~~1. Dashboard thống kê~~ · ~~2. Quên/đặt lại mật khẩu~~ · ~~3. Nhóm Admin~~ — **cả ba đã
xong 2026-08-04.** Không còn màn hình nào trong lộ trình ban đầu ở trạng thái ⬜.

**Còn lại:**

> ✅ **Cả mục 1 và mục 2 dưới đây đã XONG 2026-08-05.** Giữ lại nguyên văn làm hồ sơ vì phần
> mô tả bẫy vẫn còn giá trị tra cứu; trạng thái mới nằm ở hạng mục 9b của bảng lộ trình và ở
> ADR-049. ⚠️ **Cập nhật 2026-08-05 tối:** mục E (vòng đời Sprint) nay cũng đã XONG (ADR-050).
> Việc còn lại của §1 chỉ còn **mục 3 (search toàn cục)**.

1. ~~🆕 **Ba tính năng backend đã xong mà frontend chưa có gì (ADR-048, 2026-08-04).**~~
   ✅ **đã dựng 2026-08-05.** Không bị chặn, không cần quyết định nào — chỉ là chưa dựng.
   Ghi ra đây vì cả ba đều **vô hình** nếu chỉ nhìn UI: backend có đường đi, người dùng không
   có nút.

   | Backend đã có | Frontend hiện tại |
   |---|---|
   | `POST /projects/{id}/complete` + `/reopen` (PM-only) | Không nút nào — `Project.Status` **không đổi được từ UI**, dù nó nằm trong DTO và là khóa `sortBy`. `reopen` trả **409** nếu project chưa `Done` |
   | `GET /employees?search=` (mọi người đã đăng nhập) | Ô mời thành viên vẫn bắt gõ **đúng email** bằng tay. Từ khóa **≥ 2 ký tự**, ngắn hơn trả **400** |
   | `mentionedEmployeeIds` trong comment | `types/comment.ts` đã có trường, **chưa có ô chọn**. Client **gửi id**; server **không** parse `@tên` từ nội dung |

2. ~~🆕 **Ba chỗ "bấm vào không thấy gì" — rà giao diện 2026-08-05.**~~ ✅ **đã gỡ cả ba
   cùng ngày.** Cộng một chỗ thứ tư không ai đi tìm: **menu người dùng SẬP khi mở** (ADR-049).

   a. 🔴 **Nút "Mở trang riêng" trong dialog chi tiết Task là một no-op.**
      `components/tasks/task-detail-header.tsx` dùng `<Link>` trỏ tới
      `/projects/{id}/tasks/{taskId}` — nhưng khi dialog đang mở, **URL hiện tại đã đúng là
      chuỗi đó** (intercepting route `(.)` giữ nguyên đường dẫn, ADR-043). Soft navigation
      tới chính URL đang đứng không đổi router state, nên dialog ở nguyên đó.
      Sửa bằng **điều hướng cứng** (`<a href>` thường, hoặc `window.location.assign`):
      intercepting route **chỉ** áp cho soft navigation, một lần tải trang đầy đủ sẽ render
      trang thật. Đây là cái bẫy cấu trúc của ADR-043, không phải lỗi cẩu thả.

   b. **Không có trang hồ sơ cá nhân.** `UserMenu` chỉ có mục Đăng xuất; không route nào
      trong `app/`. ⚠️ Và backend **chưa có đường sửa hồ sơ**: `AuthController` chỉ có
      `GET /auth/me`, không có `PUT /employees/me`, không có đổi mật khẩu khi đã đăng nhập
      (chỉ `forgot-password` qua email). Trang **chỉ đọc** làm được ngay; muốn sửa được thì
      phải làm backend trước — và nhớ `/auth/me` **dựng DTO từ CLAIM chứ không đọc DB**, nên
      đổi tên sẽ không hiện ra cho tới khi token được làm mới. Đó là một quyết định cần
      **ADR riêng**, không phải chi tiết cài đặt.

   c. **Sidebar chỉ còn 4 mục** và không phản ánh phạm vi sản phẩm khi đang ở trong một dự án.
      🔴 **Đính chính một khẳng định SAI trong `components/layout/sidebar.tsx`:** comment ở đó
      nói `AppShell` "không biết project nào đang mở vì nó nằm TRÊN segment `[id]`".
      Không đúng với client component — `SidebarNav` **đã** gọi `usePathname()`, mà hàm đó
      trả **toàn bộ** đường dẫn kể cả `[id]`. Rút id bằng regex trên pathname là hợp lệ và
      **không** dính rủi ro "hai tab nói dối" mà comment lo: đó là rủi ro của **store**, còn
      URL thì vốn đã thuộc về từng tab. Hướng đúng: một khối theo **ngữ cảnh dự án** hiện khi
      pathname khớp `/projects/{id}/*` (Bảng · Backlog · Sprint · Thống kê · Thành viên) cộng
      danh sách dự án gần đây. Sửa luôn comment đó, đừng để nguyên một lý do sai.

3. **Search toàn cục** — ⬜ và **vẫn chưa có API**.

   ⚠️ **Đính chính quan trọng (2026-08-04).** Tài liệu này từng ghi "chỉ `EmployeeRepository`
   và `NotificationRepository` dùng tới `?search=`" — **SAI**. Đã kiểm từng repository:
   **5/6 vẫn luôn lọc thật** (Employee theo Name/Email, Notification và Comment theo
   Content, Task và Project theo Name). Repository DUY NHẤT bỏ qua là `ActivityLogRepository`
   — và nó đã được sửa 2026-08-04 (ADR-046), nên **hiện KHÔNG còn ngoại lệ nào**.

   Điều còn đúng: `?search=` chỉ lọc **một trường** ở phần lớn endpoint (Task chỉ theo `Name`,
   không theo mã `PMS-12` hay `Description`; Project chỉ theo `Name`, không theo `Key`), nên
   nó **không thay thế được** một chức năng tìm kiếm toàn cục thật sự. Lời giải đúng cho mục
   này là Elasticsearch (hạng mục 14 trong lộ trình), không phải nới `?search=`.

#### C. Nợ kỹ thuật đã biết

- **Không có trường thứ tự task ở bất kỳ đâu.** Sắp xếp thẻ trong cùng một cột (kiểu
  Trello) là **không làm được** — cần thêm cột rank + endpoint reorder ở backend. Đây là lý
  do đã cố ý bỏ `@dnd-kit/sortable`. *Màn chi tiết Task không cần nó: subtask và liên kết
  hiển thị theo thứ tự backend trả.*
- ~~Tạo subtask chưa có giao diện~~ ✅ **xong 2026-08-03** — `TaskFormDialog` nhận thêm prop
  `parentTaskId`; khi tạo subtask thì ẩn ô Sprint và gửi `sprintId: null` (repository lọc
  board/backlog theo `ParentTaskId == null` nên sprint của subtask là giá trị vô hình).
- **Nhãn vẫn là dữ liệu TOÀN CỤC** (ADR-037). Ô chọn nhãn ở chi tiết Task vì vậy liệt kê nhãn
  của cả hệ thống, không lọc theo project — đúng thiết kế hiện tại, nhưng sẽ khó dùng khi số
  nhãn tăng. Cách sửa gốc (`Label.ProjectId`) đã ghi ở ADR-037.
- **`ProjectMembers.CreatedAt` VÀ `Employees.CreatedAt` của dữ liệu cũ là `0001-01-01`** —
  cùng nguyên nhân với `Tasks.CreatedAt` ở ADR-033 (migration thêm cột với `defaultValue`).
  Trước đây chỉ ghi `ProjectMembers`; **bổ sung `Employees` 2026-08-04**, khi màn
  `/admin/employees` hiện "—" ở cột Ngày tạo cho *mọi* dòng seed. Đã kiểm: bản ghi **mới**
  (đăng ký thật) có `CreatedAt` đúng, nên `ApplyAuditFields` không hỏng — đây thuần túy là nợ
  dữ liệu của DB dev, và một lần seed lại từ đầu sẽ ra đúng. Frontend đã chặn ở tầng hiển thị
  (`isSentinelDate` trả `—` cho mọi mốc trước năm 1900); **chưa** backfill ở DB.
- **Màu nhãn của dữ liệu cũ đều là `#6B7280`** — cùng hình dạng: migration thêm cột `Color`
  với giá trị mặc định, còn `DbSeeder` thì đặt năm màu khác nhau nên DB seed lại từ đầu sẽ
  đúng. Đã sửa dữ liệu dev bằng chính màn `/admin/labels` vừa dựng.
- **Sprint không có `RowVersion`** → sửa đồng thời là last-write-wins, không có tín hiệu.
  Đừng dựng UI cảnh báo stale cho sprint.
- **`invalidateQueries(projectDataKeys.all)` hơi rộng**: chuyển task sang sprint cũng làm
  mới cả danh sách thành viên. Có chủ đích — đổi an toàn lấy chính xác, và danh sách thành
  viên nhỏ. Nếu sau này thấy chậm thì mới tách nhỏ khóa.
- **`seq-12-refresh-token`** mới có `.mmd`, chưa sinh `.drawio`/`.png` (máy chưa cài
  draw.io Desktop). Lệnh ở `docs/uml/README.md`.
- Hai file `backend/postman/.../Login|Logout.request.yaml` đang có thay đổi **chưa commit**
  — chỉ là đổi ký tự xuống dòng do công cụ Postman sinh ra, không phải thay đổi nội dung.

#### E. Backend tầng 3 — ✅ **CẢ BỐN ĐÃ XONG** (2026-08-05)

✅ **Đã làm 2026-08-04:**

1. **`Project.Status` nay đổi được** — `POST /projects/{id}/complete` + `/reopen`, PM-only,
   có ActivityLog và thông báo cho thành viên. Trước đó `Project.Complete()` có đúng MỘT
   caller trong toàn bộ solution (`DbSeeder`), nên mọi project tạo qua API vĩnh viễn ở `ToDo`
   trong khi `Status` vẫn nằm trong DTO và vẫn là khóa `sortBy` — một **trường chết đội lốt
   tính năng**. `Complete()` idempotent; `Reopen()` đưa về `InProgress` chứ không về `ToDo`
   (project từng chạy tới Done thì công việc đã diễn ra) và trả **409** nếu chưa Done.

   > 🔴 Dùng `NotificationType.**ProjectStatusChanged**` chứ không phải `StatusChanged`:
   > `RelatedEntityKind` được SUY RA từ `Type` (ADR-025), và `StatusChanged` suy ra `Task` —
   > dùng nhầm sẽ khiến chuông điều hướng tới `/tasks/{projectId}`, một id không tồn tại.
   > Đây đúng là loại lệch mà việc suy ra (thay vì lưu hai cột) sinh ra để chặn.

2. **`GET /employees?search=`** — tra nhân viên cho ô gợi ý khi mời thành viên, mở cho mọi
   người đã đăng nhập. Trước đó chỉ có `GET /admin/employees` sau quyền `employees:manage`,
   nên PM bình thường phải gõ **đúng email** bằng tay.

   > 🔴 Ba ràng buộc là **lý do nó được phép tồn tại**, không phải chi tiết cài đặt: từ khóa
   > **≥ 2 ký tự** (một ký tự khớp phần lớn danh bạ, lặp 26 lần là có toàn bộ), **trần kết
   > quả cứng ở server** (không nhận từ client), và DTO **chỉ ba trường** — không
   > `systemRole`, không `isLocked`. Có test khẳng định trên **JSON thô**, vì deserialize vào
   > record sẽ âm thầm bỏ qua trường thừa và test vẫn xanh. Chỉ trả người chưa bị khóa.

3. **@mention trong comment** — client gửi `mentionedEmployeeIds`, server **không parse
   `@tên`** từ nội dung (tên hiển thị không phải định danh: trùng tên, đổi tên, `@abc` có thể
   chỉ là một mẩu email).

   > 🔴 **Nhưng server BẮT BUỘC lọc lại.** Id do client gửi, nên không lọc nghĩa là bất kỳ ai
   > cũng bắn được thông báo tới bất kỳ ai bằng cách nhét id lạ vào body — người nhận sẽ thấy
   > tên một task thuộc dự án họ không có quyền mở. Vừa rò rỉ, vừa là kênh quấy rối. Chỉ giữ
   > thành viên `Accepted`. Người được nhắc bị loại khỏi lượt `CommentAdded` để không nhận
   > hai thông báo cho cùng một hành động. **Đã mutation test**: bỏ bộ lọc làm 2 test đỏ.
   >
   > Nhắc tên người ngoài dự án vẫn trả **thành công** (bình luận hợp lệ, chỉ phần nhắc tên
   > bị lọc bỏ) — trả 400 sẽ tiết lộ "id này có tồn tại nhưng không thuộc dự án", tức lại là
   > rò rỉ.

✅ **Mục thứ tư — vòng đời Sprint — XONG 2026-08-05 (ADR-050).**

Đã có: `Sprint.Status {Planned/Active/Completed}` + `CompletedAt` + migration ·
`POST /sprints/{id}/start` (bất biến: tối đa **MỘT** sprint `Active` mỗi project, 409 nếu vi
phạm) · `GET /sprints/{id}/completion-preview` · `POST /sprints/{id}/complete` **hỏi task
chưa xong đi đâu** (`targetSprintId: null` = Backlog, và đó là lựa chọn hợp lệ chứ không
phải "chưa chọn").

⚠️ **Migration cố ý KHÔNG backfill** — mọi sprint cũ ở `Planned`. Đặt `Active` theo ngày sẽ
phá bất biến "một sprint đang chạy" bằng dữ liệu; đặt `Completed` phải bịa `CompletedAt`, mà
đó chính là mốc velocity đo theo. Người dùng bấm "Bắt đầu" một lần cho sprint hiện tại — một
thao tác thật, thay cho một lịch sử giả.

<details>
<summary>Mô tả gốc của hạng mục (giữ làm hồ sơ thiết kế)</summary>

`Sprint` không có trường trạng thái nào; `IsActive` suy từ ngày. Không có start / complete /
đẩy task chưa xong sang sprint kế — tức **vòng lặp Scrum cốt lõi**.

Cắt ra khỏi phiên 2026-08-04 **có ý thức**, không phải bỏ quên: nó cần cột `Sprint.Status` +
migration, và trước đó cần một **quyết định sản phẩm** mà nhiều câu trả lời đều bảo vệ được —
*task chưa xong đi đâu khi đóng sprint?* (về Backlog · sang sprint kế · hỏi người dùng lúc
đóng, như Jira). Đó là lựa chọn của người sở hữu sản phẩm chứ không phải của người viết code,
nên **phải chốt và viết ADR riêng trước khi gõ dòng đầu tiên**.

⚠️ **Phụ thuộc:** hạng mục "velocity" của nhóm báo cáo kiểu Jira **cần cái này trước** —
không có mốc "đóng sprint" thì không có gì để đo tốc độ theo.

</details>

📌 **Hai món nợ khác, ghi rõ chứ không im lặng:**
- **`AuthService` không ghi một dòng `ActivityLog` nào.** Đăng ký, đăng xuất và **đặt lại mật
  khẩu** đều nằm ngoài nhật ký kiểm toán, chỉ có Serilog dạng văn bản. Với một hệ thống làm
  báo cáo thực tập ngân hàng thì đây là bề mặt đáng ghi nhất mà lại chưa phủ. `ActivityAction`
  cũng chưa có member nào cho nhóm này.
- **`RMG020` bị tắt ở CẢ 11 mapper.** Đó đúng là analyzer sẽ bắt một field entity mới không
  tới được DTO — tức đúng lớp lỗi mà dự án đã gặp nhiều lần. Bật lại từng mapper một là việc
  của một phiên dọn dẹp riêng.

#### D. Chưa kiểm chứng bằng tay

**✅ Đã trả trong phiên 2026-08-03** (kiểm thật trên trình duyệt, tài khoản seed):
- **Giao diện màn hình nhỏ** — chi tiết Task ở 375×812: hai cột xếp chồng đúng, sidebar thu
  về hamburger, `scrollWidth === clientWidth` (không tràn ngang).
- **Quyền theo vai trò thật, không suy luận** — đăng nhập `dung@pms.local` (`Viewer`) trên
  chi tiết Task: nút **Theo dõi CÓ** và bấm được thật (ngoại lệ ghi duy nhất của ADR-036),
  còn tải file lên / gắn nhãn / tạo liên kết / soạn comment / đổi trạng thái / sửa mô tả
  đều **không hiện**; nút tải file **VỀ** vẫn có (đọc đi qua `ProjectAction.View`).
- **Luồng lời mời khép kín** — `em@pms.local` có lời mời `Pending` trong seed: badge sidebar
  → Chấp nhận → vào thẳng board.
- **Vòng 409 của `RowVersion`** — sửa từ một client thứ hai rồi sửa trên UI: 409 → banner +
  tự tải lại → **lần thứ hai thành công** (chứng minh không rơi vào 409 vĩnh viễn).
- **Bốn mã lỗi upload** — `.exe` → 415; file EXE đổi tên thành `.png` → **400 magic number**;
  file hợp lệ → tải lên rồi tải về được (Blob `application/octet-stream`, ADR-035).
- **`LinkType` chuẩn hóa (ADR-038)** — `Blocks(A→B)` rồi `IsBlockedBy(B→A)` → **409** với
  thông điệp riêng; xem từ B thì cùng một hàng hiện là `IsBlockedBy`. UI còn chặn sớm hơn:
  ô chọn task lọc bỏ task đã có liên kết nên nước đi đó **không tạo ra request nào**.

**Vẫn CHƯA kiểm:**
- Kéo–thả bằng **cảm ứng** thật (đã cấu hình `TouchSensor` với `delay: 220`, chưa thử trên
  thiết bị thật).
- Kéo–thả bằng **bàn phím** thật. ⚠️ Phiên 2026-08-03 **không** kiểm được: công cụ trình
  duyệt của phiên này không bắn được sự kiện chuột/bàn phím tổng hợp tới `<button>` thường
  (mọi thao tác phải gọi `.click()` bằng JS), nên một kết quả "kéo bằng bàn phím OK" sẽ
  không đáng tin. Để nguyên là nợ thay vì báo sai.

---

## 2. Tech Stack

| Thành phần | Lựa chọn | Lý do |
|---|---|---|
| Ngôn ngữ | C# (.NET 8) | Thể hiện OOP rõ ràng, có nền tảng sẵn |
| Backend Framework | ASP.NET Core Web API | Ổn định, hiệu suất tốt, tooling mạnh |
| ORM | Entity Framework Core (Code-First + Migrations) | Mapping object ↔ table tự nhiên, đúng yêu cầu đề bài |
| Database | SQL Server thông qua Rosetta 2 tích hợp trong OrbStack | Quan hệ dữ liệu phức tạp (Project-Task-Employee) |
| Authentication | JWT Bearer Token | Chuẩn công nghiệp cho REST API |
| API Docs | Swagger / OpenAPI (Swashbuckle) | Tự động sinh doc, tiện demo báo cáo |
| Logging | Serilog | Structured logging, dễ giám sát |
| Testing | xUnit + NSubstitute + Shouldly | Unit/Integration test cho Service layer — xem ADR-009 (§15) về lý do bỏ Moq/FluentAssertions |
| Containerization | Docker *(tùy chọn, giai đoạn sau)* | Dễ deploy, dễ trình bày |
| CI/CD | GitHub Actions *(tùy chọn, giai đoạn sau)* | Build + test tự động |
| Source Control | Git (GitHub/GitLab) | Branching: `main` / `dev` / `feature/*` |
| **Frontend** | **Next.js 15 (App Router, TypeScript) + TailwindCSS 4 + shadcn/ui** | Type-safety khớp tinh thần OOP backend. **Next 15 chứ không phải 16** — xem ADR-031. App Router dùng cho routing/layout, không dùng SSR data-fetching — xem ADR-028 |
| **Data Fetching** | **TanStack Query 5** | Caching, loading state, đồng bộ API chuẩn công nghiệp |
| **State Management** | **Zustand 5** | Client state (phiên đăng nhập, filter, modal). Store auth **không persist** — ADR-027 |
| **Form + Validation (FE)** | **react-hook-form + Zod 4** | shadcn/ui v4 đã bỏ component `form` khi chuyển sang Base UI nên nối tay; schema Zod soi gương FluentValidation phía backend |
| **Data Visualization** | **Recharts** | Dashboard thống kê (mục "Thống kê" trong mindmap) — *chưa cài, làm cùng màn Dashboard* |
| **Real-time** | **SignalR** (tích hợp sau khi core ổn định) | Cập nhật task/project real-time giữa nhiều người dùng, không cần F5 |
| **Background Job** | **Hangfire** *(hoặc BackgroundService của .NET)* | Chạy job định kỳ check task quá hạn để sinh Notification |
| **Validation** | **FluentValidation** | Tách luật validate khỏi Controller/DTO, dễ unit test, hỗ trợ rule async (vd check email đã tồn tại). Free, Apache 2.0 |
| **Object Mapping** | **Mapperly (Riok.Mapperly)** | Map Entity ↔ DTO tại compile-time (source generator), free MIT, không reflection lúc chạy — thay cho AutoMapper đã chuyển thương mại |

---

## 3. Kiến trúc tổng thể

Áp dụng **Layered Architecture** (đơn giản hóa từ Clean Architecture, phù hợp quy mô
đồ án nhưng vẫn thể hiện tư duy tách lớp chuyên nghiệp):

```
┌─────────────────────────────────────────┐
│           API Layer (Controllers)       │  ← Nhận request, trả response (DTO)
├─────────────────────────────────────────┤
│         Application Layer (Services)    │  ← Business logic
├─────────────────────────────────────────┤
│   Infrastructure Layer (Repositories,   │  ← Truy cập dữ liệu, EF Core
│         DbContext, External services)   │
├─────────────────────────────────────────┤
│           Domain Layer (Entities)       │  ← Model nghiệp vụ thuần (Project, Task,
│                                         │     Employee, Status enum...)
└─────────────────────────────────────────┘
```

**Nguyên tắc phụ thuộc:** Domain không phụ thuộc vào bất kỳ layer nào khác.
API → Application → Infrastructure → Domain (dependency đi vào trong).

### Design Patterns áp dụng
- **Repository Pattern**: trừu tượng hóa truy cập dữ liệu, tách biệt Service khỏi EF Core
- **Unit of Work**: đảm bảo tính toàn vẹn khi 1 nghiệp vụ chạm nhiều repository
- **Dependency Injection**: dùng built-in DI container của .NET
- **DTO (Data Transfer Object)**: tách biệt Entity (database model) khỏi dữ liệu trả về API. Mapperly là công cụ hiện thực bước "tách Entity ↔ DTO"

---

## 4. Cấu trúc thư mục (dự kiến)

```
ProjectManagementSystem/
├── backend/
│   ├── src/
│   │   ├── PMS.Domain/              # Entities, Enums, Interfaces cốt lõi
│   │   ├── PMS.Application/         # Services, DTOs, business logic, interfaces
│   │   ├── PMS.Infrastructure/      # DbContext, Repositories, EF Migrations
│   │   └── PMS.API/                 # Controllers, Program.cs, appsettings, SignalR Hubs
│   └── tests/
│       ├── PMS.UnitTests/
│       └── PMS.IntegrationTests/
├── frontend/                        # hiện trạng 2026-07-31, không còn là dự kiến
│   ├── app/                         # App Router. KHÔNG dùng src/
│   │   ├── layout.tsx               # Providers (TanStack Query) + Toaster
│   │   ├── (auth)/                  # route group — tên trong ngoặc KHÔNG vào URL
│   │   │   ├── layout.tsx           #   GuestOnly: đã đăng nhập thì đẩy vào /projects
│   │   │   ├── login/ register/
│   │   └── (app)/                   # mọi trang cần đăng nhập
│   │       ├── layout.tsx           #   AuthGuard + AppShell (đặt 1 lần, không lặp)
│   │       └── projects/
│   ├── components/
│   │   ├── ui/                      # shadcn/ui (Base UI ở v4, không còn Radix)
│   │   ├── auth/ form/ layout/ projects/
│   ├── lib/
│   │   ├── api/                     # ⭐ tầng API client — mọi màn hình đi qua đây
│   │   │   ├── config.ts            #   base URL, đường dẫn auth, ngưỡng refresh
│   │   │   ├── problem.ts           #   ProblemDetails -> ApiError (4 hình dạng lỗi)
│   │   │   ├── refresh.ts           #   🔴 single-flight refresh (ADR-030)
│   │   │   ├── http.ts              #   apiFetch: JWT, retry 401, 204
│   │   │   └── endpoints/           #   auth.ts, projects.ts
│   │   ├── hooks/ validation/       # TanStack Query hooks; schema Zod
│   │   └── format.ts form.ts utils.ts
│   ├── store/                       # Zustand — auth-store.ts (KHÔNG persist)
│   └── types/                       # soi gương DTO backend 1-1 (ADR-029)
├── docs/
│   ├── uml/                     # Class diagram, Use case, Sequence diagram
│   ├── erd/
│   └── ARCHITECTURE.md
└── README.md
```

---

## 5. Domain Model (tóm tắt — chi tiết xem Class Diagram)

> 📌 Đã mở rộng từ mindmap gốc để đạt mức "sản phẩm dùng được thật", không chỉ dừng
> ở CRUD đơn thuần. Các entity in đậm là bổ sung mới so với bản đầu tiên.

### Entity cốt lõi
- **Project** ✅ *(CRUD + phân quyền + soft delete đã chạy được qua API)*: Tên, Mô tả tổng quan, Thời gian dự kiến hoàn thành, Status, `IsDeleted`, `DeletedAt` *(Soft Delete)*
- **Task** ✅ *(CRUD + Subtask + Workflow + giao việc đã chạy được qua API)* (kể cả Subtask qua self-reference): Tên, thuộc Project nào, **thuộc Sprint
  nào (nullable — null = Backlog)**, Due Date, Status, `Priority` (`Highest`/`High`/
  `Medium`/`Low`/`Lowest`), `ReporterId` (người tạo/báo cáo task — khác với người được
  assign làm), **cờ IsOverdue (tính toán, không lưu cứng)**, `IsDeleted`, `DeletedAt`
  *(Soft Delete)*
- **Employee** ✅ *(qua Auth — xem §10)* (Nhân sự / User): Tên, Email, Password hash, Chức vụ (System Role)
- **Status**: Enum dùng chung cho Project/Task: `ToDo`, `InProgress`, `Review`, `Done`

> 📌 **Reporter vs Assignee** (theo mô hình Jira): `Reporter` là người tạo/báo cáo task
> (thường là PM hoặc bất kỳ ai phát hiện việc cần làm), `Assignee` (qua `TaskAssignment`)
> là người thực sự thực hiện — 2 vai trò tách biệt, có thể là 2 người khác nhau hoặc
> cùng 1 người.

> 📌 **Soft Delete**: Project/Task khi "xóa" chỉ đánh dấu `IsDeleted = true`, không xóa
> cứng khỏi database. Lý do: giữ nguyên vẹn `ActivityLog`/`Comment` liên quan (audit trail),
> và cho phép khôi phục nếu xóa nhầm. EF Core dùng Global Query Filter để tự động ẩn
> record đã xóa khỏi mọi query mặc định.

### Entity phân quyền (Role 2 tầng) ✅ *(đã hoạt động thật cho Project, có test)*
- **`ProjectMember`** *(bảng trung gian Employee–Project, thay cho quan hệ N–N đơn thuần)*:
  `EmployeeId`, `ProjectId`, `RoleInProject`, `JoinedDate` *(nullable — chỉ set khi Accept)*,
  `InvitationStatus` (`Pending` / `Accepted` / `Declined`)
  → Đây là nơi quyết định 1 người làm PM ở project này nhưng chỉ là Member ở project khác.

  **Luồng mời nhân sự vào Project:**
  - PM mời bằng email → nếu email đã có tài khoản, tạo `ProjectMember` với
    `InvitationStatus = Pending`, sinh `Notification` cho người được mời
  - Người được mời chấp nhận → `InvitationStatus = Accepted`, chính thức có quyền
    theo `RoleInProject`
  - Nếu email chưa có tài khoản trong hệ thống: hiển thị thông báo "chưa có tài khoản,
    người này cần đăng ký trước" — **không tự tạo tài khoản hộ** (tránh phát sinh tài
    khoản rác không ai sở hữu)

### Entity giao việc ✅ *(`TaskAssignmentService` đã hoạt động, có test)*
- **`TaskAssignment`** *(bảng trung gian Employee–Task, cho phép nhiều người/1 task)*:
  `TaskId`, `EmployeeId`, `AssignedDate`, `RoleInTask` (`Owner` / `Contributor`)

  **Quy tắc gán việc:**
  | Hành động | Ai được làm |
  |---|---|
  | Gán người khác vào task | Chỉ `ProjectManager` |
  | Tự nhận task về mình (self-assign) | `Member`/`ProjectManager`, chỉ khi task đang ở `ToDo` (chưa ai làm) và thuộc project mình là `ProjectMember` |
  | Tự rút khỏi task (self-unassign) | Người đang được gán tự làm, không cần PM duyệt |
  | Gỡ người khác khỏi task | Chỉ `ProjectManager` |

  Mọi hành động assign/unassign đều sinh `ActivityLog` + `Notification` cho PM, để PM
  luôn nắm được ai đang làm gì dù không tự tay gán.

  > 📌 Bảng trên đã được xác nhận bằng `seq-02-assign-task` (check `IsProjectMember`
  > trước khi assign, 403 nếu target không phải member) — không chỉ là ý định thiết kế
  > chưa kiểm chứng. Assignee **bắt buộc** là `ProjectMember` với `InvitationStatus =
  > Accepted` của đúng project chứa task đó — chặn ở `TaskAssignmentService`, không
  > phải ở domain (`Task` không có nav property tới `ProjectMember`).

### Entity Sprint/Board (nay là core, không còn là "tương lai") ✅ *(`SprintService` + endpoint Board/Backlog đã hoạt động)*
- **`Sprint`**: Tên, `ProjectId`, `StartDate`, `EndDate`, `Goal` (mục tiêu sprint ngắn)
  - 1 Project có nhiều Sprint
  - 1 Sprint có nhiều Task (qua `Task.SprintId`)
  - Task chưa gán Sprint (`SprintId = null`) = nằm ở **Backlog**

### Entity phân loại & liên kết (theo mô hình Jira thật) ✅ *(API đầy đủ từ 2026-08-03)*
- **`Label`** ✅: Tên tag tự do (`bug`, `frontend`, `urgent`) + **`Color`** (`#RRGGBB`) —
  Task N—N Label. **Toàn cục**, tên duy nhất toàn hệ thống; quyền tách theo bán kính ảnh
  hưởng (ADR-037): tạo = mọi user, gắn/gỡ = PM/Member, **sửa/xóa = chỉ SystemAdmin**
- **`Watcher`** ✅ *(bảng trung gian Employee–Task)*: `TaskId`, `EmployeeId` — người
  theo dõi task để nhận Notification dù không được assign làm (khác với `TaskAssignment`).
  ⚠️ **Không** kế thừa `BaseEntity` → có repository riêng và phải **tự set `CreatedAt`**
  (ADR-036). Là thao tác ghi duy nhất mà `Viewer` làm được
- **`TaskLink`** ✅ *(self-referencing giữa 2 Task)*: `SourceTaskId`, `TargetTaskId`,
  `LinkType` (`Blocks` / `IsBlockedBy` / `RelatesTo` / `Duplicates`) — quản lý phụ
  thuộc giữa các task. ⚠️ **`IsBlockedBy` là giá trị chỉ dùng ở ĐẦU VÀO, không bao giờ
  được lưu**: backend chuẩn hóa nó về `Blocks` đảo chiều để unique index bắt được trùng
  ngữ nghĩa (ADR-038). Có guard chặn vòng chặn
- **`Attachment`** ✅ *(mới 2026-08-03)*: file đính kèm của **Task hoặc Project** — hai FK
  nullable + CHECK constraint đúng-một-chủ. `FileName` (tên gốc, chỉ để hiển thị),
  `StoredFileName` (tên trên đĩa do hệ thống sinh), `ContentType`, `SizeBytes`,
  `UploaderId`. Whitelist đuôi + kiểm magic number + thư mục ngoài `wwwroot` (ADR-035)

### Workflow Transition Rules (Status không đổi tự do) ✅ *(`TaskStatusTransitionService`, có test)*
Thay vì cho phép đổi `Status` tự do giữa 4 giá trị enum, áp dụng quy tắc chuyển trạng
thái tường minh (state machine), phản ánh đúng cách Jira dùng Workflow:
```
ToDo → InProgress → Review → Done
```
- Không được nhảy thẳng `ToDo → Done` (bỏ qua các bước)
- Được phép lùi lại (`Review → InProgress` nếu bị reject)
- Nếu Task có `TaskLink` loại `IsBlockedBy` mà task chặn chưa `Done`, không cho chuyển
  sang `InProgress`
- Logic này đặt trong Application layer (`TaskStatusTransitionService`), không phải
  if-else rải rác — dễ mở rộng quy tắc sau này, đúng tinh thần OOP (Strategy Pattern
  hoặc State Pattern có thể áp dụng ở đây)
- **Ai được gọi đổi status:** `Assignee` của chính task đó, HOẶC `ProjectManager` của
  project (override được cả task không do mình assign) — xem ADR-017 (§15). `Viewer`
  và `Member` không phải assignee thì không được.

### Subtask — là 1 Task đầy đủ, không phải checklist item đơn giản ✅ *(dùng chung endpoint tạo task, chỉ khác ở `ParentTaskId`)*
Vì `Task` tự tham chiếu chính nó (`ParentTaskId`, self-referencing), **Subtask thừa
hưởng toàn bộ khả năng của Task**, không phải một checkbox rút gọn kiểu "Trello
checklist". Cụ thể, mỗi Subtask có đầy đủ:
- `Status` riêng (theo Workflow Transition Rules ở trên) — không chỉ 2 trạng thái
  "chưa tick/đã tick"
- `Assignee` riêng (qua `TaskAssignment`) — có thể giao cho người khác với Task cha
- `Reporter`, `Priority`, `DueDate`, `Label` riêng
- **`Comment`** riêng trên Subtask (trao đổi cụ thể cho phần việc nhỏ đó)
- **`Watcher`** riêng — người khác có thể theo dõi riêng 1 Subtask mà không cần theo
  dõi cả Task cha
- **`TaskLink`** riêng — 1 Subtask có thể bị block bởi Task/Subtask khác
- Sinh `ActivityLog` riêng khi có thay đổi

Task cha chỉ khác Subtask ở việc **có `ParentTaskId = null`** — về mặt entity, Task
và Subtask dùng chung 1 class `Task`, không tách class riêng (đúng nguyên lý OOP:
tái sử dụng thay vì trùng lặp code cho 2 khái niệm về bản chất giống nhau).

**Giới hạn hợp lý:** Subtask không được có Subtask con của chính nó (chỉ 1 cấp cha–con,
không đệ quy vô hạn) — tránh phức tạp hóa UI/logic không cần thiết ở quy mô hệ thống này.
Đã xác nhận trong use-case diagram (annotation trên bubble "Tạo subtask"). Enforce ở
**domain method** `Task.AddSubtask()` (từ chối nếu `this.ParentTaskId != null`), nhất
quán với cách invariant của `Project` đặt trong aggregate root thay vì Service (ADR-012).

### Subtask — Progress Bar, không tự động đóng Task cha ✅ *(`TaskItem.SubtaskProgress`, trả trong `TaskDetailResponse`)*
Theo đúng hành vi mặc định của Jira (đã xác nhận): Subtask có Status/Assignee độc
lập với Task cha, nhưng **Task cha không tự động chuyển sang `Done` dù mọi subtask
đã `Done`** — Reporter/PM/người phụ trách Task cha vẫn phải tự tay đóng Task cha.
Lý do: Task cha có thể còn việc khác ngoài các subtask đã liệt kê (review tổng thể,
tổng hợp kết quả...).
- **Progress bar**: Task cha hiển thị % subtask đã `Done` / tổng số subtask (tính
  toán, không lưu cứng — tương tự `IsOverdue`)
- Không cần thêm logic tự động trong `TaskStatusTransitionService` cho việc này —
  chỉ cần 1 hàm tính `SubtaskProgress` ở tầng Application để hiển thị lên UI

### Entity cộng tác (Nhóm A — core) *(Comment ✅ và Notification ✅ đã có API đầy đủ từ phiên 2026-07-30 (tiếp); ActivityLog vẫn ⬜ — ghi đủ nhưng chưa có API đọc)*
- **`Comment`** ✅ *(CRUD đã chạy được qua API)*: `TaskId`, `EmployeeId` (người viết),
  `Content`, `CreatedAt`
  → Quyền theo ADR-026: đọc = mọi thành viên kể cả `Viewer`; viết = `ProjectManager`/`Member`;
    sửa = **chỉ tác giả**; xóa = tác giả hoặc `ProjectManager`. Xóa **cứng** (nhất quán
    ADR-012), audit trail do `ActivityLog` đảm nhiệm.
  → `CommentConfiguration` có `HasQueryFilter(c => !c.Task.IsDeleted)`: comment của task đã
    xóa mềm tự biến mất khỏi mọi query — bảo đảm bằng cấu trúc, không service nào phải nhớ lọc.
- **`ActivityLog`** ⬜ *(ghi đủ qua `IActivityLogger` nhưng chưa có API đọc)*:
  `EntityType` (Project/Task), `EntityId`, `EmployeeId` (người thực hiện),
  `Action` (Created/Updated/StatusChanged/Assigned/Commented...), `Detail`, `CreatedAt`
  → Dùng để hiển thị lịch sử thay đổi trên Task/Project (audit trail). Đây là khoảng trống
    còn lại **cùng loại** với khoảng trống mà Notification vừa đóng: dữ liệu đã ghi đủ ở mọi
    luồng nhưng không có đường nào đọc ra. Xem hạng mục 4 của bảng lộ trình §1.
- **`Notification`** ✅ *(API đọc/đánh dấu đã đọc đã chạy được)*: `EmployeeId` (người nhận),
  `Type` (TaskAssigned/DueSoon/CommentAdded/...), `Content`, `IsRead`, `CreatedAt`,
  `RelatedEntityId`
  → Sinh ra bởi các sự kiện: được gán task, đổi trạng thái task, có comment mới trên task
    mình theo dõi, các luồng mời/đổi vai trò/gỡ thành viên. Task sắp đến hạn (`DueSoon`) còn
    chờ background job — hạng mục 3 của bảng lộ trình §1.
  → `RelatedEntityKind` (`Project`/`Task`/`None`) là property **computed** suy ra từ `Type`,
    không lưu thành cột (ADR-025) — client dùng cặp (Kind, Id) để điều hướng khi bấm vào
    thông báo.
  → Là **ngoại lệ hợp lệ duy nhất** của phân quyền project-scoped (ADR-023): chỉ lọc theo
    `EmployeeId`, không đi qua `IProjectAuthorizationService`.

### Quan hệ tổng hợp
- Project 1—N Task
- Project 1—N Sprint
- Sprint 1—N Task (qua `SprintId` nullable)
- Task 1—N Task (subtask, self-referencing, tùy chọn)
- Task N—N Task (qua `TaskLink`, khác mục đích với quan hệ subtask)
- Task 1—N Comment
- Task 1—N Attachment · Project 1—N Attachment *(đúng một trong hai, CHECK constraint)*
- Project 1—1 ProjectTaskCounter *(bộ đếm mã task — ADR-033)*
- Employee 1—N PasswordResetToken *(ADR-041)*
- Task N—N Label
- Task N—N Employee (qua `Watcher` — theo dõi, khác với `TaskAssignment` — thực hiện)
- Employee N—N Project (qua `ProjectMember`, có `RoleInProject`)
- Employee N—N Task (qua `TaskAssignment`, có `RoleInTask`)
- Employee 1—N Task (là `Reporter` — người tạo task)
- Employee 1—N Comment, 1—N Notification, 1—N ActivityLog (là người thực hiện)

> 📌 Ghi chú: các quan hệ trên là bản nháp, sẽ chốt chính thức khi hoàn thành ERD/Class Diagram.

---

## 6. Kiến trúc Frontend

**Stack:** Next.js 15 (App Router, TypeScript) + TailwindCSS 4 + shadcn/ui + TanStack Query 5
+ Zustand 5 + react-hook-form + Zod 4 + `@dnd-kit` (Kanban) + **Recharts 3** (Thống kê) + Vitest

> Từ 2026-07-31 mục này mô tả **hiện trạng**, không còn là dự kiến. Năm quyết định nền
> tảng nằm ở ADR-027 → ADR-032 (§15).

**Font chữ: IBM Plex Sans** (`next/font/google`, weight 400/500/600/700, subset
`latin` + `vietnamese`).

> 🔴 **Hai cái bẫy về font, cả hai đều hỏng IM LẶNG:**
>
> 1. **Bộ `vietnamese` là bắt buộc.** Geist mặc định của scaffold đã bị loại vì thiếu dấu
>    ở một số ký tự tổ hợp (`ế ệ ỗ ữ`…) — lỗi chỉ lộ ở vài từ nên rất dễ lọt. Trước khi
>    đổi font, kiểm `subsets` trong
>    `next/dist/compiled/@next/font/dist/google/font-data.json`.
> 2. **Class `.variable` của `next/font` phải đặt trên `<html>`, KHÔNG phải `<body>`.**
>    Nó định nghĩa `--font-sans`, mà `globals.css` lại `@apply font-sans` ở tầng `html`.
>    Đặt ở `<body>` thì lúc `<html>` tính `font-family` biến chưa tồn tại → giá trị không
>    hợp lệ → trình duyệt rơi về **Times New Roman**, và `<body>` thừa kế luôn cái đó.
>    Không lỗi, không cảnh báo, chỉ là cả ứng dụng dùng font serif.
>    **Bug này đã tồn tại từ phiên dựng scaffold cho tới khi phát hiện ngày 2026-08-02** —
>    mọi ảnh chụp giao diện trước mốc đó đều là Times New Roman.
>
> Cách kiểm nhanh trong console, đừng tin mắt:
> ```js
> getComputedStyle(document.documentElement).fontFamily  // phải ra tên font thật
> ```

**Cấu trúc phân lớp Frontend** (đúng như đã vẽ từ đầu, nay đã dựng thật):
```
UI Components (shadcn/ui — Base UI ở v4)
      ↓
Pages / App Router (Next.js)          <- routing + layout, KHÔNG fetch (ADR-028)
      ↓
Custom Hooks (TanStack Query — cache, loading/error state)
      ↓
API Client (lib/api — JWT, single-flight refresh, map ProblemDetails)
      ↓
Backend API (ASP.NET Core)
```

**Quản lý state:**
- **Server state** (Project, Task, Employee): TanStack Query — cache, refetch, invalidate
- **Client state** (phiên đăng nhập, filter, modal): Zustand. Store auth **không persist**
  (ADR-027) — access token chỉ sống trong bộ nhớ

**Xác thực — tóm tắt, chi tiết ở ADR-027:**

| | Lưu ở đâu | Ai đọc được | Vì sao |
|---|---|---|---|
| Refresh token (7 ngày) | Cookie `HttpOnly; Secure; SameSite=Strict; Path=/api/v1/auth` | Chỉ trình duyệt + backend | Một lỗ XSS mà đọc được nó là chiếm phiên vĩnh viễn qua rotation |
| Access token (15 phút) | Bộ nhớ (Zustand, không persist) | JS của chính trang | Mất khi F5 — đổi lại bằng một lượt `/refresh` |

Mọi endpoint nghiệp vụ xác thực bằng header `Authorization: Bearer` chứ không bằng cookie,
nên chúng **miễn nhiễm CSRF**; cookie chỉ đi tới 4 endpoint auth nhờ `Path`.

**Các trang chính:**
- ✅ Trang đăng nhập / đăng ký
- ✅ Danh sách Project + tạo / **sửa / xóa** (đủ luồng `RowVersion` 409-tải-lại của ADR-016
  và luồng 409 "còn task chưa hoàn thành" của ADR-008)
- ✅ Chi tiết Project — bốn tab là **segment định tuyến thật** (`[id]/board|backlog|sprints|members`),
  không phải tab state: mỗi tab giữ query riêng (`?sprint=`), chia sẻ link được, Back đúng
- ✅ **Board Kanban** kéo–thả (`@dnd-kit`) + cập nhật lạc quan, **cột do người dùng cấu
  hình** (ADR-052): thêm/sửa/xóa/đổi thứ tự qua dialog "Quản lý cột", thu từng cột, cuộn
  ngang khi nhiều cột.
  ⚠️ **Ba bẫy 409 mà `useDroppable disabled` từng chặn nay chỉ còn MỘT.** Sau ADR-052 mọi
  cột đều là đích hợp lệ, nên `disabled` chỉ còn loại trừ chính cột nguồn. Guard duy nhất
  không đoán trước được: cột đích thuộc nhóm `InProgress` khi task đang bị chặn
- ✅ **Backlog** — chuyển task vào Sprint bằng **menu**, không kéo–thả: `PUT /tasks/{id}/sprint`
  không có ngữ nghĩa vị trí nên kéo–thả sẽ hứa một thứ tự mà backend không lưu được
- ✅ **Tab Sprint kiểu Jira** (ADR-050) — mỗi sprint thu/mở được, mở ra hiện danh sách task
  inline (mã · tên · chip trạng thái · hạn · người đảm nhận). Nút **Bắt đầu** / **Đóng
  sprint** theo trạng thái; dialog đóng **hỏi task chưa xong đi đâu**.
  📌 Task của sprint chỉ nạp khi sprint ĐANG MỞ (`useBoard(..., { enabled })`) — một project
  vài chục sprint mà nạp hết là vài chục request cho dữ liệu không ai nhìn
- ✅ Quản lý Sprint (CRUD đầy đủ) + quản lý Thành viên (mời / đổi vai trò / gỡ / rời dự án)
- ✅ **"Việc của tôi"** (`/my-work`, ADR-053) — màn hình duy nhất KHÔNG nằm dưới
  `/projects/{id}`, và là màn duy nhất trả lời được *"sáng nay tôi cần làm gì"*. Gom theo dự
  án hoặc xếp phẳng theo hạn
- ✅ Dark mode + màu thương hiệu (xanh Jira) + breadcrumb trên header
- ✅ **Chi tiết Task** — hai vỏ dùng chung một `TaskDetailContent` (ADR-043): dialog chặn
  route khi bấm thẻ từ board/backlog, trang thật `/projects/{id}/tasks/{taskId}` khi mở link
  hoặc F5. Bố cục hai cột: trái là mô tả (sửa tại chỗ), Subtask (progress bar + tạo subtask +
  mỗi subtask mở ra như một Task đầy đủ), Tệp đính kèm, Liên kết, rồi cụm
  `Bình luận | Lịch sử` (tab **cục bộ**, không phải segment định tuyến); phải là cột dính
  gồm trạng thái, người đảm nhận + tự nhận việc, độ ưu tiên, hạn, nhãn màu, người theo dõi,
  mã `PMS-12`, người tạo
- ✅ **"Lời mời của tôi"** (`/invitations`) — khép kín luồng mời thành viên
- ✅ **Notification bell** (góc header) + trang `/notifications` có lọc `Tất cả | Chưa đọc`.
  Điều hướng bằng cặp `(relatedEntityKind, relatedEntityId)` (ADR-025); thông báo loại Task
  đi qua trang phân giải `/tasks/{taskId}` vì DTO thông báo không mang `projectId`
- ✅ **Quên / đặt lại mật khẩu** — `forgot-password` hiện đúng MỘT thông điệp cho mọi kết quả
  (ADR-041); `reset-password` đọc token từ query, mọi lỗi token là cùng một 400, thành công
  thì luôn về `/login` vì mọi phiên đã bị thu hồi
- ✅ **Tab Thống kê** của dự án (Recharts) — ba nhóm màu theo VIỆC, thẻ số và thanh mức cố ý
  không phải biểu đồ (ADR-047). Cả ba vai trò xem được (ADR-039)
- ✅ **Nhóm Quản trị** (`/admin`, bốn tab) — Nhân sự · Phân quyền · Nhãn toàn cục · Nhật ký
  hệ thống. Gác bằng **quyền** chứ không bằng `systemRole` (ADR-045)
- ⬜ **Thanh Search/Filter** toàn cục: tìm task theo tên, người phụ trách, status, deadline.
  ⚠️ Chưa có API, và `?search=` hiện tại chỉ lọc MỘT trường ở mỗi endpoint nên không thay
  thế được — lời giải đúng là Elasticsearch (§1, hạng mục 14)

- ✅ **Hồ sơ cá nhân** (`/profile`) — **CHỈ ĐỌC** (ADR-049). Tên, email, vai trò hệ thống và
  danh sách quyền tầng 1. Không có nút Sửa, và đó là quyết định chứ không phải việc chưa làm

> 🆕 **Sidebar ĐỔI HẲN theo ngữ cảnh, kiểu Jira** (2026-08-05, bản thứ hai trong ngày).
>
> | Đang ở đâu | Sidebar hiện gì |
> |---|---|
> | Ngoài dự án (`/projects`, `/notifications`, `/admin`, `/profile`…) | Nav toàn cục: Dự án · Lời mời · Thông báo · Quản trị |
> | Trong một dự án (`/projects/{id}/*`) | **Chỉ của dự án đó**: link "Tất cả dự án" · đầu đề dự án · **LẬP KẾ HOẠCH** (Bảng · Backlog · Sprint · Thống kê) · **QUẢN LÝ** (Thành viên) |
>
> **Vì sao đổi ngay sau khi vừa dựng bản trước:** bản đầu (sáng cùng ngày) giữ nav toàn cục
> **cộng** khối dự án **cộng** danh sách "Dự án của tôi" — tức **ba đường tới cùng một chỗ**,
> trong khi trang mặc định sau đăng nhập vốn đã là `/projects`. Sidebar dài gần hết cột mà
> phần lớn là lối đi trùng nhau. Jira giải bằng cách **tách hai ngữ cảnh**, và đó là lý do
> sidebar của họ ngắn mà vẫn đủ.
>
> 📌 Danh sách **"Dự án của tôi"** đã bỏ hẳn — nó là đường thứ ba và là đường thừa nhất.
> (Nó cũng từng phải mang nhãn "của tôi" thay vì "gần đây" vì `ProjectRepository` không có
> khóa sắp xếp theo thời gian nào; nay thì câu hỏi đó không còn đặt ra nữa.)
>
> ⚠️ **Bỏ nav toàn cục khỏi tầm mắt thì phải trả lại đường về ở chỗ NHÌN THẤY ĐƯỢC.** Có
> breadcrumb trên header rồi vẫn thêm link "Tất cả dự án" ở đầu sidebar: breadcrumb là thứ
> người ta đọc khi đã biết mình đang tìm gì, không phải thứ đập vào mắt khi đang lạc.
>
> 📌 Dòng phụ dưới tên dự án là **VAI TRÒ của chính người đang xem** (`useMyProjectRole`),
> không phải trạng thái dự án — trạng thái đã nằm ngay trên header trang, còn vai trò thì
> không hiện ở đâu khác, và nó là câu trả lời cho *"vì sao tôi không thấy nút Sửa"*.
>
> Các mục đọc từ hằng `PROJECT_SECTIONS` **dùng chung với `ProjectTabs`** — chép tay sang hai
> nơi thì thêm một khu vực ở tab sẽ âm thầm để sidebar thiếu một mục.
>
> 🔴 **Đính chính một khẳng định sai từng nằm ở đây** (bản 2026-08-04): *"Bảng Kanban /
> Backlog KHÔNG BAO GIỜ đặt được ở sidebar vì `AppShell` không biết project nào đang mở —
> nó nằm TRÊN segment `[id]`"*. **Sai với client component.** `SidebarNav` vốn đã gọi
> `usePathname()`, mà hàm ấy trả **toàn bộ** đường dẫn kể cả `[id]`; vị trí của component
> trong cây layout không giới hạn cái nó đọc được từ URL.
>
> Cảnh báo đi kèm — *đừng nhớ "project vừa mở" vào store, giá trị đó nói dối khi mở hai tab*
> — thì **vẫn đúng nguyên**. Nhưng nó là rủi ro của **store**, và khẳng định cũ đã gộp nhầm
> hai thứ để rút ra một kết luận quá rộng. **URL vốn đã thuộc về từng tab.**
>
> `href` vẫn là **bắt buộc** trong kiểu `NavItem` — không còn mục "Sắp có" nào.

**Quy ước ẩn/hiện nút theo quyền** (§10 là nguồn luật, đây là cách áp dụng):

> 🔴 **HAI TẦNG, HAI FILE, KHÔNG CHỒNG LẤN** (ADR-045) — nhầm hai cái này là dựng lại đúng
> mô hình cũ ở nửa client:
>
> | | Tầng 1 — `lib/auth/system-permissions.ts` | Tầng 2 — `lib/tasks/permissions.ts` |
> |---|---|---|
> | Phạm vi | Toàn hệ thống | Theo từng project |
> | Nguồn | `EmployeeDto.permissions` | `RoleInProject` của tôi trong project đó |
> | Soi gương | `SystemPermissions.cs` | `ProjectPermissions.cs` |
> | Đổi bằng | Màn `/admin/roles` (dữ liệu) | Đổi vai trò thành viên |
>
> Frontend **KHÔNG giải mã JWT** — quyền tới qua thân phản hồi. `hasPermission()` đọc
> `undefined` thành "không có quyền nào" (fail-closed): một tab đang mở lúc backend được
> deploy giữ `employee` cũ không có trường đó cho tới lần refresh kế, và trong 15 phút ấy
> token của họ cũng chưa có claim nên ẩn nút mới là đúng.

- Đọc `RoleInProject` từ API thành viên. **Đừng đoán quyền từ mã lỗi** — người ngoài
  project nhận **404** chứ không phải 403 (ADR-006/019), nên 404 không nói gì về quyền.
- UI **không bao giờ** hiển thị "bạn không có quyền" cho 404; phải xử lý như "không tìm thấy".
  Đã cứng hóa trong `lib/api/problem.ts`.
- Ba luật per-row không nằm trong ma trận `ProjectPermissions`, frontend phải tự áp:
  đổi status = Assignee **hoặc** PM (ADR-017); sửa comment = **chỉ tác giả**, xóa comment
  = tác giả **hoặc** PM (ADR-026).

**Real-time (SignalR):**
- Tích hợp **sau khi core CRUD đã ổn định** — không làm ngay từ đầu
- Use case: khi 1 user cập nhật Status của Task, các user khác đang xem cùng Project
  thấy thay đổi ngay lập tức mà không cần reload
- Frontend dùng `@microsoft/signalr` client kết nối tới SignalR Hub bên backend

**Type-safety giữa Frontend/Backend:** đã chốt **viết tay** ở `frontend/types/`, mỗi file
soi gương một file DTO backend (`types/auth.ts` ↔ `Features/Auth/AuthDtos.cs`). Không dùng
OpenAPI codegen — lý do và điểm chuyển đổi ở **ADR-029**.

**Chạy lần đầu trên máy mới:**
```powershell
dotnet dev-certs https --trust     # để trình duyệt tin backend https://localhost:7264
mkcert -install                    # để `next dev --experimental-https` sinh được cert
cd frontend; cp .env.example .env.local; npm install; npm run dev
```
⚠️ Cả hai lệnh trên **bắt buộc**, không phải tùy chọn — xem khối "Bắt đầu phiên mới" ở
đầu tài liệu để biết vì sao (schemeful same-site).

---

## 7. API Design Standards

### Pagination & Sorting ✅
Mọi endpoint trả về danh sách (Project, Task, Notification, ActivityLog...) đều dùng
chuẩn phân trang, tránh trả toàn bộ dữ liệu 1 lần:
```json
{
  "items": [...],
  "page": 1,
  "pageSize": 20,
  "totalCount": 154,
  "totalPages": 8
}
```
Hỗ trợ query param `?page=&pageSize=&sortBy=&sortDirection=&search=` dùng chung 1
class `PagedRequest`/`PagedResult<T>` generic.

### Global Exception Handling ✅
Dùng Middleware tập trung (`ExceptionHandlingMiddleware`) bắt mọi exception chưa
xử lý, trả về format lỗi theo chuẩn `ProblemDetails` (RFC 7807) — không tự chế format
riêng để Swagger/client hiểu sẵn:
```json
{ "title": "...", "status": 400, "traceId": "..." }
```
### Audit Fields ✅
`BaseEntity` mang `CreatedAt` (bắt buộc) và `UpdatedAt` (nullable), được đóng dấu tập
trung trong `PmsDbContext.ApplyAuditFields()`, gọi SAU `ApplySoftDelete()` để bản ghi
xóa mềm (đã bị đổi state `Deleted → Modified`) vẫn nhận được `UpdatedAt`.
Trước đây mỗi entity tự khai báo `CreatedAt` rời rạc (`Notification`, `Comment`,
`RefreshToken`) và `ActivityLog` dùng tên `Timestamp` — nay gom về một chỗ.

### API Versioning ✅
Route đã theo chuẩn `/api/v1/...` ngay từ đầu — tránh phải đổi route sau này nếu API
thay đổi breaking. *(Hiện dùng tiền tố route tĩnh, chưa cài package `Asp.Versioning.Mvc`
— chỉ cần khi cần v2 song song với v1.)*

### CORS Policy ✅
Policy `PmsFrontend` liệt kê origin tường minh qua `Cors:AllowedOrigins` — **không** dùng
`AllowAnyOrigin`. Dev hiện có `http://localhost:5173`, `http://localhost:3000` và
`https://localhost:3000` (bản https là cái Next.js thật sự chạy).

**`.AllowCredentials()` là bắt buộc kể từ ADR-027**: thiếu nó thì trình duyệt vứt bỏ
`Set-Cookie` ở phản hồi cross-origin và không đính cookie vào request sau — luồng refresh
hỏng **im lặng**. `AllowAnyOrigin` + `AllowCredentials` là tổ hợp bị chuẩn CORS cấm, nên
việc đã liệt kê origin tường minh từ đầu hóa ra là điều kiện cần để làm được cookie auth.

Có `CorsPolicyTests` (5 fact) giữ, gồm một fact riêng cho `Access-Control-Allow-Credentials`.
⚠️ Đừng sửa phần CORS trong `Program.cs` mà không chạy lại bộ test này — xem đính chính
2026-07-30 cuối §15 để biết cấu hình này từng hỏng im lặng suốt nhiều phiên như thế nào.

### Cấu hình Secrets
- **Local dev**: `dotnet user-secrets` cho connection string, JWT secret — không commit
  vào `appsettings.json`
- **Production**: biến môi trường (Environment Variables) hoặc Azure Key Vault/AWS
  Secrets Manager nếu deploy cloud
- File `appsettings.json` chỉ chứa placeholder/giá trị non-sensitive
- **Fail fast**: `JwtOptions` validate bằng `AddOptions<T>().Validate(...).ValidateOnStart()`
  — thiếu `Jwt:Secret` thì app chết ngay lúc khởi động với thông báo rõ ràng, thay vì chết
  ở request đầu tiên bằng lỗi `IDX10703` không gợi ý nguyên nhân.

### Health Check ✅
Endpoint `/health` (dùng `Microsoft.Extensions.Diagnostics.HealthChecks`) kiểm tra
API còn sống và kết nối database còn ổn — cần thiết khi có Docker/CI-CD hoặc load
balancer để biết khi nào restart instance.

### Chiến lược môi trường (Environment Strategy) ✅

| Môi trường | Mục đích | Khác biệt cấu hình |
|---|---|---|
| `Development` | Code & test local | Swagger bật, log EF Core mức Information, seed data, secrets qua user-secrets |
| `Testing` | Integration Test | Config nạp từ `AddInMemoryCollection` trong `PmsWebApplicationFactory`, DB riêng `PmsTestDb` — không dùng file `appsettings` |
| `Production` | Chạy thật/demo báo cáo | Swagger tắt, log Warning/Error, HTTPS bắt buộc, secrets qua biến môi trường |

Dùng `ASPNETCORE_ENVIRONMENT` để switch giữa các file `appsettings.{Environment}.json`.

---

## 8. Non-Functional Requirements (NFR)

| Hạng mục | Mục tiêu |
|---|---|
| **Hiệu năng (Performance)** | API response time < 300ms cho các endpoint CRUD thông thường (không tính upload file) |
| **Khả năng mở rộng (Scalability)** | Kiến trúc layered cho phép tách Frontend/Backend deploy độc lập; database có thể scale vertically trước, horizontal sau nếu cần |
| **Tính khả dụng (Availability)** | Không yêu cầu high-availability (99.9%+) ở mức đồ án, nhưng cần xử lý lỗi graceful (không crash toàn bộ khi 1 request lỗi) |
| **Bảo mật (Security)** | Tuân thủ OWASP Top 10 cơ bản (đã áp dụng ở mục Bảo mật) |
| **Khả năng bảo trì (Maintainability)** | Code tuân thủ SOLID, có Unit Test, tài liệu Swagger đầy đủ |
| **Khả năng phục hồi dữ liệu** | Soft Delete cho Project/Task, backup database định kỳ *(nếu deploy thật)* |

> 📌 Mục này rất nên đưa vào báo cáo tốt nghiệp — phần "Yêu cầu phi chức năng" là
> phần hội đồng chấm điểm thường hỏi mà sinh viên hay bỏ sót.

---

## 9. Data Seeding (cho Demo & Testing)

> ⚠️ **Đính chính 2026-08-04.** Mục này từng viết "seed qua `HasData()` **hoặc** script
> riêng" như thể hai cách thay thế được cho nhau. Chúng **không** thay thế được, và ranh giới
> giữa chúng là một quyết định kiến trúc chứ không phải sở thích:
>
> | | `HasData` | `DbSeeder` |
> |---|---|---|
> | Chạy khi | Mỗi lần `Migrate()` — **mọi** môi trường, kể cả Testing | Chỉ khi `dotnet run` ở **Development** |
> | Dùng cho | Dữ liệu là **một phần của schema** | Dữ liệu **demo** |
> | Ví dụ | `Permissions` + `RolePermissions` (ADR-045) | 6 nhân sự, 3 project, 14 task |
>
> Đặt nhầm chỗ **không** cho ra một lỗi rõ ràng: `PmsWebApplicationFactory` chỉ chạy
> `EnsureDeleted + Migrate` nên `DbSeeder` không bao giờ chạy trong test. Seed dữ liệu
> schema-level ở đó nghĩa là mọi policy trả 403 và **cả suite tích hợp đỏ cùng lúc** — hàng
> chục test chẳng liên quan gì tới thứ vừa đổi.

**Dữ liệu demo — `DbSeeder.cs`, chạy một lần lúc `dotnet run` ở Development**
(guard: bỏ qua nếu DB đã có Employee):
- 6 nhân sự, trong đó 1 `SystemAdmin` (`admin@pms.local`) — mật khẩu chung `Password123!`
- 3 project với Sprint, 14 task ở nhiều Status (có task quá hạn để demo Notification), 3
  subtask, 2 task ở Backlog
- Đủ ba `RoleInProject` (PM/Member/Viewer) cùng **một lời mời `Pending`** để demo luồng mời
- Nhãn, TaskLink, phân công, người theo dõi, Comment, ActivityLog, Notification mẫu

**Dữ liệu schema — `HasData` trong `IEntityTypeConfiguration`:** danh mục quyền và ánh xạ
vai trò → quyền (ADR-045). Thêm một mã quyền là **ba bước**: `const` → `HasData` → migration.

⚠️ **Nợ dữ liệu của DB dev đã seed từ lâu** (không phải lỗi code — seed lại từ đầu sẽ đúng):
`Employees.CreatedAt` và `ProjectMembers.CreatedAt` là `0001-01-01`, màu nhãn đều là màu mặc
định. Cùng nguyên nhân: migration thêm cột với `defaultValue`. Xem §1 mục C.

---

## 10. Bảo mật (Security) & Phân quyền

### Authentication

| Việc | Trạng thái |
|---|---|
| Self-register (Employee tự đăng ký qua form Sign Up với email/password) | ✅ Đã có (`AuthController.Register`) |
| JWT Bearer Token + refresh token: lưu DB, hash SHA-256, **rotation + reuse detection** | ✅ Đã có — mạnh hơn "cơ chế cơ bản", xem ADR §15 (2026-07-25) |
| Password hash bằng BCrypt (work factor 11), không lưu plaintext | ✅ Đã có (`BCryptPasswordHasher`) |
| Rate limiting cho endpoint đăng nhập (chống brute-force) | ✅ Đã có (`[EnableRateLimiting("login")]`) |
| HTTPS bắt buộc (`app.UseHttpsRedirection()`) | ✅ Đã có |
| Khóa/mở tài khoản, cấp `SystemAdmin` role cho người khác | ✅ Đã có — `AdminEmployeesController`, gác bằng quyền `employees:manage` (ADR-045; trước 2026-08-04 là policy `require-system-admin`). Khóa/đổi role đều thu hồi toàn bộ refresh token. Bất biến: luôn còn ≥1 SystemAdmin chưa bị khóa |
| Quên mật khẩu / Reset password qua token hết hạn 30 phút | ✅ Đã có (2026-08-03, ADR-041) — `PasswordResetToken` (hash SHA-256, dùng một lần), `POST /auth/forgot-password` **luôn 204**, `POST /auth/reset-password` gộp mọi lỗi vào một 400. Đổi mật khẩu thu hồi toàn bộ refresh token. `IEmailSender` có bản giả lập ghi Serilog cho Dev, `NullEmailSender` cho môi trường khác |
> 📌 Mục Reset password từng là ⬜ suốt nhiều phiên vì "chờ email service". Cách gỡ:
> `IEmailSender` là một abstraction hai method — cắm SMTP thật sau này chỉ là đổi một dòng
> đăng ký DI, còn nghiệp vụ token thì không phải chờ gì cả.

### Authorization — mô hình 2 tầng

> 🆕 **Cập nhật 2026-08-04 (ADR-045).** Tầng 1 nay chạy bằng **claim `permission` lấy từ hai
> bảng DB** (`Permission` + `RolePermission`), quản trị được ở `/admin/roles`. Tầng 2 **không
> đổi gì cả** — vẫn đọc `ProjectMember.RoleInProject` tươi mỗi request, vì một người có vai
> trò khác nhau ở từng project và đổi vai trò phải có hiệu lực tức thì.
>
> **Danh mục ĐÓNG, năm mã** (`PMS.Application/Common/Authorization/SystemPermissions.cs`):
>
> | Mã | Gác gì | Mặc định |
> |---|---|---|
> | `employees:manage` | `AdminEmployeesController` (list / lock / unlock / system-role) | SystemAdmin |
> | `audit:read` | `AdminAuditController` | SystemAdmin |
> | `labels:manage` | `PUT`/`DELETE /labels/{id}` (KHÔNG gác `POST`, ADR-037) | SystemAdmin |
> | `projects:create` | `POST /Projects` | **SystemAdmin + User** |
> | `roles:manage` | `AdminPermissionsController` | SystemAdmin |
>
> **Vai trò `SystemRole` nay chỉ là ĐỊNH DANH**, không còn là trục phân quyền — nó quyết định
> người này nhận tập quyền nào, chứ bản thân nó không cho phép điều gì. Hai policy cũ
> (`require-system-admin`, `can-create-project`) đã bị **xóa hẳn**; tên policy nay chính là
> mã quyền.
>
> ⚠️ **Đổi quyền không tức thì:** quyền đi trong JWT nên có hiệu lực ở token kế tiếp — tối đa
> 15 phút, và thao tác lưu thu hồi refresh token của mọi người mang vai trò đó (kể cả người
> đang bấm nút). Bất biến: `SystemAdmin` luôn giữ `roles:manage`, gỡ là **409**.
>
> 🔴 **Không mã nào được mang phạm vi project.** `projects:create` là ngoại lệ duy nhất và
> hợp lệ (lúc tạo thì chưa có project để tra membership). `SystemPermissionsCatalogTests`
> khóa điều này bằng bốn phép kiểm độc lập — đã mutation test.

**Tầng 1 — System Role** (gắn với tài khoản, không đổi theo project):
- `SystemAdmin`: quản trị **hệ thống** — khóa/mở tài khoản, cấp `SystemRole`, quản lý nhãn
  toàn cục, đọc nhật ký cấp hệ thống. **KHÔNG có bất kỳ đặc quyền nghiệp vụ nào**: không
  đọc, không ghi, không "read-only để hỗ trợ". SystemAdmin không phải thành viên của một
  project thì nhận **404** trên mọi endpoint của project đó, y hệt người ngoài. Muốn xem
  hay thao tác, phải được mời làm `ProjectMember` như người bình thường.

  > ⚠️ **Đính chính 2026-08-03 (ADR-042).** Cho tới hôm nay dòng này ghi *"Ngoại lệ:
  > SystemAdmin có quyền xem (read-only) toàn bộ project cho mục đích support/audit"* —
  > một hành vi **chưa từng có dòng code nào hiện thực**. Đã sửa **tài liệu cho khớp code**
  > chứ không phải ngược lại: quyền đọc xuyên project là "God Mode" thu nhỏ, đi ngược
  > Least Privilege, và trong một hệ thống có `Issue Security Level` ở lộ trình thì nó còn
  > là cửa hậu vô hiệu hóa luôn tầng đó. Nhu cầu chính đáng phía sau — **trách nhiệm giải
  > trình** — được đáp ứng bằng `GET /api/v1/admin/audit-logs` (chỉ ghi hành động cấp hệ
  > thống, cố định `EntityType` ở server). Hợp đồng này nay có test giữ:
  > `SystemAdminScopeTests` chạy `[Theory]` trên **16 route** project-scoped.
- `User`: nhân viên thường, chỉ thấy project mình tham gia. **Mọi `User` đều có quyền
  tạo Project mới** — khi tạo, hệ thống tự động insert `ProjectMember(EmployeeId=creator,
  RoleInProject=ProjectManager)`, người tạo tự động trở thành PM của project đó.

  > 📌 Quyền tạo project đi qua một policy riêng chứ không hardcode "mọi User đều được".
  > **Cập nhật 2026-08-04:** policy đó nay là `projects:create` lấy từ bảng
  > `RolePermissions` (ADR-045) — nghĩa là mong muốn ban đầu "đổi logic sau này mà không
  > cần sửa schema" nay còn mạnh hơn: đổi được **bằng dữ liệu**, ngay trên `/admin/roles`,
  > không cần sửa code lẫn deploy lại. Trước đó policy `can-create-project` chỉ là
  > `RequireAuthenticatedUser()` — một no-op.

**Tầng 2 — Project Role** (gắn theo từng `ProjectMember`, 1 người có thể khác role ở
project khác nhau):
| Role | Quyền hạn |
|---|---|
| `ProjectManager` | Tạo/sửa/xóa project, tạo Sprint, tạo task, gán nhân sự, xem thống kê, xóa comment/file của người khác |
| `Member` | Xem task được giao, cập nhật status task của mình, viết comment, **xem thống kê** (ADR-039), gắn nhãn, tạo liên kết task, đính kèm file, theo dõi task |
| `Viewer` | Chỉ xem, không chỉnh sửa — dùng cho stakeholder theo dõi tiến độ (cấp quản lý không trực tiếp làm việc, khách hàng/đối tác, phòng ban khác cần tham chiếu, auditor). Ngoại lệ duy nhất được GHI: **theo dõi task** (ADR-036) — nó chỉ ảnh hưởng hộp thông báo của chính họ |

> 📌 `Viewer` là actor riêng trong Use Case Diagram, chỉ có mũi tên tới các use case
> "Xem Project/Task/Thống kê" — không có bất kỳ liên kết nào tới use case ghi/sửa/xóa.

**Cách check quyền ở backend:** middleware/policy kiểm tra 2 lớp — JWT xác định
`SystemRole`, sau đó với mọi action liên quan đến 1 project cụ thể, query bảng
`ProjectMember` để lấy `RoleInProject` tương ứng rồi mới quyết định cho phép hay không
(ASP.NET Core: dùng Policy-based Authorization + Custom Authorization Handler).

### Khác
- **Input Validation** ✅: FluentValidation là cơ chế validate đầu vào — chống injection, XSS ở tầng API (đối chiếu OWASP Top 10)
- Rate limiting cho đăng nhập: xem bảng Authentication ở trên

---

## 11. Testing Strategy

- **Unit Test** (xUnit + NSubstitute + Shouldly): tập trung Service layer (business logic),
  mock Repository — xem ADR-009 (§15) về lý do chọn NSubstitute thay Moq
- **Integration Test** (xUnit + Shouldly): test API endpoint end-to-end trên SQL Server
  thật, database riêng `PmsTestDb` — xem ADR-010 (§15) về lý do không dùng EF InMemory/SQLite

### Hiện trạng (2026-08-04, sau phiên Phân quyền permission)

**489 test pass** — 232 unit + 257 integration, build 0 warning **và nay có
`TreatWarningsAsErrors`** (`backend/Directory.Build.props`), nên "0 warning" từ một quan sát
đã thành một điều kiện.
*(+70 so với phiên trước: `SystemPermissionsCatalogTests` 6, `RolePermissionAdminServiceTests`
6, `PermissionSeedTests` 5, `RolePermissionAdminTests` 11, `PermissionClaimTests` 5,
`StatisticsTests` 5, `LabelsTests` 7, `ActivityLogsTests` 6, `ProjectStatusTests` 6,
`EmployeeLookupTests` 7, `CommentMentionTests` 5, cộng một dòng nới ở `SystemAdminScopeTests`.)*

🆕 **Ba vùng trước đây KHÔNG có file test nào (10 route) nay đã có** — và việc viết chúng
lập tức có lãi: `StatisticsTests` bắt được `GET /projects/{id}/statistics` **hỏng 500 từ ngày
viết ra** ngay ở lần chạy đầu tiên (ADR-046), còn `ActivityLogsTests` khóa lại bộ lọc
`?search=` vốn bị nuốt im lặng.

⚠️ **Bài học về cách viết khẳng định:** test cho bộ lọc phải có **cả hai chiều** — "từ khóa
có thật thì số dòng GIẢM" *và* "từ khóa chắc chắn không tồn tại thì trang RỖNG". Thiếu vế
thứ hai thì một bộ lọc luôn-khớp (tức không lọc gì) vẫn làm khẳng định thứ nhất xanh.

⚠️ **Một khoảng trống test mới, ghi rõ:** không test nào so **chuỗi JSON thô** của một mốc
thời gian. Đó là lý do lỗi lệch múi giờ ở ADR-046b sống sót — test so `DateTime` với
`DateTime` thì `Kind` không ảnh hưởng tới toán tử so sánh nên mọi khẳng định đều xanh, trong
khi trình duyệt lại đọc chuỗi. Khi một giá trị đi ra ngoài dưới dạng **chuỗi**, phải có ít
nhất một test chạm vào chuỗi đó.

⚠️ **Một khoảng trống test có ý thức, ghi rõ để không ai tưởng đã được phủ:** phần
**backfill** trong migration `AddTaskCodeDescriptionLabelColorAndAttachments` **không** được
test nào chạm tới, vì `PmsWebApplicationFactory` chạy `EnsureDeleted` + `Migrate` nên nó luôn
thao tác trên database rỗng. Cách kiểm chứng duy nhất là chạy tay lên một DB có sẵn dữ liệu:
```bash
# dựng DB tạm ở migration TRƯỚC đó, chèn dữ liệu, rồi update lên mới nhất
dotnet ef database update 20260729042932_AddRowVersionAndNotificationTypeConversion
# ... chèn Projects/Tasks bằng SQL ...
dotnet ef database update
```
Đã làm việc này ngày 2026-08-03 với dữ liệu dựng đúng hình dạng dữ liệu cũ (hai task cùng
`CreatedAt = 0001-01-01`, một task đã xóa mềm) — kết quả đúng như thiết kế.

⚠️ **Chuỗi kết nối test mặc định giả định một tài khoản `sa` không có trên mọi máy.**
`PmsWebApplicationFactory` mặc định
`Server=localhost,1433;...;User Id=sa;Password=Pms@Local2026`. Máy dùng SQL Server bản cài
sẵn với Windows Authentication sẽ nhận `Login failed for user 'sa'` cho **toàn bộ** 132
integration test — trông như code hỏng nhưng thật ra là môi trường. Factory đã có sẵn
đường thoát bằng biến môi trường:
```powershell
$env:PMS_TEST_DB = "Server=localhost;Database=PmsTestDb;Integrated Security=True;TrustServerCertificate=True;Encrypt=False"
```

🔴 **Có một cách hỏng THỨ HAI cho ra đúng cùng một triệu chứng, và nó dễ chẩn đoán nhầm
hơn nhiều.** Trên máy dùng Docker (macOS/Linux), chuỗi mặc định là **đúng** — nhưng nếu
container SQL Server *vừa* được `docker start`, nó chưa nhận kết nối trong khoảng 30–60
giây đầu, và toàn bộ 199 integration test đỏ với **cùng một stack trace `AttemptOneLogin`**
như trường hợp sai mật khẩu ở trên. Đã gặp thật ngày 2026-08-04: lần chạy đầu 199/199 đỏ,
chạy lại sau vài phút **không đổi gì** thì 199/199 xanh.

Phân biệt hai ca bằng một lệnh, trước khi đi sửa chuỗi kết nối:
```bash
docker exec pms-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P '<mật-khẩu>' -C -Q "SELECT 1"
```
Lệnh này chạy được → container đã sẵn sàng, lỗi nằm ở phía host (mật khẩu/port/biến môi
trường). Lệnh này cũng hỏng → chỉ là chưa khởi động xong, **đợi rồi chạy lại**.

**Frontend đã có hạ tầng test** (Vitest, **79 test** cho `lib/api/` + logic thuần của
Kanban). Ba cổng tĩnh vẫn giữ nguyên vai trò: `tsc --noEmit`, `eslint`, và `npm run build`
(bắt được lỗi prerender mà `next dev` im lặng bỏ qua — đã bắt được thật một lỗi
`useSearchParams` thiếu `Suspense` ở trang login, và ngày 2026-08-03 là bước duy nhất xác
nhận slot song song `@modal` dựng được).

**Single-flight refresh (ADR-030) đã được kiểm chứng bằng chính module thật** ở phiên
2026-07-31, dù chưa có hạ tầng test thường trực. Cách làm: biên dịch `lib/api/**` +
`store/**` bằng `tsc` rồi chạy trên Node với `fetch` giả để **đếm** số lời gọi
`/auth/refresh` — không viết lại logic, vì bản sao chỉ chứng minh bản sao chạy đúng.
Ba request cùng nhận 401 → **đúng 1** lời gọi refresh, 6 lượt gọi nghiệp vụ (3 lần 401 +
3 lần retry), phiên vẫn sống.

Quan trọng hơn kết quả xanh: đã chạy **mutation test** — đổi `inFlight ??=` thành
`inFlight =` (tức bỏ single-flight) thì số lời gọi nhảy lên **3** và kiểm tra đỏ. Không có
bước này thì không biết test có bảo vệ được gì hay không, đúng bài học của ADR-022.

⚠️ Đáng chú ý: ở lần chạy hỏng đó, **"cả ba request thành công" vẫn PASS** — vì backend
giả không có reuse detection. Với backend THẬT thì ba lời gọi kia sẽ thu hồi sạch phiên
(đã kiểm riêng qua HTTP: gửi lại token đã xoay vòng → 401, và phiên hợp lệ **cũng** 401).
Hai nửa đó ghép lại mới thành bằng chứng đầy đủ; mỗi nửa đứng một mình đều không đủ.

Kịch bản còn **chưa** kiểm được vì browser trong môi trường phiên này không điều hướng
được tới bất kỳ origin nào (kể cả trang ngoài): giữ phiên khi F5, guard chuyển hướng
không nháy nội dung, và thao tác kéo/bấm thật trên UI. Phần HTML thì đã kiểm gián tiếp
bằng cách gọi thẳng dev server — `/projects` khi chưa đăng nhập chỉ trả skeleton, không
lộ `accessToken` lẫn `pms_refresh_token`.

| Nhóm | Unit | Integration |
|---|---|---|
| Domain (invariant, state machine) | `ProjectTests`, `ProjectMemberTests`, `TaskItemTests`, `SoftDeletableContractTests`, `NotificationTests` | — |
| Auth / Admin | `EmployeeAdminServiceTests` | `AccountLockingTests` |
| Project | `ProjectServiceTests`, `ProjectMemberServiceTests`, `ProjectPermissionsTests` | `ProjectsCrudTests`, `ProjectsAuthorizationTests`, `ProjectsDeleteTests`, `ProjectMembersTests` |
| Task / Sprint | `TaskServiceTests`, `TaskStatusTransitionServiceTests`, `TaskAssignmentServiceTests`, `SprintServiceTests` | `TasksCrudTests`, `TasksAuthorizationTests`, `TaskStatusTransitionTests`, `TaskAssignmentTests`, `SubtaskTests`, `SprintsCrudTests`, `BacklogAndBoardTests` |
| Notification / Comment | `NotificationFeedServiceTests`, `CommentServiceTests` | `NotificationsTests`, `CommentsTests` |
| Hạ tầng API (cấu hình pipeline) | — | `CorsPolicyTests`, `EnumSerializationTests`, `AuthCookieTests` |

Nhóm cuối đáng ghi lại lý do: **cấu hình pipeline cũng là quyết định kiến trúc và cũng cần
test giữ**. `CorsPolicyTests` sinh ra sau khi phát hiện CORS đã bị vô hiệu hóa im lặng suốt
nhiều phiên dù ADR ghi ✅ — không test nào đỏ, build không warning, vì middleware không tìm
thấy policy thì chỉ log rồi đi tiếp (xem đính chính ở §15).

`AuthCookieTests` (2026-07-31) theo đúng khuôn đó cho ADR-027, và lặp lại cùng một bài học
ở một hình dạng mới: nó đọc **thẳng chuỗi `Set-Cookie`** thay vì dùng `CookieContainer`,
vì `CookieContainer` nuốt mất thuộc tính cookie và thậm chí không enforce `SameSite` — test
đi qua nó sẽ vẫn xanh dù ai đó tháo mất `HttpOnly` hay `Secure`. *Test đi qua cùng lớp
trừu tượng với thứ cần bảo vệ thì không bảo vệ được lớp đó* — đã đúng với `EnumSerialization`
(phải đọc raw JSON), nay đúng thêm lần nữa với cookie.

**Không đặt mục tiêu % coverage.** Lý do: chỉ số đó thưởng cho test chạm nhiều dòng, trong
khi thứ dự án này cần bảo vệ là **các quyết định kiến trúc** — và ba lần lỗi thật đã lộ ra
đều là lỗi mà coverage cao vẫn bỏ sót (Sprint thiếu `ISoftDeletable` ở ADR-008, `RowVersion`
chỉ có ở schema ở ADR-016, `AddAssignee` không sinh `Id` ở phiên 2026-07-30). Thay vào đó
áp dụng **quy tắc: mỗi ADR phải có ít nhất một test giữ nó**, và test đặt tên nêu rõ quyết
định đang bảo vệ. Hai test kiểu "hợp đồng kiến trúc" đang làm đúng việc này:
`SoftDeletableContractTests` (mọi entity khai soft delete phải implement `ISoftDeletable`) và
`ProjectPermissionsTests.Moi_gia_tri_ProjectAction_phai_duoc_khai_bao_tuong_minh` (thêm
`ProjectAction` mới mà quên khai báo quyền thì đỏ ngay).

### Kiểm thử thủ công (Postman)

Collection ở `backend/postman/collections/PMS Endpoints v1/`, đã có **18 request cho
Task + Sprint** khớp 1-1 với 18 endpoint. Ba điểm dễ vấp khi chạy tay:

1. **Enum truyền bằng TÊN** kể từ ADR-022 — `"priority": "Medium"`, `"target": "Done"`,
   `"role": "Member"`. Giá trị hợp lệ: `Status` = `ToDo`/`InProgress`/`Review`/`Done`;
   `Priority` = `Highest`/`High`/`Medium`/`Low`/`Lowest`; `RoleInProject` =
   `ProjectManager`/`Member`/`Viewer`; `RoleInTask` = `Owner`/`Contributor`; `SystemRole` =
   `User`/`SystemAdmin`; `NotificationType` xem `PMS.Domain/Enums/NotificationType.cs`.
   Chiều request **vẫn nhận số** nên collection cũ không vỡ, nhưng response thì luôn trả
   tên — đừng viết test script Postman so sánh với số.
   Gửi `{"target": "ToDo"}` cho đổi status nghĩa là "chuyển sang ToDo" → task đang ToDo sẽ
   nhận **409** vì đứng yên không phải chuyển đổi hợp lệ.
2. **`rowVersion` là chuỗi base64**, copy nguyên từ response `GET /tasks/{id}` gần nhất.
   Gửi rỗng → 400 (validator), gửi giá trị cũ sau khi đã sửa một lần → 409 (ADR-021).
3. **Trường nullable phải gửi `null`**, không gửi chuỗi `"string"` — `sprintId`,
   `parentTaskId`, `dueDate` mà để `"string"` sẽ lỗi parse 400.
4. **`Refresh` và `Logout` không còn body** kể từ ADR-027 — refresh token đi bằng cookie
   `pms_refresh_token` mà Postman tự giữ trong cookie jar. Chỉ cần chạy `Login` hoặc
   `Register` trước là có. Biến môi trường `refresh_token` đã bỏ, `access_token` vẫn còn.
   ⚠️ Giữ `auth` **chữ thường** trong URL: `Path` của cookie phân biệt hoa thường trong khi
   route ASP.NET thì không, nên `{{api_url}}/Auth/refresh` vẫn trúng route nhưng cookie
   không được đính kèm và bạn nhận 401 không rõ nguyên nhân.
   ⚠️ Bấm Send `Refresh` **hai lần liên tiếp** sẽ kích hoạt reuse detection và thu hồi
   toàn bộ phiên — phải đăng nhập lại. Đó là hành vi đúng, không phải lỗi (xem ADR-030).

---

## 12. UML Diagrams (kế hoạch)

| Diagram | Mục đích | Trạng thái |
|---|---|---|
| Use Case Diagram | Tổng quan chức năng theo actor (SystemAdmin, ProjectManager, Member, Viewer) | ✅ Done — đã đồng bộ ADR-017 (2026-07-30). **Đính chính:** §12 trước đây ghi "box PM cần thêm bubble Cập nhật trạng thái task". Soát lại thì đó là chẩn đoán sai — diagram dùng generalization `PM --\|> Member --\|> Viewer --\|> User`, nên PM **đã** có use case đó qua kế thừa; thêm bubble trùng vào box PM mới là lỗi mô hình hóa. Cái thực sự thiếu là **điều kiện quyền**, nay đã ghi vào note của `UC_Status` (giống cách `UC_SelfAssign` ghi điều kiện "chỉ khi task đang ToDo") |
| Class Diagram | Chi tiết entity, thuộc tính, quan hệ, OOP | ✅ Done — đã đồng bộ code (2026-07-30): bỏ `SoftDelete()` khỏi **cả `Task` lẫn `Project`** (ADR-008 — không chỉ `Task` như §12 từng ghi), `IsOverdue`/`SubtaskProgress`/`Sprint.IsActive` chuyển thành property, thêm `Task.RemoveAssignee(Guid)`, sửa `Project.AddMember` → `Invite`/`ChangeMemberRole` và `GetRoleOf(Employee)` → `GetRoleOf(Guid)` cho khớp chữ ký thật |
| ERD | Thiết kế database quan hệ | Done — chưa phản ánh `RowVersion` (ADR-016) và `Notifications.Type` đã đổi sang `nvarchar` (ADR-016); không chặn code, chỉ lệch hình ảnh |
| Sequence Diagram | 11 luồng nghiệp vụ có nhánh (xem bảng đầy đủ ở `docs/uml/README.md`) | ✅ **11/11** (2026-07-30). Hai diagram mới của phiên Notification/Comment: `seq-10-read-notification` (ADR-023/024 — 404 cho thông báo người khác, idempotent lần hai, mark-all qua ChangeTracker) và `seq-11-delete-comment` (ADR-026 — ba nhánh quyền chồng nhau: 404 ngoài project / 403 không phải tác giả và không phải PM / xóa cứng). ⚠️ Bẫy mới ghi vào `docs/uml/README.md`: **không dùng `&lt;`/`&gt;` cho generic trong `.mmd`** — draw.io escape thêm một lớp nên PNG hiện nguyên văn `&lt;`; dùng `PagedResult[Notification]`. Trước 2026-07-30 (tiếp): ✅ **9/9**. `seq-01/02` (tạo task, gán người) và `seq-04/05` (mời, phản hồi lời mời) giữ nguyên — vẫn khớp code. `seq-03` vẽ lại theo ADR-017/019. Bốn diagram mới: `seq-06-self-assign-task`, `seq-07-delete-task` (ADR-018), `seq-08-move-task-sprint`, `seq-09-delete-sprint` (ADR-020). **Nguyên tắc chọn luồng để vẽ:** chỉ vẽ luồng có nhánh nghiệp vụ (guard, 403/404/409, cascade) — CRUD phẳng không vẽ lại vì `seq-01` đã là đại diện cho khuôn "authz → validate → Add → log → 1 lần SaveChanges" |

---

## 13. Quy trình phát triển

1. Use Case Diagram → Class Diagram/ERD
2. Setup project structure: backend (.NET solution theo layer, xem §4) + frontend
   (Next.js scaffold, cấu trúc thư mục theo §4)
3. Code Domain (Entity, Enum)
4. Authentication/Authorization backend (đổi lên trước — xem ADR §15, 2026-07-25 và
   ADR-006 về cơ chế phân quyền 2 tầng)
5. Code backend từng module theo nhóm function: Project → Task → Employee → Thống kê
6. Code Frontend theo từng module đã có API tương ứng: ~~Đăng nhập~~ ✅ → ~~Project (danh
   sách + CRUD)~~ ✅ → **Task/Board (Kanban + Backlog) ← đang ở đây** → Dashboard thống kê
   — làm ngay sau khi API của module đó ổn định, không đợi toàn bộ backend xong mới bắt
   đầu (§6)
7. Tích hợp Real-time (SignalR) sau khi core CRUD (backend + frontend) đã ổn định — xem §6
8. Viết Unit Test + Integration Test (backend), kiểm thử thủ công luồng chính (frontend)
9. Containerize (Docker) + CI/CD (tùy chọn)
10. Viết báo cáo song song từng giai đoạn

---

## 14. Mở rộng trong tương lai (Future Enhancements)

> Sprint/Backlog/Board **đã chuyển thành tính năng core** (xem mục 5 & 6), không còn
> nằm trong mục này. Các tính năng dưới đây vẫn giữ ở mức "làm sau" vì không ảnh hưởng
> tới trải nghiệm cốt lõi của việc quản lý dự án/task hàng ngày.

### Nhóm B — nên có, làm sau khi Core (Nhóm A) ổn định
- ~~**File attachment** trên Task~~ → ✅ **đã chuyển thành core, xong 2026-08-03** (ADR-035):
  đính kèm cho cả Task lẫn Project, có whitelist đuôi + kiểm magic number
- **Email notification** (ngoài in-app notification đã có ở core) — hạ tầng `IEmailSender`
  đã có sẵn từ ADR-041, chỉ còn cắm SMTP thật và thêm các loại email nghiệp vụ
- **Bulk actions**: chọn nhiều task, đổi status/gán người hàng loạt
- **Epic**: nhóm nhiều Task/Sprint lại thành 1 mục tiêu lớn hơn, xuyên nhiều Sprint
  (thêm 1 tầng phân cấp: Epic → Sprint → Task — chỉ nên làm khi core Sprint đã ổn định)
- **Issue Security Level**: giới hạn xem 1 Task cụ thể dù có quyền project (tầng thứ 3
  ngoài System Role + Project Role) — case nâng cao, hiếm dùng ở quy mô nhỏ
- **Advanced Search (JQL-like)**: query nâng cao kiểu `status=InProgress AND assignee=me`,
  thay vì chỉ filter theo field đơn giản

### Nhóm B+ — đã có chủ trương, chờ phiên riêng *(chốt 2026-08-04)*

Ba nhóm dưới đây không phải "nice-to-have": chúng đã được quyết định là **sẽ làm**, chỉ là
mỗi nhóm đủ lớn để chiếm trọn một phiên.

| Nhóm | Nội dung | Ghi chú khi bắt đầu |
|---|---|---|
| **Báo cáo kiểu Jira** | Backlog insight · velocity · report · timeline | ✅ **Vòng đời Sprint đã xong 2026-08-05** (ADR-050), nên velocity **hết bị chặn** — mốc đo là `Sprint.CompletedAt`. ⚠️ Gom số liệu theo `columnId`/`category`, KHÔNG theo enum (ADR-052) |
| **Kỹ thuật DB** | Trigger · stored procedure · view · index | ⚠️ Trigger đụng thẳng vào `ApplyAuditFields`/`ApplySoftDelete` của `PmsDbContext` và vào lệnh cấm bulk-update của ADR-024 — đọc cả hai trước khi viết trigger đầu tiên. View là chỗ hợp lý nhất để bắt đầu: các truy vấn tổng hợp ở `ProjectStatisticsRepository` là ứng viên sẵn |
| **Elasticsearch + Redis** | Search toàn cục · cache + rate limit phân tán | Elasticsearch là **lời giải đúng** cho "Search toàn cục" (§1 mục B) — nới `?search=` không thay thế được vì nó chỉ lọc một trường mỗi endpoint. Redis: rate limit hiện là in-memory nên không đúng khi chạy nhiều instance |

### Nhóm C — nice-to-have, chỉ làm nếu còn dư thời gian
- Dark mode
- Export báo cáo PDF/Excel
- Mobile responsive nâng cao (PWA)

**Điều kiện để triển khai:** Core (Project–Task–Employee–Sprint CRUD, Role 2 tầng,
Comment, Activity Log, Notification in-app, Auth, Test) đã hoàn thành và ổn định,
còn đủ thời gian trước deadline báo cáo.

---

## 15. Nhật ký quyết định (Architecture Decision Log)

### Task module — quyết định trước khi code

> ✅ **Đã hiện thực xong 2026-07-30.** Giữ nguyên mục này làm hồ sơ thiết kế: nó cho thấy
> quyết định nào có sẵn từ UML và quyết định nào phải chốt mới, hữu ích cho chương "Phân
> tích thiết kế" của báo cáo. Cả 5 câu đều đã có test giữ — xem §15 các ADR-017 → 021.
>
> Mục này gom lại đúng 5 câu hỏi thiết kế mà phiên trước (Auth/Project review) đã liệt
> kê là "cần quyết trước khi vào Task". Khi soát lại UML hiện có (2026-07-29), hóa ra
> 3/5 câu đã có câu trả lời sẵn — chỉ là chưa ai đối chiếu qua các diagram để viết
> tường minh vào đây. Ghi lại nguồn cụ thể để không phải suy đoán lại lần nữa.

### Đã có sẵn câu trả lời (chỉ cần implement đúng theo đã thiết kế)

| # | Câu hỏi | Trả lời | Nguồn |
|---|---|---|---|
| 1 | Assignee có bắt buộc là `ProjectMember` đã `Accepted`? | **Có** | `seq-02-assign-task`: check `IsProjectMember(projectId, employeeId)`, 403 nếu không phải; UC annotation trên bubble "Tự nhận task" |
| 2 | Subtask giới hạn 1 cấp — enforce ở đâu? | **Domain**, method `Task.AddSubtask()` | UC annotation ("tối đa 1 cấp"); nhất quán với ADR-012 (invariant trong aggregate root) |
| 3 | Sprint có tách module riêng khỏi Task không? | **Không** — build chung 1 đợt với Task | UC diagram gộp "Quản lý Sprint" + "Sắp xếp Backlog ↔ Sprint" vào cùng box PM với các use case Task khác |

### Vừa quyết định (2026-07-29, ADR-017/018 — xem chi tiết ADR ở "Log đầy đủ" bên dưới)

| # | Câu hỏi | Trả lời |
|---|---|---|
| 4 | Ai được đổi status của task? | `Assignee` HOẶC `ProjectManager` của project (ADR-017) |
| 5 | Xóa task còn subtask chưa `Done` thì sao? | Chặn **409 Conflict**, không cascade (ADR-018) |

### Diagram debt — ✅ đã dọn xong 2026-07-30 (xem chi tiết ở §12)
1. ~~`seq-03-change-status.drawio` — vẽ lại theo ADR-017~~ → đã vẽ lại, có đủ nhánh
   404 (người ngoài project), 403 (không phải Assignee/PM), 409 (blocker và nhảy bước).
2. ~~`class-diagram.drawio` — xóa `SoftDelete()` khỏi `Task`~~ → đã xóa khỏi **cả
   `Task` lẫn `Project`**, kèm 5 sửa chữ ký khác cho khớp code.
3. ~~`use-case-diagram.drawio` — thêm bubble "Cập nhật trạng thái task" vào box PM~~ →
   **chẩn đoán ban đầu sai**: PM đã có use case đó qua generalization, thêm bubble trùng
   mới là lỗi. Thay bằng ghi điều kiện quyền ADR-017 vào note của `UC_Status` — xem §12.
4. ~~(Tùy chọn) vẽ seq riêng cho luồng self-assign~~ → đã vẽ, nay là `seq-06-self-assign-task`.

**Quy trình sinh diagram (từ 2026-07-30):** nguồn thật của mỗi diagram nằm ở
`docs/uml/seq-diagram/src/*.mmd` (Mermaid) và `docs/uml/src/*.puml` (PlantUML) — text
thuần, diff được trên GitHub. File `.drawio` và `.png` đều **sinh ra** từ đó bằng draw.io
CLI, xem `docs/uml/README.md`. Trước đây nguồn chỉ nằm trong thuộc tính `mermaidData`/
`plantUmlData` nhúng trong XML nên không ai review được thay đổi thật sự là gì.

### Log đầy đủ

| Ngày | Quyết định | Lý do |
|---|---|---|
| 2026-07-20 | Chọn .NET thay vì Python | Đề bài yêu cầu OOP rõ ràng, đã có nền tảng C# |
| 2026-07-21 | Áp dụng Layered Architecture + Repository Pattern | Cân bằng giữa tính chuyên nghiệp và độ phức tạp phù hợp fresher |
| 2026-07-22 | Dùng FluentValidation + Mapperly; không dùng AutoMapper | AutoMapper từ v15 (7/2025) chuyển dual-license copyleft RPL-1.5 + thương mại (vẫn free cho giáo dục/<$5M nhưng thêm ràng buộc phải chú thích); Mapperly free MIT, compile-time, minh bạch code sinh ra — hợp báo cáo hơn |
| 2026-07-30 | Cho phép nhiều người/1 Task (Employee N–N Task qua `TaskAssignment`) | Phản ánh thực tế: task lớn thường cần nhiều người phối hợp — đã hoạt động thật, có integration test gán 2 người vào 1 task |
| 2026-07-22 | Áp dụng phân quyền 2 tầng: SystemRole + RoleInProject (`ProjectMember`) | Sát với mô hình doanh nghiệp thật, 1 người có thể khác role ở project khác nhau — đã hoạt động thật cho Project (`ProjectAuthorizationService`) |
| 2026-07-30 | Nâng Sprint/Backlog/Board từ "tương lai" thành tính năng core | Mục tiêu làm sản phẩm dùng được thật, không chỉ CRUD đơn thuần — `SprintService` + endpoint `/backlog` và `/board` đã hoạt động, có test |
| 2026-07-22 | Thêm Comment, Activity Log, Notification vào core | Đây là tính năng tối thiểu để 1 team thật sự dùng được hệ thống hàng ngày — *chỉ mới entity, chưa có Service/Controller nào* |
| 2026-07-22 | Áp dụng Soft Delete cho Project/Task | Bảo toàn ActivityLog/Comment liên quan, cho phép khôi phục — đã hoạt động thật, có test |
| 2026-07-22 | Chuẩn hóa Pagination, Global Exception Handling, API Versioning | Đạt chuẩn API production-grade, không phải sửa lại kiến trúc giữa chừng |
| 2026-07-29 | Chuẩn hóa CORS Policy | Bắt buộc trước khi Frontend gọi API thật. ⚠️ **Đính chính 2026-07-30:** dòng này từng ghi ✅ nhưng thực tế KHÔNG header CORS nào được phát ra — hai lỗi im lặng, xem đính chính chi tiết cuối §15 |
| 2026-07-22 | Thêm mục Non-Functional Requirements + Data Seeding | Phục vụ demo báo cáo trôi chảy và thể hiện đầy đủ tư duy thiết kế hệ thống — `DbSeeder` đã chạy được ở môi trường Development |
| 2026-07-22 | Mọi `User` được tạo Project, tự động thành `ProjectManager` của project đó | Tránh bottleneck xin duyệt qua SystemAdmin, khớp cách Jira/Trello vận hành thật |
| 2026-08-03 | **(ADR-042)** `SystemAdmin` tách bạch hoàn toàn: **không có đặc quyền nghiệp vụ nào**, kể cả đọc. Muốn xem/thao tác phải là `ProjectMember` như bình thường | Tránh "God Mode", giữ đúng Least Privilege. Thay dòng 2026-07-29 vốn ghi "read-only toàn hệ thống" — một hành vi tài liệu mô tả nhưng code chưa từng có. Nhu cầu giải trình chuyển sang `GET /admin/audit-logs`. Có `SystemAdminScopeTests` giữ — chi tiết bên dưới |
| 2026-07-22 | Giữ `Viewer` như 1 actor riêng trong Use Case Diagram | Phản ánh nhu cầu thực tế: stakeholder/khách hàng/auditor cần xem mà không cần sửa — đã có trong `RoleInProject` + `ProjectPermissions`, có test |
| 2026-07-30 | Cho phép `Member` tự self-assign task đang `ToDo` (không cần PM gán); gán người khác/gỡ người khác vẫn chỉ PM | Khớp mô hình Kanban thực tế (tự "pick up" task), giảm bottleneck qua PM, vẫn tránh xung đột nhờ điều kiện task phải đang `ToDo` — hiện thực bằng `ProjectAction.SelfAssign` (PM/Member, chặn Viewer) |
| 2026-07-30 | Thêm Reporter, Priority, Label, Watcher, TaskLink, Workflow Transition Rules vào core | Đối chiếu trực tiếp mô hình Jira thật — đây là các khái niệm cơ bản mà thiếu sẽ khiến hệ thống thiếu tính thực tế — Reporter/Priority/Workflow đã xong; Label/Watcher/TaskLink còn thiếu API riêng |
| 2026-07-22 | Đưa Epic, Issue Security Level, Advanced Search (JQL-like) vào Nhóm B (làm sau) | Đây là tính năng nâng cao/hiếm dùng ở quy mô nhỏ, tránh phình to quá mức trước khi core ổn định — quyết định hoãn, luôn đúng bất kể tiến độ code |
| 2026-07-22 | Employee self-register tài khoản, không cần SystemAdmin tạo hộ | Giảm bottleneck, khớp cách hầu hết SaaS thật vận hành — đã hoạt động thật (`AuthController.Register`) |
| 2026-07-22 | Thêm `InvitationStatus` (Pending/Accepted/Declined) cho `ProjectMember` | Phản ánh đúng luồng mời thành viên thực tế, không tự tạo tài khoản hộ người chưa đăng ký — enum đã dùng thật trong filter membership |
| [điền ngày khi code] | Thêm Reset Password qua token có hạn 15-30 phút | Tính năng Auth cơ bản, thiếu sẽ không dùng được thật — *`PasswordResetToken` chưa tồn tại trong `PMS.Domain`* |
| 2026-07-22 | Bắt buộc HTTPS toàn hệ thống | Bảo vệ JWT token khỏi bị nghe lén qua kênh không mã hóa — đã có `app.UseHttpsRedirection()` |
| 2026-07-29 | Thêm Health Check endpoint (`/health`) | Cần thiết khi có Docker/CI-CD để biết trạng thái API — đã có `AddHealthChecks`/`MapHealthChecks` trong `Program.cs` |
| [điền ngày khi code] | Định nghĩa 3 môi trường Dev/Staging/Production qua `ASPNETCORE_ENVIRONMENT` | Tách cấu hình rõ ràng, tránh lẫn lộn dữ liệu test và thật — *mới chỉ có nhánh rẽ Dev/không-Dev trong `Program.cs`, chưa có `appsettings.Staging.json` hay cấu hình riêng cho Staging* |
| 2026-07-30 | Subtask không tự động đóng Task cha khi tất cả subtask `Done`; chỉ hiển thị progress bar (%) | Giữ đúng hành vi mặc định của Jira thật — Task cha có thể còn việc ngoài các subtask đã liệt kê — có test khẳng định 100% progress mà task cha vẫn `ToDo` |
| 2026-07-30 | Subtask là 1 Task đầy đủ (Status, Assignee, Comment, Watcher, TaskLink riêng), không phải checklist item; giới hạn chỉ 1 cấp cha–con | Task/Subtask dùng chung class, đúng nguyên lý OOP tái sử dụng; tránh phức tạp hóa với đệ quy vô hạn — giới hạn 1 cấp enforce ở `TaskItem.AddSubtask()`, trả 409 |
| 2026-07-25 | Làm Auth trước Project (đổi thứ tự §13) | Mọi service cần `ICurrentUserService`; làm Auth trước tránh viết code với user giả rồi sửa lại |
| 2026-07-25 | Refresh token lưu DB, hash SHA-256, rotation + reuse detection | Cho phép thu hồi thật (logout/nghi bị lộ) — JWT thuần không làm được. Theo RFC 9700 |
| 2026-07-25 | `RoleInProject` KHÔNG nhét vào JWT, chỉ `SystemRole` | Quyền theo project đổi liên tục; nhét vào token thì thu hồi không kịp thời |
| 2026-07-25 | Lỗi trả theo `ProblemDetails` (RFC 7807), khác format tự chế ở §7 | Chuẩn công nghiệp, Swagger/client hiểu sẵn |
| 2026-07-27 | **(ADR-006)** Tách cơ chế phân quyền theo tầng: Policy-based Authorization cho tầng 1, `IProjectAuthorizationService` riêng cho tầng 2 | `RoleInProject` phụ thuộc dữ liệu, không check được bằng `[Authorize(Roles=...)]` tĩnh — chi tiết bên dưới |
| 2026-07-27 | **(ADR-007)** Không mở transaction tường minh khi tạo Project — sinh `Id` phía app, gộp vào 1 `SaveChangesAsync` | EF Core đã tự bọc transaction ngầm mỗi `SaveChanges` — chi tiết bên dưới |
| 2026-07-27 | **(ADR-008)** Soft delete Project: chặn 409 nếu còn task chưa `Done`, cascade tường minh xuống Task/Sprint trong cùng 1 `SaveChangesAsync` | Global Query Filter không tự lan qua quan hệ; cascade EF ngầm không kích hoạt vì entity đổi state `Deleted→Modified` — chi tiết bên dưới |
| 2026-07-27 | **(ADR-009)** Test dùng Shouldly + NSubstitute, không dùng FluentAssertions/Moq | FluentAssertions v8+ thương mại; Moq 4.20.0 từng nhúng SponsorLink thu thập email developer — chi tiết bên dưới |
| 2026-07-27 | **(ADR-010)** Integration test chạy trên SQL Server thật (`PmsTestDb`), không dùng EF InMemory/SQLite/Testcontainers | InMemory/SQLite không phản ánh đúng FK, Query Filter, dialect thật; Testcontainers chậm do emulation trên Apple Silicon — chi tiết bên dưới |
| 2026-07-28 | **(ADR-011)** Thêm `DomainException` riêng cho tầng Domain, middleware map thành 409 | Domain không được phụ thuộc Application; lỗi nghiệp vụ hợp lệ không được trả 500 — chi tiết bên dưới |
| 2026-07-28 | **(ADR-012)** Vòng đời lời mời và invariant thành viên đặt trong aggregate `Project` | Đảm bảo luôn có ≥1 PM Accepted, không mời trùng; DB unique index là chốt chặn cuối — chi tiết bên dưới |
| 2026-07-28 | **(ADR-013)** ActivityLog ghi tường minh qua `IActivityLogger`, không dùng EF Interceptor | Interceptor chỉ thấy cột đổi, không biết ý nghĩa nghiệp vụ; log chung transaction với thay đổi nghiệp vụ — chi tiết bên dưới |
| 2026-07-28 | **(ADR-014)** Gom audit fields vào `BaseEntity` | Chuẩn hóa `CreatedAt`/`UpdatedAt`, tránh property hiding và giữ dữ liệu log bằng migration `RenameColumn` — chi tiết bên dưới |
| 2026-07-28 | **(ADR-015)** Khóa tài khoản phải thu hồi refresh token | `SystemRole` nằm trong JWT claim; khóa tài khoản hoặc đổi role phải vô hiệu hóa khả năng refresh token — chi tiết bên dưới |
| 2026-07-29 | **(ADR-016)** Optimistic concurrency (`RowVersion`) cho `Project`/`TaskItem`, wire đầy đủ qua DTO cho Project | Cột `RowVersion` không tự nhiên giải quyết lost-update nếu không round-trip qua client — chi tiết bên dưới |
| 2026-07-29 | **(ADR-017)** Đổi status task: `Assignee` HOẶC `ProjectManager` được phép, không phải "chỉ assignee" hay "mọi Member" | UC diagram và seq-03 mâu thuẫn nhau về phạm vi — chi tiết bên dưới |
| 2026-07-29 | **(ADR-018)** Xóa task còn subtask chưa `Done`: chặn 409, không cascade | Nhất quán triết lý "không xóa ngầm" đã dùng ở ADR-008 (project) và invite/remove member — chi tiết bên dưới |
| 2026-07-30 | **(ADR-019)** Task/Sprint tái dùng `IProjectAuthorizationService` + mở rộng `ProjectAction`, không dựng service phân quyền riêng | Quyền trên task/sprint về bản chất là quyền project-scoped, cùng nguồn dữ liệu `ProjectMember` — chi tiết bên dưới |
| 2026-07-30 | **(ADR-020)** Xóa Sprint: đẩy task về Backlog (`SprintId = null`), không chặn, không cascade | Khác ADR-008/018 vì không mất dữ liệu nào — task vẫn sống — chi tiết bên dưới |
| 2026-07-30 | **(ADR-021)** `RowVersion` bắt buộc cho `UpdateTaskRequest` nhưng KHÔNG bắt buộc khi đổi status | State machine đã tự là chốt chặn concurrency; bắt thêm token chỉ làm vướng UX kéo-thả Kanban — chi tiết bên dưới |
| 2026-07-30 | **(ADR-022)** Enum serialize ra JSON dưới dạng TÊN, không phải số | Client không phải tự dựng bảng map số→tên ở mọi chỗ hiển thị; làm trước frontend thì rẻ, sau thì phải sửa cả TypeScript types — chi tiết bên dưới |
| 2026-07-30 | **(ADR-023)** Notification là ngoại lệ hợp lệ duy nhất của ADR-006/019: không project-scoped, phạm vi truy cập bảo đảm bằng chữ ký repository | Thông báo là dữ liệu riêng của người nhận, không thuộc project nào — chi tiết bên dưới |
| 2026-07-30 | **(ADR-024)** `MarkAllAsRead` đi qua `ChangeTracker`, không dùng `ExecuteUpdateAsync` | Bulk update bỏ qua `ApplyAuditFields()` nên mất `UpdatedAt` — cùng lý do ADR-008 chọn Option A — chi tiết bên dưới |
| 2026-07-30 | **(ADR-025)** `RelatedEntityKind` suy ra từ `NotificationType` bằng computed property, không thêm cột | Không phải migrate dữ liệu đã tồn tại, và không sinh ra khả năng `Type` lệch với `Kind` — chi tiết bên dưới |
| 2026-07-30 | **(ADR-026)** Comment: viết = PM/Member, sửa = CHỈ tác giả, xóa = tác giả hoặc PM; xóa cứng | Tách theo mức độ xâm phạm chứ không theo cấp bậc — viết lại lời người khác nặng hơn xóa — chi tiết bên dưới |
| 2026-07-31 | **(ADR-027)** Refresh token đi bằng cookie `HttpOnly; Secure; SameSite=Strict; Path=/api/v1/auth`, access token giữ trong bộ nhớ; cả hai rời khỏi `localStorage` | Refresh token sống 7 ngày và xoay vòng vô hạn nên một lỗ XSS đọc được nó là chiếm phiên vĩnh viễn — chi tiết bên dưới |
| 2026-07-31 | **(ADR-028)** Giữ App Router nhưng dùng cho routing/layout, fetch toàn bộ ở client; route guard đặt ở client chứ KHÔNG ở `middleware.ts` | Cookie có `Path=/api/v1/auth` nên middleware không đọc được; nới `Path` ra `/` thì đánh mất chính thứ đang bảo vệ — chi tiết bên dưới |
| 2026-07-31 | **(ADR-029)** TypeScript types viết tay ở `types/`, không dùng OpenAPI codegen | Swagger hiện đang sai ở 4 chỗ nên codegen sẽ sinh ra types sai theo; kèm điểm chuyển đổi tường minh — chi tiết bên dưới |
| 2026-07-31 | **(ADR-030)** Interceptor refresh phải **single-flight**: một promise dùng chung, các request khác xếp hàng rồi retry đúng một lần | Reuse detection của `AuthService.RefreshAsync` sẽ thu hồi TOÀN BỘ phiên nếu hai request cùng gọi `/refresh` — chi tiết bên dưới |
| 2026-07-31 | **(ADR-031)** Dùng Next.js **15**, không dùng 16 | Next 16 đổi `middleware.ts` → `proxy.ts` khiến mọi tài liệu tra cứu bị lệch tên — chi tiết bên dưới |
| 2026-07-31 | **(ADR-032)** `ProjectSummaryResponse` trả kèm `RoleInProject` của người gọi | Không có nó thì UI phải gọi `GET /projects/{id}/members` cho TỪNG dòng để biết được hiện nút Sửa/Xóa hay không (N+1) — chi tiết bên dưới |
| 2026-08-03 | **(ADR-033)** Đánh số task bằng bảng `ProjectTaskCounters` riêng + `UPDATE…OUTPUT` trong transaction; KHÔNG dùng `Project.RowVersion` làm khóa lạc quan | `rowversion` đổi khi bất kỳ cột nào của hàng đổi, nên mỗi lần tạo task sẽ vô hiệu token mà form sửa project đang round-trip → 409 giả (phá ADR-016) — chi tiết bên dưới |
| 2026-08-03 | **(ADR-034)** Mã hiển thị `PMS-12` ghép ở Mapper với tham số bắt buộc, không phải computed property trên entity | Computed property cần `Project` được Include, sẽ NRE ở mọi query board/backlog — đúng lớp lỗi `SubtaskProgress`-luôn-0 — chi tiết bên dưới |
| 2026-08-03 | **(ADR-035)** File đính kèm cho Task/Project: 9 bước kiểm tra gồm magic number, thư mục ngoài `wwwroot`, tải về luôn `octet-stream` + `nosniff` | Đuôi và Content-Type đều do client tự khai nên chỉ kiểm chúng là không kiểm gì; `ValidationFilter` KHÔNG chạy cho multipart — chi tiết bên dưới |
| 2026-08-03 | **(ADR-036)** `Watcher` có repository riêng ngoài `IRepository<T>`, và phải tự set `CreatedAt` | Khóa kép nên không thỏa `where T : BaseEntity`; cũng vì thế `ApplyAuditFields()` bỏ qua nó — chi tiết bên dưới |
| 2026-08-03 | **(ADR-037)** Quyền trên nhãn tách theo bán kính ảnh hưởng: tạo = mọi user, gắn/gỡ = PM/Member, sửa/xóa = chỉ SystemAdmin | Xóa một nhãn toàn cục gỡ chip khỏi board của mọi project — chi tiết bên dưới |
| 2026-08-03 | **(ADR-038)** TaskLink chuẩn hóa lúc ghi (`IsBlockedBy` không bao giờ được lưu), không tạo nghịch đảo, guard chu trình bằng BFS | Unique index hiện có KHÔNG bắt được trùng ngữ nghĩa giữa `Blocks(A,B)` và `IsBlockedBy(B,A)` — chi tiết bên dưới |
| 2026-08-03 | **(ADR-039)** `ViewStatistics` mở cho cả `Member` | Member vốn đã đọc được mọi task qua `/board`; tổng hợp của dữ liệu đã đọc được không phải đặc quyền — chi tiết bên dưới |
| 2026-08-03 | **(ADR-040)** Job quét hạn khử trùng lặp theo `(EmployeeId, Type, RelatedEntityId, ngày UTC)`; job KHÔNG gọi `IActivityLogger` lẫn `NotifyMany` | Cả hai đọc `ICurrentUserService` — một cái ném khi không có `HttpContext`, một cái "chạy đúng" do tình cờ — chi tiết bên dưới |
| 2026-08-03 | **(ADR-041)** Reset password: `forgot-password` luôn 204, mọi lỗi token gộp thành một 400, `SerilogEmailSender` chỉ ở Dev/Testing | Phân biệt được phản hồi là biến endpoint thành công cụ dò email/token; log chứa token thô ở production là rò rỉ credential — chi tiết bên dưới |
| 2026-08-03 | **(ADR-042)** `SystemAdmin` KHÔNG có đặc quyền nghiệp vụ nào, kể cả đọc; trách nhiệm giải trình chuyển sang `GET /admin/audit-logs` với `entityType` cố định ở server | §10 mô tả một ngoại lệ read-only mà code chưa từng có; sửa tài liệu cho khớp code thay vì ngược lại — chi tiết bên dưới |
| 2026-08-03 | **(ADR-043)** Chi tiết Task có HAI vỏ dùng chung một `TaskDetailContent`: dialog chặn route `(.)` + trang thật | Trang riêng làm mất ngữ cảnh board; dialog thuần thì không chia sẻ link được và Back sai — chi tiết bên dưới |
| 2026-08-03 | **(ADR-044)** `PUT /tasks/{id}` là ghi đè TOÀN PHẦN; màn chi tiết chỉ có đúng MỘT chỗ gọi nó (`useTaskFieldSave`) | Form sửa task chưa bao giờ gửi `description` nên đổi tên task là xóa trắng mô tả — lỗi đã sống thật — chi tiết bên dưới |
| **2026-08-04** | **(ADR-045)** Phân quyền tầng 1 chuyển sang **claim `permission` lấy từ hai bảng DB** (`Permission` + `RolePermission`), quản trị qua UI; tầng 2 giữ nguyên đọc `ProjectMember` mỗi request | Vai trò trong claim không tách được quyền admin, mà quyền project-scoped thì không thể vào token: token phình theo số project và cũ đi ngay khi PM đổi vai trò — chi tiết bên dưới |
| **2026-08-04** | **(ADR-046)** Kiểm kê nợ backend: 3 validator thiếu, `?search=` bị nuốt im lặng, `/health` luôn báo khỏe, `TreatWarningsAsErrors` chưa bật, và **`GET /projects/{id}/statistics` hỏng 500 từ ngày viết** | Lần thứ năm gặp cùng một hình dạng lỗi: *thứ cần kiểm chứng chưa có ai gọi tới* — chi tiết bên dưới |
| **2026-08-04** | **(ADR-046b)** `ValueConverter` đóng dấu `Kind=Utc` cho MỌI cột `DateTime` lúc đọc, sửa ở tầng EF chứ không ở `JsonSerializerOptions` | `datetime2` không lưu Kind → JSON thiếu hậu tố → **mọi mốc thời gian lệch đúng bằng múi giờ**; giá trị `Unspecified` còn chảy vào `IsOverdue` và `DueDateNotifier` chứ không chỉ ra HTTP — chi tiết bên dưới |
| **2026-08-04** | **(ADR-048)** `Project.Status` có đường ghi (`complete`/`reopen`, loại thông báo RIÊNG); `GET /employees?search=` mở cho mọi người nhưng ràng buộc 3 lớp; @mention do CLIENT gửi id còn SERVER lọc | Ba trường/luồng chết hoặc thiếu. Tái dùng `StatusChanged` cho project sẽ điều hướng sai; không lọc id @mention là cho bất kỳ ai bắn thông báo tới bất kỳ ai — chi tiết bên dưới |
| **2026-08-04** | **(ADR-047)** Màu biểu đồ chia ba nhóm theo VIỆC (trạng thái / tuần tự / phân đoạn); chỉ nhóm thứ ba cần validator; thẻ số và thanh mức KHÔNG phải biểu đồ | Một bảng màu chung cho mọi biểu đồ là dùng màu để nói bốn thứ khác nhau; và thang có thứ tự vẽ bằng màu rời rạc là mời người đọc hiểu sai — chi tiết bên dưới |
| **2026-08-05** | **(ADR-049)** Trang hồ sơ cá nhân **CHỈ ĐỌC**; hoãn mọi đường ghi sang phiên riêng. Kèm sửa một lỗi có sẵn làm **sập menu người dùng** | `/auth/me` dựng DTO từ **claim** chứ không đọc DB (ADR-045) + token sống 15 phút → nút "Lưu" ngây thơ sẽ báo thành công rồi vẫn hiện tên cũ. Không có nút thì người dùng biết đi hỏi ai; có nút mà nó nói dối thì không — chi tiết bên dưới |
| **2026-08-05** | **(ADR-050)** Đóng sprint thì **HỎI** task chưa xong đi đâu, không tự đẩy về Backlog hay sprint kế. **Đã chốt, chưa cài đặt** | Cả hai phương án tự động đều quyết hộ một thứ chỉ người đóng sprint mới biết; và im lặng dồn việc sang sprint sau chính là cách làm sprint đó vỡ kế hoạch — chi tiết bên dưới |
| **2026-08-05** | **(ADR-051)** Sidebar **đổi hẳn theo ngữ cảnh** kiểu Jira; hai vỏ chi tiết Task được cho **khác nhau thật** về bố cục | Ba đường tới cùng một chỗ trong một sidebar là thừa, không phải đầy đủ. Và hai vỏ nhìn y hệt nhau thì nút "Mở trang riêng" đang hứa một khác biệt không tồn tại — chi tiết bên dưới |
| **2026-08-05** | **(ADR-052)** Cột board thành **DỮ LIỆU của từng project** (bảng `BoardColumns`) thay cho enum `Status`; mỗi cột khai một `StatusCategory` ĐÓNG; **gỡ ma trận chuyển trạng thái** (thay thế ADR-021) | Người dùng cần quy trình của riêng họ, mà 39 chỗ trong solution lại hỏi "task xong chưa" — `Category` là hợp đồng tối thiểu giữa tên do người dùng đặt và ngữ nghĩa mã nguồn cần. Với cột tuỳ biến thì không còn cơ sở nào nói cặp chuyển nào hợp lệ — chi tiết bên dưới |
| **2026-08-05** | **(ADR-053)** `GET /tasks/my` — endpoint **xuyên dự án** đầu tiên, lọc "được gán cho tôi · chưa xong · hạn ≤ hôm nay" | Mọi endpoint task khác đều nằm dưới `/projects/{id}`, nên "sáng nay tôi cần làm gì" sẽ là N request rồi gộp ở client. Không nhận `employeeId` ở đâu cả — chi tiết bên dưới |

| | | |

### Chi tiết ADR-006 → ADR-010

#### ADR-006 (2026-07-27) — Tách cơ chế phân quyền theo tầng
**Bối cảnh:** `RoleInProject` phụ thuộc dữ liệu (phải query `ProjectMember` mới biết được),
không thể check tĩnh bằng `[Authorize(Roles = "...")]` như tầng 1 (System Role).

**Quyết định:** Policy-based Authorization (`[Authorize(Policy = "CanCreateProject")]`) cho
tầng 1; `IProjectAuthorizationService` (custom service, gọi tường minh trong Application
layer, không phải `IAuthorizationHandler`) cho tầng 2.

**Hệ quả:**
- Check quyền tầng 2 đặt ở Service layer (không phải Controller) — để mọi entry point
  tương lai (background job, gRPC...) đều đi qua cùng một chỗ, không lặp logic.
- Người không phải thành viên của project → trả **404** (không phải 403), tránh rò rỉ sự
  tồn tại của project cho người ngoài (OWASP API1:2023 — Broken Object Level Authorization).
  403 chỉ dùng khi đã là thành viên nhưng role không đủ quyền.
- `SystemAdmin` KHÔNG tự động có quyền `ProjectManager` trên mọi project (Least Privilege).

#### ADR-007 (2026-07-27) — Không dùng transaction tường minh khi tạo Project
**Bối cảnh:** Tạo `Project` + insert `ProjectMember(creator, ProjectManager)` phải nguyên tử
— không được để xảy ra tình huống có Project mà không có PM.

**Quyết định:** Sinh `Id` phía application (`Guid.NewGuid()`), gộp cả hai insert vào 1 lần
`SaveChangesAsync`.

**Lý do:** EF Core đã tự bọc mỗi `SaveChanges` trong 1 transaction ngầm — thêm transaction
tường minh (`BeginTransactionAsync`) là dư thừa. Nhất quán với cách `AuthService.RegisterAsync`
đã làm. `ExecuteInTransactionAsync` (đã có sẵn ở `IUnitOfWork`) để dành riêng cho nghiệp vụ
cần NHIỀU lần `SaveChanges` hoặc trộn lệnh ngoài `ChangeTracker` (xem ADR-008).

#### ADR-008 (2026-07-27) — Soft delete Project: cascade + guard
**Bối cảnh:** Global Query Filter áp theo từng entity riêng lẻ, không tự lan qua quan hệ.
Nếu chỉ soft-delete `Project` mà không đụng tới `Task`/`Sprint` liên quan, các query không
đi qua `Project` (ví dụ query `Task` trực tiếp) vẫn thấy task "mồ côi" của 1 project đã xóa.

**Quyết định:**
1. Chặn **409 Conflict** nếu project còn task chưa `Done`.
2. Cascade soft-delete xuống toàn bộ task/sprint còn lại, trong cùng 1 `SaveChangesAsync`.

**Lưu ý kỹ thuật:** `ApplySoftDelete()` đổi state `Deleted → Modified` TRƯỚC khi
`base.SaveChanges` chạy, nên cascade delete tự động của EF Core (dựa theo FK behavior) không
kích hoạt — bắt buộc phải cascade tường minh trong code. Có integration test khẳng định
hành vi này.

**Cập nhật (2026-07-27):**
- Chọn **Option A** (load entity + `Remove()` + 1 `SaveChangesAsync`) thay vì
  `ExecuteUpdateAsync` (bulk update). Lý do: bulk update đi thẳng xuống SQL, bỏ qua
  `ChangeTracker`, nên cũng bỏ qua interceptor `ApplySoftDelete()` — sẽ tạo ra 2 nơi triển
  khai logic soft-delete với 2 mốc `DeletedAt` khác nhau.
- Hệ quả: `ExecuteInTransactionAsync` chưa có caller nào trong v1 — đúng theo XML doc của
  chính nó (chỉ dùng khi cần nhiều `SaveChanges`).
- `Sprint` được bổ sung `ISoftDeletable` (migration `AddSoftDeleteToSprint`) thay vì yêu cầu
  mọi query `Sprint` phải đi vào từ `Project` — ưu tiên bảo đảm bằng cấu trúc (structural
  guarantee) hơn bảo đảm bằng kỷ luật lập trình viên.
- `Project.SoftDelete()` bị xóa khỏi entity: soft delete là mối quan tâm về lưu trữ
  (persistence concern), logic đóng dấu `DeletedAt` chỉ nên nằm ở interceptor, không nằm
  trong domain method.

**Bài học (2026-07-27):** Integration test phát hiện `Sprint` chỉ được thêm 2 property
`IsDeleted`/`DeletedAt` mà thiếu interface `ISoftDeletable`. Migration và build đều thành
công (vì đó chỉ là property thường, không lỗi compile), nhưng `ApplySoftDelete()` và query
filter đều lọc theo `is ISoftDeletable` nên bỏ qua `Sprint` → sprint bị **xóa cứng** khi xóa
project thay vì xóa mềm. Đã bổ sung `SoftDeletableContractTests` (unit test) để chặn lớp lỗi
này ngay từ tầng thấp nhất, không phải đợi tới integration test mới phát hiện.

**Đính chính (2026-07-28):** ADR-008 từng ghi `Project.SoftDelete()` đã bị xóa, nhưng method
vẫn còn trong `Project.cs` tới 2026-07-28 — tài liệu đi trước code. Nay đã xóa thật. Bài học:
khi ADR mô tả một thay đổi code, phải verify lại repo trước khi đánh dấu hoàn thành.

#### ADR-009 (2026-07-27) — Chọn thư viện Assertion & Mocking cho test
**Assertion: Shouldly.** Đã chốt từ đầu — `FluentAssertions` từ v8+ (2025) chuyển sang
thương mại (Xceed). Giữ nguyên lựa chọn.

**Mocking: NSubstitute**, không dùng Moq.

Lý do — cần nói chính xác để không bị bắt lỗi khi bảo vệ đồ án: Moq **không** đổi license,
nó vẫn BSD-3. Vấn đề là phiên bản 4.20.0 (8/2023) nhúng **SponsorLink**, thu thập email đã
hash của developer lúc build và gửi đi không xin phép rõ ràng. Tính năng này bị gỡ ở 4.20.2
sau phản ứng của cộng đồng, nhưng niềm tin thì đã mất. `NSubstitute` (BSD-3) không có tiền
lệ đó, và cú pháp gọn hơn cho người mới:

```csharp
var uow = Substitute.For<IUnitOfWork>();
uow.Projects.GetRoleInProjectAsync(id, userId).Returns(RoleInProject.ProjectManager);
```

Đây là quyết định thứ ba cùng một mạch logic với Mapperly (thay AutoMapper) và Shouldly
(thay FluentAssertions). Ba lần cùng lý do "phụ thuộc phải sạch về license và về hành vi"
tạo thành một luận điểm mạch lạc trong báo cáo, chứ không phải ba lựa chọn rời rạc.

> 📌 **Lưu ý về `ProjectMapper`:** Mapperly sinh ra **class partial**, không phải interface
> → không mock được. Đừng tạo `IProjectMapper` chỉ để mock. Nó không có dependency nào, cứ
> `new ProjectMapper()` thẳng trong test. Mock một thứ thuần hàm (pure function) là mock
> sai chỗ.

#### ADR-010 (2026-07-27) — Database cho Integration Test
Bốn phương án, và tại sao ba cái bị loại:

- **❌ EF Core InMemory provider.** Không phải database quan hệ: không FK constraint,
  không transaction thật, không dịch SQL thật. Với PMS thì nó **vô hiệu hóa đúng thứ cần
  test** — Global Query Filter, index, kiểu `datetime2`. Microsoft cũng khuyến cáo không
  dùng để test. Đây là bẫy phổ biến nhất trong đồ án .NET.
- **❌ SQLite in-memory.** Có quan hệ thật nhưng khác dialect: `uniqueidentifier`,
  `datetime2`, và hành vi collation của `Contains` đều lệch so với SQL Server. Migration
  hiện tại là SQL Server–specific nên phải `EnsureCreated()` thay vì `MigrateAsync()` — tức
  là **không test được migration**, mất một trong những giá trị chính của integration test.
- **⏸ Testcontainers.** Đúng bài nhất về lý thuyết, nhưng trên chip Apple Silicon (M-series)
  image SQL Server phải chạy qua emulation amd64 → mỗi lần khởi động 30–60s. Ghi vào mục 14
  (Future Enhancements) thay vì làm ngay.
- **✅ SQL Server thật, database riêng `PmsTestDb`.** Dùng lại container OrbStack đang có
  sẵn cho dev, không thêm hạ tầng gì. Fidelity 100%, test được migration thật
  (`MigrateAsync()`), và mốc `DeletedAt` so sánh được chính xác vì cùng kiểu `datetime2`.

#### ADR-011 (2026-07-28) — `DomainException` riêng cho tầng Domain
**Bối cảnh:** `TaskItem.ChangeStatus` ném `InvalidOperationException`; middleware chỉ map
`AppException`, nên lỗi nghiệp vụ hợp lệ trả về **500**.

**Quyết định:** Thêm `DomainException` trong `PMS.Domain/Common`; middleware map exception
này thành **409 Conflict**.

**Lý do:** `PMS.Domain` không được tham chiếu `PMS.Application` (dependency đi vào trong),
nên không tái dùng được `AppException`. Khối `catch (DomainException)` phải đặt **trước**
`catch (Exception)`, vì C# khớp theo thứ tự khai báo.

#### ADR-012 (2026-07-28) — Vòng đời lời mời & invariant thành viên
**Quyết định:** Lời mời đi theo `Pending → Accepted/Declined`; thành viên `Declined` có thể
được mời lại bằng cách reset row cũ, vì unique index là `(ProjectId, EmployeeId)`. "Gỡ" là
**xóa cứng** row, không thêm status `Removed`. Invariant nằm trong aggregate root `Project`:
luôn còn ≥1 `ProjectManager` Accepted và không mời trùng. `JoinedDate` nullable, chỉ set lúc
`Accept()`.

**Lý do không thêm `Removed`:** Mọi query membership sẽ phải nhớ lọc thêm một điều kiện —
đúng loại lỗi mà thiếu `ISoftDeletable` từng gây ra. Audit trail do `ActivityLog` đảm nhiệm.

**Giới hạn đã biết:** Invariant ở domain chỉ đúng khi repository nạp đủ `Members` bằng
`Include`. Chốt chặn cuối vẫn là unique index ở tầng database.

#### ADR-013 (2026-07-28) — ActivityLog ghi tường minh, không dùng EF Interceptor
**Quyết định:** Service gọi trực tiếp `IActivityLogger`; logger chỉ `Add` vào `ChangeTracker`,
không tự gọi `SaveChanges`. `ActivityLog.Action` đổi từ `string` sang enum `ActivityAction`
với `HasConversion<string>()`, nên schema không đổi.

**Lý do:** Interceptor chỉ thấy cột thay đổi, không hiểu ý nghĩa nghiệp vụ — không phân biệt
được "người được mời chấp nhận" với "PM sửa nhầm". Không tự `SaveChanges` để log và thay đổi
nghiệp vụ ở chung một transaction, nhất quán ADR-007.

**Rủi ro chấp nhận:** Có thể quên gọi logger; mỗi action phải có integration test khẳng định
số dòng `ActivityLogs` tăng đúng.

#### ADR-014 (2026-07-28) — Audit fields gom về `BaseEntity`
**Quyết định:** Đưa `CreatedAt`/`UpdatedAt` lên `BaseEntity`, đóng dấu tại
`ApplyAuditFields()` sau `ApplySoftDelete()`. Bỏ `CreatedAt` khai báo lẻ ở các entity con và
`ActivityLog.Timestamp`.

**Lý do:** Trước đó `Project`/`TaskItem` không biết thời điểm được tạo, còn các entity khác
mỗi nơi một kiểu. Khai báo trùng tên với property của lớp cha còn gây *property hiding*
(`CS0108`).

**Ghi chú migration:** `Timestamp → CreatedAt` phải sửa tay thành `RenameColumn`; EF mặc
định sinh `DropColumn` + `AddColumn`, tức mất dữ liệu log cũ.

#### ADR-015 (2026-07-28) — Khóa tài khoản phải thu hồi refresh token
**Bối cảnh:** `SystemRole` nằm trong JWT claim và access token sống 15 phút, refresh token
sống 7 ngày. Chỉ chặn `IsLocked` ở `LoginAsync` thì người bị khóa vẫn gọi `/Auth/refresh`
lấy token mới suốt 7 ngày — khóa tài khoản trở thành vô nghĩa.

**Quyết định:**
1. Kiểm `IsLocked` ở **cả** `LoginAsync` và `RefreshAsync`.
2. Thu hồi toàn bộ refresh token đang hoạt động ngay tại thời điểm khóa **và** khi đổi `SystemRole`.
3. `LoginAsync` trả **403** (kèm lý do), `RefreshAsync` trả **401** (chung chung) — refresh
   phải trả 401 để client tự chuyển sang màn hình đăng nhập.
4. Kiểm `IsLocked` **sau** khi verify mật khẩu, để không rò rỉ trạng thái tài khoản cho
   người không biết mật khẩu (giữ nguyên lớp chống user enumeration của `DummyHash`).

**Giới hạn còn lại (chấp nhận có ý thức):** access token đã phát vẫn dùng được tối đa 15 phút
sau khi khóa. Triệt tiêu hoàn toàn sẽ phải kiểm DB mỗi request — đánh mất tính stateless của
JWT. Đây chính là lý do `RoleInProject` không được nhét vào token (ADR-006).

**Bất biến mới:** hệ thống luôn còn ≥1 `SystemAdmin` **chưa bị khóa** — song song với
"project luôn còn ≥1 PM Accepted" (ADR-012). Điều kiện `!IsLocked` là bắt buộc: đếm theo
role không thôi sẽ cho phép khóa hết mọi admin.

#### ADR-016 (2026-07-29) — Optimistic concurrency (`RowVersion`) cho `Project`/`TaskItem`

**Bối cảnh:** Cột `RowVersion` (SQL `rowversion`, `IsConcurrencyToken()`) + middleware map
`DbUpdateConcurrencyException` → 409 chỉ là điều kiện cần. Pattern update hiện tại của
`ProjectService.UpdateAsync` là load entity → sửa → `SaveChanges` **trong cùng 1 request/
DbContext** — nếu không có gì khác can thiệp, EF luôn so sánh với chính version vừa load,
nên concurrency check **không bao giờ kích hoạt** cho đúng kịch bản cần chặn: 2 người cùng mở
form sửa 1 project, người thứ hai submit sau phải bị từ chối vì dữ liệu đã đổi.

**Quyết định:**
1. `RowVersion` được trả về trong `ProjectDetailResponse` và bắt buộc phải gửi lại trong
   `UpdateProjectRequest` — client phải round-trip đúng token đã nhận từ lần `GET` gần nhất.
2. `IUnitOfWork.SetConcurrencyToken<TEntity>(entity, rowVersion)` ghi đè **original value**
   của cột `RowVersion` trên entity đã tracked, gọi trước `SaveChangesAsync`, để EF build câu
   `UPDATE ... WHERE Id = @id AND RowVersion = @clientToken` thay vì so với version vừa load.
3. `UpdateProjectRequestValidator` (trước đây **không tồn tại** — `UpdateProjectRequest`
   không được validate gì cả) bắt buộc `RowVersion` không rỗng.
4. Có integration test khẳng định sửa lần 2 với `RowVersion` cũ nhận **409**.

**Giới hạn đã biết:** Chỉ mới wire cho `Project`. `TaskItem.RowVersion` đã có ở schema nhưng
chưa wire qua DTO — sẽ làm cùng lúc dựng `TaskService`, tránh lặp lại đúng cái bẫy "chỉ có ở
schema" mà ADR này vừa sửa cho Project.

**Bài học:** Migration ban đầu (`AlterColumn` `Notifications.Type` từ `int` sang
`nvarchar(50)`) cũng bị phát hiện cùng đợt — SQL Server tự CAST số thành chuỗi số ("0", "1"),
không thành tên enum ("TaskAssigned") mà `HasConversion<string>()` cần khi đọc lại. Đã sửa
migration để tự `UPDATE` map giá trị cũ sang tên enum trước khi đổi kiểu cột, tránh làm hỏng
dữ liệu `Notification` đã seed/tồn tại trong DB dev. DB dev đã migrate trước khi sửa migration
này cũng được vá tay bằng đúng câu `UPDATE` tương ứng, không cần drop/reseed.

#### ADR-017 (2026-07-29) — Ai được đổi status của task

**Bối cảnh:** Đây là 1/5 câu hỏi thiết kế được liệt kê là "cần quyết trước khi code Task".
Khi đối chiếu UML hiện có để trả lời, phát hiện 2 diagram **mâu thuẫn nhau**:
- `use-case-diagram`: bubble "Cập nhật trạng thái task" nằm trong box **Member** chung,
  không có annotation giới hạn (khác với bubble "Tự nhận task" có ghi rõ điều kiện).
- `seq-03-change-status`: actor được đặt tên cụ thể là **"Assignee"**, không phải "Member".
- Box **ProjectManager** không liệt kê use case này ở đâu cả.

Nếu implement theo đúng nghĩa đen của UC diagram (bất kỳ Accepted Member nào của project),
hệ thống lỏng hơn Jira thật và PM mất khả năng tự sửa status khi cần gấp mà không phải
assignee. Nếu theo đúng nghĩa đen của seq-03 (chỉ Assignee), PM muốn sửa phải tự
assign/unassign trước — vòng vo không cần thiết cho vai trò cao nhất của project.

**Quyết định:** Cho phép đổi status nếu người gọi là **Assignee của chính task đó** HOẶC
**ProjectManager của project chứa task đó** (override được, kể cả task không do mình gán).
`Member` không phải assignee, và `Viewer`, đều bị từ chối.

**Hệ quả:**
- Check đặt ở Service layer (`TaskStatusTransitionService`, tương tự cách `IProjectAuthorizationService`
  tách riêng khỏi domain — ADR-006), vì cần biết cả `RoleInProject` (từ `ProjectMember`) lẫn
  danh sách assignee (từ `TaskAssignment`) — hai nguồn dữ liệu domain `Task` không tự có.
- `seq-03-change-status.drawio` cần vẽ lại: thêm bước kiểm tra quyền (hiện đang thiếu hẳn,
  khác với `seq-02-assign-task` đã có bước `IsProjectMember` + nhánh 403 rõ ràng), thêm
  nhánh 403 cho người không phải Assignee/PM.
- `use-case-diagram.drawio` cần thêm bubble "Cập nhật trạng thái task" vào box PM.

#### ADR-018 (2026-07-29) — Xóa task còn subtask chưa `Done`

**Bối cảnh:** Câu hỏi thiết kế còn lại không có diagram nào đề cập. Hai lựa chọn: chặn cứng,
hoặc cascade soft-delete xuống toàn bộ subtask (giống cách Project cascade xuống Task/Sprint
ở ADR-008).

**Quyết định:** Chặn **409 Conflict** nếu task còn subtask chưa `Done` — không cascade.

**Lý do:** Đây là lần thứ ba áp dụng cùng triết lý "không xóa ngầm": Project chặn 409 nếu còn
task active (ADR-008), gỡ member chặn nếu còn task đang gán (đã có test ở `ProjectMemberService`),
và nay task chặn nếu còn subtask active. Khác với ADR-008 (Project→Task/Sprint), quan hệ
Task→Subtask là công việc con **cùng cấp chi tiết** với task cha (không phải "hạ tầng đi kèm"),
nên rủi ro mất dữ liệu ý nghĩa nếu cascade cao hơn — chặn và bắt PM xử lý dứt điểm subtask
trước là lựa chọn an toàn hơn.

**Ghi chú:** Không mâu thuẫn với ADR-008 — ADR-008 áp dụng cho quan hệ Project→Task (Task là
"nội dung" của Project), còn đây là Task→Subtask. Khi Project bị xóa (đã cascade xuống Task),
Subtask cũng cascade theo vì `Task` (cha) đã bị soft-delete — không cần rule riêng cho
trường hợp đó, chỉ cần rule này áp dụng cho `DeleteAsync` gọi trực tiếp trên 1 Task cụ thể.

#### ADR-019 (2026-07-30) — Task/Sprint tái dùng cơ chế phân quyền của Project

**Bối cảnh:** Khi dựng Task, câu hỏi là có nên tạo `ITaskAuthorizationService` song song
với `IProjectAuthorizationService` hay không. ADR-017 đã nói phần "Assignee HOẶC PM" phải
nằm ở Service layer, nhưng chưa nói phần còn lại (tạo/sửa/xóa task, quản lý sprint) đặt ở đâu.

**Quyết định:** **Không** tạo service riêng. Mở rộng `ProjectAction` thêm 6 giá trị —
`CreateTask`, `UpdateTask`, `DeleteTask`, `ManageAssignees`, `ManageSprint`, `SelfAssign` —
và dùng lại `IProjectAuthorizationService.AuthorizeAsync(projectId, action)`.

**Lý do:** Quyền trên task/sprint về bản chất là quyền project-scoped: nó đọc `RoleInProject`
từ đúng bảng `ProjectMember` mà `IProjectAuthorizationService` đã đọc. Dựng service thứ hai
chỉ nhân đôi cùng một query và tạo ra hai nơi phải nhớ cập nhật khi ma trận quyền đổi. Đúng
tinh thần ADR-006: tách theo *cơ chế* (tĩnh vs. phụ thuộc dữ liệu), không tách theo *entity*.
`ProjectPermissions.IsAllowed` vẫn là bảng quyền duy nhất, và
`ProjectPermissionsTests.Moi_gia_tri_ProjectAction_phai_duoc_khai_bao_tuong_minh` tự động
bắt lỗi nếu thêm action mới mà quên khai báo.

**Hệ quả — chuẩn hóa 404:** Mọi service Task phải load task trước để lấy `ProjectId`, nên
có hai nguồn 404 khác nhau: task không tồn tại, và task thuộc project mà người gọi không
phải thành viên (`AuthorizeAsync` ném `NotFoundException(nameof(Project), ...)`). Nếu để
nguyên, hai trường hợp phân biệt được nhau qua nội dung lỗi — đủ để người ngoài dò xem một
`taskId` có tồn tại hay không. `TaskAuthorizationExtensions.AuthorizeTaskAsync` bắt lại và
ném `NotFoundException(nameof(TaskItem), taskId)`, `SprintService.LoadAndAuthorizeAsync`
làm điều tương tự cho Sprint. Cùng lý do ADR-006 chọn 404 thay 403 (OWASP API1:2023).

**Ranh giới còn lại:** Luật cần dữ liệu per-task vẫn nằm trong service của Task, không nhét
vào `ProjectPermissions` được vì nó chỉ nhận `(action, role)`: "Assignee HOẶC PM" (ADR-017),
"chỉ tự nhận được task đang `ToDo`", "assignee phải là `ProjectMember` đã `Accepted`".

#### ADR-020 (2026-07-30) — Xóa Sprint: đẩy task về Backlog

**Bối cảnh:** Ba lựa chọn: chặn 409 nếu sprint còn task (giống ADR-008/018), cascade xóa
task theo sprint, hoặc đẩy task về Backlog rồi xóa sprint.

**Quyết định:** Đẩy toàn bộ task về Backlog (`SprintId = null`) trong cùng
`SaveChangesAsync`, rồi xóa mềm sprint. Không chặn, không cascade.

**Lý do:** Triết lý "không xóa ngầm" của ADR-008/018 sinh ra để **tránh mất dữ liệu có ý
nghĩa**. Ở đây không có gì bị mất — task vẫn sống nguyên vẹn, chỉ đổi chỗ. Chặn 409 sẽ bắt
PM phải kéo tay từng task ra khỏi sprint trước khi xóa, tức là bắt họ làm thủ công đúng cái
việc mà hệ thống làm được trong một câu lệnh. Đây cũng là hành vi của Jira thật.

**Bắt buộc phải null hóa, không được để dangling:** `Sprint` là `ISoftDeletable` và có query
filter riêng, nên task trỏ tới sprint đã xóa mềm sẽ khiến `Include(t => t.Sprint)` trả `null`
một cách khó hiểu. FK `Sprint → Tasks` cũng là `DeleteBehavior.Restrict` (comment trong
`SprintConfiguration` đã ghi sẵn "nghiệp vụ: phải chuyển Task về Backlog trước khi xóa
Sprint") nên xóa cứng sẽ nổ ở tầng DB.

#### ADR-021 (2026-07-30) — Phạm vi áp dụng `RowVersion` cho Task

**Bối cảnh:** ADR-016 để lại "giới hạn đã biết": `TaskItem.RowVersion` có ở schema nhưng
chưa wire qua DTO. Khi wire, câu hỏi là có bắt buộc cho **mọi** thao tác ghi lên task không.

**Quyết định:** Bắt buộc cho `UpdateTaskRequest` (sửa tên/hạn/độ ưu tiên).
**Không** bắt buộc cho `PATCH /tasks/{id}/status` và `PUT /tasks/{id}/sprint`.

> 🔴 **ĐÍNH CHÍNH 2026-08-05 (ADR-052): lý do gốc bên dưới KHÔNG CÒN ĐÚNG.** State machine
> đã bị gỡ, nên "đứng yên là lỗi" không còn là chốt chặn nào cả — kéo hai lần tới cùng một
> cột nay đều trả 200.
>
> **Kết luận vẫn giữ nguyên, nhưng vì một lý do KHÁC:** đổi cột nay là thao tác
> **idempotent**. Hai người cùng kéo một thẻ tới cùng một cột thì kết quả giống hệt nhau,
> nên không có gì để tranh chấp và `RowVersion` vẫn không thêm bảo đảm nào. Cái mất là khả
> năng *phát hiện* rằng đã có người khác vừa chạm vào thẻ — đó là đánh đổi có ghi nhận, xem
> ADR-052.

**Lý do (bản gốc, đã hết hiệu lực — giữ làm hồ sơ):** Với đổi trạng thái, **chính state
machine đã là chốt chặn concurrency**. Bảng
chuyển đổi từ chối cả trường hợp "đứng yên" (`InProgress → InProgress` = `false`), nên hai
người cùng kéo một thẻ tới cùng một cột thì người thứ hai load lại thấy trạng thái đã đổi và
nhận 409. Bắt round-trip thêm `RowVersion` không thêm bảo đảm nào, nhưng buộc UI Kanban phải
mang theo token mới nhất trong mỗi thao tác kéo-thả. `Update` thì khác: hai người cùng sửa
tên task là mất dữ liệu thật kiểu lost-update, không có cơ chế nào khác chặn.

**Kiểm chứng:** `TasksCrudTests.Sua_task_voi_RowVersion_cu_thi_bi_chan_409` và
`TaskStatusTransitionTests.Doi_status_khong_can_RowVersion_nhung_lan_hai_cung_dich_bi_chan_409`.

> **Cập nhật 2026-07-31 — vòng lặp đã khép ở phía UI.** Cho tới phiên này, `RowVersion`
> mới chỉ được test ở tầng HTTP; **chưa có màn hình nào từng round-trip nó thật**. Nay
> `components/projects/edit-project-dialog.tsx` là chỗ đầu tiên đi trọn luồng: nạp chi
> tiết khi mở dialog (danh sách cố ý không có `rowVersion`) → gửi lại nguyên vẹn khi PUT →
> nhận 409 thì **tải lại rồi để người dùng quyết định**, tuyệt đối không tự gửi lại.
> Tự động thử lại chính là ghi đè thay đổi của người khác — đúng thứ mà ADR này sinh ra
> để chặn, nên "xử lý 409" theo kiểu retry ngầm là làm hỏng nó một cách lịch sự.

#### ADR-022 (2026-07-30) — Enum serialize ra JSON dưới dạng tên

**Bối cảnh:** `Program.cs` không cấu hình gì cho enum nên `System.Text.Json` trả về số thứ tự:
`Status 2` = Review, `Priority 0` = Highest. Hệ quả: mọi client phải tự dựng bảng map số→tên
ở từng chỗ hiển thị, và thêm một giá trị vào GIỮA enum là đổi ngầm ý nghĩa của mọi payload đã
lưu ở nơi khác.

**Quyết định:** Thêm `JsonStringEnumConverter` vào `AddControllers().AddJsonOptions(...)`.

**Vì sao làm ngay thay vì để sau:** frontend chưa tồn tại. Đổi bây giờ chỉ động tới backend
và collection Postman; đổi sau khi đã có Next.js thì phải sửa thêm TypeScript types và mọi
màn hình đã build — cùng một thay đổi nhưng đắt hơn nhiều lần.

**Chi phí đã đo, không phải phỏng đoán:**
- Converter có tác dụng **hai chiều** và vẫn nhận số ở chiều request → 18 request Postman
  hiện có không vỡ. Có test khẳng định cả hai dạng đầu vào đều parse được.
- Chiều đọc trong test thì vỡ thật: `System.Text.Json` mặc định **không** đọc được tên enum,
  nên **46 lời gọi** `GetFromJsonAsync`/`ReadFromJsonAsync` trong **13 file** integration test
  ném `JsonException`. Gom vào một `TestJson.Options` dùng chung thay vì rải
  `JsonSerializerOptions` mới ở từng lời gọi.
- Chiều ghi (`PostAsJsonAsync`) giữ nguyên không truyền options — chính nó là bằng chứng sống
  cho tính tương thích ngược mà ADR này dựa vào.

**Cách test giữ quyết định:** `EnumSerializationTests` đọc **raw JSON** chứ không deserialize.
Nếu deserialize thì `TestJson` đọc được cả hai dạng nên test vẫn xanh dù converter bị tháo
khỏi `Program.cs` — tức là không bảo vệ được gì. Đây là dạng bẫy đáng ghi lại: *test đi qua
cùng một lớp trừu tượng với code cần bảo vệ thì không bảo vệ được lớp đó.*

**Lợi ích phụ:** Swagger tự render enum thành dropdown tên, không cần cấu hình thêm.

#### ADR-023 (2026-07-30) — Notification: ngoại lệ hợp lệ của phân quyền project-scoped

**Bối cảnh:** ADR-019 đã chốt "quyền trên task/sprint về bản chất là quyền project-scoped, tái
dùng `IProjectAuthorizationService`". Notification không vừa khuôn đó: một thông báo
`InvitedToProject` thuộc về *người nhận*, không thuộc project nào theo nghĩa phân quyền — và
có loại thông báo (`DueSoon` do background job sinh ra) không gắn với hành động của ai cả.

**Quyết định:** Notification **không** đi qua `IProjectAuthorizationService`, **không** có
`ProjectAction` nào. Chỉ lọc theo `EmployeeId` lấy từ `ICurrentUserService`.

**Nhưng "ngoại lệ" không có nghĩa là lỏng hơn.** Bỏ authz đi thì mất luôn chốt chặn tập trung,
nên phải bù bằng chỗ khác — và chỗ được chọn là **chữ ký của repository**:
- Mọi method của `INotificationRepository` đều **bắt buộc** nhận `employeeId`. Không có
  `GetPagedAsync(PagedRequest)` trần nào để service gọi rồi quên lọc.
- `GetForRecipientAsync(id, employeeId)` trả `null` cho cả hai trường hợp "không tồn tại" và
  "của người khác" — service không có cách nào phân biệt nên không thể vô tình trả 403.

Đây là cùng một nguyên tắc mà bài học ADR-008 đã rút ra khi bổ sung `ISoftDeletable` cho
`Sprint`: *ưu tiên bảo đảm bằng cấu trúc hơn bảo đảm bằng kỷ luật lập trình viên.* Ở đó là
interface trên entity, ở đây là tham số bắt buộc trên interface repository.

**Hệ quả:**
- Thông báo của người khác trả **404**, không phải 403 — 403 xác nhận cho người ngoài rằng id
  đó tồn tại thật (OWASP API1:2023, cùng lý do ADR-006).
- Đánh dấu đã đọc là **idempotent**: `Notification.MarkAsRead()` trả `false` nếu đã đọc thay
  vì ném `DomainException` như `ProjectMember.Accept()`. Bấm chuông thông báo hai lần không
  phải vi phạm nghiệp vụ, và luồng idempotent thì client không cần dò trạng thái trước khi gọi.
  Service dùng giá trị trả về để không phát sinh `UPDATE` vô nghĩa.
- **Không** gọi `IActivityLogger`. Đây là chủ ý, không phải bỏ sót ADR-013: "tôi đã xem thông
  báo của tôi" không phải thay đổi nghiệp vụ trên Project/Task, và ghi log mỗi lần mở chuông
  sẽ làm loãng chính cái audit trail đó. Đã ghi comment tường minh trong service.

**Giới hạn đã biết (chấp nhận có ý thức):** index `(EmployeeId, IsRead)` phục vụ tốt hai truy
vấn nóng — đếm chưa đọc và đánh dấu tất cả — nhưng **không phủ** sort mặc định
`CreatedAt DESC` của danh sách. Không thêm index thứ hai vì đường GHI của bảng này chạy ở MỌI
luồng nghiệp vụ (mỗi `Notify` là một INSERT), nên chi phí index cao hơn lợi ích ở quy mô đồ án.
Xem lại nếu bảng `Notifications` phình to.

#### ADR-024 (2026-07-30) — `MarkAllAsRead` không dùng bulk update

**Bối cảnh:** "Đánh dấu tất cả đã đọc" là ứng viên kinh điển cho `ExecuteUpdateAsync` — một
câu `UPDATE` thay vì nạp N entity vào bộ nhớ.

**Quyết định:** Nạp qua `ChangeTracker` (`GetUnreadForRecipientAsync` cố ý **không**
`AsNoTracking`), sửa từng entity, rồi **một** `SaveChangesAsync` (ADR-007).

**Lý do:** `ExecuteUpdateAsync` đi thẳng xuống SQL nên bỏ qua `ApplyAuditFields()` — `IsRead`
vẫn đúng nhưng `UpdatedAt` mất. Đây **đúng cùng một cái bẫy** mà ADR-008 đã từ chối một lần
khi chọn Option A cho soft delete (bulk update bỏ qua `ApplySoftDelete()`), chỉ khác
interceptor bị bỏ qua. Hai lần cùng một lý do thì nên thành nguyên tắc: *thao tác ghi nào cần
interceptor của `PmsDbContext` thì phải đi qua `ChangeTracker`.*

**Vì sao chấp nhận được về hiệu năng:** số thông báo **chưa đọc** của một người luôn nhỏ —
đọc xong là hết. Đây không phải bulk update trên toàn bảng.

**Kiểm chứng:** `NotificationsTests.MarkAllAsRead_dong_dau_UpdatedAt_vi_khong_dung_ExecuteUpdate`
đọc thẳng DB và khẳng định `UpdatedAt != null`. Bản unit test bổ sung một góc khác:
`MarkAllAsRead_nap_qua_repository_co_tracking_chu_khong_bulk_update` sẽ đỏ nếu ai đó đổi sang
`ExecuteUpdateAsync` — cần cả hai vì bulk update cho ra kết quả `IsRead` **đúng**, chỉ âm thầm
làm mất `UpdatedAt`.

#### ADR-025 (2026-07-30) — `RelatedEntityKind` suy ra, không lưu

**Bối cảnh:** `Notification.RelatedEntityId` là một `Guid?` trơn. Thông báo về project và về
task đều nhét id vào đó, nên client bấm vào thông báo mà không biết phải điều hướng tới
`/projects/{id}` hay `/tasks/{id}`.

**Ba lựa chọn:** thêm cột `RelatedEntityType`; để frontend tự dựng bảng map; hoặc suy ra ở
backend từ `Type`.

**Quyết định:** Suy ra bằng **computed property** `Notification.RelatedEntityKind` trên entity,
cùng khuôn `TaskItem.IsOverdue`/`SubtaskProgress` — là property (không phải method) để Mapperly
map tự động, và get-only không backing field nên EF Core bỏ qua.

**Lý do:**
- Thêm cột thì phải migrate dữ liệu `Notification` đã tồn tại, và tạo ra khả năng `Type` lệch
  với `RelatedEntityType` — hai nguồn sự thật cho cùng một thông tin.
- Để frontend tự map thì bảng đó sẽ lệch dần khỏi backend mỗi lần thêm `NotificationType`, và
  lệch âm thầm (điều hướng sai chỗ, không lỗi gì cả).
- Đặt ở domain chứ không ở tầng Application vì "thông báo `InvitedToProject` trỏ tới một
  Project" là **tri thức nghiệp vụ**, không phải mối quan tâm trình bày.

**Kiểm chứng:** `NotificationTests.Moi_gia_tri_NotificationType_phai_duoc_khai_bao_tro_toi_Project_hoac_Task`
là hợp đồng kiến trúc cùng loại `SoftDeletableContractTests` và
`ProjectPermissionsTests.Moi_gia_tri_ProjectAction_phai_duoc_khai_bao_tuong_minh`: thêm
`NotificationType` mới mà quên khai báo thì đỏ ngay ở tầng thấp nhất, thay vì để frontend nhận
`None` và không điều hướng được.

**Đã xác nhận 0 migration** bằng `dotnet ef migrations has-pending-model-changes` — không phải
suy luận rằng EF sẽ bỏ qua property, mà là kiểm chứng thật.

#### ADR-026 (2026-07-30) — Ma trận quyền Comment và cơ chế xóa

**Quyết định:**

| Hành động | Ai được làm | Cơ chế |
|---|---|---|
| Đọc | Mọi thành viên project, **kể cả Viewer** | `ProjectAction.View` |
| Viết | `ProjectManager` + `Member` | `ProjectAction.CreateComment` (giá trị mới) |
| Sửa | **CHỈ tác giả** — PM cũng không | Luật per-row trong `CommentService` |
| Xóa | Tác giả **HOẶC** `ProjectManager` | Luật per-row trong `CommentService` |

**Vì sao sửa hẹp hơn xóa** — đây là điểm dễ làm ngược: phản xạ thông thường là "PM quyền cao
hơn nên làm được nhiều hơn ở mọi việc". Nhưng xóa lời người khác là **kiểm duyệt**, một hành vi
hợp lý của PM; còn viết lại lời người khác thì nội dung vẫn **đứng tên tác giả cũ** — hệ thống
sẽ hiển thị một câu mà người đó không viết. Phân quyền ở đây tách theo *mức độ xâm phạm*, không
theo cấp bậc.

**Chỉ thêm MỘT giá trị vào `ProjectAction`** (đúng ADR-019: không dựng service phân quyền
riêng). Hai luật per-row không nhét vào `ProjectPermissions` được vì nó chỉ nhận `(action,
role)` — đúng "ranh giới còn lại" mà ADR-019 đã khoanh. Chúng lấy `RoleInProject` từ giá trị
trả về của `AuthorizeAsync(..., View)` rồi tự áp luật, đúng khuôn
`TaskStatusTransitionService.EnsureCanChangeStatus` của ADR-017. `ProjectPermissionsTests` tự
động bắt lỗi nếu thêm action mà quên khai báo.

**Xóa cứng, không `ISoftDeletable`:** nhất quán ADR-012 (gỡ member cũng là xóa cứng, không thêm
status `Removed`). Thêm cờ đã-xóa thì mọi query comment về sau phải nhớ lọc thêm một điều kiện
— đúng lớp lỗi mà việc thiếu `ISoftDeletable` từng gây ra ở ADR-008. Audit trail do
`ActivityLog` đảm nhiệm (3 giá trị `ActivityAction` mới; `HasConversion<string>()` nên schema
không đổi).

**Không cần `RowVersion`:** chỉ tác giả sửa được nên **không tồn tại** kịch bản hai người cùng
ghi đè — chính là điều kiện mà ADR-016/021 sinh ra để chặn. Quyền hẹp ở đây thay luôn cho
optimistic concurrency, không phải bỏ sót.

**Phát hiện kèm — một bảo đảm cấu trúc đã có sẵn mà không ai biết:** `CommentConfiguration` khai
`HasQueryFilter(c => !c.Task.IsDeleted)` từ `InitialCreate`. Comment của task đã xóa mềm tự biến
mất khỏi **mọi** query, không service nào phải nhớ lọc. Đã ghi lại và có integration test giữ —
trước đó nó chỉ là một dòng trong file configuration mà không tài liệu nào nhắc tới.

**Tái dùng thay vì viết bản sao:** `InterestedEmployeeIds` (assignee + watcher + reporter) từng
là `private static` trong `TaskStatusTransitionService`. Comment cần đúng danh sách đó cho
`CommentAdded`, nên tách ra `TaskNotificationExtensions` và refactor service cũ dùng lại — hai
bản sao sẽ lệch nhau ngay lần đầu ai đó thêm một nhóm người nhận mới.

### Chi tiết ADR-027 → ADR-032 (phiên Frontend, 2026-07-31)

#### ADR-027 (2026-07-31) — Refresh token đi bằng cookie httpOnly, access token giữ trong bộ nhớ

**Bối cảnh:** `AuthResponse` trả cả `AccessToken` lẫn `RefreshToken` trong thân JSON, nên
phương án rẻ nhất là frontend cất cả hai vào `localStorage` — không phải đụng một dòng
backend nào. Câu hỏi đặt ra đúng lúc chưa có màn hình nào, tức là lúc đổi còn rẻ nhất.

**Quyết định:**

| | Lưu ở đâu | JS đọc được? |
|---|---|---|
| Refresh token (7 ngày) | Cookie `HttpOnly; Secure; SameSite=Strict; Path=/api/v1/auth` | Không |
| Access token (15 phút) | Zustand, **không** persist | Có |

`AuthenticatedResponse` (mới) là kiểu trả ra HTTP và **không có** refresh token.
`AuthResponse` giữ nguyên làm hợp đồng nội bộ giữa `AuthService` và controller.

**Vì sao hai token đối xử khác nhau — đây là điểm chính:** hai token không cùng giá trị
với kẻ tấn công. Access token sống 15 phút và chết theo tab. Refresh token sống 7 ngày và
**xoay vòng vô hạn** — ai cầm được nó thì cứ mỗi lần sắp hết hạn lại đổi lấy một cái mới,
truy cập không bao giờ mất kể cả khi nạn nhân đổi mật khẩu. Nên chi phí bảo vệ dồn vào
đúng token đắt, còn token rẻ thì đổi lấy sự đơn giản.

**Vì sao không để refresh token trong body nữa dù đã có cookie:** đây là điểm dễ làm nửa
vời. Nếu vẫn trả `refreshToken` trong JSON "cho tiện", XSS chỉ cần gọi `/auth/refresh` với
`credentials:'include'` rồi đọc body — cookie `HttpOnly` mất **sạch** tác dụng. Có
`AuthCookieTests.Than_phan_hoi_khong_duoc_chua_refresh_token` giữ đúng điều này.

**CSRF không bị đánh đổi lấy XSS:** phản xạ thông thường là "chuyển sang cookie thì mở
cửa CSRF". Ở đây không, vì cookie chỉ tới **4 endpoint auth** nhờ `Path`, còn mọi endpoint
nghiệp vụ vẫn xác thực bằng header `Authorization` — mà header tùy chỉnh thì trang khác
không đặt được nếu không qua preflight CORS. Riêng `/auth/refresh` được `SameSite=Strict`
che. Nếu đưa cả access token vào cookie thì mới phải thêm anti-CSRF cho toàn bộ API — đó
là lý do phương án đó bị loại.

🔴 **Bẫy 1 — `Path` của cookie PHÂN BIỆT hoa thường, route ASP.NET thì KHÔNG.** Route
`[Route("api/v1/[controller]")]` sinh ra `/api/v1/Auth` (chữ A hoa). Client gọi
`/api/v1/auth/refresh` vẫn trúng route, nhưng trình duyệt so `Path=/api/v1/Auth` với
`/api/v1/auth` thấy khác nên **không đính cookie** → 401 mà không có gì chỉ ra nguyên
nhân. Đã đổi thành `[Route("api/v1/auth")]` tường minh. Postman không ảnh hưởng vì routing
case-insensitive. Cùng lớp lỗi "cấu hình đúng về hình thức nhưng im lặng không làm gì" với
đính chính CORS bên dưới.

🔴 **Bẫy 2 — schemeful same-site buộc Next dev phải chạy HTTPS.** Cookie `SameSite=Strict`
do `https://localhost:7264` đặt sẽ **không** được gửi từ trang `http://localhost:3000`:
trình duyệt tính "site" gồm cả scheme, nên http và https là hai site khác nhau dù cùng
host. Vì vậy script `dev` chạy `next dev --experimental-https` và cần `mkcert -install`.

🔴 **Bẫy 3 — `WebApplicationFactory` mặc định `BaseAddress = http://localhost`.**
`CookieContainer` lọc bỏ cookie `Secure` trên URI http, nên test nào cần cookie chảy qua
phải tạo client với `BaseAddress = https://localhost`. Thêm nữa `CookieContainer` là
**riêng cho từng `HttpClient`** — `AccountLockingTests` trước đây dùng hai client cho login
và refresh nên đã gộp về một.

**Chi phí thật đã trả:** 1 controller, 1 DTO mới, 1 dòng `.AllowCredentials()`, 1 origin
thêm vào config, 1 test phải sửa. **Không** đụng `AuthService`, **không** migration,
**không** đụng 185 call site register/login trong test — vì cookie là mối quan tâm
*transport* nên xử lý ở tầng API, đúng tinh thần ADR-006 tách theo cơ chế.

**Kiểm chứng:** `AuthCookieTests` (5 fact) đọc **thẳng header `Set-Cookie`** chứ không qua
`CookieContainer` — cùng lý do `EnumSerializationTests` phải đọc raw JSON: đi qua cùng lớp
trừu tượng với thứ cần bảo vệ thì không bảo vệ được gì (`CookieContainer` thậm chí không
enforce `SameSite`, nên thuộc tính đó **chỉ** kiểm được ở mức chuỗi).

**Giới hạn đã biết (chấp nhận có ý thức):** `Secure=true` cứng, không rẽ theo môi trường.
Nghĩa là backend chạy thuần HTTP thì đăng nhập không hoạt động. Chấp nhận vì `Program.cs`
đã có `UseHttpsRedirection()` vô điều kiện và §15 đã chốt HTTPS bắt buộc từ 2026-07-22 —
thêm nhánh rẽ chỉ tạo một cấu hình có thể vô tình bật ở production.

---

#### ADR-028 (2026-07-31) — Giữ App Router nhưng thừa nhận vai trò routing/layout; guard ở client

**Bối cảnh — mâu thuẫn phải nói thẳng:** §2 chốt Next.js *vì* "SSR/routing chuẩn", nhưng
§2 cũng chốt TanStack Query, và ADR-027 vừa đặt access token vào bộ nhớ trình duyệt. Ba
thứ đó cộng lại nghĩa là **Server Component không có token để gọi API**, nên gần như mọi
trang phải là `"use client"` — tức là không dùng được điểm mạnh SSR data-fetching mà lý do
chọn Next.js ban đầu viện dẫn.

**Ba lựa chọn:**
1. Đổi sang Pages Router cho trung thực với kiến trúc client-side.
2. Làm SSR thật: Server Component gọi backend, token đọc từ cookie ở phía server, thêm một
   tầng route handler proxy.
3. Giữ App Router, dùng cho routing/layout, fetch toàn bộ ở client.

**Quyết định: phương án 3**, và ghi rõ đánh đổi thay vì giả vờ không có.

**Vì sao không phương án 1:** App Router vẫn cho những thứ Pages Router không có **kể cả
khi mọi trang đều là client component** — nested layout (`(app)/layout.tsx` giữ nguyên
header khi đổi route, không remount), route group để tách `(auth)`/`(app)` mà không thêm
cấp URL, và `loading.tsx`/`error.tsx` theo từng nhánh. Ngoài ra Pages Router là chế độ kế
thừa; chọn nó năm 2026 sẽ phải giải thích vì sao dùng cái cũ.

**Vì sao không phương án 2:** buộc phải có tầng proxy và làm TanStack Query gần như thừa.
Quy mô vượt xa lợi ích ở đồ án này — dữ liệu đều sau đăng nhập, không có trang công khai
nào cần SEO hay first-paint nhanh.

🔴 **Hệ quả quan trọng nhất — KHÔNG dùng `middleware.ts` để chặn route.** Đây là thứ ai
cũng làm theo phản xạ và nó **hỏng im lặng theo chiều tệ nhất**: middleware chặn route
bằng cách đọc cookie phiên, nhưng cookie của ADR-027 có `Path=/api/v1/auth` nên request
tới `/projects` **không** mang nó theo. Middleware luôn thấy "chưa đăng nhập" và đá cả
người đã đăng nhập về `/login` — người dùng không bao giờ vào được ứng dụng.

Nới `Path` ra `/` để middleware đọc được thì đánh mất chính điều ADR-027 bảo vệ: cookie sẽ
đi kèm mọi request nghiệp vụ và biến chúng thành mục tiêu CSRF. Nên guard nằm ở
`components/auth/auth-guard.tsx`, và lý do đã ghi ngay trong file đó để phiên sau không ai
"sửa" lại. *(Có phương án thứ ba: thêm một cookie "gợi ý phiên" không chứa bí mật với
`Path=/` cho middleware đọc. Chưa làm — nó là một nguồn sự thật thứ hai về trạng thái đăng
nhập, và cái giá phải trả là mỗi lần lệch sẽ khó chẩn đoán.)*

**Ba trạng thái, không phải hai:** `unknown` / `authenticated` / `anonymous`. Gộp `unknown`
vào `anonymous` là bug nhìn thấy được: sau F5 access token đã mất nên phải gọi
`/auth/refresh` một lần mới biết, và trong lúc chờ mà coi là chưa đăng nhập thì người dùng
bị văng về `/login` rồi mới quay lại.

**Đánh đổi đã chấp nhận:** không có SSR cho dữ liệu, first paint là skeleton, không SEO.
Cả ba đều không quan trọng với ứng dụng nội bộ sau đăng nhập.

---

#### ADR-029 (2026-07-31) — TypeScript types viết tay, không dùng OpenAPI codegen

**Bối cảnh:** §6 để ngỏ "cân nhắc dùng OpenAPI codegen từ Swagger để tự sinh types, giảm
sai sót thủ công". Đến lúc phải chốt.

**Quyết định:** viết tay ở `frontend/types/`, mỗi file soi gương một file DTO backend
(`types/auth.ts` ↔ `Features/Auth/AuthDtos.cs`) để đối chiếu được bằng mắt.

**Lý do quyết định — không phải "đỡ thêm bước build" mà là codegen sẽ sinh ra thứ SAI:**
`ProjectMembersController` có **4 chỗ `[ProducesResponseType]` khai sai** — hai chỗ khai
`PagedResult<T>` trong khi handler trả mảng trần, hai chỗ khai `201` trong khi trả `200`.
Codegen tin vào tài liệu Swagger, nên nó sẽ sinh ra types sai một cách **tự tin và im
lặng**, và người đọc sẽ tin types hơn là tin code. Viết tay từ chữ ký C# thật thì bốn chỗ
đó lộ ra ngay.

Ba lý do phụ: số DTO cần cho phiên này chỉ khoảng 12; output codegen vài nghìn dòng không
chèn được vào báo cáo; và types viết tay là chỗ tự nhiên để ghi lại tri thức mà OpenAPI
không diễn đạt được — ví dụ "gửi `description: ''` chứ không phải `null` vì
`ProjectService` gọi `.Trim()`", hay "`rowVersion` bắt buộc round-trip khi `PUT` nhưng
không cần khi `PATCH status`".

**Rủi ro và cách giảm thiểu:** types viết tay sẽ lệch dần khỏi backend. Giảm bằng quy ước
đặt tên soi gương 1-1 và bằng việc dùng `Record<Enum, T>` cho mọi bảng tra (nhãn, màu) —
thêm giá trị enum mới mà quên cập nhật thì đỏ ngay lúc biên dịch.

**Điểm chuyển đổi, ghi rõ để không phải tranh luận lại:** chuyển sang
`openapi-typescript` khi **hoặc** số DTO vượt ~40, **hoặc** xảy ra lần lệch type thứ hai
gây bug thật. Điều kiện tiên quyết: sửa 4 chỗ `[ProducesResponseType]` sai trước, nếu
không codegen chỉ tự động hóa việc sinh ra lỗi.

---

#### ADR-030 (2026-07-31) — Interceptor refresh bắt buộc single-flight

**Bối cảnh:** đây là bug tốn kém nhất của cả bước frontend nếu để lọt, và nó **không** tự
lộ ra khi thử tay từng thao tác một.

`AuthService.RefreshAsync` dùng rotation kèm reuse detection theo RFC 9700: token cũ bị
thu hồi ngay khi đổi, và dùng lại một token **đã thu hồi** bị coi là token bị đánh cắp nên
backend gọi `RevokeAllAsync` — thu hồi **toàn bộ** phiên của người dùng đó.

Hệ quả: nếu ba request cùng nhận 401 rồi cùng gọi `/auth/refresh` với một refresh token,
request đầu đổi thành công, hai request sau gửi đúng token vừa bị thu hồi → người dùng bị
đá khỏi mọi thiết bị. Triệu chứng ngoài đời là **"thỉnh thoảng tự đăng xuất"** — chỉ xuất
hiện khi có từ hai request cùng hết hạn một lúc, nên gần như không tái hiện được theo yêu
cầu và rất khó lần ra nguyên nhân.

**Quyết định:** `lib/api/refresh.ts` giữ **một** biến `inFlight`. Lời gọi thứ hai trở đi
nhận lại đúng promise đang chạy thay vì tạo request mới; xong thì tất cả cùng retry
**đúng một lần**. Không lặp: lần hai vẫn 401 nghĩa là phiên đã chết thật, thử tiếp chỉ tạo
thêm lời gọi vô ích.

**Refresh chủ động, không chỉ phản ứng với 401:** `AuthenticatedResponse` có
`accessTokenExpiresAt`, nên khi còn dưới 60s là đổi token trước lúc gửi. Ngưỡng 60s rộng
hơn `ClockSkew` 30s của backend. Đường này cũng đi qua cùng `inFlight`, nên nhiều request
cùng phát hiện token sắp hết hạn vẫn chỉ tạo một lời gọi.

**Bảo đảm bằng cấu trúc, không bằng kỷ luật:** hàm `performRefresh()` để `private` trong
module, chỉ `refreshAccessToken()` được export. Không có đường gọi thẳng để ai đó vô tình
lách qua hàng đợi. Cùng nguyên tắc ADR-023 dùng cho chữ ký `INotificationRepository`.

**Đã kiểm chứng thật, không phải suy luận** (2026-07-31, gọi trực tiếp vào backend đang
chạy): gửi lại một refresh token đã xoay vòng → 401; và ngay sau đó **phiên hợp lệ cũng
trả 401**. Tức là kịch bản hỏng có thật đúng như mô tả, không phải lo xa.

**Vì sao access token không persist lại là một quyết định tốt cho việc này:** mỗi lần F5
đều mất access token nên đều chạy qua đúng đường single-flight. Cơ chế khó nhất của phiên
được diễn tập liên tục trong lúc phát triển, thay vì chỉ chạy sau mỗi 15 phút.

**Điều KHÔNG làm:** tầng API không tự điều hướng khi refresh hỏng — nó chỉ `clearSession()`
rồi để `AuthGuard` đọc `status` và `replace('/login')`. Giữ được ranh giới thì `lib/api/`
không phụ thuộc React và test được độc lập.

---

#### ADR-031 (2026-07-31) — Dùng Next.js 15, không dùng 16

**Bối cảnh:** lúc scaffold, bản mới nhất là Next 16.2 (stable), còn 15.5 vẫn được hỗ trợ.
Phản xạ mặc định là lấy bản mới nhất.

**Quyết định:** Next 15.5 + React 19 + Tailwind 4.

**Lý do:** Next 16 đổi `middleware.ts` → `proxy.ts` và hàm `middleware()` → `proxy()`. Đây
là đồ án còn nhiều phiên nữa và người làm sẽ phải tra tài liệu liên tục — toàn bộ hướng
dẫn shadcn/ui, TanStack Query và phần lớn bài viết về Next hiện có đều viết theo tên cũ.
Với một đồ án tốt nghiệp, **độ khớp tài liệu đáng giá hơn độ mới**: chi phí của việc chọn
bản mới không nằm ở lúc cài mà nằm ở mỗi lần tra cứu về sau.

**Nghịch lý đáng ghi lại:** ADR-028 đã quyết định *không* dùng `middleware.ts`, nên trên lý
thuyết việc đổi tên đó không ảnh hưởng gì tới code hiện tại. Lý do giữ Next 15 vì vậy
không phải là kỹ thuật mà là **chi phí tra cứu** — vẫn là lý do chính đáng, chỉ cần trung
thực rằng nó không phải chuyện code chạy hay không.

**Điểm xem lại:** khi Next 17 ra hoặc khi hệ sinh thái đã theo kịp tên `proxy.ts`, việc
nâng cấp là chạy `npx @next/codemod@latest middleware-to-proxy .` — rẻ, vì ta không có
file nào cần đổi.

#### ADR-032 (2026-07-31) — Danh sách project trả kèm vai trò của người gọi

**Bối cảnh:** khi làm nút Sửa/Xóa cho từng dòng danh sách, phát hiện frontend **không có
cách nào** biết mình là `ProjectManager` hay `Viewer` trong project đó.
`ProjectSummaryResponse` chỉ có `Id`, `Name`, `Status`, `ExpectedCompletionDate`; vai trò
chỉ lấy được qua `GET /projects/{id}/members` — tức là một request cho **mỗi dòng**.

Ba lựa chọn: (1) cứ hiện nút rồi để backend trả 403; (2) gọi thêm endpoint thành viên cho
từng dòng; (3) trả vai trò kèm trong danh sách.

**Quyết định: (3).** `ProjectSummaryResponse` thêm trường `RoleInProject`.

**Vì sao không (1):** §10 đã chốt "muốn ẩn/hiện nút đúng thì đọc `RoleInProject` từ API,
đừng đoán từ mã lỗi". Hiện nút rồi để backend từ chối là đẩy việc phân quyền thành một
thông báo lỗi — người dùng bấm xong mới biết mình không được phép.

**Vì sao không (2):** N+1 thẳng thừng, 10 dòng là 11 request.

**Vì sao (3) rẻ hơn vẻ ngoài:** truy vấn phân trang **vốn đã** lọc theo
`p.Members.Any(m => m.EmployeeId == @me && Accepted)` — nó buộc phải chạm đúng hàng
`ProjectMember` chứa vai trò rồi. Lấy thêm một cột từ hàng đã đọc không tốn round-trip
nào. Nói cách khác dữ liệu đã ở sẵn đó, chỉ là trước giờ không được chiếu ra.

🔴 **Chi tiết quan trọng nhất — vì sao `ToSummary` được viết TAY thay vì để Mapperly sinh:**
vai trò phụ thuộc *người đang hỏi* nên không suy ra được từ `Project`. Nếu giữ bản một
tham số do Mapperly sinh, call site nào quên truyền vai trò sẽ nhận giá trị mặc định của
enum — mà `RoleInProject` có ordinal 0 là **`ProjectManager`**. Hỏng theo đúng chiều nguy
hiểm nhất: UI hiện nút Sửa/Xóa cho cả `Viewer`, và hỏng **im lặng** vì không có gì đỏ.

Nên `ProjectMapper.ToSummary(Project, RoleInProject)` bắt buộc hai tham số và không có
overload một tham số. Trình biên dịch chặn ngay tại chỗ, không cần ai phải nhớ. Cùng
nguyên tắc *bảo đảm bằng cấu trúc hơn bảo đảm bằng kỷ luật lập trình viên* mà ADR-023 dùng
cho chữ ký `INotificationRepository` và ADR-008 dùng cho `ISoftDeletable`.

**Kiểm chứng:**
`ProjectsCrudTests.Danh_sach_tra_ve_vai_tro_cua_chinh_nguoi_goi_chu_khong_phai_cua_project`
— cùng **một** project, PM và Viewer gọi danh sách và nhận về hai vai trò khác nhau. Test
này cũng là chốt chặn hồi quy cho đúng cái bẫy ordinal-0 ở trên.

**Ảnh hưởng tương thích:** thêm trường vào response là thay đổi cộng thêm — không client
nào vỡ. Không test nào phải sửa (không chỗ nào `new ProjectSummaryResponse(...)` bằng tay,
tất cả đều deserialize). `IProjectRepository.GetPagedForEmployeeAsync` đổi kiểu trả về
sang `PagedResult<ProjectWithRole>`, chỉ ảnh hưởng một call site trong `ProjectService`.

### Chi tiết ADR-033 → ADR-042 (phiên Backend hoàn chỉnh, 2026-08-03)

#### ADR-033 (2026-08-03) — Đánh số task bằng bảng đếm riêng, KHÔNG dùng `Project.RowVersion`

**Bối cảnh:** mã task kiểu Jira (`PMS-12`) cần một số tăng dần **trong phạm vi từng project**.
Phương án phản xạ đầu tiên: thêm cột `TaskCounter` lên `Projects`, tăng nó trong
`TaskService.CreateAsync`, và để `Projects.RowVersion` (đã có sẵn) làm khóa lạc quan — hai
người tạo task cùng lúc thì người thứ hai nhận `DbUpdateConcurrencyException` rồi retry.

**Vì sao phương án đó SAI** — nó chạy được, và đó chính là chỗ nguy hiểm:
1. 🔴 **Phá vỡ ADR-016.** `rowversion` của SQL Server đổi khi **bất kỳ** cột nào của hàng
   đó đổi. Mỗi lần tạo task sẽ vô hiệu hóa token mà `edit-project-dialog.tsx` đang
   round-trip → PM mở form sửa project, đồng đội tạo một task, PM bấm Lưu và nhận **409
   giả** trên một trường hoàn toàn không liên quan. Trên project đông người, form sửa
   project thành vô dụng.
2. `ExceptionHandlingMiddleware` map `DbUpdateConcurrencyException` → 409 kèm thông điệp
   *"vui lòng tải lại và thử lại"*. Trên `POST /tasks` đó là một lời nói dối: người gọi
   không sửa gì và không có gì để tải lại.
3. Retry ngây thơ còn không chạy: sau exception, entry `Project` vẫn giữ **original
   RowVersion cũ**, nên lần thử thứ hai hỏng y hệt — vĩnh viễn.

**Quyết định:** bảng riêng `ProjectTaskCounters` (`ProjectId` PK, `NextNumber`), cấp số bằng
đúng một câu lệnh nguyên tử, chạy trong `IUnitOfWork.ExecuteInTransactionAsync`:
```sql
UPDATE ProjectTaskCounters SET NextNumber = NextNumber + 1
OUTPUT INSERTED.NextNumber WHERE ProjectId = @projectId;
```
Dưới READ COMMITTED, câu này giữ X lock trên hàng bộ đếm tới **hết transaction**, nên người
tạo đồng thời **chờ một nhịp rồi nhận số kế tiếp** thay vì thất bại. Không retry, không 409,
không đụng `Projects.RowVersion`, số liên tục không thủng.

**Đây là caller hợp lệ ĐẦU TIÊN của `ExecuteInTransactionAsync`** kể từ khi ADR-007 tạo ra
nó — và đúng loại việc XML doc của chính nó đã dành chỗ sẵn ("nghiệp vụ cần NHIỀU lần
SaveChanges hoặc trộn lệnh ngoài ChangeTracker"). Nó cũng đã bọc sẵn execution strategy,
quan trọng vì `EnableRetryOnFailure(3)` đang bật.

**Không vi phạm lệnh cấm bulk-update của ADR-024:** lệnh cấm đó sinh ra để bảo vệ
`ApplySoftDelete()`/`ApplyAuditFields()` khỏi bị bỏ qua. Hàng `ProjectTaskCounters` **không
có** cột audit lẫn cờ soft-delete — không có interceptor nào để bỏ qua. Ngoại lệ có ý thức,
ghi ở đây để lần sau không ai tưởng luật kia đã bị nới lỏng.

**Số không tái sử dụng, kể cả khi task bị xóa mềm.** Unique index `(ProjectId, Number)` cố ý
**không lọc** theo `IsDeleted`: mã `PMS-12` đã phát tán ra comment, URL và tài liệu ngoài hệ
thống, nên cấp lại số đó cho task khác là làm sai lệch mọi tham chiếu cũ. Tương tự
`Projects.Key`.

**Backfill và cái bẫy trong nó:** migration đánh số dữ liệu cũ bằng
`ROW_NUMBER() OVER (PARTITION BY ProjectId ORDER BY CreatedAt, Id)`. Tie-break bằng `Id` là
**bắt buộc**: migration `20260728032237` thêm `Tasks.CreatedAt` với
`defaultValue: 0001-01-01`, nên mọi task tạo trước 2026-07-28 có `CreatedAt` **giống hệt
nhau** và `ROW_NUMBER` sẽ không tất định giữa các lần chạy.

⚠️ **Không test nào chạm tới backfill**: `PmsWebApplicationFactory` chạy `EnsureDeleted` +
`Migrate` nên nó luôn thao tác trên DB rỗng. Đã kiểm chứng **bằng tay** trên một database
tạm có sẵn dữ liệu (2 project, 4 task trong đó 2 task cùng `CreatedAt` và 1 task đã xóa
mềm): kết quả `PRJ1`/`PRJ2`, số `1,2,3` và `1`, bộ đếm `3` và `1` — đúng như thiết kế.

#### ADR-034 (2026-08-03) — Mã hiển thị ghép ở Mapper, không phải computed property

**Bối cảnh:** chỗ tự nhiên nhất để đặt `PMS-12` là một computed property trên `TaskItem`,
đúng khuôn `IsOverdue`/`SubtaskProgress`/`RelatedEntityKind` đã dùng ba lần trước đó.

**Quyết định: KHÔNG.** `Code => $"{Project.Key}-{Number}"` cần navigation `Project`, mà nó
không phải lúc nào cũng được `Include`. Hậu quả là **NRE ở mọi query board/backlog/paged** —
hoặc, nếu ai đó vá bằng cách thêm `Include` khắp nơi, là một ràng buộc ngầm mà mỗi query mới
phải nhớ. Đó đúng lớp lỗi đã xảy ra **hai lần** trong dự án này: `SubtaskProgress` luôn trả 0
vì thiếu `Include(Subtasks)`, và `Assignee.Employee` NRE trong `GetForStatusChangeAsync`.

Thay vào đó `TaskMapper.ToSummary(task, projectKey)` và `ToDetail(task, projectKey,
currentEmployeeId)` **viết tay, tham số bắt buộc** — cùng khuôn `ProjectMapper.ToSummary`
của ADR-032. Trình biên dịch chặn ngay tại call site, không cần ai phải nhớ. Service lấy key
**một lần** cho cả request rồi truyền xuống, nên board 40 thẻ vẫn chỉ một truy vấn phụ.

**DTO trả cả `Number` lẫn chuỗi `Code` đã ghép** — không bắt frontend tự nối từ
`projectKey` + `number`: hai nơi định dạng thì chắc chắn có lúc lệch nhau.

#### ADR-035 (2026-08-03) — Mô hình bảo mật file đính kèm

**Quyết định:** file đính kèm cho **Task hoặc Project** (subtask dùng chung endpoint task vì
subtask là `TaskItem` đầy đủ). Hai FK nullable + CHECK constraint đúng-một-chủ, thay vì cặp
`(TargetKind, TargetId)` đa hình — giữ được ràng buộc khóa ngoại và query filter thật.

**Chín bước kiểm tra và mã lỗi tương ứng:**

| # | Kiểm tra | Mã |
|---|---|---|
| 1 | Quyền (`UploadAttachment`) — **trước khi đọc byte nào** | 404 / 403 |
| 2 | File rỗng | 400 |
| 3 | Vượt `MaxFileBytes` | **413** |
| 4 | Tên file chứa dấu phân cách / `..` / bắt đầu bằng `.` / quá 255 ký tự | 400 |
| 5 | **Đuôi kép** — đoạn Ở GIỮA nằm trong deny-list (`a.php.png`) | 400 |
| 6 | Đuôi không thuộc whitelist | **415** |
| 7 | `Content-Type` không thuộc whitelist | 415 |
| 8 | **Magic number** 8 byte đầu không khớp đuôi | 400 |
| 9 | Path containment ở `LocalFileStorage` | 500 (assertion) |

**Vì sao bước 8 là bước quan trọng nhất:** cả đuôi lẫn `Content-Type` đều do **client tự
khai**. Không có bước đọc nội dung thì đổi tên `evil.exe` → `evil.png` và khai
`image/png` là qua sạch bảy bước còn lại.

**Vì sao bước 8 trả 400 chứ không 415:** 415 nghĩa là "định dạng này chưa được hỗ trợ"; file
**nói dối** về định dạng của mình là đầu vào sai lệch. Ranh giới này cũng quyết định bước 5
chỉ soi các đoạn **ở giữa**: `script.exe` không có đoạn giữa nên rơi xuống bước 6 và nhận
415 ("không hỗ trợ" — câu trả lời đúng), còn `shell.php.png` nhận 400 ("tên có ý đồ").

🔴 **`ValidationFilter` KHÔNG chạy cho upload.** Nó duyệt `context.ActionArguments.Values`
rồi tra `IValidator<kiểu-tham-số>`; với action multipart tham số là `IFormFile`, và không có
validator nào đăng ký cho kiểu đó. Toàn bộ kiểm tra vì vậy nằm trong
`AttachmentContentValidator` gọi từ service. Thiết kế dựa vào FluentValidation ở đây là để
ngỏ **toàn bộ** cửa mà vẫn trông như đã khóa.

🔴 **Path traversal bất khả thi về CẤU TRÚC.** `IFileStorage.SaveAsync(stream, extension)`
không nhận tên file hay đường dẫn — tên trên đĩa do implementation tự sinh
(`{guid}{ext}`). Không có tham số nào để nhét `../` vào. Cùng nguyên tắc *bảo đảm bằng cấu
trúc hơn bằng kỷ luật* mà ADR-023 dùng cho `INotificationRepository` và ADR-008 cho
`ISoftDeletable`. `LocalFileStorage` vẫn kiểm containment thêm một lần khi ĐỌC, vì lúc đó
tên đến từ cột DB — "dữ liệu trong DB luôn sạch" là giả định, không phải bảo đảm.

🔴 **Bất biến: thư mục lưu file nằm NGOÀI `wwwroot`, và `Program.cs` KHÔNG BAO GIỜ được
thêm `UseStaticFiles()`.** Hiện dự án không có `wwwroot` — ghi lại ở đây để đó là một quyết
định chứ không phải tình cờ. Thêm static file serving là mở đường cho một payload HTML/SVG
được phục vụ nguyên trạng trên chính origin của API.

**Endpoint tải về trả `application/octet-stream` chứ không phải `ContentType` đã lưu**, cộng
`X-Content-Type-Options: nosniff` và `Content-Disposition: attachment`. Ba thứ đi cùng nhau
triệt mọi đường render inline. **Cái giá đã chấp nhận:** xem trước ảnh inline sẽ cần một
endpoint riêng chỉ nhận ảnh — chưa làm.

**Rủi ro tồn dư ghi rõ:** không quét virus; `.txt`/`.csv` **không có chữ ký** để kiểm nên
được đánh dấu miễn trừ tường minh trong `SignatureOptional` (chúng cũng không có khả năng
thực thi, và đường tải về đã chặn diễn giải nội dung).

#### ADR-036 (2026-08-03) — `Watcher` ngoài `IRepository<T>`, và `CreatedAt` thủ công

`Watcher` dùng khóa kép `(TaskId, EmployeeId)` và **không có cột `Id`**, nên ràng buộc
`IRepository<T> where T : BaseEntity` không phục vụ được — phải có `IWatcherRepository` độc
lập.

🔴 **Hệ quả thứ hai, dễ bỏ sót hơn nhiều:** `ApplyAuditFields()` duyệt
`ChangeTracker.Entries<BaseEntity>()`, nên `Watcher.CreatedAt` **không** được đóng dấu tự
động, và `WatcherConfiguration` cũng không có default value. Không tự set thì mọi watcher
mang mốc `0001-01-01` và `OrderBy(CreatedAt)` trở nên vô nghĩa — sai im lặng. Có integration
test khẳng định `CreatedAt.Year > 2000`.

**`ProjectAction.Watch` là action RIÊNG dù cả ba vai trò đều được**, thay vì mượn `View`:
`View` không bao giờ được phép cho qua một mutation, kể cả mutation vô hại. `Viewer` theo dõi
được task — ngoại lệ ghi duy nhất của vai trò này, và hợp lý vì nó chỉ ảnh hưởng hộp thông
báo của chính họ.

**`IsWatching` phải truyền `currentEmployeeId` vào mapper**: giá trị phụ thuộc *người hỏi*,
không suy ra được từ entity. Và `GetWithDetailsAsync` **bắt buộc** `Include(t => t.Watchers)`
— thiếu thì `IsWatching` luôn `false`, đúng bug `SubtaskProgress`-luôn-0 lần thứ ba.

#### ADR-037 (2026-08-03) — Quyền trên nhãn toàn cục, tách theo BÁN KÍNH ẢNH HƯỞNG

Nhãn là dữ liệu **toàn cục** (unique `Name` toàn hệ thống). Quyền vì vậy không tách theo cấp
bậc mà theo **phạm vi tác dụng phụ** — cùng tinh thần ADR-026 tách quyền comment theo *mức độ
xâm phạm*:

| Thao tác | Ai | Vì sao |
|---|---|---|
| Tạo nhãn | mọi user đã đăng nhập | Cộng thêm, không ảnh hưởng ai. Trùng tên → 409 |
| Gắn/gỡ nhãn trên task | `ManageTaskLabels` (PM + Member) | Phạm vi một project |
| **Sửa / xóa nhãn** | **chỉ `SystemAdmin`** | Xóa nhãn `urgent` là gỡ chip khỏi board của **mọi** project. Không PM nào nên sở hữu một tác dụng phụ xuyên project |

**Khoản hoãn có ý thức:** cách sửa gốc là **nhãn theo project** (`Label.ProjectId`, unique
`(ProjectId, Name)`), khi đó thế lưỡng nan trên biến mất hoàn toàn. Chưa làm vì cần thêm một
migration dữ liệu cho bảng nối `TaskLabels`. Ghi ra đây thay vì giả vờ nhãn toàn cục là ổn.

**Phát hiện kèm — một bẫy hiệu năng có sẵn:** `TaskSummaryResponse` cần nhãn cho chip board,
tức collection `Include` **thứ ba**. `GetPagedByProjectAsync` cố ý **không** `AsSplitQuery`
(split + `Skip/Take` trên `OrderBy` không duy nhất thì thứ tự không xác định), nên ba
collection trong một câu sẽ nhân dòng theo `assignees × subtasks × labels`. Đã tách thành
**hai bước**: phân trang lấy `Id` trước (không Include, thứ tự hoàn toàn xác định), rồi nạp
lại đúng các Id đó với đủ Include + `AsSplitQuery`. Việc này khử luôn phép nhân dòng vốn đã
tồn tại sẵn với hai collection, và tiện thể thêm tie-break `ThenBy(Id)` cho mọi nhánh sort.

#### ADR-038 (2026-08-03) — TaskLink: chuẩn hóa lúc ghi, không nghịch đảo, guard chu trình

🔴 **Unique index `(SourceTaskId, TargetTaskId, LinkType)` KHÔNG bắt được trùng ngữ nghĩa.**
`Blocks(A,B)` và `IsBlockedBy(B,A)` là **cùng một sự thật** với giá trị cột khác nhau — index
lưu cả hai vui vẻ. Đây là lỗ hổng đã tồn tại từ `InitialCreate` mà chưa ai chạm tới vì
`TaskLink` chưa có API.

**Quyết định — chuẩn hóa lúc ghi**, để index cũ thực sự kín:
- `IsBlockedBy(A,B)` → lưu thành `Blocks(B,A)` (đảo chiều, đổi loại)
- Loại đối xứng (`RelatesTo`, `Duplicates`) → sắp cặp theo thứ tự `Guid`

**Hệ quả phải nhớ khi đọc DB:** `LinkType.IsBlockedBy` trở thành **giá trị chỉ dùng ở đầu
vào, không bao giờ được lưu**. Hướng hiển thị được diễn giải lại theo người xem
(`TaskLinkGraph.ViewFrom`): cùng một hàng `Blocks(A,B)` hiện là "chặn B" khi xem từ A và "bị
A chặn" khi xem từ B.

**KHÔNG tự tạo link nghịch đảo** — và lý do đúng không phải cái nghĩ đầu tiên: nó *không* gây
đếm trùng ở `GetUnfinishedBlockersAsync` (query là `WHERE Id IN (subquery)`, id trùng tự
gộp). Vấn đề thật nằm ở **màn chi tiết task**: cùng một sự thật hiện hai lần, một lần ở
`OutgoingLinks` một lần ở `IncomingLinks`.

**Guard chu trình — gọi đúng tên hiện tượng.** `A Blocks B, B Blocks A` **không** gây vòng
lặp vô hạn trong code (`GetUnfinishedBlockersAsync` không đệ quy); nó là **livelock nghiệp
vụ**: cả hai task vĩnh viễn không vào được `InProgress` vì mỗi cái chờ cái kia `Done`. Chặn
bằng BFS trong bộ nhớ trên toàn bộ cạnh `Blocks` của project (vài trăm cạnh là cùng, không
cần recursive CTE). **Race còn lại:** hai insert đồng thời vẫn tạo được chu trình — chấp
nhận có ý thức, vì hậu quả là một livelock phát hiện được, không phải crash, và chi phí chặn
triệt để (khóa toàn đồ thị) không xứng đáng.

#### ADR-039 (2026-08-03) — `ViewStatistics` mở cho `Member`

Ma trận cũ cho `ProjectManager` + `Viewer` nhưng **không** `Member` — đọc §10 theo nghĩa đen
thì đúng (mục quyền của Member không liệt kê "xem thống kê", còn "chỉ xem" của Viewer thì
bao hàm).

**Nhưng đó không phải một ranh giới bảo mật:** `Member` vốn đã đọc được **mọi** task ở **mọi**
trạng thái qua `ProjectAction.View` trên `/board` và `/backlog`. Tổng hợp của dữ liệu đã đọc
được không phải một đặc quyền — nó chỉ là phép đếm mà client tự làm được. Giữ nguyên chỉ tạo
ra một khác biệt vô nghĩa mà người dùng sẽ đọc là lỗi.

**Quyết định: thêm `Member`.** Sửa đồng bộ ba chỗ — `ProjectPermissions.cs`, dòng
`[InlineData]` trong `ProjectPermissionsTests`, và bảng vai trò §10.

#### ADR-040 (2026-08-03) — Job quét hạn: khóa khử trùng lặp và hai quả mìn

**Khóa de-dup: `(EmployeeId, Type, RelatedEntityId, NGÀY UTC)`** — không thêm cột, không
thêm bảng. Trạng thái nằm ở **DB** chứ không ở bộ nhớ, nên nó đúng qua cả restart lẫn nhiều
instance, và **độc lập với chu kỳ tick**: đổi tick từ 1 giờ xuống 5 phút cũng không làm người
dùng bị dội thông báo.

🔴 **Hai quả mìn trong scoped service mà job tuyệt đối không được giẫm:**
1. **`IActivityLogger.Log` gọi `_currentUser.RequireEmployeeId()`** → ném
   `UnauthorizedException` khi không có `HttpContext`. Job gọi nó sẽ chết ngay tick đầu
   tiên. → Job **không ghi ActivityLog**.
2. **`NotificationService.NotifyMany` đọc `_currentUser.EmployeeId`** để loại người thực
   hiện khỏi danh sách nhận. Ngoài request, giá trị đó là `null`, và phép so `Guid != Guid?`
   được nâng kiểu nên **luôn true** — việc lọc "chạy đúng" hoàn toàn do tình cờ. → Job dựng
   thẳng `Notification` qua `IUnitOfWork`, để hành vi là thứ đọc được từ code chứ không phải
   thứ suy ra từ một tai nạn.

**Đảo một quyết định cũ, ghi rõ để không ai tưởng là bỏ sót:** `NotificationConfiguration` có
comment cố ý **tránh** index thứ hai vì đường ghi của bảng này chạy ở mọi luồng nghiệp vụ.
Nay thêm `(RelatedEntityId, Type)`. Lý do đánh đổi đổi chiều: job chạy **mỗi giờ, vĩnh viễn**,
và truy vấn khử trùng lặp của nó sẽ quét toàn bảng — chậm dần đúng theo tốc độ bảng phình ra.
Có thêm một đường đọc thường trực thì chi phí ghi của một index là đáng.

**Đăng ký hosted service NẰM TRONG `if (!IsEnvironment("Testing"))`**, cùng kiểu gác với
`UseRateLimiter`. Nếu nó chạy trong test, một luồng nền sẽ ghi `Notification` xen vào giữa và
những test đếm delta thông báo sẽ đỏ **ngẫu nhiên** — loại hỏng khó chẩn đoán nhất vì nó phụ
thuộc thời điểm.

**Nghiệp vụ tách khỏi timer:** `IDueDateNotifier` là service Application bình thường,
`BackgroundService` chỉ còn là cái đồng hồ. Nhờ đó unit test gọi thẳng được (5 test giữ luật
khử trùng lặp), và sau này chuyển sang Hangfire hay một endpoint admin cũng không phải viết
lại.

#### ADR-041 (2026-08-03) — Đặt lại mật khẩu: phản hồi không phân biệt được

**Quyết định:** `PasswordResetToken` lưu **hash SHA-256**, hạn 30 phút, dùng một lần. Cấp
token mới thì vô hiệu mọi token còn treo.

**Ba quy tắc, tất cả đều về việc KHÔNG rò rỉ thông tin:**
1. `POST /auth/forgot-password` **luôn trả 204**, kể cả email không tồn tại — nếu không, nó
   thành công cụ dò xem ai đã đăng ký. Nhánh "email lạ" vẫn **đốt đúng lượng công việc**
   (sinh + hash một token rồi vứt), theo tiền lệ `DummyHash` của `LoginAsync`: không có bước
   này thì thời gian phản hồi tự nó tố cáo, và việc trả 204 ở cả hai nhánh chỉ là bảo mật
   trên giấy.
2. Token sai / hết hạn / đã dùng → **cùng một 400 với cùng một thông điệp**, không phải 404.
   Phân biệt được ba trường hợp là xác nhận cho kẻ tấn công rằng một token có thật.
3. Tài khoản **bị khóa vẫn đặt lại được** mật khẩu. Từ chối là để lộ trạng thái khóa cho
   người chỉ cầm địa chỉ email; việc chặn nằm ở `LoginAsync` (403).

**Thu hồi toàn bộ refresh token khi đổi mật khẩu** — tiền lệ ADR-015. Lý do đổi mật khẩu
thường là "nghi bị lộ", nên để phiên cũ sống tiếp là bỏ qua đúng mối đe dọa người dùng đang
cố xử lý. Dùng lại `RevokeAllAsync` thay vì chép lại vòng lặp.

🔴 **Việc chọn implementation `IEmailSender` là một quyết định BẢO MẬT.**
`SerilogEmailSender` ghi nguyên thân email — trong đó có token **thô** — ra
`logs/pms-*.log`. Ở production, ai đọc được log (hoặc bất kỳ hệ thống gom log nào) sẽ đặt lại
được mật khẩu của mọi tài khoản. Vì vậy nó **chỉ** được đăng ký ở Development/Testing; mọi
môi trường khác dùng `NullEmailSender`. `NullEmailSender` nuốt im lặng thay vì ném, vì một
exception ở đây sẽ biến thành 500 và trở thành đúng cái kênh rò rỉ mà quy tắc 1 đang chặn.

**`HashRefreshToken` đổi tên thành `HashToken`** khi có người dùng thứ hai — thay vì thêm một
method thứ hai làm cùng một việc rồi để hai bản lệch nhau.

#### ADR-042 (2026-08-03) — `SystemAdmin` không có đặc quyền nghiệp vụ nào

**Bối cảnh — tài liệu mô tả một hành vi không tồn tại.** §10 ghi từ 2026-07-29 rằng
SystemAdmin có quyền *"xem (read-only) toàn bộ project cho mục đích support/audit"*. Rà toàn
bộ mã nguồn: `SystemRole` chỉ xuất hiện ở đúng ba chỗ có ý nghĩa — enum, policy
`require-system-admin` trên `AdminEmployeesController`, và bất biến "≥1 admin chưa khóa".
`ICurrentUserService.SystemRole` **chưa từng được một service nào đọc**. SystemAdmin không
phải thành viên vẫn nhận 404 như người ngoài.

Đây là lần thứ tư dự án gặp cùng một hình dạng lỗi (ADR-008: tài liệu nói đã xóa
`Project.SoftDelete()`; ADR-016: `RowVersion` chỉ có ở schema; đính chính CORS). Khác ba lần
trước ở chỗ: lần này **code mới là bản đúng**.

**Quyết định: sửa tài liệu cho khớp code.** SystemAdmin là vai trò quản trị **hệ thống**
thuần túy — không đọc, không ghi, không "read-only để hỗ trợ".

**Vì sao không hiện thực hóa ngoại lệ như tài liệu mô tả:**
- Quyền đọc xuyên project là "God Mode" thu nhỏ, đi ngược Least Privilege mà chính dòng
  ADR-006 đã chốt.
- Nó sẽ vô hiệu hóa trước `Issue Security Level` — tính năng đã nằm ở §14 Nhóm B.
- Chi phí thật cao hơn vẻ ngoài: phải rà lại **mọi** query lọc theo membership (danh sách
  project, board, backlog, thành viên…). Sửa nửa vời thì admin "xem được" project qua
  `GET /projects/{id}` nhưng danh sách vẫn trống — một trạng thái mâu thuẫn khó chẩn đoán.

**Nhu cầu chính đáng phía sau ngoại lệ đó là TRÁCH NHIỆM GIẢI TRÌNH**, và nó được đáp ứng
bằng `GET /api/v1/admin/audit-logs`.

🔴 **Endpoint đó cố ý KHÔNG nhận tham số `entityType`.** Danh sách loại đối tượng được đọc
hard-code ở server: `Employee` (khóa/mở tài khoản, đổi `SystemRole`) và `Label` (thao tác
nhãn toàn cục — ADR-037). Nhận nó từ query param, hoặc thêm `Project`/`TaskItem` vào danh
sách, là mở lại đúng cái god mode vừa đóng. Có integration test khẳng định endpoint trả
**0 dòng** cho một task, **kể cả task do chính admin tạo**.

**Kiểm chứng:** `SystemAdminScopeTests` — `[Theory]` trên **16 route** project-scoped (mọi
route đều 404), một test ghi (`POST /tasks` vào project lạ → 404), và một **positive control**
(admin *là* Member → 200). Positive control là bắt buộc: không có nó, một đường authz hỏng
toàn cục khiến mọi thứ trả 404 vẫn làm theory xanh — tức là nó không bảo vệ được gì.

### Chi tiết ADR-043 → ADR-044 (phiên Frontend "chi tiết Task", 2026-08-03 tiếp)

#### ADR-043 (2026-08-03) — Chi tiết Task có HAI vỏ: dialog chặn route + trang thật

**Bối cảnh:** chi tiết Task là màn phức tạp nhất của sản phẩm (bảy khối). Hai lựa chọn quen
thuộc đều có khuyết điểm thật: trang riêng làm mất ngữ cảnh board sau mỗi lần bấm một thẻ;
dialog thuần thì không chia sẻ link được, nút Back sai, và breadcrumb không diễn tả được.

**Quyết định: làm cả hai bằng *intercepting route* của App Router**, đúng mô hình Jira —
bấm thẻ trên board thì hiện dialog đè lên board, còn mở link/F5/tab mới thì ra trang đầy đủ.

```
app/(app)/projects/[id]/
├── layout.tsx                       ← nhận prop `modal`, ẩn thanh tab khi segment là `tasks`
├── tasks/[taskId]/page.tsx          ← trang thật
└── @modal/
    ├── default.tsx                  ← BẮT BUỘC, trả null
    └── (.)tasks/[taskId]/page.tsx   ← vỏ dialog
```

Cả hai vỏ render **cùng một** `TaskDetailContent`, nên không có bề mặt nào để hai lối vào
lệch nội dung. Chúng không bao giờ mount đồng thời: khi dialog mở, slot `children` vẫn giữ
board; khi tải cứng, `@modal` rơi về `default.tsx`.

🔴 **Tiền tố là `(.)`, KHÔNG phải `(..)`** — và điều này được xác minh bằng cách đọc
`node_modules/next/dist/shared/lib/router/utils/interception-routes.js` của **chính bản
15.5.22 đang cài**, không phải theo trí nhớ hay tài liệu. `normalizeAppPath` (trong
`app-paths.js`) bỏ **cả** segment nhóm `(…)` **lẫn** segment slot `@…`, nên
`/(app)/projects/[id]/@modal/` chuẩn hóa về `/projects/[id]` và `(.)tasks/[taskId]` ghép ra
đúng `/projects/[id]/tasks/[taskId]`. Dùng `(..)` sẽ trỏ nhầm sang `/projects/tasks/…` — một
route không tồn tại, và triệu chứng là "dialog không bao giờ hiện" mà không có lỗi nào.

🔴 **`@modal/default.tsx` không phải tùy chọn.** Với mọi URL dưới `projects/[id]` mà slot
không khớp — tức gần như mọi URL — `next-app-loader` đi tìm đúng file đó; không có nó thì nó
rơi về `PARALLEL_ROUTE_DEFAULT_PATH` và gọi `notFound()`, tức **404 cho cả trang**. Hình dạng
lỗi rất khó truy: *board* tự nhiên 404 vì một file **không** được tạo ở chỗ khác.

**Đã bác một phương án trông sạch hơn:** gom bốn tab vào route group `[id]/(tabs)/` để trang
task không phải chia layout với thanh tab. Interception vẫn chạy (route group không tính vào
đường dẫn), **nhưng** flight router state giữ nguyên segment nhóm, nên
`useSelectedLayoutSegment()` trong `project-tabs.tsx` trả `'(tabs)'` thay vì `'board'` và
**thanh tab mất hẳn trạng thái active** — hỏng im lặng, chỉ thấy bằng mắt. Thay bằng một dòng
`showTabs = useSelectedLayoutSegment() !== 'tasks'`, và dòng đó còn *đúng hơn*: lúc dialog đè
lên board, `children` vẫn là board nên tab vẫn hiện và vẫn active — đúng hành vi Jira.

**Đóng dialog = `router.back()`**, `open` truyền cứng `true`. Trạng thái mở/đóng **chính là
URL**, nên Escape, bấm nền và nút Back của trình duyệt đi chung một đường, không phải đồng bộ
gì thêm. Link tới subtask và task liên quan dùng `<Link replace>`: với `push`, thoát khỏi một
chuỗi subtask ba tầng phải bấm Back đúng ba lần, và `router.back()` rơi về task cha thay vì
về board.

#### ADR-044 (2026-08-03) — `PUT /tasks/{id}` ghi đè toàn phần: một trục ghi duy nhất

**Phát hiện trong lúc khảo sát, không ai đi tìm:** `TaskService.UpdateAsync` gán thẳng cả
bốn trường (`Name`/`Description`/`DueDate`/`Priority`) từ request — đây là **PUT thật**, không
phải PATCH. Nhưng `task-form-dialog.tsx` **chưa bao giờ gửi `description`**, nên record C#
bind `Description = null` và **mô tả bị xóa trắng mỗi lần sửa tên task**.

Lỗi này build sạch, test xanh, và **không lộ ra** suốt thời gian chưa màn nào ghi được mô tả —
nó sẽ nổ đúng vào ngày màn chi tiết Task lên. Cùng lớp với ba phát hiện của phiên trước
(`ProjectService` không ghi ActivityLog, `ValidationFilter` không chạy cho upload, §10 mô tả
quyền không tồn tại): *thứ cần kiểm chứng chưa có ai gọi tới.*

**Hai quyết định để nó không tái diễn:**
1. **Form sửa task nay có ô Mô tả**, và `taskSchema.description` là trường **bắt buộc của
   form** (không optional) — chú thích ngay tại schema nói rõ vì sao.
2. **Màn chi tiết Task có đúng MỘT chỗ gọi PUT**: `useTaskFieldSave`. Bốn khối sửa được (tên,
   mô tả, ưu tiên, hạn) đều đi qua một closure nhận `Partial<>` rồi tự điền các trường còn
   lại từ bản chi tiết hiện tại. Cho mỗi khối tự gọi mutation là tạo bốn cơ hội quên một
   trường.

⚠️ Trong closure đó, `patch.dueDate ?? current.dueDate` là **sai** với hai trường nullable:
xóa hạn/xóa mô tả gửi `null`, mà `null ?? current` lấy lại giá trị cũ nên phép xóa im lặng
không có tác dụng. Phải phân biệt "không truyền" (`undefined`) với "truyền `null`".

**Ba bước 409 của ADR-016 giữ nguyên**, cộng một luật mới: **khóa mọi nút lưu khi
`detail.isFetching`** — trong đó có lượt tải lại sau 409. Bấm Lưu lúc đó là gửi lại đúng
`rowVersion` đã chết, và người dùng rơi vào 409 vĩnh viễn. Đã kiểm bằng tay: sửa từ tab thứ
hai → 409 → banner + tự tải lại → **lần thử thứ hai thành công**.

#### Đính chính 2026-07-30 — CORS ghi ✅ nhưng chưa từng hoạt động

Cùng mạch với hai lần đính chính trước (ADR-008: tài liệu nói đã xóa `Project.SoftDelete()`
nhưng method vẫn còn; ADR-016: `RowVersion` chỉ có ở schema). Lần này là **cấu hình pipeline**.

ADR ngày 2026-07-29 ghi "Chuẩn hóa CORS Policy ✅ — đã có `AddCors`/`UseCors` với policy
`PmsFrontend`". Cả hai lệnh đó đều có thật trong `Program.cs`. Nhưng **không một header CORS nào
được phát ra**, vì hai lỗi độc lập cộng lại:

1. `app.UseCors()` gọi **không tham số** nên đi tìm DEFAULT policy, trong khi code chỉ khai báo
   policy *đặt tên* và không gọi `AddDefaultPolicy`. `CorsMiddleware` không tìm thấy policy thì
   chỉ ghi log rồi gọi `next()` — không exception, không lỗi.
2. `builder.Configuration.GetSection("Cors:AllowedOrigins")` đọc **ngay** tại thời điểm dòng đó
   chạy, nên chỉ thấy các nguồn cấu hình đã đăng ký tới lúc ấy. Nguồn thêm sau bị bỏ qua hoàn
   toàn → policy nhận mảng origin **rỗng**, tức là dù có gắn đúng tên policy thì vẫn không có
   origin nào khớp.

**Cách phát hiện:** viết test trước khi sửa. Test đỏ 3/4 → sửa lỗi 1 → **vẫn đỏ** → in
`IOptions<CorsOptions>` ra và thấy `CONFIG Cors:AllowedOrigins = [http://localhost:3000]` nhưng
`POLICY origins = []`, `DEFAULT policy null? True`. Nếu chỉ sửa lỗi 1 rồi tin vào chẩn đoán ban
đầu thì bug vẫn còn nguyên và ADR lại được đánh ✅ lần thứ hai.

**Cách sửa:** truyền tên policy tường minh, và đọc origin qua
`AddOptions<CorsOptions>().Configure<IConfiguration>(...)` để hoãn tới lúc DI resolve.

**Vì sao sống sót được nhiều phiên:** lỗi 2 không lộ khi `dotnet run` (`WebApplicationBuilder`
đã nạp sẵn appsettings + user-secrets + biến môi trường trước khi tới dòng đó), và lỗi 1 chỉ
biểu hiện ở browser — thứ mà dự án chưa có vì `frontend/` chưa tồn tại.

**Rút ra, bổ sung cho bài học 2026-07-30 bên dưới:** bài học đó nói "entity viết trước service
thì không có gì kiểm chứng nó". Lần này mở rộng phạm vi: **cấu hình pipeline mà chưa có client
thật gọi vào thì cũng không có gì kiểm chứng nó**, và middleware của ASP.NET Core có xu hướng
*bỏ qua im lặng* thay vì nổ — nên "build sạch, test xanh, ADR ghi ✅" vẫn có thể là ba lời khai
sai cùng lúc. Từ nay cấu hình pipeline có ảnh hưởng nghiệp vụ (CORS, rate limit, auth scheme,
JSON options) phải có ít nhất một integration test chạm vào hành vi thật của nó — đó là lý do
§11 có thêm nhóm "Hạ tầng API".

#### Bài học 2026-07-30 — "chỉ có ở domain nhưng chưa ai gọi nên chưa lộ"

Cùng mạch với bài học của ADR-008 (`Sprint` thiếu `ISoftDeletable`) và ADR-016 (`RowVersion`
chỉ có ở schema). Trước khi dựng `TaskService`, đối chiếu `TaskItem` với các ADR đã chốt
phát hiện **năm lỗi thật** trong code đã build sạch và không test nào bắt được:

1. `AddAssignee`/`LinkTo` tạo entity con mà không sinh `Id`. `ApplyIdNeverGenerated()` tắt
   sinh Id ở DB nên bản ghi thứ hai vi phạm khóa chính. `DbSeeder` gọi `AddAssignee` 10 lần
   — seeder trên DB trắng sẽ chết. Không ai thấy vì seeder có guard "đã có data thì bỏ qua",
   còn `PmsTestDb` chạy môi trường `Testing` nên không seed. Kiểm chứng bằng cách chạy seeder
   trên database tạm: 10 bản ghi, 10 Id phân biệt.
2. `AddSubtask` ném `InvalidOperationException` → middleware trả **500** thay vì 409. Đúng
   loại lỗi mà ADR-011 sinh ra để chặn, nhưng chưa có caller nên chưa lộ.
3. `TaskItem.SoftDelete()` vẫn tồn tại dù ADR-008 đã tuyên bố xóa — lặp lại đúng tình huống
   "tài liệu đi trước code" mà ADR-008 đã phải đính chính một lần cho `Project`.
4. `PmsDbContext.SaveChanges(bool)` đồng bộ thiếu `ApplyAuditFields()` mà bản async có.
5. `AddAssignee` không gán navigation `Employee`, nên map bản ghi vừa tạo sang DTO là NRE —
   lỗi này chỉ lộ khi `TaskAssignmentServiceTests` được viết.

**Rút ra:** entity viết trước service thì không có gì kiểm chứng nó, và "build sạch" không
nói lên điều gì về ràng buộc runtime (`ValueGeneratedNever`, mapping middleware, EF fixup).
Từ nay khi một entity nằm chờ nhiều phiên trước lúc có service dùng tới, phải đối chiếu lại
với ADR **trước** khi xây tầng trên, coi như một bước bắt buộc chứ không phải tùy hứng.

#### Chi tiết ADR-045 → ADR-047 (phiên "phân quyền permission", 2026-08-04)

#### ADR-045 (2026-08-04) — Phân quyền tầng 1 bằng claim `permission` lấy từ DB

**Bối cảnh.** Trước phiên này quyền hệ thống chỉ là hai policy viết tay trong `Program.cs`:
`require-system-admin` (kiểm `ClaimTypes.Role`) và `can-create-project` (một no-op). Hệ quả:
không tách được "đọc nhật ký" khỏi "khóa tài khoản", và muốn đổi bất cứ gì cũng phải sửa
code rồi deploy lại. Yêu cầu đặt ra là **admin phải quản lý được quyền của từng vai trò**.

**Bốn điểm phải chốt trước khi gõ dòng code đầu tiên** — ghi lại nguyên văn vì chúng mới là
nội dung thật của quyết định này:

**(a) Quyền project-scoped KHÔNG vào token.** Đây là điểm chí mạng. Một người có vai trò
**khác nhau ở từng project** (`ProjectMember.RoleInProject`). Nhét quyền per-project vào
claim nghĩa là token phình tuyến tính theo số project, và tệ hơn: **nó cũ đi ngay khi PM đổi
vai trò của ai đó**. Hiện tại đổi vai trò có hiệu lực **tức thì** vì
`ProjectAuthorizationService` đọc lại `ProjectMember` mỗi request; chuyển sang claim là chấp
nhận độ trễ tối đa bằng tuổi access token — trong đó có cả ca "vừa gỡ một người khỏi project
mà họ vẫn ghi được". Tiền lệ đã có: ADR-015 phải thu hồi refresh token khi khóa tài khoản,
đúng vì lý do này.

→ **Mô hình LAI**: claim cho tầng 1, DB-mỗi-request cho tầng 2. `ProjectPermissions.cs` và
`ProjectAuthorizationService.cs` **không sửa một dòng nào** trong cả phiên.

**(b) Danh mục và cách thay policy.** Năm mã dạng `resource:action`:
`employees:manage` · `audit:read` · `labels:manage` · `projects:create` · `roles:manage`.
Nguồn sự thật là hai bảng `Permission` (danh mục) + `RolePermission` (vai trò → quyền), seed
bằng **`HasData`**. Policy đăng ký bằng một vòng lặp trên danh mục, **tên policy == mã
quyền**; hai tên cũ bị xóa hẳn.

> 🔴 **`HasData` chứ KHÔNG phải `DbSeeder` — ba lý do độc lập, mỗi lý do đủ để quyết định:**
> `PmsWebApplicationFactory` chỉ chạy `EnsureDeleted + Migrate` nên không gọi `DbSeeder`;
> `DbSeeder` lại nằm trong nhánh `IsDevelopment()` còn test dùng env `Testing`; và nó còn
> early-return khi DB đã có Employee. Không có hàng permission thì `projects:create` biến
> mất và **gần như toàn bộ suite tích hợp đỏ** — hàng chục test chẳng liên quan gì tới quyền,
> chỉ vì chúng gọi `CreateProjectAsync`. Hệ quả cần nhớ: **hàng permission nay là SCHEMA,
> không phải data.**

> 🔴 **`projects:create` phải cấp cho cả vai trò `User`.** §10 vốn ghi "mọi `User` đều có
> quyền tạo Project mới", và policy cũ là no-op nên điều đó luôn đúng. Chỉ cấp cho admin là
> đổi hành vi sản phẩm giấu trong một refactor.

**(c) Chống mở lại god mode.** Cám dỗ tự nhiên của mô hình permission là thêm
`projects:read:all`. Danh mục vì vậy là **ĐÓNG** và có `SystemPermissionsCatalogTests` khóa
bằng bốn phép kiểm độc lập: `All` phải khớp một mảng literal viết trong test; mọi `const`
phải nằm trong `All`; **không mã nào được mang phạm vi project** — ngoại lệ duy nhất
`projects:create` phải được gọi **đích danh**; và định dạng `^[a-z-]+:[a-z-]+$`.

> `projects:create` là ngoại lệ hợp lệ chứ không phải rò rỉ: lúc gọi endpoint đó **chưa có
> project nào** để tra membership, nên không có tầng 2 nào để đi qua. Mọi động từ project
> khác đều cần một project đã tồn tại.
>
> Đã **mutation test** chốt chặn này: thêm tạm `projects:read:all` vào danh mục làm **ba
> test đỏ độc lập**, rồi hoàn nguyên.

**(d) Frontend không giải mã JWT.** Cùng một nguồn DB ra hai mặt: **claim** cho server cưỡng
chế, **`EmployeeDto.permissions`** cho UI gác nút. Client chưa từng có dòng nào đọc nội dung
token (ADR-027) và thêm một bộ phân tích token ở đó là thêm một chỗ nữa để lệch.
`lib/tasks/permissions.ts` (tầng 2) **không đổi**; tầng 1 là file mới
`lib/auth/system-permissions.ts`. Hai file, hai tầng, không có mô hình song song.

**Ba cái bẫy đã trả giá trong lúc làm:**

1. 🔴 **`AuthController.Me()` dựng `EmployeeDto` từ CLAIM, không đọc DB.** Chỉ nối dây quyền
   ở `AuthService` thì `/auth/login` trả quyền thật còn `/auth/me` trả mảng rỗng — hai câu
   trả lời mâu thuẫn từ cùng một kiểu DTO, và người dùng thấy cái nào phụ thuộc vào việc họ
   vừa đăng nhập hay vừa F5. **Không có gì bắt lỗi này lúc biên dịch.** Nay có test đối chiếu
   thẳng hai endpoint.
2. **`#pragma warning disable` phải nằm TRƯỚC attribute.** Span của chẩn đoán bắt đầu ở danh
   sách attribute chứ không ở dòng khai báo method, nên đặt pragma xen giữa
   `[MapperIgnoreTarget]` và method làm **15 cảnh báo RMG020 quay lại**.
3. **Lưu quyền tự đăng xuất chính người đang thao tác.** Thu hồi refresh token của "mọi người
   mang vai trò đó" bao gồm cả admin đang bấm nút. Đúng hợp đồng bảo mật, nhưng UI phải nói
   thẳng — banner cảnh báo ghi rõ "kể cả phiên của chính bạn".

**Thu hồi và giới hạn đã biết.** Đổi quyền một vai trò thu hồi mọi refresh token của người
mang vai trò đó, kéo cửa sổ dùng quyền cũ từ 7 ngày (tuổi refresh token) xuống 15 phút (tuổi
access token) — cùng cách xử lý và cùng lý do với ADR-015. Cửa sổ 15 phút đó **vẫn còn** và
được nói thẳng trên UI. Cột `TokenVersion` kiểm mỗi request sẽ tức thì nhưng biến mọi request
thành một lượt đọc DB, tức đảo ngược chính lý do đưa quyền vào token — ghi là giới hạn đã
biết, **không xây dở dang**.

**Bất biến chống tự khóa.** `SystemAdmin` luôn phải giữ `roles:manage` (409 nếu gỡ). Đây là
quyền **tự phục hồi duy nhất**: mất nó thì màn phân quyền không vào được, `DbSeeder` không
chạy ở production, `HasData` chỉ áp lúc migrate mới — phục hồi sẽ phải sửa bảng bằng tay
trong SSMS. Bất biến giữ **tối thiểu đúng một mã**: bất biến quá rộng là cách một mô hình
permission lặng lẽ trở lại thành mô hình role cứng.

**Hai thứ cố ý KHÔNG làm:**
- **Không cache** tập quyền. Query là seek trên khóa clustered trả ≤5 hàng; cache đánh đổi
  một vấn đề hiệu năng chưa ai đo lấy một vấn đề bảo mật vô hình (admin gỡ quyền mà cache
  vẫn phát), và ở nhiều instance thì việc vô hiệu hóa cache không còn cục bộ.
- **Không thêm `Permissions` vào `ICurrentUserService`** — và còn **xóa `SystemRole` khỏi
  đó**. Cưỡng chế 100% ở tầng policy; thêm một member không có người đọc là dựng lại đúng
  hình dạng lỗi mà `ValidationFilter` tra `IValidator<IFormFile>` đã trả giá. Sau ADR-045
  `SystemRole` ở đó còn **gây hiểu nhầm**: người đọc sẽ tưởng vai trò vẫn là trục phân quyền.

> 📌 Ghi lại một khác biệt nhỏ để lần sau không tưởng là sót: `RolePermissions.SystemRole`
> lưu dạng **chuỗi** (đọc được bằng mắt lúc điều tra), còn `Employees.SystemRole` lưu dạng
> **int**. Vô hại vì không bao giờ có JOIN giữa hai cột đó.

**Không thêm validator cho `RefreshTokenRequest`, và đó là quyết định.** Bản kiểm kê liệt kê
nó là "thiếu". Đúng là không có, nhưng thêm vào sẽ là **code chết**: cả `/auth/refresh` lẫn
`/auth/logout` đều dựng DTO đó **bên trong thân action** từ cookie, không phải tham số bind,
nên `ValidationFilter` (duyệt action arguments) không bao giờ nhìn thấy. Hành vi đúng đã có
sẵn và có test giữ (`Refresh_khong_co_cookie_tra_401`).

---

#### ADR-046 (2026-08-04) — Kiểm kê nợ backend: "0 warning" là ảnh chụp, không phải bất biến

**Phát hiện lớn nhất, và không ai đi tìm nó:** `GET /projects/{id}/statistics` trả **500 ở
MỌI lần gọi** kể từ commit tạo ra nó (`845de0a`, 2026-08-03). `.OrderByDescending(x =>
x.Total)` đặt **sau** `.Select(...)` — EF không dịch được thứ tự trên property của một record
vừa dựng trong projection. Endpoint được ghi ✅ "Xong 2026-08-03", **chưa từng có test nào
chạm tới**, nên nó hỏng hoàn toàn suốt từ đó. Đây là **lần thứ năm** dự án gặp cùng một hình
dạng lỗi (sau `ProjectService` không ghi ActivityLog, `ValidationFilter` không chạy cho
upload, §10 mô tả quyền không tồn tại, CORS ghi ✅ mà chưa từng hoạt động):
*thứ cần kiểm chứng chưa có ai gọi tới.*

`StatisticsTests` — viết ra chính vì khoảng trống đó — bắt được nó ở **lần chạy đầu tiên**.

**Đã sửa:**

| Lỗ hổng | Vì sao nó lọt |
|---|---|
| `ActivityLogRepository` **nhận `?search=` rồi bỏ qua im lặng** ở cả 3 endpoint | Trả HTTP 200 kèm nguyên trang chưa lọc — client không có cách nào phát hiện. Là repository cuối cùng còn sót; 5 repo kia đều lọc thật |
| Thiếu validator `CreateTaskLinkRequest` → `LinkType` không hợp lệ **được lưu xuống DB** | `ValidationFilter` bỏ qua im lặng khi không có `IValidator<T>`; không cảnh báo, không lỗi |
| Thiếu validator `ChangeTaskStatusRequest` | Như trên — `{"target":99}` tới thẳng state machine |
| `LabelService.CreateAsync` không ghi ActivityLog | Bất đối xứng vô tình với Update/Delete, và lệch đúng chỗ nguy hiểm: tạo nhãn là thao tác duy nhất trong nhóm mà **mọi user** làm được |
| Thiếu tie-break `.ThenBy(Id)` ở Employee/Project/Notification | Hai bản ghi cùng khóa sắp xếp có thứ tự không xác định → phân trang trả trùng/sót dòng. Chỉ lộ khi dữ liệu đủ nhiều, tức ở production |
| `/health` trả `Healthy` kể cả khi mất SQL | `AddHealthChecks()` trần không kiểm gì cả. Đứng sau load balancer thì đó là chủ động giữ một instance đã hỏng trong vòng nhận traffic. Đã kiểm thật bằng `docker stop pms-sqlserver` |
| `TreatWarningsAsErrors` không bật ở project nào | "0 warning" trong tài liệu là quan sát một lần build, không phải điều kiện. Nay có `backend/Directory.Build.props` |

**Hai đính chính so với bản kiểm kê ban đầu** — ghi lại để không ai đi tìm lại:

1. Bản kiểm kê nói **thiếu 7 index khóa ngoại. SAI.** Đã kiểm `sys.indexes` trên database
   thật: **6/7 đã có sẵn** do EF Core tự sinh index cho cột khóa ngoại theo quy ước. Chỉ
   index **ghép** `(DueDate, Status)` là thật sự thiếu — không quy ước nào tạo hộ, và
   `DueDateNotificationWorker` quét đúng hai cột đó ở mỗi nhịp timer. Các khai báo thừa đã
   gỡ **cùng những comment nói sai mà chúng mang theo**.
2. Bản kiểm kê nói thiếu validator `RefreshTokenRequest` — xem ADR-045, đó là code chết.

> **Rút ra, bổ sung cho bài học 2026-07-30:** một bản kiểm kê tự động cũng là một *lời khai*,
> và phải đối chiếu với hệ thống thật trước khi hành động theo nó. Hai trong bảy hạng mục
> "thiếu" hóa ra là dương tính giả; làm theo mà không kiểm thì kết quả là code thừa cộng với
> **comment khẳng định một điều sai** — tức là đúng thứ tài liệu này tồn tại để chống lại.

---

#### ADR-046b (2026-08-04) — Mọi mốc thời gian trong ứng dụng lệch đúng bằng múi giờ

Ghi thành mục riêng vì nó không thuộc bản kiểm kê — nó lộ ra khi nhìn màn nhật ký hệ thống
và thấy một thao tác **vừa mới làm** hiện là *"7 giờ trước"*.

**Nguyên nhân.** `datetime2` của SQL Server **không lưu `DateTimeKind`**. Ghi một `DateTime`
có `Kind = Utc` xuống rồi đọc lên thì nhận lại `Kind = Unspecified`, và `System.Text.Json`
serialize giá trị Unspecified **không kèm hậu tố** — ra `"2026-08-04T14:15:06"` thay vì
`"...Z"`. Trình duyệt hiểu chuỗi không hậu tố là **giờ địa phương**, nên mọi mốc lệch đi đúng
bằng chênh múi giờ (+7 ở Việt Nam).

**Phạm vi:** hạn hoàn thành, bình luận, thông báo, nhật ký hoạt động — gần như mọi màn hình.
Chỉ `accessTokenExpiresAt` là đúng, vì giá trị đó không bao giờ đi qua EF.

**Cách sửa:** một `ValueConverter` đóng dấu lại `Kind = Utc` **lúc đọc**, áp cho mọi cột
`DateTime`/`DateTime?` trong `OnModelCreating`. Không migration, không dịch chuyển thời điểm
nào — chỉ khôi phục thông tin mà tầng lưu trữ đánh rơi.

> Đã loại phương án cấu hình `JsonSerializerOptions` ở tầng API: nó chỉ vá đường đi ra HTTP,
> trong khi cùng giá trị `Unspecified` đó còn chảy vào so sánh nghiệp vụ (`IsOverdue`,
> `DueDate < UtcNow`) và vào `DueDateNotifier`. Sửa ở tầng đọc là sửa một lần cho mọi người
> tiêu thụ.

🔴 **Hệ quả bắt buộc phải biết:** sau khi một cột có `ValueConverter`, **EF không dịch được
`.Date` trên cột đó** — nó ném lúc chạy, thành HTTP 500. Bốn chỗ lọc theo hạn đã đổi sang so
sánh thẳng với mốc nửa đêm (`DueDate < today`, tương đương về mặt toán học). Ai thêm truy vấn
theo ngày sau này phải nhớ luật đó.

> **Vì sao nó sống sót lâu như vậy:** không test nào so **chuỗi JSON thô** của một mốc thời
> gian. Test so `DateTime` với `DateTime` thì `Kind` không ảnh hưởng tới toán tử so sánh, nên
> mọi khẳng định đều xanh. Đáng chú ý hơn cả: comment ở `frontend/lib/format.ts:92-94` đã
> **dự báo đúng lỗi này** ("nếu về sau có endpoint nào trả `DateTime` không hậu tố thì mọi
> mốc lệch đi đúng bằng chênh múi giờ — kiểm chuỗi thô trước khi nghi ngờ hàm này"). Nó nằm
> đó từ trước; chỉ là chưa ai đi kiểm chuỗi thô.

---

#### ADR-047 (2026-08-04) — Màu biểu đồ chia theo VIỆC nó làm, và hai lỗi chỉ thấy bằng mắt

Màn thống kê không dùng bảng màu chung cho mọi biểu đồ, mà chia **ba nhóm token** theo đúng
công việc của màu (`globals.css`):

| Nhóm | Việc | Dùng ở đâu |
|---|---|---|
| `--viz-status-*` | **Trạng thái** — màu dành riêng, luôn kèm nhãn chữ | Biểu đồ theo trạng thái; dùng lại ngữ nghĩa của `status-tone.ts` để khớp với board |
| `--viz-seq-*` | **Tuần tự** một sắc, nhạt → đậm | Độ ưu tiên (Highest…Lowest là thang CÓ THỨ TỰ, không phải danh mục ngang hàng) |
| `--viz-load-*` | Ba phân đoạn phải phân biệt bằng màu | Biểu đồ khối lượng theo người |

Chỉ nhóm thứ ba là bộ người đọc **buộc** phải phân biệt bằng màu, nên chỉ nó cần chạy
validator — PASS toàn bộ ở cả hai chế độ. Chế độ sáng có cảnh báo tương phản < 3:1 ở màu xanh
lá → **bắt buộc nhãn số hiện rõ**, nên mỗi hàng luôn in "x/y xong" thay vì chỉ tô màu. Bước
màu chế độ tối là bộ **riêng** đã validate trên nền tối, không phải phép lật tự động.

**Hai khối cố ý KHÔNG phải biểu đồ:** ba con số đầu trang là **thẻ số** (tỷ lệ hoàn thành là
một **thanh mức**, không phải biểu đồ tròn hai lát); tiến độ sprint là **danh sách thanh
mức**, vì mỗi sprint tính theo phạm vi riêng nên vẽ chung sẽ ngầm mời người đọc so chiều cao
với nhau — tức mời họ đọc sai.

🔴 **Hai lỗi validator không bắt được, vì nó kiểm màu chứ không kiểm bố cục:**

1. Rãnh nền thanh mức ban đầu dùng bậc `--viz-seq-1`. Ở chế độ tối bậc đó đủ bão hòa để một
   sprint **"0/2" hiện ra thanh xanh ĐẦY chiều ngang** — đọc thành đã xong 100%. Rãnh nền
   phải trung tính (`--muted`); chỉ phần đã đầy mới được mang màu.
2. Trang tràn ngang **104px** ở 375px. Nguyên nhân nằm ở **gốc layout dự án**, không ở trang
   thống kê: `app/(app)/projects/[id]/layout.tsx` có một `div.grid` là grid item với
   `min-width:auto`, nên bề rộng bị sàn hóa theo min-content của tab đang mở thay vì theo
   viewport. Recharts `ResponsiveContainer` đo bề rộng của cha, nên nó và cái sàn đó nuôi
   nhau thành một vòng nở ra không có điểm dừng. Sửa ở gốc layout — và điều đó **đồng thời
   sửa luôn** lỗi tràn 8px của trang Board vốn đã được ghi nhận là "có sẵn từ trước".

> **Rút ra:** quy trình màu là thứ **tính được**, nên phải chạy validator thay vì nhìn. Nhưng
> validator chỉ kiểm màu — bố cục thì bắt buộc phải **mở ra nhìn**. Cả hai lỗi trên đều chỉ
> hiện ra ở bước chụp màn hình, sau khi mọi phép kiểm màu đã xanh.

---

#### ADR-048 (2026-08-04) — Ba tính năng tầng 3, và ba ranh giới an ninh trong đó

**1. `Project.Status` — cho một trường chết một đường ghi.** `Project.Complete()` có đúng
một caller trong toàn bộ solution (`DbSeeder`), nên mọi project tạo qua API vĩnh viễn ở
`ToDo` — trong khi `Status` vẫn nằm trong DTO và vẫn là khóa `sortBy` hợp lệ.

Tách thành `POST /complete` + `/reopen` chứ **không** thêm một trường vào
`UpdateProjectRequest`: `Status` là chuyển trạng thái có luật riêng (mở lại project chưa xong
là vô nghĩa → 409), còn Update là ghi đè thông tin mô tả. Gộp lại thì mỗi lần sửa tên project
cũng phải gửi kèm status, và quên gửi là đặt lại trạng thái — **đúng lỗi ADR-044 đã trả giá**
với `description` của task. Cùng lý do đó, hai endpoint này **không cần `RowVersion`**
(ADR-021).

> 🔴 Phải thêm `NotificationType.**ProjectStatusChanged**` chứ không tái dùng `StatusChanged`.
> `RelatedEntityKind` được **suy ra** từ `Type` (ADR-025) và `StatusChanged` suy ra `Task`,
> nên tái dùng sẽ khiến chuông điều hướng tới `/tasks/{projectId}` — một id không tồn tại.
> Đây chính là loại lệch mà việc suy-ra (thay vì lưu hai cột độc lập) sinh ra để chặn: nó
> biến một lỗi thầm lặng thành một quyết định phải nghĩ.

**2. `GET /employees?search=` — mở cho mọi người, nên ràng buộc LÀ tính năng.** Trước đó chỉ
có `GET /admin/employees` sau quyền `employees:manage`, nên PM bình thường muốn mời ai vào dự
án phải **gõ đúng địa chỉ email**.

Ba ràng buộc, và cả ba đều là lý do endpoint được phép tồn tại chứ không phải chi tiết cài đặt:

| Ràng buộc | Bỏ đi thì sao |
|---|---|
| Từ khóa **≥ 2 ký tự** (400 nếu ngắn hơn) | Một ký tự khớp phần lớn danh bạ; lặp 26 lần là có toàn bộ |
| **Trần kết quả cứng ở server**, không nhận từ client | Client tự chọn `limit=10000` là mở lại đúng cánh cửa vừa khép |
| DTO **chỉ 3 trường** (`id`, `name`, `email`) | Mỗi trường thêm vào là một mẩu thông tin nhân sự phát cho toàn công ty |

Trả **400** chứ không phải danh sách rỗng khi từ khóa ngắn: rỗng khiến người dùng tưởng
"không có ai tên vậy", trong khi thật ra họ mới gõ chưa đủ.

> 📌 Test cho ràng buộc thứ ba khẳng định trên **JSON thô**, không deserialize vào record —
> deserialize sẽ âm thầm bỏ qua mọi trường thừa và test vẫn xanh trong khi API vẫn đang rò rỉ.
> Cùng lớp bài học với ADR-046b: khi một giá trị đi ra ngoài dưới dạng **chuỗi**, phải có ít
> nhất một test chạm vào chuỗi đó.

**3. @mention — client gửi ID, server LỌC.** Server cố ý **không parse `@tên`** từ nội dung:
tên hiển thị không phải định danh (trùng tên, đổi tên, `@abc` có thể chỉ là một mẩu email).
Client vốn đã biết chính xác id — nó lấy từ chính ô gợi ý người dùng vừa chọn.

> 🔴 **Nhưng chính vì id do client gửi, server bắt buộc phải lọc lại.** Không lọc nghĩa là
> bất kỳ ai cũng bắn được thông báo tới bất kỳ ai bằng cách nhét id lạ vào body, và người
> nhận sẽ thấy **tên một task thuộc dự án họ không có quyền mở** — vừa là rò rỉ thông tin,
> vừa là một kênh quấy rối. Chỉ giữ lại thành viên `Accepted` của đúng dự án đó.
>
> **Đã mutation test**: bỏ bộ lọc làm 2 test đỏ.

Hai chi tiết nhỏ nhưng có chủ đích:
- Người được nhắc **bị loại khỏi lượt `CommentAdded`** — hai thông báo cho cùng một hành động
  là nhiễu, và cái cụ thể hơn ("bạn được nhắc tên") thắng.
- Nhắc tên người ngoài dự án vẫn trả **thành công**. Bình luận là hợp lệ, chỉ phần nhắc tên
  bị lọc bỏ; trả 400 ở đây sẽ tiết lộ "id này có tồn tại nhưng không thuộc dự án" — tức lại
  là rò rỉ, chỉ đổi hình dạng.

**Hạng mục thứ tư của tầng 3 — vòng đời Sprint — CHƯA làm**, và đó là quyết định chứ không
phải bỏ sót: nó cần một lựa chọn sản phẩm (*task chưa xong đi đâu khi đóng sprint?*) mà nhiều
câu trả lời đều bảo vệ được. Chi tiết và phụ thuộc ở §1 mục E.

> 🆕 **Frontend của cả ba đã dựng xong 2026-08-05.** Một điểm bổ sung mà chỉ lộ ra khi làm
> giao diện: vì server nhận **id** còn người dùng sửa **chữ**, hai thứ đó **trôi khỏi nhau
> được** — chọn `@Nam` rồi xóa chữ đó đi trước khi gửi thì id vẫn nằm trong state, và một
> bình luận không hề nhắc ai vẫn bắn thông báo "bạn được nhắc tới". Client vì vậy phải lọc
> lại lần nữa theo nội dung thật lúc submit (`lib/comments/mentions.ts`, `reconcileMentions`).
>
> Nói cách khác: **bộ lọc của server chống được kẻ xấu, nhưng không chống được người dùng
> bình thường đổi ý** — hai bộ lọc giải hai bài toán khác nhau, không cái nào thay được cái
> nào. Đã kiểm chứng trên máy chủ thật chứ không chỉ bằng unit test: chọn hai người, xóa chữ
> của một người, gửi → chỉ người còn tên trong bài nhận `Mentioned`.

---

#### ADR-049 (2026-08-05) — Hồ sơ cá nhân CHỈ ĐỌC, vì `/auth/me` dựng từ claim

**Quyết định:** dựng `/profile` **chỉ đọc** ngay, và **hoãn** toàn bộ đường ghi
(`PUT /employees/me`, đổi mật khẩu khi đã đăng nhập) sang một phiên riêng.

**Vấn đề không nằm ở endpoint còn thiếu.** Viết `PUT /employees/me` là việc của một buổi
chiều. Thứ chặn là: `GET /auth/me` **dựng `EmployeeDto` từ CLAIM chứ không đọc DB** (ADR-045),
và access token sống 15 phút (ADR-027). Ghép hai điều đó lại, một nút "Lưu" ngây thơ sẽ cho
ra màn hình **báo lưu thành công rồi vẫn hiện tên cũ suốt tới 15 phút** — kể cả sau F5, vì
phiên khôi phục bằng `/refresh` cũng lấy từ claim.

Đó là một lời nói dối tệ hơn hẳn việc không có nút: người dùng bấm lại lần hai, lần ba, rồi
kết luận hệ thống hỏng. **Không có nút thì họ biết phải đi hỏi ai; có nút mà nó nói dối thì
họ không biết gì cả.**

Hai đường ra, cả hai đều là quyết định kiến trúc chứ không phải chi tiết cài đặt — phiên sau
chọn một, đừng vừa gõ vừa nghĩ:

| Cách | Đổi lấy |
|---|---|
| Ghi xong thì **xoay token ngay** (trả `AuthenticatedResponse` mới từ chính endpoint ghi) | Giữ được claim là nguồn nhanh; nhưng mọi endpoint ghi hồ sơ nay phải biết về vòng đời token, và **các tab khác vẫn cũ tới 15 phút** |
| Cho `/auth/me` **đọc DB** | Hết lệch ngay lập tức ở mọi tab; nhưng mất đúng cái lợi mà ADR-045 chọn claim để có, và thêm một truy vấn vào đường nóng |

**Trang chỉ đọc không gọi `/auth/me`** — nó đọc `useAuthStore`. Cùng một bộ claim, nên thêm
một request chỉ tốn thời gian mà không mang lại thông tin nào mới.

> 🔴 **Kèm theo: một lỗi có sẵn được phát hiện đúng lúc gắn mục "Hồ sơ" vào `UserMenu` —
> mở menu người dùng làm SẬP cả menu.**
>
> `DropdownMenuLabel` ánh xạ sang `Menu.GroupLabel` của Base UI, thứ **bắt buộc** phải nằm
> trong một `Menu.Group`. `UserMenu` dùng nó trần từ ngày dựng → `MenuGroupContext is missing`
> → menu sập, kéo theo **lối ra duy nhất để Đăng xuất**. Đã kiểm chứng là lỗi có sẵn chứ
> không phải do phiên này: `git stash` thay đổi rồi mở lại — vẫn sập y hệt.
>
> **Cách sửa không phải bọc vào `Menu.Group`.** Khối đó hiển thị **danh tính** người đang
> đăng nhập, không phải nhãn cho một nhóm mục nào; bọc `Group` cho hết lỗi là hứa với trình
> đọc màn hình một quan hệ không tồn tại. Dùng `<div>` thường — bỏ hẳn chỗ dùng sai.
>
> Đây là lần thứ **sáu** dự án gặp đúng hình dạng lỗi §15 đã đặt tên từ 2026-07-30: *thứ cần
> kiểm chứng chưa có ai gọi tới.* Và lần này nó nằm ở chỗ khó tin nhất — nút **Đăng xuất**,
> thứ mà mọi phiên "đã kiểm chứng trên trình duyệt" đều nhìn thấy trên header mà **chưa ai
> bấm mở**. Bảng tiến độ ghi ✅ cho `UserMenu` từ 2026-07-31.
>
> **Rút ra, cụ thể hơn lần trước:** "kiểm chứng trên trình duyệt" mà chỉ đi theo luồng nghiệp
> vụ thì bỏ sót đúng những thứ nằm **ngoài** luồng — menu, dropdown, trang lỗi, trạng thái
> rỗng. Chúng không thuộc kịch bản nào nên không phiên nào có lý do bấm vào.

> 🆕 **Bổ sung cùng ngày:** khối "Quyền hệ thống" liệt kê `projects:create`,
> `employees:manage`… đã **gỡ khỏi `/profile`**. Đó là màn hình cho **người viết code**:
> người dùng cuối không hành động được gì với danh sách đó — họ không tự cấp quyền cho mình,
> và khi thiếu quyền thì UI đã ẩn nút sẵn rồi. Badge vai trò hệ thống là đủ; ai cần đọc ma
> trận quyền thì `/admin/roles` mới là chỗ của nó.

---

#### ADR-050 (2026-08-05) — Đóng sprint thì HỎI, không tự quyết hộ

**Đã chốt, CHƯA cài đặt.** Ghi trước để phiên làm vòng đời Sprint không phải quyết lại — đây
đúng là "quyết định sản phẩm phải chốt trước khi gõ dòng code đầu tiên" mà §1 mục E cảnh báo.

**Quyết định:** khi đóng một sprint, hiện dialog liệt kê **task chưa xong** và để người dùng
chọn nơi chúng đi tới — một sprint khác, hay về Backlog. Không tự động làm thay.

**Vì sao không chọn hai phương án tự động:**

| Phương án | Vì sao loại |
|---|---|
| Tự đẩy hết về Backlog | Đội chạy sprint liên tiếp phải kéo lại từng task vào sprint mới bằng tay — biến thao tác một lần thành thao tác N lần, đúng lúc người ta đang vội đóng sổ |
| Tự đẩy sang "sprint kế tiếp" | Phải định nghĩa "kế tiếp" là gì khi chưa ai tạo nó. Và **im lặng dồn việc sang sprint sau chính là cách làm sprint đó vỡ kế hoạch** — người lập kế hoạch không thấy phần nợ mình vừa nhận |

Điểm chung của cả hai: chúng quyết hộ một thứ mà **chỉ người đóng sprint mới biết** — task
đó còn giá trị không, có ai đang làm dở không, có nên cắt bỏ không. Hỏi một lần lúc đóng thì
rẻ; đoán sai thì người dùng phải đi dọn mà không biết là mình cần dọn.

⚠️ Việc này **cần cột `Sprint.Status` + migration** (hiện `IsActive` chỉ suy từ ngày, không có
mốc "đã đóng" nào). Và hạng mục **velocity** của nhóm báo cáo phụ thuộc nó: không có mốc đóng
sprint thì không có gì để đo tốc độ theo.

---

#### ADR-051 (2026-08-05) — Sidebar đổi theo ngữ cảnh, và hai vỏ Task phải khác nhau thật

Hai quyết định giao diện, chung một câu hỏi: *thứ này đang tồn tại để làm gì?*

**1. Sidebar: ba đường tới cùng một chỗ là THỪA, không phải đầy đủ.**

Bản dựng buổi sáng có đồng thời mục "Dự án", khối khu vực của dự án đang mở, và danh sách
"Dự án của tôi" — cộng thêm trang mặc định sau đăng nhập vốn đã là `/projects`. Sidebar dài
gần hết cột, mà phần lớn là lối đi trùng nhau.

Jira giải bằng **tách hai ngữ cảnh**: ngoài dự án thì sidebar là điều hướng toàn cục, vào
trong một dự án thì sidebar **thuộc về dự án đó**. Ta làm y vậy. Bảng đối chiếu ở §6.

> ⚠️ **Điều kiện đi kèm, không phải tùy chọn:** đã bỏ nav toàn cục khỏi tầm mắt thì **phải
> trả lại đường về ở chỗ nhìn thấy được** — link "Tất cả dự án" ở đầu sidebar. Có breadcrumb
> rồi vẫn thêm, vì breadcrumb là thứ người ta đọc khi **đã biết** mình đang tìm gì, không
> phải thứ đập vào mắt khi đang lạc.

**2. Chi tiết Task: trang thật là BẮT BUỘC, nhưng "giống hệt dialog" thì không.**

Câu hỏi đặt ra rất đúng: *nếu trang riêng không khác gì dialog thì nó tồn tại để làm gì?*

Nửa đầu của câu trả lời là **nó không phải lựa chọn**. Intercepting route `(.)` chỉ chặn
*soft navigation*; xóa `tasks/[taskId]/page.tsx` thì F5 → 404, chia sẻ link → 404, mở tab mới
→ 404. Trang phải có.

Nhưng nửa sau thì câu hỏi trúng đích: **hai vỏ nhìn y hệt nhau thì nút "Mở trang riêng" đang
hứa một khác biệt không tồn tại** — và trớ trêu là nó vừa được sửa cho chạy đúng ở đầu cùng
phiên, để rồi phần thưởng cho cú bấm là *không thấy gì đổi*.

Nay hai vỏ khác nhau về **cấu trúc**, không phải trang trí: trong dialog, khối
`Bình luận | Lịch sử` nằm trong cột trái; ở trang thật nó **xuống dưới hai cột và lấy trọn bề
ngang**.

| | Bề rộng ô soạn bình luận (đo ở viewport 1400px) |
|---|---|
| Dialog | **608px** |
| Trang thật | **1096px** — hơn **80%** |

Chọn đúng khối đó vì nó là phần **đọc-và-viết nhiều nhất** của màn hình, và cũng là phần chịu
thiệt nhất khi hẹp: mỗi dòng có avatar + tên + mốc thời gian rồi mới tới nội dung, nên trong
dialog một câu ngắn cũng xuống dòng ba lần.

> 📌 Đặt khối đó **ngoài** lưới hai cột chứ không phải `lg:col-span-2` bên trong: cột phải là
> `sticky`, cho khối này nằm cùng lưới sẽ kéo dài track và làm mốc dính nhảy khi đoạn bình
> luận dài ra.

---

#### ADR-052 (2026-08-05) — Cột board là DỮ LIỆU của từng project, không còn là enum

**Thay đổi lớn nhất của dự án tính tới nay.** Trạng thái task chuyển từ enum `Status` đóng
(4 giá trị do hệ thống định nghĩa) sang bảng `BoardColumns` do **người dùng cấu hình theo
từng project**: thêm, sửa, đổi màu, đổi thứ tự, xóa.

##### Vì sao nó đắt hơn vẻ ngoài

Con số đo trước khi bắt đầu, không phải cảm giác: `Status` bị tham chiếu **39 chỗ trong 10
file backend** và 35 file frontend, cộng một state machine viết cứng ở `TaskItem.cs`. Và
một cái bẫy chỉ lộ ra khi đo: **`Project.Status` và `TaskItem.Status` dùng CHUNG một enum**
— nên việc đầu tiên phải làm là tách chúng, nếu không trạng thái project bị cuốn theo trong
khi nó chỉ cần đúng bốn nấc.

##### 🔑 `StatusCategory` — thứ giữ cho cột tuỳ biến không phá vỡ phần còn lại

Phần lớn trong 39 chỗ đó không hỏi "task ở trạng thái nào" mà hỏi **"task xong chưa"**:
guard chặn task đang bị `Blocks`, `IsOverdue`, tiến độ subtask, mọi con số thống kê, guard
"không xóa project còn task chưa xong", job nhắc hạn.

Nếu cột chỉ có tên do người dùng đặt thì **không câu hỏi nào ở trên trả lời được** — một cột
tên "Đã ship" hay "Hủy bỏ" thì mã nguồn không có cách nào biết nó nghĩa là đã kết thúc.

Nên mỗi cột phải khai mình thuộc **một trong ba nhóm ĐÓNG**: `ToDo` · `InProgress` · `Done`.
Tên là của người dùng, nhóm là hợp đồng với mã nguồn. Jira giải đúng bằng cách này.

##### 🔴 `TaskItem.Category` — dữ liệu TRÙNG, có chủ đích

Nhóm được **lưu cứng trên chính task**, không đọc qua `BoardColumn`. Hai lý do, cả hai đều
có tiền lệ trả giá trong dự án:

1. **Computed property đọc navigation là NRE chờ sẵn.** `IsOverdue`/`SubtaskProgress` cần
   biết task đã kết thúc chưa; đọc `BoardColumn.Category` thì mọi query quên `Include` sẽ nổ
   lúc chạy — đúng bẫy đã khiến `SubtaskProgress` luôn trả 0 (ADR-034).
2. **EF dịch được thành SQL phẳng.** 39 chỗ `t.Status != Status.Done` thành
   `t.Category != Done` — đổi một định danh, **không viết lại query nào**, và index ghép
   `(DueDate, Category)` còn dùng được nguyên vẹn.

Giá phải trả: nó **trôi được**. Chốt chặn: `private set`, người ghi duy nhất là `MoveTo`, và
`BoardColumnService` bắt buộc gọi `SyncTaskCategoriesAsync` khi cột đổi nhóm.

##### 🗑️ Ma trận chuyển trạng thái bị GỠ — ADR-052 thay thế ADR-021

`CanTransitionTo` liệt kê sáu cặp hợp lệ. Đó là luật đúng khi hệ thống sở hữu bốn trạng
thái; với cột do **người dùng** tạo thì không còn cơ sở nào để nói cặp nào hợp lệ — hệ thống
không biết "Chờ QA" đứng trước hay sau "Đang sửa". Ép một luật lên đó là đoán hộ quy trình
của người khác.

Ba hệ quả, và **cả ba đều là test cũ bị đảo chiều**, không phải nới lỏng cho tiện:

| Trước | Sau |
|---|---|
| Kéo về đúng cột đang đứng → **409** | → **200** (no-op) |
| `ToDo → Done` → **409** ("nhảy bước") | → **200** |
| ADR-021 dùng "đứng yên là lỗi" thay `RowVersion` làm chốt concurrency | Chốt đó **mất** — đổi trạng thái nay idempotent, hai người cùng kéo về một cột ra kết quả giống nhau nên không có gì để tranh chấp |

Guard duy nhất còn lại: **cột đích thuộc nhóm `InProgress`** mà task đang bị chặn → 409. Điều
kiện đổi từ "target == InProgress" sang **nhóm**, nhờ vậy một cột tự đặt tên "Chờ QA" cũng
được bảo vệ.

##### 🪤 Ba cái bẫy đã trả giá khi làm

1. 🔴 **Migration suýt hỏng dữ liệu IM LẶNG.** EF tự sinh `RenameColumn(Status → Category)`
   giữ nguyên số. Nhưng hai enum **không khớp**:

   ```
   Status:         ToDo=0  InProgress=1  Review=2  Done=3
   StatusCategory: ToDo=0  InProgress=1  Done=2    (không có 3)
   ```

   Để nguyên thì mọi task `Review` bị đọc thành **Done**, và task `Done` mang giá trị **không
   tồn tại** trong enum mới. EF cast int sang enum **không kiểm miền giá trị** nên không có
   gì báo. Phải remap `2→1` TRƯỚC rồi `3→2` — làm ngược thì các hàng vừa đặt thành 2 bị kéo
   xuống 1. Đã kiểm trên DB thật: 4/4 phép kiểm toàn vẹn ra 0.

2. 🔴 **Xóa project trả 500 — sai HAI lần trước khi đúng.** `BoardColumn` không phải
   `ISoftDeletable`, nên khi project bị đánh dấu xóa mềm:
   - `Cascade` → EF đánh dấu cột là Deleted → DELETE thật → FK từ `Tasks` chặn → **500**.
   - `Restrict` → EF không xóa được mà cũng không null hóa FK bắt buộc → *"association has
     been severed"* → vẫn **500**.
   - `ClientNoAction` → **đúng**: EF không đụng gì tới cột, xóa mềm chỉ là UPDATE trên hàng
     Projects.

   ⚠️ Cũng KHÔNG giải bằng cách cho `BoardColumn` cài `ISoftDeletable`: khi đó `Remove()` ở
   luồng xóa cột của người dùng biến thành xóa mềm, và unique index `(ProjectId, Name)` sẽ
   chặn việc tạo lại cột trùng tên. Đổi một lỗi lấy một lỗi khó thấy hơn.

   📌 **Chỉ integration test bắt được lớp lỗi này.** Xóa project là đường ít ai đi lúc phát
   triển, và unit test không có DB thật để FK lên tiếng.

3. **`AutoInclude` cho `BoardColumn`.** `TaskMapper.ToStatusRef` đọc `task.BoardColumn.Name`,
   tức 10+ chuỗi `Include` rải khắp `TaskRepository` đều phải nhớ thêm một dòng. Quên một chỗ
   thì không có gì đỏ lúc biên dịch — nó nổ NRE ở đúng request không ai test tới. `AutoInclude`
   biến "phải nhớ" thành "mặc định đúng"; giá là một INNER JOIN vào bảng vài hàng.

##### Hợp đồng API đổi

| | Trước | Sau |
|---|---|---|
| `TaskSummaryResponse.status` | `"ToDo"` (chuỗi enum) | `{ columnId, name, color, category }` |
| `ChangeTaskStatusRequest` | `{ target: "InProgress" }` | `{ targetColumnId: "guid" }` |
| `BoardResponse.columns` | luôn **4** | **mọi cột của project**, theo `order` |
| `StatusCount` (thống kê) | `{ status, count }` | `{ columnId, name, color, order, category, count }` |

Endpoint mới: `GET/POST /projects/{id}/columns` · `PUT/DELETE /columns/{id}` ·
`PUT /projects/{id}/columns/order`.

> 🔴 **Xóa cột bắt buộc chọn cột đích khi còn task** (400 kèm số task nếu thiếu), và **không
> xóa được cột cuối cùng** (409). Không có đường "xóa cuốn theo task": task là dữ liệu người
> dùng đã bỏ công tạo, một cú bấm đổi cấu hình board không được phép làm mất chúng — mà cũng
> không được âm thầm dồn vào một cột do máy chọn, vì khi đó họ không biết chỗ nào mà tìm.
>
> ⚠️ `DELETE` có **thân request** (khác thường): đưa `targetColumnId` lên query string sẽ
> khiến một thao tác phá hủy phụ thuộc vào chuỗi URL, thứ dễ sao chép nhầm và nằm lại trong
> log máy chủ.

---

#### ADR-053 (2026-08-05) — "Việc của tôi": endpoint XUYÊN DỰ ÁN đầu tiên

`GET /tasks/my` — task được gán cho **chính người gọi**, chưa thuộc cột nhóm `Done`, có hạn
**≤ hôm nay**, gom sẵn theo dự án.

**Vì sao cần endpoint mới thay vì gọi `/projects/{id}/tasks` nhiều lần:** mọi endpoint task
khác đều nằm dưới `/projects/{id}/…`, nên trả lời câu *"sáng nay tôi cần làm gì"* sẽ là N
request rồi gộp ở client — và thứ tự lẫn cách đếm sẽ do client tự quyết định một lần nữa.

**Ba quyết định đáng ghi:**

1. **`≤ hôm nay` chứ không `= hôm nay`.** Việc trễ hạn phải nổi lên cùng việc hôm nay; giấu
   nó đi là đúng cách để nó bị quên tiếp.
2. **Không nhận `employeeId` ở đâu cả**, kể cả query string. Nhận vào là biến nó thành
   endpoint xem lịch làm việc của người khác mà không ai chủ ý thiết kế. Quyền nằm trong
   chính điều kiện truy vấn ("được gán cho tôi"), nên service **không gọi `_authz`** — muốn
   được gán thì phải là thành viên đang hoạt động.
3. **Trả về mốc `today` mà SERVER dùng.** Client ở múi giờ khác sẽ tính ra một "hôm nay"
   khác; hiện lại mốc thật để người dùng biết phạm vi mình đang xem (ADR-046b).

⚠️ **Không dùng `.Value.Date` trong truy vấn** — mọi cột `DateTime` đi qua `ValueConverter`
đóng dấu `Kind=Utc`, và EF **không dịch được** `.Date` trên cột đã chuyển đổi (ném lúc chạy
thành HTTP 500). So thẳng với mốc nửa đêm: `t.DueDate < todayUtc.AddDays(1)`.

---

#### Bài học 2026-08-04 — cùng một lớp lỗi bố cục, ba lần trong một phiên

`min-width:auto` của grid/flex item xuất hiện **ba lần** trong phiên này, mỗi lần một triệu
chứng khác nhau: chữ tràn khỏi dialog chi tiết Task, trang board lệch 8px, trang thống kê
lệch 104px. Cả ba cùng một gốc — một item không có `min-w-0` nên nở theo nội dung dài nhất
thay vì co về bề rộng khung.

Kèm theo một cái bẫy phụ đáng nhớ: **`break-words` KHÔNG sửa được nó.**
`overflow-wrap: break-word` cho phép ngắt để *khỏi tràn* nhưng **không làm giảm min-content**,
nên track vẫn phồng và đoạn chữ vẫn không có bề rộng hữu hạn nào để ngắt theo. Cách sửa gốc
là `min-w-0` (+ `grid-cols-[minmax(0,1fr)]` khi con lại là grid item).

**Cách chẩn đoán, nên dùng lại:** duyệt ngược chuỗi tổ tiên và so `getBoundingClientRect()`
của từng cấp với cha của nó — phần tử đầu tiên rộng hơn cha chính là chỗ rò rỉ. Nhanh hơn
nhiều so với đoán từ class.

> 📌 Cập nhật bảng này mỗi khi có quyết định kiến trúc mới hoặc thay đổi — đây sẽ là
> phần rất hữu ích khi viết chương "Phân tích thiết kế" trong báo cáo tốt nghiệp.

---

### Chi tiết ADR-054 → ADR-056 (phiên 2026-08-06 — bốn hạng mục còn lại của lộ trình)

Phiên này làm đúng bốn việc còn lại của §1 (mục 11–12) theo thứ tự ưu tiên người dùng chốt:
đường ghi hồ sơ cá nhân → kiểm tay board → kỹ thuật DB → nhóm báo cáo. Search toàn cục và
SignalR **cố ý không làm** — giữ nguyên định hướng "làm sau" đã ghi ở §6.

#### ADR-054 (2026-08-06) — Đường ghi hồ sơ cá nhân: phát lại token, không đổi `/auth/me`

**Chốt phương án (a) của ADR-049**: `PUT /auth/profile` và `POST /auth/change-password` trả
về `AuthenticatedResponse` MỚI (tái dùng `AuthService.BuildTokensAsync` +
`AuthController.IssueSession`), frontend `setSession(...)` lại ngay. `GET /auth/me` **giữ
nguyên** đọc từ claim — không đụng bất biến mà `PermissionClaimTests` khóa.

Đặt cả hai endpoint trên chính `AuthController`, **không** tách sang `EmployeesController`:
`IssueSession` là method private gắn với `RefreshCookieName`/`RefreshCookiePath` khai trên
đúng controller đó — bốn thuộc tính cookie phải khớp nguyên vẹn giữa set/xóa (ADR-027), tách
ra là phải chép lại logic này ở hai nơi.

`ChangePasswordAsync` theo đúng khuôn `ResetPasswordAsync`: thu hồi **mọi phiên khác**
(`RevokeAllAsync`), nhưng khác `ResetPasswordAsync` ở chỗ **vẫn phát token mới cho chính tab
đang thao tác** — người dùng đổi mật khẩu trong khi đang đăng nhập hợp lệ thì không có lý do
gì để tự đăng xuất tab đó, chỉ các thiết bị/tab khác mới cần đăng xuất.

Đã kiểm chứng đầu-cuối bằng integration test: đổi tên xong gọi lại `/auth/me` bằng access
token **mới** (không refresh) và thấy tên mới ngay — đây là bằng chứng đường phát-lại-token
chạy thật, không phải chỉ DB được ghi.

#### ADR-055 (2026-08-06) — Kỹ thuật DB: trigger đụng độ với `OUTPUT` của EF Core

Bốn đối tượng DB cho báo cáo thực tập, tất cả trong một migration
(`AddReportingDbObjects`): **index** (`IX_TaskAssignments_EmployeeId` nâng thành covering
index qua `IncludeProperties(TaskId)`) · **view** (`vw_SprintVelocity`, chỉ sprint
`Completed`) · **hai stored procedure** (`sp_GetProjectBacklogInsight` +
`sp_GetProjectBacklogByPriority`, tách hai vì `Database.SqlQuery<T>` của EF Core 8 không xử
lý gọn multi-resultset) · **CHECK constraint** (`CK_Sprints_EndDate_After_StartDate`, qua
Fluent API `HasCheckConstraint` — cùng khuôn `CK_Attachments_ExactlyOneOwner` đã có sẵn) ·
**trigger** (`trg_Tasks_MaintainProjectTaskCount`, duy trì `Projects.TaskCount`).

> 🔴 **Nói thẳng: cột `Projects.TaskCount` và trigger nuôi nó là đối tượng KÉM CẦN THIẾT
> nhất trong bốn cái.** `ProjectStatisticsRepository.CountTasksAsync` đã tính đúng số này tại
> chỗ mỗi khi cần — ứng dụng không thực sự cần một cột đếm phi chuẩn hoá. Nó tồn tại để có
> một trigger THẬT cho báo cáo, không phải ngược lại. Constraint mới là câu trả lời kỹ thuật
> đúng cho "toàn vẹn dữ liệu"; trigger là minh họa kỹ thuật.

🔴 **Bẫy lớn nhất phiên này, không nằm ở logic trigger mà ở chỗ không ai ngờ:** thêm trigger
vào `Tasks` làm **MỌI** `INSERT`/`UPDATE` qua EF vào bảng đó ném `DbUpdateException` ngay
lập tức — kể cả tạo task đơn giản nhất, không liên quan gì tới trigger. Lỗi SQL Server thật:

```
The target table 'Tasks' of the DML statement cannot have any enabled triggers if the
statement contains an OUTPUT clause without INTO clause.
```

Nguyên nhân: `TaskItem.RowVersion` là cột `rowversion`, và EF Core mặc định sinh
`INSERT ... OUTPUT INSERTED.RowVersion` để đọc lại giá trị vừa ghi — SQL Server **cấm**
`OUTPUT` không có `INTO` trên bảng có trigger đang bật. Đây là giới hạn của SQL Server, không
phải lỗi ở công thức trigger. Sửa bằng khai báo Fluent API mà EF Core tài liệu hóa sẵn:

```csharp
builder.ToTable("Tasks", t => t.HasTrigger("trg_Tasks_MaintainProjectTaskCount"));
```

Thiếu dòng này, provider vẫn dùng chiến lược `OUTPUT` cũ và mọi request tạo/sửa task trả
500 — **build sạch, migration chạy được, nhưng API hỏng hoàn toàn**, đúng lớp lỗi §1 đã đặt
tên nhiều lần. Chỉ integration test thật (gọi `POST /tasks` qua HTTP) bắt được lỗi này; unit
test mock repository sẽ không bao giờ chạm tới SQL Server thật để lộ nó ra.

> 📌 **Bài học tổng quát:** thêm trigger vào một bảng có cột `rowversion`/computed luôn cần
> khai báo `HasTrigger` — không phải tùy chọn tối ưu, mà là điều kiện để bảng đó còn ghi được
> qua EF Core. Kiểm bằng cách gọi thật một request ghi vào bảng đó sau khi thêm trigger,
> đừng tin "migration áp được" là đủ.

🔴 **Bẫy thứ hai, nhỏ hơn nhưng dễ lặp lại:** công thức trigger đầu tiên dùng
`AFTER INSERT, DELETE` để tăng/giảm bộ đếm — sai, vì `Tasks` xóa MỀM
(`ApplySoftDeleteQueryFilter`/`ApplySoftDelete` đổi `EntityState.Deleted` → `Modified` trước
`SaveChanges`), nên `AFTER DELETE` gần như không bao giờ chạy và bộ đếm chỉ tăng, không bao
giờ giảm. Sửa bằng `AFTER INSERT, UPDATE, DELETE` và một công thức gộp `inserted`/`deleted`
theo `IsDeleted` thay vì theo loại sự kiện — xử lý đúng cả ba trường hợp (task mới, xóa mềm,
cập nhật không đụng `IsDeleted`) bằng một biểu thức duy nhất thay vì rẽ nhánh theo trigger
event. Chi tiết công thức xem chú thích trong migration.

#### ADR-056 (2026-08-06) — Nhóm báo cáo: velocity đọc "hiện trạng", không phải "lịch sử"

`GET /projects/{id}/reports/{backlog-insight,velocity,timeline}` — cả ba cùng quyền với
Thống kê (`ProjectAction.ViewStatistics`, ADR-039), không tạo action mới.

`timeline` liệt kê **MỌI** sprint (Planned/Active/Completed, sắp theo `StartDate`) — khác
`velocity` chỉ có sprint đã đóng sổ, và khác `TallyBySprintAsync` của Thống kê ở chỗ mang
theo `SprintStatus` + `CompletedAt` thật thay vì suy "đang chạy" từ so ngày. Dựng bằng LINQ
thuần trên `Sprints` (không qua view — `vw_SprintVelocity` lọc sẵn `Status = Completed` nên
không dùng lại được cho timeline).

**Ở frontend, ba báo cáo này tách thành BA route/tab riêng** (`backlog-insight`, `velocity`,
`timeline` trong `PROJECT_SECTIONS`) thay vì dồn vào một tab "Báo cáo" chung như bản đầu
2026-08-06 — vừa để mỗi báo cáo có chỗ đứng riêng không phải cuộn trong một trang dài, vừa
làm sidebar dự án đủ đầy hơn theo đúng yêu cầu. `SprintTimelineChart` là component mới,
KHÔNG dùng Recharts (Gantt theo ngày thật không hợp thư viện biểu đồ dạng cột/tròn có sẵn):
mỗi sprint là một `<div>` định vị bằng `%` theo mốc `min(StartDate)`–`max(EndDate)` của toàn
bộ danh sách, tô màu theo `SprintStatus` bằng đúng bảng `--viz-status-*` đã có (Planned ↔
xám "chưa bắt đầu", Active ↔ xanh dương, Completed ↔ xanh lá) — không bày thêm thang màu
thứ tư.

`TallyVelocityAsync` gọi view `vw_SprintVelocity` qua ADO thô (không qua LINQ) — lý do là
`Database.SqlQuery<T>` của EF Core 8 chỉ dịch gọn kiểu vô hướng, còn view/stored procedure
nhiều cột cần một `DbCommand` thật. Đây cũng là điểm khiến "view"/"stored procedure" của
ADR-055 có người dùng thật thay vì chỉ tồn tại để có.

> 🔴 **Một phát hiện đáng nhớ khi viết integration test cho velocity:** đóng sprint với
> `TargetSprintId = null` (đẩy task chưa xong về Backlog, ADR-050) làm task đó **RỜI KHỎI**
> sprint hẳn (`SprintId = null`), không phải "ở lại nhưng đánh dấu chưa xong". Nên
> `vw_SprintVelocity.TotalTasks` của một sprint sau khi đóng **có thể nhỏ hơn** số task nó
> từng có lúc đang chạy — view phản ánh **hiện trạng thật**, không phải một bản chụp lịch sử.
> Đây không phải lỗi: một bản test đầu tiên giả định sai điều này và đỏ đúng chỗ, sửa lại
> assertion là đúng, không phải sửa code.

`EnumZeroFill` (tách từ `StatisticsService.ZeroFill` cũ, dùng `internal` → `public static`
trong `PMS.Application/Common/`) — khi hai chỗ cần đúng một công thức "bù đủ mọi giá trị
enum" (Thống kê và giờ là backlog insight theo Priority), tách ra dùng chung thay vì chép
lại là tránh đúng lớp lỗi ADR-034 đã đặt tên (hai nơi định dạng thì chắc chắn có lúc lệch).

#### 📌 Cập nhật nợ kiểm chứng (frontend-next-session.md §0) — kéo–thả vẫn còn treo

Môi trường phiên này **không có công cụ trình duyệt nào** (không Playwright/Puppeteer/CDP),
nặng hơn cả giới hạn "không bắn được sự kiện chuột tổng hợp" mà các phiên trước ghi nhận —
ở đây hoàn toàn không mở được một trình duyệt thật. Vì vậy:

- ✅ **Đổi thứ tự cột** và **đổi category cột có task ảnh hưởng thống kê** — đã kiểm chứng
  **thật**, nhưng bằng round-trip HTTP + đọc DB trực tiếp (`BoardColumnsTests.cs`, 3 test
  mới), không phải bằng click chuột trên UI. Đây là bằng chứng logic backend đúng — phần
  UI (nút bấm, cảnh báo hiện đúng lúc) đã được đọc lại bằng mắt qua mã nguồn ở phiên trước,
  chưa bấm thử thật.
- ⬜ **Kéo–thả bằng chuột/cảm ứng/bàn phím vẫn CHƯA kiểm chứng bằng thao tác thật** — không
  đổi so với trạng thái trước phiên này. Đừng đọc "Mục 2 đã xong" thành "đã kéo thử trên UI".
