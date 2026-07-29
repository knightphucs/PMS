# ARCHITECTURE.md
## Hệ thống Quản lý Dự án & Task (Project Management System)

> Tài liệu này ghi lại các quyết định kiến trúc (Architecture Decisions) của dự án.
> Mục đích: đảm bảo tính nhất quán xuyên suốt quá trình phát triển, và làm tài liệu
> tham chiếu cho báo cáo thực tập tốt nghiệp.
>
> Cập nhật lần cuối: 2026-07-27

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
| Sprint | ⬜ | Chỉ có entity + migration, chưa có `SprintService`/Controller |
| Task (kể cả Subtask, Workflow Transition Rules) | ⬜ | Chưa có `TaskService`/Controller — chưa bắt đầu |
| Comment / Activity Log / Notification | ⬜ | ActivityLog đã ghi qua `IActivityLogger` (ADR-013); Comment/Notification-API vẫn chưa có Service/Controller |
| Employee management (ngoài Auth) | ✅ | `AdminEmployeesController` — khóa/mở tài khoản, cấp `SystemAdmin` — *bảng này từng ghi ⬜ dù đã code xong, đã sửa lại 2026-07-29* |
| Thống kê / Dashboard | ⬜ | Chưa bắt đầu |
| Frontend (toàn bộ) | ⬜ | Thư mục `frontend/` chưa tồn tại trong repo |
| Real-time (SignalR) | ⬜ | Có chủ đích — chỉ làm sau khi core CRUD ổn định (xem §6) |

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
| **Frontend** | **Next.js (TypeScript) + TailwindCSS + shadcn/ui** | SSR/routing chuẩn, type-safety khớp tinh thần OOP backend |
| **Data Fetching** | **TanStack Query** | Caching, loading state, đồng bộ API chuẩn công nghiệp |
| **State Management** | **Zustand** | Quản lý state phức tạp (filter, sort, cache) |
| **Data Visualization** | **Recharts** | Dashboard thống kê (mục "Thống kê" trong mindmap) |
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
├── frontend/
│   ├── app/                     # Next.js App Router (pages, layouts)
│   ├── components/              # shadcn/ui components, shared UI
│   ├── lib/                     # API client, TanStack Query hooks
│   ├── store/                   # Zustand stores
│   └── types/                   # TypeScript types (khớp với DTO backend)
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
- **Task** ⬜ *(entity đã có, chưa có `TaskService`/Controller)* (kể cả Subtask qua self-reference): Tên, thuộc Project nào, **thuộc Sprint
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

### Entity giao việc ⬜ *(entity đã có, chưa có `TaskService`/API)*
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

### Entity Sprint/Board (nay là core, không còn là "tương lai") ⬜ *(entity + migration đã có, chưa có `SprintService`/Controller)*
- **`Sprint`**: Tên, `ProjectId`, `StartDate`, `EndDate`, `Goal` (mục tiêu sprint ngắn)
  - 1 Project có nhiều Sprint
  - 1 Sprint có nhiều Task (qua `Task.SprintId`)
  - Task chưa gán Sprint (`SprintId = null`) = nằm ở **Backlog**

### Entity phân loại & liên kết (theo mô hình Jira thật) ⬜ *(field/entity đã có, chưa có API dùng được)*
- **`Label`**: Tên tag tự do (ví dụ: `bug`, `frontend`, `urgent`) — Task N—N Label,
  giúp lọc/tìm kiếm linh hoạt hơn Status/Priority cố định
- **`Watcher`** *(bảng trung gian Employee–Task)*: `TaskId`, `EmployeeId` — người
  theo dõi task để nhận Notification dù không được assign làm (khác với `TaskAssignment`)
- **`TaskLink`** *(self-referencing giữa 2 Task)*: `SourceTaskId`, `TargetTaskId`,
  `LinkType` (`Blocks` / `IsBlockedBy` / `RelatesTo` / `Duplicates`) — quản lý phụ
  thuộc giữa các task, ví dụ Task B không thể `Done` nếu Task A (blocking) chưa xong

