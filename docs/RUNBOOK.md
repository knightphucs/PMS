# RUNBOOK — chạy, deploy, kiểm chứng

> Soạn 2026-08-12. Mọi lệnh chạy từ **thư mục gốc repo**.
> Quyết định thiết kế đằng sau các lệnh này nằm ở `ARCHITECTURE.md` **ADR-058**.

---

## 0. Bẫy sẽ cắn bạn đầu tiên

🔴 **User-secrets của .NET CHỈ nạp khi `ASPNETCORE_ENVIRONMENT=Development`.**

Connection string và `Jwt:Secret` đang nằm trong `dotnet user-secrets`. Vừa chuyển sang
Production là cả hai **biến mất** và app chết lúc khởi động với
`Thiếu ConnectionStrings:DefaultConnection`.

🔴 **`dotnet run` đọc `launchSettings.json`, và cả ba profile ở đó đều ép
`ASPNETCORE_ENVIRONMENT=Development`** — kể cả khi shell đã `export` Production. Phải có
`--no-launch-profile` mới thoát ra được.

`backend/run-production.sh` xử lý cả hai. Đừng chạy `dotnet run` tay cho bản deploy.

---

## 1. Chạy local (phát triển)

```bash
# Backend — Development: có Swagger, có seed data, email chỉ ghi ra log
cd backend && dotnet run --project src/PMS.API

# Frontend
cd frontend && npm run dev        # https://localhost:4176
```

⚠️ Frontend **phải** chạy https: cookie refresh có `SameSite=Strict`, mà theo luật
*schemeful same-site* thì `http://localhost` và `https://localhost` là **hai site khác
nhau** — trỏ vào http thì luồng refresh hỏng im lặng. Script `dev` đã có
`--experimental-https`.

---

## 2. Deploy demo (Cloudflare Tunnel + Vercel)

### 2.1 Chuẩn bị một lần

```bash
# Lấy lại giá trị đang có trong user-secrets
dotnet user-secrets list --project backend/src/PMS.API

# Tạo file cấu hình Production (đã nằm trong .gitignore)
cp backend/.env.prod.example backend/.env.prod
```

Điền vào `backend/.env.prod`:

| Biến | Giá trị |
|---|---|
| `ConnectionStrings__DefaultConnection` | y hệt user-secrets |
| `Jwt__Secret` | y hệt user-secrets |
| `App__FrontendBaseUrl` | `https://pms-six-gamma.vercel.app` |
| `Cors__AllowedOrigins__0` | cùng giá trị trên |
| `Database__MigrateOnStartup` | `true` |
| `Smtp__*` | xem §3 |

### 2.2 Chạy — hai terminal

```bash
# Terminal 1 — API ở chế độ Production (KHÔNG Swagger, KHÔNG seed, KHÔNG log token)
./backend/run-production.sh

# Terminal 2 — mở tunnel, IN THẲNG domain + hai biến cần dán lên Vercel
./backend/run-tunnel.sh
```

### 2.3 Cập nhật Vercel — **mỗi lần cloudflared khởi động lại**

Cloudflare Quick Tunnel cấp domain `*.trycloudflare.com` **mới** mỗi lần chạy. Trên Vercel →
*Project Settings → Environment Variables*:

```
BACKEND_ORIGIN=https://<domain-tunnel-hiện-tại>.trycloudflare.com   # KHÔNG có /api
NEXT_PUBLIC_API_BASE_URL=/api/v1                                    # giữ nguyên
```

rồi bấm **Redeploy**.

🔴 `BACKEND_ORIGIN` được `next.config.ts` đọc lúc `next build` và **đóng băng vào bản build**
— đổi biến mà không redeploy thì không có tác dụng gì.

> Muốn hết vòng lặp này: cần **named tunnel** gắn domain cố định (phải có một domain trỏ
> nameserver về Cloudflare). Nó cũng làm bản demo trông chuyên nghiệp hơn hẳn
> `abc-def-ghi.trycloudflare.com`.

---

## 3. Email thật (SMTP)

Bỏ trống `Smtp__Host` = hệ thống dùng `NullEmailSender`: **quên mật khẩu và mời-qua-email
vẫn trả về THÀNH CÔNG nhưng không có thư nào tới**. Đó là đúng thiết kế (ADR-041 cấm để lộ
email nào tồn tại), nên **sẽ không có lỗi nào báo cho bạn biết**.

**Gmail App Password** — nhanh nhất, gửi được tới người nhận bất kỳ, ~500 thư/ngày:

1. Bật **2-Step Verification** cho tài khoản Google (bắt buộc)
2. https://myaccount.google.com/apppasswords → tạo → nhận chuỗi **16 ký tự**
3. Dán vào `Smtp__Password`, **bỏ hết dấu cách**

```
Smtp__Host=smtp.gmail.com
Smtp__Port=587
Smtp__EnableSsl=true
Smtp__User=<địa chỉ gmail>
Smtp__Password=<app password 16 ký tự>
Smtp__FromAddress=<cùng địa chỉ gmail>
```

Lựa chọn khác: **Brevo** (miễn phí 300 thư/ngày, `smtp-relay.brevo.com:587`, không cần
domain riêng) · **Mailtrap** (chỉ *bắt* thư, **không gửi đi thật** — tốt để kiểm luồng, vô
dụng cho demo).

