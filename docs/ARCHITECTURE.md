# ARCHITECTURE.md
## Hệ thống Quản lý Dự án & Task (Project Management System)

> Tài liệu này ghi lại các quyết định kiến trúc (Architecture Decisions) của dự án.
> Mục đích: đảm bảo tính nhất quán xuyên suốt quá trình phát triển, và làm tài liệu
> tham chiếu cho báo cáo thực tập tốt nghiệp.
>
> Cập nhật lần cuối: 2026-07-22

---

## 1. Tổng quan dự án

**Mô tả:** Hệ thống quản lý dự án và task, cho phép nhóm và các thành viên phân công
công việc, giám sát đầu việc rõ ràng trực quan, theo dõi timeline và hiện trạng của
các task và dự án. Tương tự phiên bản thu nhỏ của Jira/Trello.

**Mục đích sử dụng:** Đồ án tốt nghiệp / báo cáo thực tập tốt nghiệp.

**Yêu cầu bắt buộc từ đề bài:**
- Sử dụng kỹ thuật lập trình hướng đối tượng (OOP)
- Xây dựng cơ sở dữ liệu quan hệ để mapping các đối tượng một cách logic

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
| Testing | xUnit + Moq | Unit test cho Service layer |
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
│   ├──  erd/
│   └── ARCHITECTURE.md
└── README.md
```

---

## 5. Domain Model (tóm tắt — chi tiết xem Class Diagram)

> 📌 Đã mở rộng từ mindmap gốc để đạt mức "sản phẩm dùng được thật", không chỉ dừng
> ở CRUD đơn thuần. Các entity in đậm là bổ sung mới so với bản đầu tiên.

### Entity cốt lõi
- **Project**: Tên, Mô tả tổng quan, Thời gian dự kiến hoàn thành, Status, `IsDeleted`, `DeletedAt` *(Soft Delete)*
- **Task** (kể cả Subtask qua self-reference): Tên, thuộc Project nào, **thuộc Sprint
  nào (nullable — null = Backlog)**, Due Date, Status, `Priority` (`Highest`/`High`/
  `Medium`/`Low`/`Lowest`), `ReporterId` (người tạo/báo cáo task — khác với người được
  assign làm), **cờ IsOverdue (tính toán, không lưu cứng)**, `IsDeleted`, `DeletedAt`
  *(Soft Delete)*
- **Employee** (Nhân sự / User): Tên, Email, Password hash, Chức vụ (System Role)
- **Status**: Enum dùng chung cho Project/Task: `ToDo`, `InProgress`, `Review`, `Done`

> 📌 **Reporter vs Assignee** (theo mô hình Jira): `Reporter` là người tạo/báo cáo task
> (thường là PM hoặc bất kỳ ai phát hiện việc cần làm), `Assignee` (qua `TaskAssignment`)
> là người thực sự thực hiện — 2 vai trò tách biệt, có thể là 2 người khác nhau hoặc
> cùng 1 người.

> 📌 **Soft Delete**: Project/Task khi "xóa" chỉ đánh dấu `IsDeleted = true`, không xóa
> cứng khỏi database. Lý do: giữ nguyên vẹn `ActivityLog`/`Comment` liên quan (audit trail),
> và cho phép khôi phục nếu xóa nhầm. EF Core dùng Global Query Filter để tự động ẩn
> record đã xóa khỏi mọi query mặc định.

### Entity phân quyền (Role 2 tầng)
- **`ProjectMember`** *(bảng trung gian Employee–Project, thay cho quan hệ N–N đơn thuần)*:
  `EmployeeId`, `ProjectId`, `RoleInProject` (`ProjectManager` / `Member` / `Viewer`),
  `JoinedDate`, `InvitationStatus` (`Pending` / `Accepted` / `Declined`)
  → Đây là nơi quyết định 1 người làm PM ở project này nhưng chỉ là Member ở project khác.

  **Luồng mời nhân sự vào Project:**
  - PM mời bằng email → nếu email đã có tài khoản, tạo `ProjectMember` với
    `InvitationStatus = Pending`, sinh `Notification` cho người được mời
  - Người được mời chấp nhận → `InvitationStatus = Accepted`, chính thức có quyền
    theo `RoleInProject`
  - Nếu email chưa có tài khoản trong hệ thống: hiển thị thông báo "chưa có tài khoản,
    người này cần đăng ký trước" — **không tự tạo tài khoản hộ** (tránh phát sinh tài
    khoản rác không ai sở hữu)

### Entity giao việc
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

### Entity Sprint/Board (nay là core, không còn là "tương lai")
- **`Sprint`**: Tên, `ProjectId`, `StartDate`, `EndDate`, `Goal` (mục tiêu sprint ngắn)
  - 1 Project có nhiều Sprint
  - 1 Sprint có nhiều Task (qua `Task.SprintId`)
  - Task chưa gán Sprint (`SprintId = null`) = nằm ở **Backlog**

### Entity phân loại & liên kết (theo mô hình Jira thật)
- **`Label`**: Tên tag tự do (ví dụ: `bug`, `frontend`, `urgent`) — Task N—N Label,
  giúp lọc/tìm kiếm linh hoạt hơn Status/Priority cố định
- **`Watcher`** *(bảng trung gian Employee–Task)*: `TaskId`, `EmployeeId` — người
  theo dõi task để nhận Notification dù không được assign làm (khác với `TaskAssignment`)
- **`TaskLink`** *(self-referencing giữa 2 Task)*: `SourceTaskId`, `TargetTaskId`,
  `LinkType` (`Blocks` / `IsBlockedBy` / `RelatesTo` / `Duplicates`) — quản lý phụ
  thuộc giữa các task, ví dụ Task B không thể `Done` nếu Task A (blocking) chưa xong

### Workflow Transition Rules (Status không đổi tự do)
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

### Subtask — là 1 Task đầy đủ, không phải checklist item đơn giản
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

### Subtask — Progress Bar, không tự động đóng Task cha
Theo đúng hành vi mặc định của Jira (đã xác nhận): Subtask có Status/Assignee độc
lập với Task cha, nhưng **Task cha không tự động chuyển sang `Done` dù mọi subtask
đã `Done`** — Reporter/PM/người phụ trách Task cha vẫn phải tự tay đóng Task cha.
Lý do: Task cha có thể còn việc khác ngoài các subtask đã liệt kê (review tổng thể,
tổng hợp kết quả...).
- **Progress bar**: Task cha hiển thị % subtask đã `Done` / tổng số subtask (tính
  toán, không lưu cứng — tương tự `IsOverdue`)
- Không cần thêm logic tự động trong `TaskStatusTransitionService` cho việc này —
  chỉ cần 1 hàm tính `SubtaskProgress` ở tầng Application để hiển thị lên UI

### Entity cộng tác (Nhóm A — core)
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

### Pagination & Sorting
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

### Global Exception Handling
Dùng Middleware tập trung (`ExceptionHandlingMiddleware`) bắt mọi exception chưa
xử lý, trả về format lỗi theo chuẩn `ProblemDetails` (RFC 7807) — không tự chế format
riêng để Swagger/client hiểu sẵn:
```json
{ "title": "...", "status": 400, "traceId": "..." }
```

### API Versioning
Chuẩn bị sẵn `/api/v1/...` ngay từ đầu (dùng `Asp.Versioning.Mvc`) — không bắt buộc
dùng ngay nhưng tránh phải đổi route sau này nếu API thay đổi breaking.

### CORS Policy
Cấu hình CORS rõ ràng cho phép origin của Frontend (Next.js dev: `localhost:3000`,
production: domain thật) — không dùng `AllowAnyOrigin` khi có JWT/cookie.

### Cấu hình Secrets
- **Local dev**: `dotnet user-secrets` cho connection string, JWT secret — không commit
  vào `appsettings.json`
- **Production**: biến môi trường (Environment Variables) hoặc Azure Key Vault/AWS
  Secrets Manager nếu deploy cloud
- File `appsettings.json` chỉ chứa placeholder/giá trị non-sensitive

### Health Check
Endpoint `/health` (dùng `Microsoft.Extensions.Diagnostics.HealthChecks`) kiểm tra
API còn sống và kết nối database còn ổn — cần thiết khi có Docker/CI-CD hoặc load
balancer để biết khi nào restart instance.

### Chiến lược môi trường (Environment Strategy)
| Môi trường | Mục đích | Khác biệt cấu hình |
|---|---|---|
| `Development` | Code & test local | Swagger bật, log chi tiết (Debug), seed data đầy đủ, HTTPS không bắt buộc redirect gắt |
| `Staging` *(tùy chọn)* | Test trước khi release | Giống Production nhưng dùng database riêng, dữ liệu giả |
| `Production` | Chạy thật/demo báo cáo | Swagger tắt hoặc giới hạn, log mức Warning/Error, HTTPS bắt buộc, secrets qua biến môi trường |

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
- **Đăng ký tài khoản**: Self-register (Employee tự đăng ký qua form Sign Up với
  email/password) — không cần SystemAdmin tạo hộ, giảm bottleneck. SystemAdmin chỉ
  can thiệp khi cần khóa/mở tài khoản hoặc cấp `SystemAdmin` role cho người khác.
- JWT Bearer Token, refresh token cơ chế cơ bản
- Password hash bằng BCrypt, không lưu plaintext
- **Quên mật khẩu / Reset password**: gửi email chứa link reset có token hết hạn sau
  15-30 phút (`PasswordResetToken` entity: `EmployeeId`, `Token`, `ExpiresAt`, `IsUsed`)
- **HTTPS bắt buộc**: `app.UseHttpsRedirection()` trong Program.cs — JWT token phải
  luôn truyền qua kênh mã hóa, không chấp nhận HTTP thuần ở bất kỳ môi trường nào

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
- **Input Validation**: FLuent Validation là cơ chế validate đầu vào - chống injection, XSS ở tầng API (liên hệ kinh nghiệm OWASP Top 10)
- **Rate limiting** cơ bản cho endpoint đăng nhập (chống brute-force)

---

## 11. Testing Strategy

- **Unit Test** (xUnit + Moq): tập trung Service layer (business logic), mock Repository
- **Integration Test**: test API endpoint end-to-end với in-memory hoặc test database
- Mục tiêu coverage: [điền mục tiêu, ví dụ 70% cho Service layer]

---

## 12. UML Diagrams (kế hoạch)

| Diagram | Mục đích | Trạng thái |
|---|---|---|
| Use Case Diagram | Tổng quan chức năng theo actor (SystemAdmin, ProjectManager, Member, Viewer) | ⬜ Chưa làm |
| Class Diagram | Chi tiết entity, thuộc tính, quan hệ, OOP | ⬜ Chưa làm |
| ERD | Thiết kế database quan hệ | ⬜ Chưa làm |
| Sequence Diagram | Luồng: Tạo task, Gán nhân sự, Hoàn thành task, Gửi Notification khi gán task | ⬜ Chưa làm |

---

## 13. Quy trình phát triển

1. Use Case Diagram → Class Diagram/ERD
2. Setup project structure (.NET solution theo layer)
3. Code Domain (Entity, Enum)
4. Authentication/Authorization (đổi lên trước — xem ADR §15, 2026-07-25)
5. Code từng module theo nhóm function: Project → Task → Employee → Thống kê
6. Viết Unit Test
7. Containerize (Docker) + CI/CD (tùy chọn)
8. Viết báo cáo song song từng giai đoạn

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
| [điền ngày] | Cho phép nhiều người/1 Task (Employee N–N Task qua `TaskAssignment`) | Phản ánh thực tế: task lớn thường cần nhiều người phối hợp |
| [điền ngày] | Áp dụng phân quyền 2 tầng: SystemRole + RoleInProject (`ProjectMember`) | Sát với mô hình doanh nghiệp thật, 1 người có thể khác role ở project khác nhau |
| [điền ngày] | Nâng Sprint/Backlog/Board từ "tương lai" thành tính năng core | Mục tiêu làm sản phẩm dùng được thật, không chỉ CRUD đơn thuần |
| [điền ngày] | Thêm Comment, Activity Log, Notification vào core | Đây là tính năng tối thiểu để 1 team thật sự dùng được hệ thống hàng ngày |
| [điền ngày] | Áp dụng Soft Delete cho Project/Task | Bảo toàn ActivityLog/Comment liên quan, cho phép khôi phục |
| [điền ngày] | Chuẩn hóa Pagination, Global Exception Handling, API Versioning, CORS | Đạt chuẩn API production-grade, không phải sửa lại kiến trúc giữa chừng |
| [điền ngày] | Thêm mục Non-Functional Requirements + Data Seeding | Phục vụ demo báo cáo trôi chảy và thể hiện đầy đủ tư duy thiết kế hệ thống |
| [điền ngày] | Mọi `User` được tạo Project, tự động thành `ProjectManager` của project đó | Tránh bottleneck xin duyệt qua SystemAdmin, khớp cách Jira/Trello vận hành thật |
| [điền ngày] | `SystemAdmin` tách bạch khỏi Project Role: chỉ read-only toàn hệ thống, muốn thao tác phải là `ProjectMember` như bình thường | Tránh "God Mode" — giữ đúng nguyên tắc Least Privilege, dễ audit trách nhiệm |
| [điền ngày] | Giữ `Viewer` như 1 actor riêng trong Use Case Diagram | Phản ánh nhu cầu thực tế: stakeholder/khách hàng/auditor cần xem mà không cần sửa |
| [điền ngày] | Cho phép `Member` tự self-assign task đang `ToDo` (không cần PM gán); gán người khác/gỡ người khác vẫn chỉ PM | Khớp mô hình Kanban thực tế (tự "pick up" task), giảm bottleneck qua PM, vẫn tránh xung đột nhờ điều kiện task phải đang `ToDo` |
| [điền ngày] | Thêm Reporter, Priority, Label, Watcher, TaskLink, Workflow Transition Rules vào core | Đối chiếu trực tiếp mô hình Jira thật — đây là các khái niệm cơ bản mà thiếu sẽ khiến hệ thống thiếu tính thực tế |
| [điền ngày] | Đưa Epic, Issue Security Level, Advanced Search (JQL-like) vào Nhóm B (làm sau) | Đây là tính năng nâng cao/hiếm dùng ở quy mô nhỏ, tránh phình to quá mức trước khi core ổn định |
| [điền ngày] | Employee self-register tài khoản, không cần SystemAdmin tạo hộ | Giảm bottleneck, khớp cách hầu hết SaaS thật vận hành |
| [điền ngày] | Thêm `InvitationStatus` (Pending/Accepted/Declined) cho `ProjectMember` | Phản ánh đúng luồng mời thành viên thực tế, không tự tạo tài khoản hộ người chưa đăng ký |
| [điền ngày] | Thêm Reset Password qua token có hạn 15-30 phút | Tính năng Auth cơ bản, thiếu sẽ không dùng được thật |
| [điền ngày] | Bắt buộc HTTPS toàn hệ thống | Bảo vệ JWT token khỏi bị nghe lén qua kênh không mã hóa |
| [điền ngày] | Thêm Health Check endpoint (`/health`) | Cần thiết khi có Docker/CI-CD để biết trạng thái API |
| [điền ngày] | Định nghĩa 3 môi trường Dev/Staging/Production qua `ASPNETCORE_ENVIRONMENT` | Tách cấu hình rõ ràng, tránh lẫn lộn dữ liệu test và thật |
| [điền ngày] | Subtask không tự động đóng Task cha khi tất cả subtask `Done`; chỉ hiển thị progress bar (%) | Giữ đúng hành vi mặc định của Jira thật — Task cha có thể còn việc ngoài các subtask đã liệt kê |
| [điền ngày] | Subtask là 1 Task đầy đủ (Status, Assignee, Comment, Watcher, TaskLink riêng), không phải checklist item; giới hạn chỉ 1 cấp cha–con | Task/Subtask dùng chung class, đúng nguyên lý OOP tái sử dụng; tránh phức tạp hóa với đệ quy vô hạn |
| 2026-07-25 | Làm Auth trước Project (đổi thứ tự §13) | Mọi service cần `ICurrentUserService`; làm Auth trước tránh viết code với user giả rồi sửa lại |
| 2026-07-25 | Refresh token lưu DB, hash SHA-256, rotation + reuse detection | Cho phép thu hồi thật (logout/nghi bị lộ) — JWT thuần không làm được. Theo RFC 9700 |
| 2026-07-25 | `RoleInProject` KHÔNG nhét vào JWT, chỉ `SystemRole` | Quyền theo project đổi liên tục; nhét vào token thì thu hồi không kịp thời |
| 2026-07-25 | Lỗi trả theo `ProblemDetails` (RFC 7807), khác format tự chế ở §7 | Chuẩn công nghiệp, Swagger/client hiểu sẵn |
| | | |

> 📌 Cập nhật bảng này mỗi khi có quyết định kiến trúc mới hoặc thay đổi — đây sẽ là
> phần rất hữu ích khi viết chương "Phân tích thiết kế" trong báo cáo tốt nghiệp.
