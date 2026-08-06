# Chuẩn bị cho phiên Frontend kế tiếp

> Soạn ngày 2026-07-31, cuối phiên "Frontend — nền tảng".
> **Cập nhật 2026-08-06** — **đọc §00 trước**, rồi §0, §0-chiều, §0-cũ và §0a; các mục bên
> dưới lỗi thời phần lớn. Đọc cùng `ARCHITECTURE.md` §6 và ADR-027 → **ADR-056**.

---

## 00. 🆕 Cập nhật 2026-08-06 — hồ sơ cá nhân · kỹ thuật DB · nhóm báo cáo

**Đây là mục mới nhất.** Bốn hạng mục còn lại của lộ trình (§1 ARCHITECTURE.md) làm trong
một phiên, theo đúng thứ tự ưu tiên đã chốt với người dùng — **không** làm Search/SignalR.

### ✅ Đã làm xong

| Việc | Kết quả |
|---|---|
| **Đường ghi hồ sơ cá nhân** (ADR-054) | `/profile` hết chỉ-đọc: sửa tên tại chỗ + dialog đổi mật khẩu. Cả hai gọi endpoint trả token mới, `setSession` lại ngay — không còn "báo lưu thành công mà vẫn hiện tên cũ" |
| **Kiểm tay board** | Đổi thứ tự cột + đổi category cột ảnh hưởng thống kê — kiểm chứng THẬT qua round-trip HTTP + đọc DB (3 test mới `BoardColumnsTests.cs`), không phải bấm chuột. ⚠️ Kéo–thả thật (chuột/cảm ứng/bàn phím) **vẫn chưa kiểm** — môi trường phiên này không có công cụ trình duyệt nào |
| **Kỹ thuật DB** (ADR-055) | Index · view · 2 stored procedure · trigger · CHECK constraint, migration `AddReportingDbObjects`, 7 integration test. **Tìm và sửa một lỗi có sẵn dạng mới**: trigger trên bảng có cột `rowversion` làm mọi ghi qua EF 500 cho tới khi khai `HasTrigger` |
| **Nhóm báo cáo** (ADR-056) | `GET /projects/{id}/reports/{backlog-insight,velocity,timeline}` — **ba tab/route riêng** (`backlog-insight`/`velocity`/`timeline` trong `PROJECT_SECTIONS`, tự lên cả thanh tab lẫn sidebar "Lập kế hoạch"), không dồn vào một tab "Báo cáo" như bản làm buổi sáng — tách ra cho sidebar đủ đầy hơn và mỗi báo cáo có chỗ đứng riêng |

### 🪤 Bẫy mới đáng nhớ nhất của phiên này

🔴 **Thêm trigger vào một bảng có cột `rowversion` (hoặc bất kỳ cột computed nào EF cần đọc
lại sau ghi) mà KHÔNG khai `HasTrigger` trong Fluent API thì MỌI INSERT/UPDATE qua EF vào
bảng đó ném `DbUpdateException`** — không liên quan gì tới logic trigger, mà vì SQL Server
cấm `OUTPUT` không có `INTO` trên bảng có trigger đang bật, và EF Core mặc định sinh đúng
dạng `OUTPUT` đó để đọc `RowVersion` sau khi ghi. Migration áp được, build sạch, nhưng
`POST /tasks` (và mọi endpoint ghi Task khác) trả 500 tuyệt đối cho tới khi thêm:

```csharp
builder.ToTable("Tasks", t => t.HasTrigger("trg_Tasks_MaintainProjectTaskCount"));
```

Chi tiết đầy đủ + bẫy thứ hai (trigger tính sai nếu dùng `AFTER DELETE` trên bảng xóa MỀM)
ở ADR-055.

**Cập nhật cuối ngày:** Timeline đã làm nốt cùng ngày (`GET /projects/{id}/reports/timeline`
— mọi sprint kể cả `Planned`, sắp theo `StartDate`, LINQ thuần trên `Sprints` chứ không qua
view). `SprintTimelineChart` là Gantt tự dựng bằng `<div>` định vị `%`, KHÔNG dùng Recharts —
thư viện đó không hợp cho việc vẽ thanh theo khoảng ngày thật. Cả ba báo cáo (backlog
insight/velocity/timeline) tách thành ba tab/route riêng thay vì một tab "Báo cáo" gộp, theo
đúng yêu cầu làm sidebar dự án "phong phú" hơn.

### Việc còn lại

1. **Kéo–thả thật trên UI** (chuột/cảm ứng/bàn phím) — nợ kiểm chứng vẫn treo, không phiên
   nào trong chuỗi gần đây có công cụ trình duyệt để trả nợ này.
