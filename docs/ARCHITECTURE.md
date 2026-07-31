# ARCHITECTURE.md
## Hệ thống Quản lý Dự án & Task (Project Management System)

> Tài liệu này ghi lại các quyết định kiến trúc (Architecture Decisions) của dự án.
> Mục đích: đảm bảo tính nhất quán xuyên suốt quá trình phát triển, và làm tài liệu
> tham chiếu cho báo cáo thực tập tốt nghiệp.
>
> Cập nhật lần cuối: 2026-07-30 (phiên Notification API + Comment API)

> ## 🧭 Bắt đầu phiên mới ở đây
> **Trạng thái:** backend core đã ĐẦY ĐỦ. Auth, Project (kể cả quản lý thành viên),
> Employee management, Task, Sprint/Backlog/Board xong từ trước; **Notification API +
> Comment API vừa hoàn thành ở phiên 2026-07-30 (tiếp)** — build sạch, **315 test pass**
> (189 unit + 126 integration), **0 migration** cho cả phiên vì schema đã đủ sẵn.
>
> ### ⚠️ Phiên tiếp theo PHẢI là Frontend, không thêm module backend nào nữa
> Bảng lộ trình ở §1 đã khuyên "xong hạng mục 1–2 rồi chuyển sang frontend" — hai hạng mục
> đó (Notification, Comment) đã xong. Thư mục `frontend/` vẫn **chưa tồn tại**, trong khi
> đây là phần chiếm nhiều thời gian nhất và là thứ hội đồng nhìn thấy đầu tiên. Mọi hạng
> mục backend còn lại (Watcher/Label/TaskLink, ActivityLog API, Dashboard, job quá hạn)
> đều làm xen kẽ được và **không cái nào chặn** việc dựng 4 màn hình đầu tiên.
>
> **Hai cái bẫy đắt nhất của bước frontend đã được dọn trước ở phiên này:**
> - Enum nay trả về dạng **chuỗi** (`"Review"` thay vì `2`) — ADR-022. Định nghĩa
>   TypeScript types theo string union, đừng theo số.
> - CORS trước đây **chưa từng hoạt động** dù ADR ghi ✅ (hai lỗi im lặng, xem đính chính
>   cuối §15). Nay đã sửa và có `CorsPolicyTests` giữ. Thêm origin của Next.js vào
>   `Cors:AllowedOrigins` trong `appsettings.Development.json` là đủ.
>
> **Trước khi viết code frontend, đọc theo thứ tự:**
> 1. §6 (Kiến trúc Frontend) và §13 bước 6 — thứ tự màn hình đã chốt
> 2. Swagger ở `/swagger` khi chạy Development — enum nay hiện dropdown tên, dùng để đối
>    chiếu DTO thay vì đọc code C#
> 3. `backend/postman/collections/PMS Endpoints v1/` — 8 folder, đã có Notifications và
>    Comments; §11 có 3 điểm dễ vấp khi gọi tay
> 4. §15 ADR-022 → ADR-026 — năm quyết định mới của phiên này
>
> **Nếu vì lý do nào đó vẫn phải làm backend trước:** khuôn mẫu gần nhất là
> `PMS.Application/Features/Comments/` (mới nhất, đủ cả 4 thao tác CRUD + phân quyền
> per-row) hoặc `Features/Notifications/` (nếu là dữ liệu không project-scoped).
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
| Sprint (CRUD + Backlog ↔ Sprint) | ✅ | `SprintService`/`SprintsController`, có Unit + Integration Test. Xóa sprint đẩy task về Backlog — ADR-020 |
| Task (CRUD + Subtask + optimistic concurrency) | ✅ | `TaskService`/`TasksController`. `RowVersion` wire đầy đủ qua DTO, đóng lại "giới hạn đã biết" của ADR-016 |
| Task — Workflow Transition Rules | ✅ | `TaskStatusTransitionService`, quyền theo ADR-017 (Assignee HOẶC PM), chặn task đang bị `Blocks`/`IsBlockedBy` |
| Task — giao việc (gán/tự nhận/gỡ) | ✅ | `TaskAssignmentService`, đúng bảng "Quy tắc gán việc" ở §5 và seq-02 |
| Board (Kanban) + Backlog | ✅ | `GET /projects/{id}/board?sprintId=` và `/backlog`; board luôn trả đủ 4 cột kể cả cột rỗng |
| Comment — API | ✅ | `CommentService`/`CommentsController`, có Unit + Integration Test. Quyền theo ADR-026: viết = PM/Member, sửa = chỉ tác giả, xóa = tác giả hoặc PM. Xóa cứng |
| Watcher / Label / TaskLink — API | ⬜ | Entity + configuration + migration đã có; `TaskLink` đang được dùng gián tiếp qua blocker check nhưng chưa có API tạo/xóa link |
| Notification — API đọc | ✅ | `NotificationFeedService`/`NotificationsController` — danh sách có phân trang, đếm chưa đọc, đánh dấu một/tất cả. Ngoại lệ hợp lệ của ADR-006/019 — xem ADR-023 |
| Activity Log — API đọc | ⬜ | Đã ghi đủ qua `IActivityLogger` (ADR-013) ở mọi luồng Project/Task/Comment, nhưng chưa có endpoint nào đọc lịch sử ra — cùng loại khoảng trống mà Notification vừa đóng |
| Employee management (ngoài Auth) | ✅ | `AdminEmployeesController` — khóa/mở tài khoản, cấp `SystemAdmin` — *bảng này từng ghi ⬜ dù đã code xong, đã sửa lại 2026-07-29* |
| Thống kê / Dashboard | ⬜ | Chưa bắt đầu |
| Frontend (toàn bộ) | ⬜ | Thư mục `frontend/` chưa tồn tại trong repo |
| Real-time (SignalR) | ⬜ | Có chủ đích — chỉ làm sau khi core CRUD ổn định (xem §6) |

