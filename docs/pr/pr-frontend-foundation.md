# Mô tả PR — dán tay vào GitHub

> `gh` CLI chưa cài trên máy nên PR phải mở tay.
> **Base:** `dev` ← **Compare:** `module/frontend-foundation`
> **Tiêu đề:** `Frontend: nền tảng + luồng Auth + danh sách Project (ADR-027 → ADR-031)`

---

## Tóm tắt

Thư mục `frontend/` lần đầu tồn tại trong repo. Phiên này dựng phần nền mà mọi màn hình
sau đều đi qua — scaffold, tầng API client, luồng Auth end-to-end, danh sách Project — và
đổi cơ chế lưu token ở backend cho an toàn trước khi có màn hình nào phụ thuộc vào nó.

Board/Backlog Kanban để phiên sau (kéo-thả là phần nặng riêng).

## Thay đổi backend — vì sao PR frontend lại đụng vào backend

Câu hỏi "lưu JWT ở đâu" phải trả lời **trước** khi có màn hình, vì đổi sau là sửa lại mọi
chỗ gọi API. Chọn cookie httpOnly thay vì `localStorage`, và điều đó bắt buộc phải sửa
`AuthController` + CORS.

**Lý do (ADR-027):** hai token không cùng giá trị với kẻ tấn công. Access token sống 15
phút và chết theo tab. Refresh token sống **7 ngày và xoay vòng vô hạn** — ai đọc được nó
thì cứ mỗi lần sắp hết hạn lại đổi lấy cái mới, truy cập không bao giờ mất kể cả khi nạn
nhân đổi mật khẩu. Nên chi phí bảo vệ dồn vào đúng token đắt.

| | Lưu ở đâu | JS đọc được? |
|---|---|---|
| Refresh token (7 ngày) | Cookie `HttpOnly; Secure; SameSite=Strict; Path=/api/v1/auth` | Không |
| Access token (15 phút) | Zustand, không persist | Có |

**CSRF không bị đánh đổi lấy XSS.** Cookie chỉ tới 4 endpoint auth nhờ `Path`; mọi endpoint
nghiệp vụ vẫn dùng header `Authorization` nên miễn nhiễm CSRF. Riêng `/auth/refresh` được
`SameSite=Strict` che.

Phạm vi thật: 1 controller, 1 DTO mới, 1 dòng `.AllowCredentials()`, 1 origin thêm vào
config, 1 test phải sửa. **Không** đụng `AuthService`, **không** migration, **không** đụng
185 call site register/login trong test — vì cookie là mối quan tâm *transport* nên xử lý
ở tầng API, đúng tinh thần ADR-006.

## Năm ADR mới

| ADR | Quyết định |
|---|---|
| **027** | Refresh token qua cookie httpOnly, access token trong bộ nhớ |
| **028** | Giữ App Router (routing/layout), guard ở client chứ **không** ở `middleware.ts` |
| **029** | TypeScript types viết tay, không dùng OpenAPI codegen |
| **030** | Interceptor refresh phải **single-flight** |
| **031** | Next.js 15, không dùng 16 |

## Ba cái bẫy đã xử lý (đáng đọc nhất trong PR này)

**1. `Path` của cookie phân biệt HOA THƯỜNG, route ASP.NET thì không.**
`[Route("api/v1/[controller]")]` sinh ra `/api/v1/Auth` (chữ A hoa). Client gọi
`/api/v1/auth/refresh` vẫn trúng route nhưng trình duyệt so `Path` thấy khác nên **không
đính cookie** → 401 không rõ nguyên nhân. Đã đổi thành `[Route("api/v1/auth")]` tường minh.
Cùng lớp lỗi "cấu hình đúng hình thức nhưng im lặng không làm gì" với đính chính CORS ở §15.

**2. `middleware.ts` KHÔNG đọc được cookie refresh.** Cookie có `Path=/api/v1/auth` nên
request tới `/projects` không mang nó theo — middleware luôn thấy "chưa đăng nhập" và sẽ
đá cả người đã đăng nhập về `/login`. Nới `Path` ra `/` thì đánh mất chính điều đang bảo
vệ. Guard đặt ở client, lý do ghi ngay trong `auth-guard.tsx` để phiên sau không ai "sửa".

