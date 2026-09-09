# PMS — Project Management System

Hệ thống quản lý dự án và task cho một phòng ban (mini-Jira): tạo dự án, phân quyền
thành viên hai tầng, quản lý sprint/backlog, board Kanban tùy biến cột, task/subtask,
comment, đính kèm file, nhãn, liên kết task, phê duyệt chuyển trạng thái, cổng tiếp
nhận yêu cầu, thông báo và nhật ký hoạt động.

> Đồ án tốt nghiệp / báo cáo thực tập. Xem [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)
> để biết định hướng sản phẩm, tiến độ từng module và toàn bộ quyết định kiến trúc (ADR).

## Kiến trúc & công nghệ

**Backend** — `backend/` — .NET 8, Clean Architecture 4 lớp:

```
PMS.Domain          Entity, Enum — không phụ thuộc lớp nào khác
PMS.Application     Service, DTO, FluentValidation, Mapperly
PMS.Infrastructure  EF Core (SQL Server), JWT, BCrypt, gửi email
PMS.API             Controller, middleware, Serilog, Swagger
```

**Frontend** — `frontend/` — Next.js 15 (App Router), TailwindCSS 4, shadcn/ui,
TanStack Query 5, Zustand 5. Chi tiết: [`frontend/README.md`](frontend/README.md).

**Hạ tầng chạy** — SQL Server (Docker), JWT access + refresh token (cookie
`SameSite=Strict`), Serilog, health check, CI trên GitHub Actions.

## Chạy nhanh bằng Docker (API + SQL Server)

```bash
cp .env.example .env      # điền MSSQL_SA_PASSWORD, JWT_SECRET, FRONTEND_BASE_URL
docker compose up -d
curl http://localhost:8080/health   # -> {"status":"Healthy",...}
```

> Trên Mac Apple Silicon: bật **Rosetta** trong Docker Desktop (Settings → General) —
> SQL Server chưa có ảnh arm64. Không publish cổng 1433 ra host, xem chú thích trong
> [`docker-compose.yml`](docker-compose.yml) nếu cần soi DB bằng tay.

## Chạy local để phát triển

Yêu cầu: .NET 8 SDK, Node.js 20+, SQL Server (local hoặc Docker).

```bash
# Backend — Development: có Swagger, seed data, email chỉ ghi ra log
cd backend && dotnet run --project src/PMS.API
```

```bash
# Frontend — bắt buộc chạy HTTPS (refresh token dùng cookie SameSite=Strict)
cd frontend
cp .env.example .env.local
npm install
npm run dev        # https://localhost:4176
```

Lần đầu chạy trên máy mới cần trust chứng chỉ dev cục bộ — hai lệnh bắt buộc, xem
[`frontend/README.md`](frontend/README.md#chạy-lần-đầu):

```bash
dotnet dev-certs https --trust
mkcert -install
```

Xem đầy đủ quy trình deploy demo (Cloudflare Tunnel + Vercel), cấu hình SMTP thật và
cách kiểm chứng sau deploy tại [`docs/RUNBOOK.md`](docs/RUNBOOK.md).

## Test

```bash
cd backend  && dotnet test                                    # unit + integration
cd frontend && npm run typecheck && npm run lint && npm test  # type-check + lint + unit
```

## Tài liệu

| File | Nội dung |
|---|---|
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Định hướng sản phẩm, tiến độ module, mô hình domain, toàn bộ ADR |
| [`docs/RUNBOOK.md`](docs/RUNBOOK.md) | Lệnh chạy, deploy, cấu hình SMTP, kiểm chứng sau deploy |
| [`docs/frontend-next-session.md`](docs/frontend-next-session.md) | Nhật ký & việc tiếp theo phía frontend |
| [`docs/uml/`](docs/uml) | Sequence diagram các luồng chính |
| [`frontend/README.md`](frontend/README.md) | Hướng dẫn chạy, quy ước, bẫy thường gặp phía frontend |
| [`backend/postman`](backend/postman) | Bộ collection Postman gọi thử API |

## Cấu trúc thư mục

```
backend/    API .NET 8 (Domain / Application / Infrastructure / API) + test
frontend/   Ứng dụng Next.js
docs/       Tài liệu kiến trúc, runbook, sơ đồ UML
scripts/    Script tiện ích (setup solution, v.v.)
docker-compose.yml   Dựng API + SQL Server bằng một lệnh
```