2. Search toàn cục (Elasticsearch) · SignalR — cố ý ngoài phạm vi, xem §6 ARCHITECTURE.md.

---

## 0. Cập nhật 2026-08-05 (tối) — cột tuỳ biến · vòng đời Sprint · Việc của tôi

**Đây là mục mới nhất.** Ba mục §0 bên dưới là các đợt trước trong cùng ngày.

### ⚠️ Đọc dòng này trước khi sửa bất kỳ thứ gì chạm tới trạng thái task

**`task.status` KHÔNG còn là chuỗi enum.** Nó là một object:

```ts
{ columnId: string; name: string; color: string; category: 'ToDo' | 'InProgress' | 'Done' }
```

Nếu bạn thấy một trong hai lỗi này, đây là nguyên nhân — không phải bug mới:

| Triệu chứng | Nguyên nhân |
|---|---|
| `undefined is not an object (evaluating 'STATUS_TONE[status].badge')` | Tra bảng enum bằng một object → `undefined` |
| `Each child in a list should have a unique "key" prop … from ChartCard` | `d.status` không còn tồn tại → mọi `<Cell key={undefined}>` |

🔴 **Luật thay thế:**
- Màu/tên chip trạng thái task → `TaskStatusChip` / `TaskStatusDot`
  (`components/tasks/task-status-chip.tsx`), màu lấy từ `status.color`.
- `STATUS_TONE` **chỉ còn** cho trạng thái **PROJECT** (vẫn là enum 4 giá trị).
- Mọi phép kiểm "task xong chưa" → `status.category === 'Done'`.
  **Không so tên cột** (người dùng đặt tuỳ ý), **không so `columnId` với hằng**.

### ✅ Đã làm xong trong đợt này

| Việc | Kết quả |
|---|---|
| **Cột board tuỳ biến** (ADR-052) | Dialog "Quản lý cột" trên trang Bảng: thêm/sửa/đổi màu/đổi thứ tự/xóa. **Xóa cột còn task bắt buộc chọn cột đích**; không xóa được cột cuối |
| **Thu/mở cột** | Cột thu về `w-11`, tên xoay dọc; vẫn nhận thả được |
| **Board cuộn ngang** | Đổi từ lưới 4 cột sang flex cuộn — số cột nay không cố định |
| **Vòng đời Sprint** (ADR-050) | Tab Sprint kiểu Jira: thu/mở, dòng task inline, nút Bắt đầu/Đóng, dialog đóng **hỏi task chưa xong đi đâu** |
| **"Việc của tôi"** (ADR-053) | `/my-work` — gom theo dự án hoặc xếp phẳng theo hạn. Mục đầu trong sidebar |
| Ô chọn trạng thái ở chi tiết Task | Liệt kê **mọi cột**, không còn lọc theo ma trận chuyển trạng thái |
| Biểu đồ thống kê | Dùng **đúng màu cột** người dùng đặt, `key` là `columnId` |

### 🪤 Năm cái bẫy MỚI của đợt này

1. 🔴 **`min-width:auto` cắn thêm HAI lần nữa** (tổng cộng năm).
   - Board cuộn ngang: `min-w-0` trên chính dải cuộn **chưa đủ** — grid trần có track ngầm
     cỡ `auto`, mà `auto` phân giải thành **max-content**. Phải thêm
     `grid-cols-[minmax(0,1fr)]` lên container cha.
   - Hàng nút `PageHeader` ba nút ở 375px: rộng 439px trong khung 343px, đẩy `scrollWidth`
     lên 455. Sửa gốc là `flex-wrap` + `min-w-0` **ở chính `PageHeader`**, không ở từng trang
     — mọi màn có từ ba nút trở lên đều sẽ gặp.

   📌 **Cách chẩn đoán nhanh nhất, dùng lại:** duyệt mọi phần tử, tìm cái nào rộng hơn cha
   *mà cha không phải scroll container*. Nó chỉ thẳng ra thủ phạm thay vì hậu quả:
   ```js
   document.querySelectorAll('*').forEach(el => {
     const p = el.parentElement; if (!p) return;
     const ox = getComputedStyle(p).overflowX;
     if (el.getBoundingClientRect().width > p.getBoundingClientRect().width + 1
         && ox !== 'auto' && ox !== 'scroll') console.log(el.className, el.getBoundingClientRect().width);
   });
   ```
   ⚠️ **Nút TanStack devtools nằm ngoài mép phải là BÌNH THƯỜNG** — nó `position: fixed` và
   không có ở production build. Đo `main.scrollWidth === main.clientWidth` thay vì
   `document`, nếu không bạn sẽ đuổi theo một con ma.