### Lộ trình các phiên tiếp theo

> Sắp theo thứ tự phụ thuộc và giá trị, không phải theo độ khó. Cập nhật 2026-07-30 (phiên
> Notification + Comment). Hai hạng mục 1–2 của bảng cũ **đã xong**, nên Frontend lên #1.

| # | Hạng mục | Vì sao xếp ở đây | Quy mô ước tính |
|---|---|---|---|
| 1 | **Frontend — bắt đầu** (Next.js scaffold → Auth → Project list → Board/Backlog) | API của Project + Task + Sprint + Comment + Notification đã đủ ổn định để dựng các màn hình chính. §13 bước 6 đã nói không đợi backend xong hết. **Đây là rủi ro tiến độ lớn nhất** — thư mục `frontend/` vẫn chưa tồn tại, mà đây là phần chiếm nhiều thời gian nhất và là thứ hội đồng nhìn thấy đầu tiên. Enum đã trả về dạng chuỗi (ADR-022) và CORS đã thật sự hoạt động, nên hai cái bẫy đắt nhất của bước này đã được dọn trước | Lớn — nhiều phiên |
| 2 | **Watcher + Label + TaskLink API** | Ba cái nhỏ, gom một đợt. ⚠️ `Watcher` **không** kế thừa `BaseEntity` nên `IRepository<T>` không phục vụ được, phải xử lý riêng. `TaskLink` cần thêm guard chống link vòng (A blocks B và B blocks A cùng lúc sẽ khóa chết cả hai ở blocker check). `Watcher` giờ có thêm lý do rõ ràng để làm: nó đã nằm trong `InterestedEmployeeIds` của cả luồng đổi status lẫn comment, nhưng chưa có API nào để đăng ký theo dõi | Vừa |
| 3 | **Background job task quá hạn** → Notification `DueSoon` | `ITaskRepository.GetOverdueAsync` **đã tồn tại nhưng chưa có caller nào** — đúng loại code chờ sẵn mà nếu để lâu sẽ lệch khỏi nghiệp vụ. Phụ thuộc cũ đã được giải: giờ đã đọc được thông báo nên job có ý nghĩa thật | Nhỏ |
| 4 | **Activity Log API đọc** | Khoảng trống cùng loại với Notification trước phiên này: `IActivityLogger` ghi ở mọi luồng (nay có cả 3 action của Comment) nhưng chưa endpoint nào đọc ra. Cần cho tab "Activity" ở màn hình chi tiết task (§6) | Nhỏ — 1 service, 1–2 endpoint |
| 5 | **Dashboard thống kê** | `ProjectAction.ViewStatistics` đã có sẵn trong ma trận quyền nhưng chưa ai dùng. Cần cả API lẫn màn hình Recharts | Vừa |
| 6 | **Reset password** | Mục ⬜ cuối cùng của Auth. Còn phụ thuộc email service nên để sau | Nhỏ–vừa |
| 7 | **Real-time (SignalR)** | Theo §6, chỉ làm sau khi core CRUD **và** frontend đã ổn định | Vừa |

**Rủi ro tiến độ cần nói thẳng:** backend core giờ đã đầy đủ (Auth, Project, Member, Task,
Sprint, Board, Comment, Notification) nhưng frontend **vẫn ở con số không**. Lời khuyên của
bảng này ở phiên trước là "xong hạng mục 1–2 rồi chuyển sang frontend" — hai hạng mục đó
đã xong, nên **phiên tới phải là frontend**, không thêm module backend nào nữa. Các hạng mục
2–5 đều là thứ làm xen kẽ được khi frontend cần tới chúng; không hạng mục nào trong đó chặn
việc dựng 4 màn hình đầu tiên.

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