### Workflow Transition Rules (Status không đổi tự do) ⬜ *(`TaskStatusTransitionService` chưa tồn tại)*
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

### Subtask — là 1 Task đầy đủ, không phải checklist item đơn giản ⬜ *(`ParentTaskId` đã có trong domain, chưa có `TaskService`/API)*
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

### Subtask — Progress Bar, không tự động đóng Task cha ⬜ *(chưa code)*
Theo đúng hành vi mặc định của Jira (đã xác nhận): Subtask có Status/Assignee độc
lập với Task cha, nhưng **Task cha không tự động chuyển sang `Done` dù mọi subtask
đã `Done`** — Reporter/PM/người phụ trách Task cha vẫn phải tự tay đóng Task cha.
Lý do: Task cha có thể còn việc khác ngoài các subtask đã liệt kê (review tổng thể,
tổng hợp kết quả...).
- **Progress bar**: Task cha hiển thị % subtask đã `Done` / tổng số subtask (tính
  toán, không lưu cứng — tương tự `IsOverdue`)
- Không cần thêm logic tự động trong `TaskStatusTransitionService` cho việc này —
  chỉ cần 1 hàm tính `SubtaskProgress` ở tầng Application để hiển thị lên UI

### Entity cộng tác (Nhóm A — core) ⬜ *(chỉ mới entity, chưa có Service/Controller)*
- **`Comment`**: `TaskId`, `EmployeeId` (người viết), `Content`, `CreatedAt`
- **`ActivityLog`**: `EntityType` (Project/Task), `EntityId`, `EmployeeId` (người thực hiện),
  `Action` (Created/Updated/StatusChanged/Assigned...), `Detail`, `Timestamp`
  → Dùng để hiển thị lịch sử thay đổi trên Task/Project (audit trail)
- **`Notification`**: `EmployeeId` (người nhận), `Type` (TaskAssigned/DueSoon/CommentAdded/...),
  `Content`, `IsRead`, `CreatedAt`, `RelatedEntityId`
  → Sinh ra bởi các sự kiện: được gán task, task sắp đến hạn (background job check định kỳ),
  có comment mới trên task mình theo dõi

### Quan hệ tổng hợp
- Project 1—N Task
- Project 1—N Sprint
- Sprint 1—N Task (qua `SprintId` nullable)
- Task 1—N Task (subtask, self-referencing, tùy chọn)
- Task N—N Task (qua `TaskLink`, khác mục đích với quan hệ subtask)
- Task 1—N Comment
- Task N—N Label
- Task N—N Employee (qua `Watcher` — theo dõi, khác với `TaskAssignment` — thực hiện)
- Employee N—N Project (qua `ProjectMember`, có `RoleInProject`)
- Employee N—N Task (qua `TaskAssignment`, có `RoleInTask`)
- Employee 1—N Task (là `Reporter` — người tạo task)
- Employee 1—N Comment, 1—N Notification, 1—N ActivityLog (là người thực hiện)

> 📌 Ghi chú: các quan hệ trên là bản nháp, sẽ chốt chính thức khi hoàn thành ERD/Class Diagram.

---

## 6. Kiến trúc Frontend

**Stack:** Next.js (TypeScript) + TailwindCSS + shadcn/ui + TanStack Query + Zustand + Recharts

**Cấu trúc phân lớp Frontend:**
```
UI Components (shadcn/ui)
      ↓
Pages/App Router (Next.js)
      ↓
Custom Hooks (TanStack Query — gọi API, cache, loading/error state)
      ↓
API Client (fetch/axios wrapper, xử lý JWT token)
      ↓
Backend API (ASP.NET Core)
```

**Quản lý state:**
- **Server state** (dữ liệu từ API: Project, Task, Employee): TanStack Query — tự động cache, refetch, invalidate
- **Client state** (UI state: filter đang chọn, modal đang mở, theme...): Zustand