2. 🔴 **`patchTaskInBoard` từng ĐỆ QUY VÔ HẠN.** Nó gọi `moveTaskInBoard` rồi tự gọi lại;
   `moveTaskInBoard` trả về **chính board cũ** khi cột đích không có trên board đang xem —
   chuyện xảy ra thật sau ADR-052 (người khác vừa tạo cột, hoặc board đang lọc theo sprint).
   Đã thêm chốt `if (movedBoard === board) return board;`. Thiếu nó là **treo cứng tab**.

3. 🔴 **`useBoard(projectId, null)` KHÔNG phải "không nạp"** — `null` nghĩa là board
   **"Tất cả task"** của project. Muốn hoãn thì dùng `{ enabled }`. Nhầm hai thứ này là nạp
   cả project trong khi chỉ muốn một sprint.

4. **`SprintResponse.status` ≠ `SprintResponse.isActive`.** `isActive` suy từ NGÀY; `status`
   do người dùng bấm. Chỗ chúng **lệch nhau** chính là tín hiệu đáng hiện: `status==='Active'`
   mà `isActive===false` nghĩa là **sprint quá hạn mà chưa ai đóng** → badge riêng.

5. **`DELETE /columns/{id}` có THÂN request.** Khác thường nhưng cố ý — `targetColumnId`
   không được nằm trên query string. Nhớ `apiFetch` hỗ trợ `body` cho DELETE.

### ⬜ Việc còn lại cho phiên sau

1. **Nhóm báo cáo kiểu Jira** — backlog insight · velocity · report · timeline.
   ✅ **Velocity ĐÃ mở khóa** (`Sprint.CompletedAt`). Sidebar có sẵn nhóm **LẬP KẾ HOẠCH**.
   ⚠️ Gom theo `columnId`/`category`, **không theo enum** — số cột khác nhau giữa các project.
2. **Áp kỹ thuật DB** — trigger · stored procedure · view · index. Không có giao diện nào.
3. **Đường GHI cho hồ sơ cá nhân** — đọc **ADR-049** trước.
4. Search toàn cục (Elasticsearch) · SignalR.

### 📌 Nợ kiểm chứng của đợt này — nói thẳng

- **Kéo–thả bằng chuột/cảm ứng/bàn phím trên board cột động chưa kiểm bằng tay.** Logic
  `useDroppable` đã đổi (bỏ lọc theo ma trận), và công cụ trình duyệt của phiên không bắn
  được sự kiện kéo tổng hợp. Phần đổi trạng thái đã kiểm qua **ô chọn ở chi tiết Task** và
  qua API trực tiếp, nhưng đó không thay được một lần kéo thật.
- **Đổi thứ tự cột (nút ← →) chưa bấm thử trên giao diện** — endpoint `PUT /columns/order`
  đã có test, nhưng đường UI thì chưa.
- **Đổi `category` của một cột đang có task chưa thử trên UI.** Backend có
  `SyncTaskCategoriesAsync` và cảnh báo đã hiện trong form, nhưng chưa xác nhận bằng mắt
  rằng số liệu thống kê đổi theo.

---

## 0-chiều. Cập nhật 2026-08-05 (chiều) — phiên "ba lỗ hổng UI + ADR-048" ĐÃ XONG

**Đây là mục mới nhất.** §0-cũ ngay bên dưới là bản buổi sáng cùng ngày; nó đã hoàn thành
nhiệm vụ và **cảnh báo nhánh của nó là SAI** — xem ngay dưới đây.

### 🔴 Đính chính: cảnh báo nhánh ở §0-cũ đã tự nói sai về chính nó

§0-cũ viết *"nhánh làm việc là `module/authorization-permission`, `main` đi sau 8 commit"*.
Đo lại lúc bắt đầu phiên này:

```bash
git rev-list --left-right --count main...module/authorization-permission   # -> 15   0
```

**Ngược hẳn:** `main` đi **trước 15 commit**, và `module/authorization-permission` **không
còn commit riêng nào** — nó đã merge vào `main` qua PR #23, rồi PR #24 merge tiếp `dev`.
Nhánh đó nay là một con trỏ chết. `git status` cũng sạch tuyệt đối, `git stash list` rỗng —
không có "5 file chưa commit trong `components/tasks/`" nào cả (công việc đó nằm trong commit
`5968108`, đã có trong `main`).

