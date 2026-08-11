#!/usr/bin/env bash
#
# Chạy API ở chế độ Production trên máy local, để cloudflared tunnel trỏ vào.
#
#     cp backend/.env.prod.example backend/.env.prod   # rồi điền
#     ./backend/run-production.sh
#
# Vì sao cần script này thay vì `dotnet run`:
#
#   1. `dotnet run` đọc launchSettings.json, và MỌI profile ở đó đều ép
#      ASPNETCORE_ENVIRONMENT=Development — kể cả khi shell đã export Production.
#      Phải có --no-launch-profile mới thoát được.
#   2. user-secrets CHỈ nạp ở Development. Sang Production thì ConnectionStrings và
#      Jwt:Secret biến mất -> app chết lúc khởi động. Nên phải nạp lại từ .env.prod.
#
# Hệ quả của Production (đúng như mong muốn): KHÔNG Swagger, KHÔNG trang lỗi chi tiết,
# KHÔNG SerilogEmailSender ghi token đặt lại mật khẩu ra file log.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="$SCRIPT_DIR/.env.prod"
URL="${PMS_URL:-https://localhost:7264}"

if [[ ! -f "$ENV_FILE" ]]; then
  echo "Thiếu $ENV_FILE" >&2
  echo "  cp backend/.env.prod.example backend/.env.prod   # rồi điền giá trị thật" >&2
  exit 1
fi

# set -a: mọi biến gán trong file được export sang tiến trình con.
set -a
# shellcheck disable=SC1090
source "$ENV_FILE"
set +a

export ASPNETCORE_ENVIRONMENT=Production
export ASPNETCORE_URLS="$URL"

for required in ConnectionStrings__DefaultConnection Jwt__Secret App__FrontendBaseUrl; do
  if [[ -z "${!required:-}" ]]; then
    echo "Thiếu biến bắt buộc: $required (trong $ENV_FILE)" >&2
    exit 1
  fi
done

if [[ -z "${Smtp__Host:-}" ]]; then
  echo "⚠️  Smtp__Host trống -> hệ thống sẽ KHÔNG gửi email nào."
  echo "   Quên mật khẩu và mời-qua-email vẫn trả về THÀNH CÔNG nhưng không có thư tới"
  echo "   (đúng thiết kế ADR-041), nên sẽ không có lỗi nào báo cho bạn biết."
fi

echo "→ Production trên $URL"
echo "→ Tunnel:  cloudflared tunnel --url $URL --no-tls-verify"
echo

exec dotnet run --project "$SCRIPT_DIR/src/PMS.API" --no-launch-profile