**3. 🔴 Single-flight refresh.** `AuthService.RefreshAsync` có reuse detection: dùng lại
token đã thu hồi bị coi là token bị đánh cắp → `RevokeAllAsync` thu hồi **toàn bộ** phiên.
Ba request cùng gọi `/refresh` là người dùng bị đá khỏi mọi thiết bị. Triệu chứng ngoài
đời: *"thỉnh thoảng tự đăng xuất"* — gần như không tái hiện được theo yêu cầu.

Đã **kiểm chứng thật** chứ không suy luận: gọi trực tiếp vào backend đang chạy, gửi lại
một refresh token đã xoay vòng → 401, và ngay sau đó **phiên hợp lệ cũng trả 401**.

## Kiểm chứng

**Backend** — clean build 0 warning, **321 test pass** (189 unit + 132 integration, +6):
- `AuthCookieTests` (5 fact mới) đọc **thẳng chuỗi `Set-Cookie`** chứ không qua
  `CookieContainer` — vì `CookieContainer` nuốt mất thuộc tính và không hề enforce
  `SameSite`, nên test đi qua nó vẫn xanh dù ai đó tháo mất `HttpOnly`. Có một fact riêng
  khẳng định thân phản hồi **không** chứa `refreshToken` — đó mới là chốt chặn thật.
- `CorsPolicyTests` +1 fact cho `Access-Control-Allow-Credentials`.

**Frontend** — `tsc --noEmit` sạch, `eslint` sạch, `npm run build` sinh đủ 8 trang.

**Gọi thật vào backend đang chạy** (`https://localhost:7264`), đã xác nhận:

| Kiểm tra | Kết quả |
|---|---|
| Cookie đủ 4 thuộc tính | `path=/api/v1/auth; secure; samesite=strict; httponly` ✅ |
| Thân phản hồi không có `refreshToken` | ✅ |
| `/auth/refresh` bằng cookie, không body | 200, access token đổi, **cookie xoay vòng** ✅ |
| Gửi lại token cũ | 401 — reuse detection ✅ |
| Sau reuse detection, phiên hợp lệ | **cũng 401** — chính là bug mà ADR-030 chặn ✅ |
| `/auth/refresh` không kèm cookie | 401 ✅ |
| Logout | 204, cookie bị xóa, refresh sau đó 401 ✅ |
| CORS preflight từ `https://localhost:3000` | `Allow-Origin` + `Allow-Credentials: true` ✅ |
| Tạo project → danh sách | `PagedResult` đúng shape, `status` là **chuỗi** `"ToDo"` ✅ |
| Ngày trong quá khứ | 400 `ValidationProblemDetails`, key **PascalCase** ✅ |
| 404 project không tồn tại | `ProblemDetails`, tiếng Việt ở `title` ✅ |
| 401 không kèm token | body **rỗng**, không content-type ✅ |

Hai dòng cuối xác nhận `lib/api/problem.ts` phải làm đúng hai việc dễ bỏ qua: chuẩn hóa
key PascalCase → camelCase (nếu không `setError('Name')` trỏ vào field không tồn tại và
lỗi biến mất không dấu vết), và kiểm tra content-type trước khi parse (gọi `res.json()`
trên body rỗng sẽ ném `SyntaxError` và nuốt mất mã lỗi thật).

## Còn nợ, đã ghi vào tài liệu

- **`seq-12-refresh-token` mới chỉ có `.mmd`**, chưa có `.drawio`/`.png` — máy làm phiên
  này là Windows và không cài draw.io Desktop. Nguồn Mermaid là thứ review được; chạy hai
  lệnh ở `docs/uml/README.md` trên máy có draw.io là ra đủ. README nay đã có cả nhánh
  Windows (trước chỉ có đường dẫn macOS).
- **Frontend chưa có hạ tầng test** (Vitest/Playwright) — khoảng trống **có ý thức**, cần
  chốt riêng vì nó thêm một bộ công cụ và một vòng CI. Hiện `tsc` + `eslint` + `npm run
  build` đang giữ chỗ.
- Sửa/xóa project (cần round-trip `RowVersion`), Board Kanban, chi tiết Task, Notification
  bell, Dashboard.

## ⚠️ Người review cần chạy trước khi thử

```powershell
dotnet dev-certs https --trust
```

```powershell
mkcert -install
```

Cookie `SameSite=Strict` + luật *schemeful same-site* nghĩa là `http://localhost` và
`https://localhost` là hai site khác nhau — chạy frontend trên http thì cookie không bao
giờ được gửi và luồng refresh hỏng im lặng. Chi tiết ở `frontend/README.md`.