> **Đây là lần thứ hai cùng một hình dạng lỗi, và lần này nạn nhân là chính tài liệu.**
> Buổi sáng: một phiên tin mô tả lỗi mà không kiểm `git log`, sửa lại thứ đã sửa. Buổi chiều:
> tài liệu viết ra để cảnh báo chuyện đó lại chứa một khẳng định nhánh đã cũ, và nếu tin nó
> thì phiên này đã làm việc trên một nhánh đi sau 15 commit.
>
> Bài học cập nhật: **`git log` không phải bước kiểm tra một lần rồi ghi vào tài liệu — nó là
> bước phải chạy lại mỗi phiên.** Một dòng "nhánh làm việc là X" trong file markdown có hạn
> sử dụng tính bằng giờ. Câu lệnh thì luôn đúng; câu chữ thì không.

### ✅ Đã làm xong trong phiên này

| Việc | Kết quả |
|---|---|
| Nút "Mở trang riêng" no-op | ✅ `<a href>` thường thay `<Link>` — `task-detail-header.tsx` |
| Không có trang hồ sơ | ✅ `/profile` **chỉ đọc** + mục trong `UserMenu` (**ADR-049**) |
| Sidebar quá thưa | ✅ Khối ngữ cảnh dự án + "Dự án của tôi"; comment sai đã sửa |
| ADR-048 (a) `Project.Status` | ✅ Nút Hoàn thành / Mở lại ở header dự án, PM-only |
| ADR-048 (b) `GET /employees?search=` | ✅ Ô gợi ý trong dialog mời thành viên |
| ADR-048 (c) @mention | ✅ Ô chọn + `lib/comments/mentions.ts` (6 test mới) |
| 🆕 **Menu người dùng SẬP** (lỗi có sẵn) | ✅ Đã sửa — xem bẫy #1 bên dưới |

> ⚠️ **Hai dòng trong bảng trên đã bị đợt hai cùng ngày sửa lại** (xem mục ngay dưới): sidebar
> nay **đổi hẳn theo ngữ cảnh** chứ không phải thêm khối, và `/profile` đã gỡ khối quyền.

### 🪤 Bốn cái bẫy MỚI của phiên này

1. 🔴 **`DropdownMenuLabel` làm SẬP cả menu — lỗi có sẵn trên `main`, không ai biết.**

   Nó ánh xạ sang `Menu.GroupLabel` của Base UI, thứ **bắt buộc** phải nằm trong một
   `Menu.Group`. `UserMenu` dùng nó trần từ ngày dựng → mở menu là ném
   `MenuGroupContext is missing` và **toàn bộ menu sập**, kéo theo **lối ra duy nhất để
   Đăng xuất**.

   **Đã kiểm chứng là có sẵn chứ không phải do phiên này gây ra**: `git stash` thay đổi của
   phiên rồi mở lại menu — vẫn sập y hệt.

   > Đây là lần thứ **sáu** dự án gặp đúng hình dạng lỗi mà §15 đã đặt tên từ 2026-07-30:
   > *thứ cần kiểm chứng chưa có ai gọi tới.* Và lần này nó nằm ở chỗ khó tin nhất — nút
   > Đăng xuất, thứ mọi phiên "kiểm chứng trên trình duyệt" đều đi ngang qua mà **chưa ai
   > bấm mở**. Tài liệu ghi ✅ cho `UserMenu` từ 2026-07-31.
   >
   > Cách sửa **không** phải bọc vào `Menu.Group`: khối đó là **danh tính** người đang đăng
   > nhập, không phải nhãn của một nhóm mục. Một `<div>` thường mới đúng ngữ nghĩa — bọc
   > `Group` chỉ để hết lỗi là hứa với trình đọc màn hình một quan hệ không tồn tại.

2. 🔴 **"Dự án gần đây" là thứ backend KHÔNG dựng được.** `ProjectRepository` chỉ nhận
   `sortBy` = `name` / `status` / `expectedCompletionDate` — **không có khóa thời gian nào**.
   Đã đổi nhãn thành **"Dự án của tôi"** (A→Z) thay vì sắp theo tên rồi gắn nhãn "gần đây",
   vì cái sau là nói dối người dùng về ý nghĩa của danh sách. Muốn "gần đây" thật thì phải
   thêm cột `LastAccessedAt` (hoặc `UpdatedAt` + `sortBy` mới) ở backend trước.

3. **@mention: chữ và id có thể trôi khỏi nhau.** Client gửi **id**, còn người dùng thì sửa
   **chữ** — chèn `@Nam` rồi xóa đi trước khi gửi thì id vẫn nằm trong state. `reconcileMentions`
   (`lib/comments/mentions.ts`) lọc lại theo nội dung thật lúc submit.
   **Đã kiểm chứng trên máy chủ thật, không chỉ bằng unit test**: chọn cả Bình và Cường, xóa
   chữ `@Le Van Cuong`, gửi → Bình nhận `Mentioned`, **Cường không nhận gì**.

