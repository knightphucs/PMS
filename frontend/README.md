# PMS Frontend

Next.js 15 (App Router) + TailwindCSS 4 + shadcn/ui + TanStack Query 5 + Zustand 5.

Quyết định kiến trúc ở `docs/ARCHITECTURE.md` §6 và **ADR-027 → ADR-031** (§15).

## Chạy lần đầu

### 1. Hai lệnh ghi vào kho chứng chỉ tin cậy — phải tự chạy tay

Cả hai đều mở hộp thoại xác nhận của Windows nên **không tự động hóa được**:

```powershell
dotnet dev-certs https --trust
```

```powershell
mkcert -install
```

**Vì sao bắt buộc, không phải tùy chọn.** Refresh token đi trong cookie `SameSite=Strict`
(ADR-027). Theo luật *schemeful same-site*, trình duyệt tính "site" gồm cả scheme — nên
`http://localhost` và `https://localhost` là **hai site khác nhau**. Chạy frontend trên
http thì cookie không bao giờ được gửi tới `https://localhost:7264`, và toàn bộ luồng
refresh hỏng **im lặng**: bạn nhận 401 mà không có gì chỉ ra nguyên nhân.

- `dotnet dev-certs https --trust` → trình duyệt tin backend ở `https://localhost:7264`.
  Thiếu nó thì **mọi** lời gọi API thất bại ở tầng TLS trước khi chạm tới code.
- `mkcert -install` → cài CA cục bộ để `next dev --experimental-https` sinh được cert cho
  `https://localhost:3000`. Thiếu nó thì `npm run dev` **treo** ở bước
  "Attempting to generate self signed certificate".

Kiểm tra đã xong:

```powershell
dotnet dev-certs https --check --trust
```

### 2. Cấu hình và cài đặt

```powershell
Copy-Item .env.example .env.local
```

```powershell
npm install
```

### 3. Chạy

Backend trước (từ `backend/src/PMS.API`):

```powershell
dotnet run --launch-profile https
```

Rồi frontend:

```powershell
npm run dev
```

- Frontend: <https://localhost:3000>
- Backend + Swagger: <https://localhost:7264/swagger>

## Lệnh

| Lệnh | Việc |
|---|---|
| `npm run dev` | Dev server **có HTTPS** (`--experimental-https`) |
| `npm run build` | Production build — **chạy trước khi commit** |
| `npm run lint` | ESLint |
| `npx tsc --noEmit` | Kiểm tra kiểu |

⚠️ `npm run build` bắt được lỗi mà `npm run dev` bỏ qua im lặng — cụ thể là lỗi prerender.
Nó đã bắt được thật một lỗi `useSearchParams` thiếu `Suspense` ở trang login ngay trong
phiên đầu tiên. Đừng chỉ dựa vào `dev`.

## Bản đồ thư mục

```
app/            App Router. (auth) và (app) là route group — tên trong ngoặc KHÔNG vào URL
components/     ui/ là shadcn (Base UI ở v4); còn lại chia theo tính năng
lib/api/        ⭐ tầng API client — mọi màn hình đi qua đây
lib/hooks/      hook TanStack Query
store/          Zustand — auth-store.ts KHÔNG persist
types/          soi gương DTO backend 1-1 (types/auth.ts <-> AuthDtos.cs)
```

## Ba điều dễ làm sai

**1. Đừng thêm `middleware.ts` để chặn route.** Cookie có `Path=/api/v1/auth` nên
middleware không đọc được và sẽ đá cả người đã đăng nhập về `/login`. Route guard nằm ở
`components/auth/auth-guard.tsx`. Lý do đầy đủ: ADR-028.

**2. Đừng gọi `performRefresh()` trực tiếp.** Chỉ dùng `refreshAccessToken()` trong
`lib/api/refresh.ts`. Backend có reuse detection: hai lời gọi `/refresh` song song với
cùng một token bị coi là token bị đánh cắp và **thu hồi toàn bộ phiên**. Biến `inFlight`
là chốt chặn duy nhất. Lý do đầy đủ: ADR-030.

**3. Đừng hiển thị "không có quyền" cho lỗi 404.** Người ngoài project nhận 404 một cách
**cố ý** để không lộ sự tồn tại của project (ADR-006/019). `lib/api/problem.ts` đã cứng
hóa thông điệp này — đừng ghi đè ở màn hình.

## Khuôn mẫu khi làm màn hình mới

Copy `app/(app)/projects/page.tsx` + `lib/hooks/use-projects.ts` +
`components/projects/`. Bộ này đã có đủ bốn trạng thái (skeleton / empty / error / data),
phân trang theo `PagedResult`, và dialog validate hai phía.

## `npm audit`

`npm audit` báo 9 CVE high; **`npm audit --omit=dev` sạch** — không CVE nào đi vào bundle
gửi tới trình duyệt. Cả 9 đều từ một gốc `brace-expansion` kéo qua `minimatch` tới các
`eslint-plugin`, và hiện **chưa có bản vá thượng nguồn** (dải bị ảnh hưởng của eslint là
`4.1.0 - 10.0.0-rc.2`). Lý do không override được đã ghi trong `package.json` — đừng chạy
`npm audit fix --force`, nó sẽ hạ `next` xuống 9.3.3.