**Các trang chính (dự kiến):**
- Trang đăng nhập / đăng ký (JWT)
- Danh sách Project + tạo/sửa/xóa (theo quyền RoleInProject)
- Chi tiết Project → **Board dạng Kanban** (cột theo Status) + **Backlog** (task chưa gán Sprint)
- Quản lý Sprint: tạo Sprint, kéo task từ Backlog vào Sprint
- Chi tiết Task: thông tin, Priority badge, Labels, danh sách người đảm nhận (Assignee)
  + Reporter, nút "Watch"/"Unwatch", **Comment**, **Linked Issues** (Blocks/Relates to),
  **Activity Log** (lịch sử thay đổi), **danh sách Subtask** (progress bar % hoàn thành
  + mỗi Subtask click vào mở ra như 1 Task đầy đủ, không phải checkbox tĩnh)
- Trang quản lý Nhân sự + phân quyền theo từng Project
- **Thanh Search/Filter** toàn cục: tìm task theo tên, người phụ trách, status, deadline
- **Notification bell** (góc header): danh sách thông báo, đánh dấu đã đọc
- Dashboard thống kê (Recharts): tỷ lệ hoàn thành, task theo nhân sự, task quá hạn

**Real-time (SignalR):**
- Tích hợp **sau khi core CRUD đã ổn định** — không làm ngay từ đầu
- Use case: khi 1 user cập nhật Status của Task, các user khác đang xem cùng Project
  thấy thay đổi ngay lập tức mà không cần reload
- Frontend dùng `@microsoft/signalr` client kết nối tới SignalR Hub bên backend

**Type-safety giữa Frontend/Backend:**
- Định nghĩa TypeScript types ở `frontend/types/` khớp 1-1 với DTO của backend
  (cân nhắc dùng OpenAPI codegen từ Swagger để tự sinh types, giảm sai sót thủ công)

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
Cấu hình CORS rõ ràng cho phép origin của Frontend (Next.js dev: `localhost:3000`,
production: domain thật) — không dùng `AllowAnyOrigin` khi có JWT/cookie. Bắt buộc phải
làm trước khi Frontend (chưa có, xem "Tiến độ triển khai theo module" ở §1) gọi API thật,
nếu không mọi request từ browser sẽ bị chặn bởi same-origin policy.

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

Để tránh phải nhập tay dữ liệu lúc demo/bảo vệ, chuẩn bị sẵn:
- **Seed data** qua EF Core `HasData()` hoặc script riêng, chạy khi migration:
  - 1 tài khoản `SystemAdmin` mẫu
  - 3-5 Project mẫu với đầy đủ Sprint, Task ở nhiều Status khác nhau (bao gồm cả task quá hạn để demo Notification)
  - 5-10 Employee mẫu với các `RoleInProject` khác nhau (PM/Member/Viewer) để demo phân quyền
  - Vài Comment và ActivityLog mẫu để demo tính năng cộng tác
- Có thể tách riêng `DbSeeder` class, chạy 1 lần khi `dotnet run` ở môi trường Development

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
| Khóa/mở tài khoản, cấp `SystemAdmin` role cho người khác | ✅ Đã có — `AdminEmployeesController`, policy `RequireSystemAdmin`. Khóa/đổi role đều thu hồi toàn bộ refresh token. Bất biến: luôn còn ≥1 SystemAdmin chưa bị khóa |
| Quên mật khẩu / Reset password qua token hết hạn 15-30 phút | ⬜ Chưa code — `PasswordResetToken` chưa tồn tại; còn phụ thuộc email service |
> 📌 Hai mục ⬜ là quyết định thiết kế có sẵn từ đầu (ADR §15, 2026-07-22), chưa tới
> lượt implement — không phải bug hay bị bỏ sót giữa chừng.

### Authorization — mô hình 2 tầng
**Tầng 1 — System Role** (gắn với tài khoản, không đổi theo project):
- `SystemAdmin`: quản lý toàn bộ user, cấu hình hệ thống. **Không tự động có quyền
  thao tác (action) trong bất kỳ project nào** — nếu muốn tạo/sửa task như PM, phải
  được thêm làm `ProjectMember` như người bình thường. Ngoại lệ: SystemAdmin có quyền
  **xem (read-only)** toàn bộ project cho mục đích support/audit, tương tự cách admin
  site của Jira hỗ trợ kỹ thuật mà không tự ý sửa dữ liệu nghiệp vụ.