4. **Ô SỬA comment không có @mention** — `UpdateCommentRequest` chỉ có `content`. Đừng thêm
   nút nhắc tên vào nhánh sửa.

### 📌 Cách kiểm "điều hướng cứng hay soft nav" — dùng lại

Với ADR-043, URL **không đổi** khi thoát dialog ra trang thật, nên nhìn URL không phân biệt
được. Gieo một biến lên `window` rồi bấm; biến **mất** nghĩa là ngữ cảnh JS bị hủy, tức là
tải trang đầy đủ:

```js
window.__marker = 1;                                  // trước khi bấm
// … bấm "Mở trang riêng" …
window.__marker   // undefined  -> hard nav ✅ ;  1 -> vẫn là soft nav ❌
```

Kèm hai dấu hiệu nữa: `[role="dialog"]` biến mất, và `nav[aria-label="Khu vực của dự án"]`
**ẩn** (`showTabs === false` khi segment là `'tasks'`).

### 🆕 Đợt hai cùng ngày — sidebar kiểu Jira, hai vỏ Task, gọn hồ sơ (ADR-051)

| Việc | Kết quả |
|---|---|
| Sidebar **đổi hẳn theo ngữ cảnh** | Ngoài dự án → nav toàn cục. Trong dự án → **chỉ của dự án đó** (link "Tất cả dự án" · đầu đề + vai trò của bạn · LẬP KẾ HOẠCH · QUẢN LÝ). Bỏ hẳn danh sách "Dự án của tôi" |
| Hai vỏ chi tiết Task **khác nhau thật** | Ở trang thật, `Bình luận \| Lịch sử` xuống dưới hai cột, lấy trọn bề ngang: ô soạn **608px → 1096px** ở viewport 1400px |
| `/profile` gọn lại | Gỡ khối "Quyền hệ thống" — đó là màn hình cho người viết code, không phải người dùng |

**Ba nhận xét dẫn tới đợt này, đáng ghi vì cả ba đều đúng:**

1. *"Để quá nhiều Project rồi Dự án trong sidebar thì thừa"* — **thừa nặng hơn thế**: có ba
   đường tới cùng một chỗ, cộng trang mặc định sau đăng nhập cũng là `/projects`.
2. *"Task details cần trang riêng để làm gì nếu không khác dialog?"* — trang là **bắt buộc**
   (F5/deep-link/tab mới, nếu không thì 404), nhưng **nút "Mở trang riêng" thì đang hứa một
   khác biệt không tồn tại**. Sửa bằng cách cho nó khác thật, chứ không bỏ trang.
3. *"Không ai show chi tiết Quyền hệ thống ra vậy"* — đúng, và người dùng cũng không hành
   động được gì với danh sách đó.

> 📌 **Bài học chung của cả ba:** chúng đều là *thừa* chứ không phải *thiếu* — và một phiên
> đang hào hứng "dựng thêm" thì rất khó tự thấy. Đợt sáng cùng ngày vừa **thêm** khối dự án
> và danh sách "Dự án của tôi" vào sidebar; đợt chiều **gỡ** đúng thứ vừa thêm, vì thêm đủ
> rồi mới lộ ra là quá nhiều.

### ⬜ ~~Việc còn lại cho phiên sau~~ — ✅ mục 1 đã xong ngay trong ngày

> ⚠️ Danh sách này của **đợt chiều**; mục 1 (vòng đời Sprint) đã hoàn thành ở **đợt tối**
> cùng ngày. Danh sách còn hiệu lực nằm ở **§0**.

1. ~~**Vòng đời Sprint**~~ ✅ **xong 2026-08-05 tối** — ADR-050 đã cài đặt đầy đủ.
2. **Nhóm báo cáo kiểu Jira** — velocity nay đã mở khóa.
3. **Áp kỹ thuật DB** — không có giao diện nào.
4. **Đường GHI cho hồ sơ cá nhân** — đọc **ADR-049** trước.
5. Search toàn cục (Elasticsearch) · SignalR.

---

## 0-cũ. Cập nhật 2026-08-05 (sáng) — ĐÃ XỬ LÝ HẾT, giữ lại làm hồ sơ

> ⚠️ Cảnh báo nhánh trong mục này **đã sai** — xem §0. Phần kỹ thuật thì đúng nguyên văn và
> đã được dùng làm đề bài cho phiên chiều.

### Ba tính năng backend đã xong mà frontend CHƯA CÓ GÌ (ADR-048) — ✅ nay đã có

Không bị chặn, không cần quyết định. Cả ba **vô hình nếu chỉ nhìn UI** — backend có đường
đi, người dùng không có nút.

