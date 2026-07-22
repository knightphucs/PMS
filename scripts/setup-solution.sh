#!/usr/bin/env bash
# ============================================================
#  Setup - Project Management System (PMS) .NET solution
#  Chạy tại thư mục gốc repo, trên máy có .NET 8 SDK + internet
#  (script tự tải đúng version package mới nhất tương thích net8.0).
#  Kiểm tra trước:  dotnet --version   ->   phải là 8.0.x
# ============================================================
set -euo pipefail

# 0) (một lần cho máy) công cụ EF Core CLI để tạo migration ở bước code sau
dotnet tool install --global dotnet-ef 2>/dev/null || dotnet tool update --global dotnet-ef

# 1) Thư mục backend + solution
mkdir -p backend && cd backend
dotnet new sln -n PMS

# 2) 4 project layer + 2 project test (đều net8.0)
dotnet new classlib -n PMS.Domain          -f net8.0 -o src/PMS.Domain
dotnet new classlib -n PMS.Application      -f net8.0 -o src/PMS.Application
dotnet new classlib -n PMS.Infrastructure   -f net8.0 -o src/PMS.Infrastructure
dotnet new webapi   -n PMS.API              -f net8.0 -o src/PMS.API --use-controllers
dotnet new xunit    -n PMS.UnitTests        -f net8.0 -o tests/PMS.UnitTests
dotnet new xunit    -n PMS.IntegrationTests -f net8.0 -o tests/PMS.IntegrationTests

# 3) Đưa hết vào solution
dotnet sln add \
  src/PMS.Domain src/PMS.Application src/PMS.Infrastructure src/PMS.API \
  tests/PMS.UnitTests tests/PMS.IntegrationTests

# 4) Tham chiếu project (phụ thuộc hướng VÀO Domain)
dotnet add src/PMS.Application        reference src/PMS.Domain
dotnet add src/PMS.Infrastructure     reference src/PMS.Application
dotnet add src/PMS.API                reference src/PMS.Application src/PMS.Infrastructure
dotnet add tests/PMS.UnitTests        reference src/PMS.Application src/PMS.Domain
dotnet add tests/PMS.IntegrationTests reference src/PMS.API

# 5) PACKAGE - CORE (khớp tech stack đã chốt trong ARCHITECTURE.md)
# --- Application: validation (tách khỏi Controller) + mapping compile-time ---
dotnet add src/PMS.Application package FluentValidation      # free, Apache 2.0
dotnet add src/PMS.Application package Riok.Mapperly          # free MIT, source generator (map lúc compile)

# --- Infrastructure: EF Core + SQL Server + Design(migration) + hash mật khẩu ---
dotnet add src/PMS.Infrastructure package Microsoft.EntityFrameworkCore --version 8.0.29
dotnet add src/PMS.Infrastructure package Microsoft.EntityFrameworkCore.SqlServer --version 8.0.29
dotnet add src/PMS.Infrastructure package Microsoft.EntityFrameworkCore.Design --version 8.0.29
dotnet add src/PMS.Infrastructure package BCrypt.Net-Next

# --- API: JWT + Swagger + Serilog + API versioning + health check ---
dotnet add src/PMS.API package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.0.29
dotnet add src/PMS.API package Swashbuckle.AspNetCore
dotnet add src/PMS.API package Serilog.AspNetCore
dotnet add src/PMS.API package Serilog.Sinks.Console
dotnet add src/PMS.API package Serilog.Sinks.File
dotnet add src/PMS.API package Asp.Versioning.Mvc --version 8.1.1
dotnet add src/PMS.API package Asp.Versioning.Mvc.ApiExplorer --version 8.1.1
dotnet add src/PMS.API package Microsoft.EntityFrameworkCore.Design --version 8.0.29
dotnet add src/PMS.API package AspNetCore.HealthChecks.SqlServer

# --- UnitTests: Moq (xUnit + Test SDK + coverlet đã có sẵn từ template) ---
dotnet add tests/PMS.UnitTests package Moq

# --- IntegrationTests: host test cho API + DB in-memory ---
dotnet add tests/PMS.IntegrationTests package Microsoft.AspNetCore.Mvc.Testing --version 8.0.29
dotnet add tests/PMS.IntegrationTests package Microsoft.EntityFrameworkCore.InMemory --version 8.0.29

# 6) OPTIONAL (đọc kỹ license trước khi bật)
# dotnet add tests/PMS.UnitTests package Shouldly    # assert đọc như tiếng Anh, MIT free
#   -> KHÔNG khuyến nghị FluentAssertions v8+ (license thương mại Xceed)
#   -> KHÔNG khuyến nghị AutoMapper v15+ (license copyleft RPL-1.5) — đã thay bằng Mapperly

# 7) .gitignore chuẩn .NET (đặt ở gốc repo)
cd .. && dotnet new gitignore

# 8) Kiểm tra biên dịch sạch (chưa có code nghiệp vụ, chỉ build khung)
cd backend && dotnet build
echo ""
echo "==> Solution dựng xong. CHƯA có code nghiệp vụ (đúng ý)."
