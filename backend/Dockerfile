# syntax=docker/dockerfile:1
#
# Ảnh production cho PMS.API. Build từ thư mục `backend/`:
#     docker build -t pms-api backend/
#
# Multi-stage: SDK (~800 MB) chỉ tồn tại ở tầng build, ảnh cuối chạy trên runtime
# ASP.NET (~220 MB). Gộp một tầng cho gọn sẽ đưa cả trình biên dịch và mã nguồn lên
# server — vừa nặng vừa là bề mặt tấn công không có lý do tồn tại.

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy TRƯỚC mọi file .csproj rồi restore, sau đó mới copy phần còn lại.
# Docker cache theo tầng: tách như vậy thì sửa một dòng C# không làm mất cache của
# `dotnet restore` (bước chậm nhất, phải ra mạng). Copy tất cả rồi restore là ném
# bỏ cache đó ở mỗi lần build.
COPY Directory.Build.props ./
COPY PMS.sln ./
COPY src/PMS.Domain/PMS.Domain.csproj                 src/PMS.Domain/
COPY src/PMS.Application/PMS.Application.csproj       src/PMS.Application/
COPY src/PMS.Infrastructure/PMS.Infrastructure.csproj src/PMS.Infrastructure/
COPY src/PMS.API/PMS.API.csproj                       src/PMS.API/
COPY tests/PMS.UnitTests/PMS.UnitTests.csproj                 tests/PMS.UnitTests/
COPY tests/PMS.IntegrationTests/PMS.IntegrationTests.csproj   tests/PMS.IntegrationTests/

RUN dotnet restore src/PMS.API/PMS.API.csproj

COPY . .
RUN dotnet publish src/PMS.API/PMS.API.csproj -c Release -o /app/publish --no-restore

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Chạy bằng user không phải root. Ảnh .NET 8 có sẵn user `app` (uid 1654).
# Quan trọng hơn bình thường ở đây vì container này GHI vào đĩa: file đính kèm đi qua
# LocalFileStorage (ADR-035), nên một lỗ hổng upload dưới quyền root sẽ chạm được cả
# filesystem của container.
USER app

# App_Data là nơi LocalFileStorage ghi file đính kèm. Khai VOLUME để dữ liệu không
# biến mất cùng container — không có dòng này thì mỗi lần deploy là mất sạch file
# người dùng đã tải lên, và sẽ không có lỗi nào báo cho biết.
VOLUME ["/app/App_Data"]

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "PMS.API.dll"]