| Backend đã có | Cần dựng | Bẫy |
|---|---|---|
| `POST /projects/{id}/complete` + `/reopen` | Nút đổi trạng thái dự án, PM-only | `reopen` trả **409** nếu project chưa `Done`; `reopen` đưa về `InProgress` **chứ không** về `ToDo` |
| `GET /employees?search=` | Ô gợi ý khi mời thành viên | Từ khóa **≥ 2 ký tự**, ngắn hơn là **400**. DTO chỉ 3 trường — đừng trông chờ `systemRole`/`isLocked` |
| `mentionedEmployeeIds` | Ô chọn @mention trong comment | Client **gửi id**; server **không** parse `@tên`. Nhắc người ngoài dự án vẫn trả **thành công**, phần nhắc bị lọc bỏ im lặng (trả 400 sẽ là rò rỉ) |

### 🪤 Ba chỗ "bấm vào không thấy gì" — rà giao diện 2026-08-05 sáng (✅ đã gỡ cả ba)

1. 🔴 **Nút "Mở trang riêng" trong dialog chi tiết Task là một NO-OP.**
   `components/tasks/task-detail-header.tsx` dùng `<Link>` trỏ tới
   `/projects/{id}/tasks/{taskId}` — nhưng khi dialog đang mở, **URL hiện tại đã đúng là
   chuỗi đó** (intercepting route `(.)` giữ nguyên đường dẫn, ADR-043). Bấm `<Link>` tới
   chính URL đang đứng không đổi router state → dialog ở nguyên đó.

   Sửa bằng **điều hướng cứng** — `<a href>` thường hoặc `window.location.assign()`, **không
   phải** `next/link`. Intercepting route **chỉ** áp cho soft navigation; một lần tải trang
   đầy đủ mới render trang thật.

   > Đây là cái bẫy **cấu trúc** của ADR-043: hai vỏ dùng chung một URL là điều làm cho
   > dialog chia sẻ link được — và cũng chính là điều làm cho "thoát ra trang thật" không
   > thể là một soft navigation. Kiểm chứng đúng: mở dialog → bấm → dialog phải **biến mất**
   > và thanh tab dự án phải **ẩn** (`showTabs === false` khi segment là `'tasks'`).

2. **Không có trang hồ sơ cá nhân.** `UserMenu` chỉ có mục Đăng xuất; không route nào trong
   `app/`. ⚠️ Backend cũng **chưa có đường sửa hồ sơ**: `AuthController` chỉ có `GET /auth/me`,
   không `PUT /employees/me`, không đổi mật khẩu khi đã đăng nhập.

   Trang **chỉ đọc** làm được ngay. Muốn sửa được thì phải làm backend trước — và nhớ
   `/auth/me` **dựng DTO từ CLAIM chứ không đọc DB**, nên đổi tên sẽ **không hiện ra** cho
   tới khi token được làm mới. Đó là quyết định cần **ADR riêng**, không phải chi tiết cài đặt.

3. **Sidebar chỉ còn 4 mục**, không phản ánh phạm vi sản phẩm khi đang ở trong một dự án.

   🔴 **Trong `components/layout/sidebar.tsx` có một khẳng định SAI, sửa luôn khi làm:**
   comment ở đó nói `AppShell` "không biết project nào đang mở vì nó nằm TRÊN segment `[id]`".
   Không đúng với client component — `SidebarNav` **đã** gọi `usePathname()`, mà hàm đó trả
   **toàn bộ** đường dẫn kể cả `[id]`. Rút id bằng regex trên pathname là hợp lệ, và **không**
   dính rủi ro "hai tab nói dối" mà comment lo: đó là rủi ro của **store**, còn URL vốn đã
   thuộc về từng tab.

   Hướng đúng: khối theo **ngữ cảnh dự án** hiện khi pathname khớp `/projects/{id}/*`
   (Bảng · Backlog · Sprint · Thống kê · Thành viên), cộng danh sách dự án gần đây.

### Cách chẩn đoán tràn ngang — dùng lại, nhanh hơn đoán từ class

Đo `width: min-content` của **từng grid item** rồi so với bề rộng khả dụng của container:
item nào có min-content lớn hơn chính là thứ sàn hóa track. Nhanh hơn nhiều so với duyệt
`getBoundingClientRect()` từng cấp, vì nó chỉ thẳng ra **nguyên nhân** thay vì hậu quả.

```js
for (const child of container.children) {
  const prev = child.style.width;
  child.style.width = 'min-content';
  console.log(child.className, child.getBoundingClientRect().width);
  child.style.width = prev;
}
```