- `User`: nhân viên thường, chỉ thấy project mình tham gia. **Mọi `User` đều có quyền
  tạo Project mới** — khi tạo, hệ thống tự động insert `ProjectMember(EmployeeId=creator,
  RoleInProject=ProjectManager)`, người tạo tự động trở thành PM của project đó.

  > 📌 Quyền tạo project được thiết kế qua 1 policy riêng (`CanCreateProject`), không
  > hardcode "mọi User đều được" — mặc định áp dụng cho mọi User, nhưng cho phép đổi
  > logic sau này (ví dụ giới hạn chỉ vài người) mà không cần sửa schema.

**Tầng 2 — Project Role** (gắn theo từng `ProjectMember`, 1 người có thể khác role ở
project khác nhau):
| Role | Quyền hạn |
|---|---|
| `ProjectManager` | Tạo/sửa/xóa project, tạo Sprint, tạo task, gán nhân sự, xem thống kê |
| `Member` | Xem task được giao, cập nhật status task của mình, viết comment |
| `Viewer` | Chỉ xem, không chỉnh sửa — dùng cho stakeholder theo dõi tiến độ (cấp quản lý không trực tiếp làm việc, khách hàng/đối tác, phòng ban khác cần tham chiếu, auditor) |

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
- Mục tiêu coverage: [điền mục tiêu, ví dụ 70% cho Service layer]

---

## 12. UML Diagrams (kế hoạch)

| Diagram | Mục đích | Trạng thái |
|---|---|---|
| Use Case Diagram | Tổng quan chức năng theo actor (SystemAdmin, ProjectManager, Member, Viewer) | Done |
| Class Diagram | Chi tiết entity, thuộc tính, quan hệ, OOP | Done |
| ERD | Thiết kế database quan hệ | Done |
| Sequence Diagram | Tạo task, Gán nhân sự, Đổi status, Mời thành viên, Phản hồi lời mời | 5/5: `seq-01/02/03` (task) + `seq-04` (mời) + `seq-05` (accept/decline). Luồng Notification không tách riêng — đã thể hiện trong seq-04/05 |

---

## 13. Quy trình phát triển

1. Use Case Diagram → Class Diagram/ERD
2. Setup project structure: backend (.NET solution theo layer, xem §4) + frontend
   (Next.js scaffold, cấu trúc thư mục theo §4)
3. Code Domain (Entity, Enum)
4. Authentication/Authorization backend (đổi lên trước — xem ADR §15, 2026-07-25 và
   ADR-006 về cơ chế phân quyền 2 tầng)
5. Code backend từng module theo nhóm function: Project → Task → Employee → Thống kê
6. Code Frontend theo từng module đã có API tương ứng: Đăng nhập → Project (danh sách +
   CRUD) → Task/Board (Kanban + Backlog) → Dashboard thống kê — làm ngay sau khi API của
   module đó ổn định, không đợi toàn bộ backend xong mới bắt đầu (xem §6)
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
- **File attachment** trên Task (đính kèm tài liệu, hình ảnh)
- **Email notification** (ngoài in-app notification đã có ở core)
- **Bulk actions**: chọn nhiều task, đổi status/gán người hàng loạt
- **Epic**: nhóm nhiều Task/Sprint lại thành 1 mục tiêu lớn hơn, xuyên nhiều Sprint
  (thêm 1 tầng phân cấp: Epic → Sprint → Task — chỉ nên làm khi core Sprint đã ổn định)
- **Issue Security Level**: giới hạn xem 1 Task cụ thể dù có quyền project (tầng thứ 3
  ngoài System Role + Project Role) — case nâng cao, hiếm dùng ở quy mô nhỏ
- **Advanced Search (JQL-like)**: query nâng cao kiểu `status=InProgress AND assignee=me`,
  thay vì chỉ filter theo field đơn giản