⚠️ Development/Testing **luôn** ép `SerilogEmailSender` bất kể `Smtp__*` — máy dev dễ thừa
hưởng biến của production, và một lượt chạy test bắn email thật là không hoàn tác được.

---

## 4. Kiểm chứng sau khi deploy

```bash
curl -k https://localhost:7264/health                                    # {"status":"Healthy"}
curl -k -o /dev/null -w "%{http_code}\n" https://localhost:7264/swagger  # 401
```

`401` là **đúng** — `SetFallbackPolicy(RequireAuthenticatedUser)` áp cho cả request không
khớp endpoint nào. Nếu ra **200** thì bạn vẫn đang chạy Development.

Rồi trên chính bản Vercel: **Quên mật khẩu** bằng email thật → phải **nhận được thư** → link
mở đúng domain Vercel (không phải `localhost:4176`) → đổi mật khẩu thành công. Đây là phép
kiểm chứng minh cả ba thứ ADR-058 sửa đều thật.

---

## 5. Test

```bash
cd backend  && dotnet test                       # 614 test (249 unit + 365 integration)
cd frontend && npm run typecheck && npm run lint && npm test    # 72 test
```

⚠️ Integration test cần **SQL Server thật**. Bộ test phụ thuộc `rowversion`, trigger, view,
stored procedure (ADR-055) — provider khác sẽ xanh vì lý do sai.

🔴 **Mặc định là `localhost,1433` + user `sa`, và máy dev hiện tại KHÔNG phải vậy** (nó chạy
một named instance với user riêng). Triệu chứng nếu quên: **mọi test đỏ cùng lúc trong vài
chục mili-giây** — trông hệt như lỗi cascade của ADR-059, nhưng thông điệp thật là
`Login failed for user 'sa'`. *Đọc thông điệp, đừng đoán theo tiền lệ.*

Lối thoát là biến `PMS_TEST_DB`. Dựng nó từ chính connection string dev, chỉ đổi tên
database để **không đụng DB `PMS` thật**:

```bash
cd backend
DEV_CS=$(dotnet user-secrets list --project src/PMS.API \
  | grep '^ConnectionStrings:DefaultConnection' | sed 's/^.* = //')
export PMS_TEST_DB=$(echo "$DEV_CS" | sed 's/Database=PMS;/Database=PmsTestDb;/')
dotnet test
```

⚠️ Test chạy `EnsureDeleted` + `Migrate` mỗi lượt, nên `PmsTestDb` bị **xoá và dựng lại**
từ đầu — đừng bao giờ trỏ biến này vào một database có dữ liệu cần giữ.

### Kiểm lệch model snapshot

```bash
cd backend
dotnet ef migrations add __DriftCheck --project src/PMS.Infrastructure \
  --startup-project src/PMS.API --output-dir Persistence/Migrations
# -> Up() phải RỖNG. Không rỗng = snapshot lệch so với entity/configuration.
dotnet ef migrations remove --project src/PMS.Infrastructure --startup-project src/PMS.API
```

CI chạy đúng phép kiểm này ở job `backend`.

---

## 6. Docker

```bash
docker build -t pms-api backend/

# Chạy API trong container, trỏ vào SQL Server có sẵn trên host (OrbStack)
docker run -d --name pms-api -p 8080:8080 \
  -e "ConnectionStrings__DefaultConnection=Server=host.docker.internal,1433;Database=PMS;User Id=sa;Password=<mật khẩu>;TrustServerCertificate=True" \
  -e "Jwt__Secret=<jwt secret>" \
  -e "App__FrontendBaseUrl=https://pms-six-gamma.vercel.app" \
  -e "Cors__AllowedOrigins__0=https://pms-six-gamma.vercel.app" \
  -e "Database__MigrateOnStartup=true" \
  pms-api

PMS_URL=http://localhost:8080 ./backend/run-tunnel.sh
```

Cả stack bằng một lệnh (**cần Rosetta** trong Docker Desktop — SQL Server không có ảnh
arm64, thiếu Rosetta thì container `db` exit 139):

```bash
cp .env.example .env      # rồi điền
docker compose up -d
curl http://localhost:8080/health
```

> 📌 **Docker hiện KHÔNG nằm trong luồng vận hành hằng ngày** — bản demo chạy bằng
> `run-production.sh`. Giá trị của nó là lúc **bàn giao cho phòng hạ tầng** (`docker compose
> up` thay cho nửa ngày dựng môi trường) và cho chương triển khai của báo cáo. CI có job
> build **và khởi động thật** cái ảnh đó mỗi lần push, nên nó không mục dần trong góc.

---

## 7. CI

`.github/workflows/ci.yml` — ba job:

| Job | Làm gì |
|---|---|
| `backend` | build (`TreatWarningsAsErrors`) + unit + integration trên SQL Server thật + kiểm lệch snapshot |
| `frontend` | typecheck + lint + test + `next build` |
| `docker` | build ảnh **và khởi động thật**: `/health` phải trả 200 hoặc 503, `id -u` phải khác 0 |

503 ở job `docker` là **hợp lệ và mong đợi**: không có DB, nên nó chứng minh pipeline đã
chạy tới health check — tức app sống. Container chết thì không có mã HTTP nào cả.