Ví dụ thật (thanh tab dự án ở 375px): header `66.8` · **thanh tab `366.8`** · thân board
`224` → thủ phạm là thanh tab, **không phải** cột Kanban như mô tả lỗi ban đầu nói. Con số
`366.8` khớp chính xác tổng bề rộng 4 tab cộng gap — đó là bằng chứng, không phải suy đoán.

⚠️ Và một `nav` **đã có** `overflow-x-auto` vẫn có thể là nguồn: `min-width` computed của
chính nó là `0px`, nhưng `<div>` bọc ngoài là block thường với `min-width: auto` nên nó
**khai báo hộ** trọn bề rộng nội tại lên cấp trên.

---

## 0a. Cập nhật 2026-08-04 — KHÔNG CÒN MÀN HÌNH NÀO ⬜

**Đọc §0 trước mục này; §0b, §2, §3 và §4 bên dưới đã lỗi thời phần lớn.**

Ba nhóm việc mà §0 cũ liệt kê — Dashboard, Quên/đặt lại mật khẩu, nhóm Admin — **đã xong
hết**. Cộng thêm một nhóm không có trong danh sách đó: **màn Phân quyền** (`/admin/roles`),
sinh ra từ mô hình permission mới (ADR-045).

### Còn lại gì cho phiên sau

| Việc | Trạng thái | Ghi chú |
|---|---|---|
| **Backend tầng 3** | ⬜ đã khảo sát, chưa làm | Bốn tính năng — chi tiết đầy đủ ở `ARCHITECTURE.md` §1 mục E. Nặng nhất: vòng đời Sprint (cần ADR riêng) |
| **Báo cáo kiểu Jira** | ⬜ phiên riêng | Velocity **phụ thuộc** vòng đời Sprint — làm hạng mục kia trước |
| **Kỹ thuật DB** | ⬜ phiên riêng | Trigger · stored procedure · view · index |
| Search toàn cục | ⬜ vẫn chưa có API | Lời giải đúng là Elasticsearch, không phải nới `?search=` |

### 🪤 Bốn cái bẫy MỚI của phiên 2026-08-04

1. 🔴 **`min-width:auto` của grid/flex item** — cắn **ba lần trong một phiên**: chữ tràn khỏi
   dialog chi tiết Task, board lệch 8px, thống kê lệch 104px ở 375px.

   **`break-words` KHÔNG sửa được nó.** `overflow-wrap: break-word` cho phép ngắt để *khỏi
   tràn* nhưng **không làm giảm min-content**, nên track vẫn phồng và đoạn chữ vẫn không có
   bề rộng hữu hạn nào để ngắt theo. Sửa gốc: `min-w-0`, cộng
   `grid-cols-[minmax(0,1fr)]` khi con lại là grid item.

   **Cách chẩn đoán, nên dùng lại:** duyệt ngược chuỗi tổ tiên và so
   `getBoundingClientRect().width` của từng cấp với cha của nó — phần tử đầu tiên rộng hơn
   cha chính là chỗ rò rỉ. Nhanh hơn nhiều so với đoán từ class.

2. 🔴 **HAI tầng quyền, HAI file, đừng nhầm** (ADR-045):
   - `lib/auth/system-permissions.ts` — tầng 1, đọc `EmployeeDto.permissions`
   - `lib/tasks/permissions.ts` — tầng 2, đọc `RoleInProject`, **không đổi gì trong phiên này**

   Frontend **KHÔNG giải mã JWT**. `hasPermission()` đọc `undefined` thành "không quyền nào"
   (fail-closed) — cần thiết vì tab đang mở lúc deploy giữ `employee` cũ không có trường đó.

   Gác UI bằng **quyền**, không bằng `systemRole === 'SystemAdmin'`: vai trò nay chỉ là định
   danh, quyền mới là thứ quyết định và nó đổi được bằng dữ liệu.

3. 🔴 **shadcn v4 (Base UI) KHÔNG có `asChild`.** Nút-là-liên-kết thì gắn thẳng
   `buttonVariants()` lên `<Link>`. Dropdown/trigger thì dùng prop `render={<Button …/>}`.

4. **Rãnh nền thanh mức phải TRUNG TÍNH** (`--muted`), không phải một bậc của thang màu biểu
   đồ. Đã thử `--viz-seq-1` và ở chế độ tối nó đủ bão hòa để một sprint **"0/2" hiện ra thanh
   đầy chiều ngang** — đọc thành đã xong 100%. Chỉ phần đã đầy mới được mang màu.

### 📌 Ba đính chính với những gì §0 cũ viết