### Nhóm C — nice-to-have, chỉ làm nếu còn dư thời gian
- Dark mode
- Export báo cáo PDF/Excel
- Mobile responsive nâng cao (PWA)

**Điều kiện để triển khai:** Core (Project–Task–Employee–Sprint CRUD, Role 2 tầng,
Comment, Activity Log, Notification in-app, Auth, Test) đã hoàn thành và ổn định,
còn đủ thời gian trước deadline báo cáo.

---

## 15. Nhật ký quyết định (Architecture Decision Log)

| Ngày | Quyết định | Lý do |
|---|---|---|
| 2026-07-20 | Chọn .NET thay vì Python | Đề bài yêu cầu OOP rõ ràng, đã có nền tảng C# |
| 2026-07-21 | Áp dụng Layered Architecture + Repository Pattern | Cân bằng giữa tính chuyên nghiệp và độ phức tạp phù hợp fresher |
| 2026-07-22 | Dùng FluentValidation + Mapperly; không dùng AutoMapper | AutoMapper từ v15 (7/2025) chuyển dual-license copyleft RPL-1.5 + thương mại (vẫn free cho giáo dục/<$5M nhưng thêm ràng buộc phải chú thích); Mapperly free MIT, compile-time, minh bạch code sinh ra — hợp báo cáo hơn |
| [điền ngày khi code] | Cho phép nhiều người/1 Task (Employee N–N Task qua `TaskAssignment`) | Phản ánh thực tế: task lớn thường cần nhiều người phối hợp — *entity đã có, chưa có `TaskService`/API dùng được* |
| 2026-07-22 | Áp dụng phân quyền 2 tầng: SystemRole + RoleInProject (`ProjectMember`) | Sát với mô hình doanh nghiệp thật, 1 người có thể khác role ở project khác nhau — đã hoạt động thật cho Project (`ProjectAuthorizationService`) |
| [điền ngày khi code] | Nâng Sprint/Backlog/Board từ "tương lai" thành tính năng core | Mục tiêu làm sản phẩm dùng được thật, không chỉ CRUD đơn thuần — *entity + migration đã có, chưa có `SprintService`/Controller* |
| 2026-07-22 | Thêm Comment, Activity Log, Notification vào core | Đây là tính năng tối thiểu để 1 team thật sự dùng được hệ thống hàng ngày — *chỉ mới entity, chưa có Service/Controller nào* |
| 2026-07-22 | Áp dụng Soft Delete cho Project/Task | Bảo toàn ActivityLog/Comment liên quan, cho phép khôi phục — đã hoạt động thật, có test |
| 2026-07-22 | Chuẩn hóa Pagination, Global Exception Handling, API Versioning | Đạt chuẩn API production-grade, không phải sửa lại kiến trúc giữa chừng |
| 2026-07-29 | Chuẩn hóa CORS Policy | Bắt buộc trước khi Frontend gọi API thật — đã có `AddCors`/`UseCors` với policy `PmsFrontend` |
| 2026-07-22 | Thêm mục Non-Functional Requirements + Data Seeding | Phục vụ demo báo cáo trôi chảy và thể hiện đầy đủ tư duy thiết kế hệ thống — `DbSeeder` đã chạy được ở môi trường Development |
| 2026-07-22 | Mọi `User` được tạo Project, tự động thành `ProjectManager` của project đó | Tránh bottleneck xin duyệt qua SystemAdmin, khớp cách Jira/Trello vận hành thật |
| 2026-07-29 | `SystemAdmin` tách bạch khỏi Project Role: chỉ read-only toàn hệ thống, muốn thao tác phải là `ProjectMember` như bình thường | Tránh "God Mode" — giữ đúng nguyên tắc Least Privilege, dễ audit trách nhiệm — *chưa có code nào cho SystemAdmin bypass đọc toàn hệ thống* |
| 2026-07-22 | Giữ `Viewer` như 1 actor riêng trong Use Case Diagram | Phản ánh nhu cầu thực tế: stakeholder/khách hàng/auditor cần xem mà không cần sửa — đã có trong `RoleInProject` + `ProjectPermissions`, có test |
| [điền ngày khi code] | Cho phép `Member` tự self-assign task đang `ToDo` (không cần PM gán); gán người khác/gỡ người khác vẫn chỉ PM | Khớp mô hình Kanban thực tế (tự "pick up" task), giảm bottleneck qua PM, vẫn tránh xung đột nhờ điều kiện task phải đang `ToDo` — *chưa có `TaskService`* |
| [điền ngày khi code] | Thêm Reporter, Priority, Label, Watcher, TaskLink, Workflow Transition Rules vào core | Đối chiếu trực tiếp mô hình Jira thật — đây là các khái niệm cơ bản mà thiếu sẽ khiến hệ thống thiếu tính thực tế — *field/entity đã có, `TaskStatusTransitionService` chưa tồn tại* |
| 2026-07-22 | Đưa Epic, Issue Security Level, Advanced Search (JQL-like) vào Nhóm B (làm sau) | Đây là tính năng nâng cao/hiếm dùng ở quy mô nhỏ, tránh phình to quá mức trước khi core ổn định — quyết định hoãn, luôn đúng bất kể tiến độ code |
| 2026-07-22 | Employee self-register tài khoản, không cần SystemAdmin tạo hộ | Giảm bottleneck, khớp cách hầu hết SaaS thật vận hành — đã hoạt động thật (`AuthController.Register`) |
| 2026-07-22 | Thêm `InvitationStatus` (Pending/Accepted/Declined) cho `ProjectMember` | Phản ánh đúng luồng mời thành viên thực tế, không tự tạo tài khoản hộ người chưa đăng ký — enum đã dùng thật trong filter membership |
| [điền ngày khi code] | Thêm Reset Password qua token có hạn 15-30 phút | Tính năng Auth cơ bản, thiếu sẽ không dùng được thật — *`PasswordResetToken` chưa tồn tại trong `PMS.Domain`* |
| 2026-07-22 | Bắt buộc HTTPS toàn hệ thống | Bảo vệ JWT token khỏi bị nghe lén qua kênh không mã hóa — đã có `app.UseHttpsRedirection()` |
| 2026-07-29 | Thêm Health Check endpoint (`/health`) | Cần thiết khi có Docker/CI-CD để biết trạng thái API — đã có `AddHealthChecks`/`MapHealthChecks` trong `Program.cs` |
| [điền ngày khi code] | Định nghĩa 3 môi trường Dev/Staging/Production qua `ASPNETCORE_ENVIRONMENT` | Tách cấu hình rõ ràng, tránh lẫn lộn dữ liệu test và thật — *mới chỉ có nhánh rẽ Dev/không-Dev trong `Program.cs`, chưa có `appsettings.Staging.json` hay cấu hình riêng cho Staging* |
| [điền ngày khi code] | Subtask không tự động đóng Task cha khi tất cả subtask `Done`; chỉ hiển thị progress bar (%) | Giữ đúng hành vi mặc định của Jira thật — Task cha có thể còn việc ngoài các subtask đã liệt kê — *chưa có `TaskService` để áp dụng quy tắc này* |
| [điền ngày khi code] | Subtask là 1 Task đầy đủ (Status, Assignee, Comment, Watcher, TaskLink riêng), không phải checklist item; giới hạn chỉ 1 cấp cha–con | Task/Subtask dùng chung class, đúng nguyên lý OOP tái sử dụng; tránh phức tạp hóa với đệ quy vô hạn — *`ParentTaskId` đã có trong domain, chưa có logic giới hạn 1 cấp hay API thao tác* |
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
dữ liệu `Notification` đã seed/tồn tại trong DB dev.

> 📌 Cập nhật bảng này mỗi khi có quyết định kiến trúc mới hoặc thay đổi — đây sẽ là
> phần rất hữu ích khi viết chương "Phân tích thiết kế" trong báo cáo tốt nghiệp.