### Entity phân loại & liên kết (theo mô hình Jira thật) ⬜ *(entity đã có; `TaskLink` đang được dùng gián tiếp qua blocker check của `TaskStatusTransitionService`, nhưng cả ba chưa có API riêng)*
- **`Label`**: Tên tag tự do (ví dụ: `bug`, `frontend`, `urgent`) — Task N—N Label,
  giúp lọc/tìm kiếm linh hoạt hơn Status/Priority cố định
- **`Watcher`** *(bảng trung gian Employee–Task)*: `TaskId`, `EmployeeId` — người
  theo dõi task để nhận Notification dù không được assign làm (khác với `TaskAssignment`)
- **`TaskLink`** *(self-referencing giữa 2 Task)*: `SourceTaskId`, `TargetTaskId`,
  `LinkType` (`Blocks` / `IsBlockedBy` / `RelatesTo` / `Duplicates`) — quản lý phụ
  thuộc giữa các task, ví dụ Task B không thể `Done` nếu Task A (blocking) chưa xong

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

### Hiện trạng (2026-07-30, sau phiên Notification + Comment)

**315 test pass** — 189 unit + 126 integration, build 0 warning.

| Nhóm | Unit | Integration |
|---|---|---|
| Domain (invariant, state machine) | `ProjectTests`, `ProjectMemberTests`, `TaskItemTests`, `SoftDeletableContractTests`, `NotificationTests` | — |
| Auth / Admin | `EmployeeAdminServiceTests` | `AccountLockingTests` |
| Project | `ProjectServiceTests`, `ProjectMemberServiceTests`, `ProjectPermissionsTests` | `ProjectsCrudTests`, `ProjectsAuthorizationTests`, `ProjectsDeleteTests`, `ProjectMembersTests` |
| Task / Sprint | `TaskServiceTests`, `TaskStatusTransitionServiceTests`, `TaskAssignmentServiceTests`, `SprintServiceTests` | `TasksCrudTests`, `TasksAuthorizationTests`, `TaskStatusTransitionTests`, `TaskAssignmentTests`, `SubtaskTests`, `SprintsCrudTests`, `BacklogAndBoardTests` |
| Notification / Comment | `NotificationFeedServiceTests`, `CommentServiceTests` | `NotificationsTests`, `CommentsTests` |
| Hạ tầng API (cấu hình pipeline) | — | `CorsPolicyTests`, `EnumSerializationTests` |

Nhóm cuối là loại test mới của phiên này và đáng ghi lại lý do: **cấu hình pipeline cũng là
quyết định kiến trúc và cũng cần test giữ**. `CorsPolicyTests` sinh ra sau khi phát hiện CORS
đã bị vô hiệu hóa im lặng suốt nhiều phiên dù ADR ghi ✅ — không test nào đỏ, build không
warning, vì middleware không tìm thấy policy thì chỉ log rồi đi tiếp (xem đính chính ở §15).

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
| 2026-07-29 | `SystemAdmin` tách bạch khỏi Project Role: chỉ read-only toàn hệ thống, muốn thao tác phải là `ProjectMember` như bình thường | Tránh "God Mode" — giữ đúng nguyên tắc Least Privilege, dễ audit trách nhiệm — *chưa có code nào cho SystemAdmin bypass đọc toàn hệ thống* |
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

**Lý do:** Với đổi trạng thái, **chính state machine đã là chốt chặn concurrency**. Bảng
chuyển đổi từ chối cả trường hợp "đứng yên" (`InProgress → InProgress` = `false`), nên hai
người cùng kéo một thẻ tới cùng một cột thì người thứ hai load lại thấy trạng thái đã đổi và
nhận 409. Bắt round-trip thêm `RowVersion` không thêm bảo đảm nào, nhưng buộc UI Kanban phải
mang theo token mới nhất trong mỗi thao tác kéo-thả. `Update` thì khác: hai người cùng sửa
tên task là mất dữ liệu thật kiểu lost-update, không có cơ chế nào khác chặn.

**Kiểm chứng:** `TasksCrudTests.Sua_task_voi_RowVersion_cu_thi_bi_chan_409` và
`TaskStatusTransitionTests.Doi_status_khong_can_RowVersion_nhung_lan_hai_cung_dich_bi_chan_409`.

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

> 📌 Cập nhật bảng này mỗi khi có quyết định kiến trúc mới hoặc thay đổi — đây sẽ là
> phần rất hữu ích khi viết chương "Phân tích thiết kế" trong báo cáo tốt nghiệp.