- **`?search=` KHÔNG phải "chỉ Employee + Notification"** — 5/6 repository vẫn luôn lọc thật;
  chỉ `ActivityLogRepository` là không, và nay đã sửa (ADR-046). Nên **ô tìm ở màn nhật ký hệ
  thống là hợp lệ** và đã dựng. Điều còn đúng: `?search=` chỉ lọc **một trường** mỗi endpoint
  (Task chỉ theo `Name`, không theo mã `PMS-12`), nên nó không thay được search toàn cục.
- **Tầng dữ liệu `AdminEmployees` nay đã có đủ** — `types/admin.ts`,
  `lib/api/endpoints/admin.ts`, `lib/hooks/use-admin.ts`. Đã kiểm chứng trên trình duyệt rằng
  `search` ở endpoint đó **thật sự chạy**.
- **Hợp đồng `AdminEmployeesController` ghi ở §0 cũ vẫn đúng** và đã dùng nguyên vẹn; bốn mã
  lỗi (409 admin cuối cùng · 409 trạng thái khóa · 400 tự thao tác lên mình · 404) đều có
  thông điệp riêng ở UI.

### Đã hết hiệu lực — đừng làm lại

- ~~Dashboard chưa có màn~~ → `app/(app)/projects/[id]/statistics/page.tsx`, tab thứ 5
- ~~Quên/đặt lại mật khẩu chưa có màn~~ → `app/(auth)/forgot-password/`, `reset-password/`
- ~~Nhóm Admin chưa có màn~~ → `app/(app)/admin/` với **bốn** tab
- ~~`AdminEmployees` chưa có tầng dữ liệu~~ → đã có
- ~~Sidebar còn mục "Sắp có"~~ → đã gỡ hẳn; `href` nay bắt buộc trong `NavItem`
- ~~`recharts` chưa cài~~ → đã cài, Recharts 3

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

> 🔴 **BA MỤC ĐẦU CỦA KHỐI NÀY ĐÃ HẾT HIỆU LỰC TỪ 2026-08-05 (ADR-052).** Giữ lại gạch ngang
> vì chúng từng là kiến thức trả giá đắt, và vì ai đọc code cũ sẽ gặp lại dấu vết của chúng.
> **Đừng cài theo.**

- ~~Thả thẻ về **đúng cột nó đang đứng** → **409**~~ → nay trả **200** (no-op). Vẫn nên chặn
  ở client để khỏi bắn request thừa, nhưng lọt qua thì cũng không hỏng.
- ~~**Nhảy bước** `ToDo → Done` → **409**~~ → nay **200**. Không còn "bước" nào để nhảy.
- ~~**Đúng sáu bước hợp lệ** (`ToDo→InProgress`, `Done→Review`…)~~ → **ma trận đã GỠ**.
  `ALLOWED_TRANSITIONS` và `canTransition` **không còn tồn tại** trong
  `lib/tasks/status-transitions.ts`; file đó nay chỉ còn `mayFailUnpredictably`.

  **Vì sao gỡ:** cột do NGƯỜI DÙNG tạo thì không còn cơ sở nào nói cặp nào hợp lệ — hệ thống
  không biết "Chờ QA" đứng trước hay sau "Đang sửa". Dựng lại một bảng luật ở client "cho
  chắc" sẽ chặn đúng những nước đi backend cho phép, và người dùng không có cách nào biết
  vì sao.

  ⚠️ Kèm theo, ADR-021 mất một chốt: state machine từng **thay `RowVersion`** làm chốt chặn
  concurrency cho đổi trạng thái. Nay đổi trạng thái là **idempotent** nên không còn gì để
  tranh chấp — đó là đánh đổi có ghi nhận, không phải bỏ sót.

- Task đang bị `Blocks` chặn → **409**, nhưng chỉ khi **NHÓM của cột đích là `InProgress`**.
  ⚠️ Điều kiện đổi từ "đích là `InProgress`" sang **`category`** (ADR-052): nhờ vậy một cột
  người dùng tự đặt tên "Chờ QA" thuộc nhóm InProgress cũng được bảo vệ. So theo tên sẽ
  trượt ngay lần đầu ai đó đổi cấu hình board. Dùng `mayFailUnpredictably(target.category)`.
- `PATCH /tasks/{id}/status` và `PUT /tasks/{id}/sprint` **KHÔNG** cần `RowVersion` (ADR-021); `PUT /tasks/{id}` thì **bắt buộc**.
  ⚠️ Thân request nay là `{ targetColumnId }`, **không phải** `{ target }`.
- Board luôn trả **đủ MỌI cột của project** kể cả cột rỗng, đã sắp theo `order`.
  ⚠️ Số cột **không còn cố định 4** — đừng viết code dựa trên độ dài mảng.
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
