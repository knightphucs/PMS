#!/usr/bin/env bash
#
# Mở Cloudflare Quick Tunnel tới API đang chạy, rồi IN THẲNG domain vừa được cấp kèm
# đúng hai biến cần dán lên Vercel.
#
#     Terminal 1:  ./backend/run-production.sh
#     Terminal 2:  ./backend/run-tunnel.sh
#
# Vì sao là script RIÊNG chứ không gộp vào run-production.sh: domain do Quick Tunnel cấp
# đổi mỗi lần cloudflared khởi động lại, còn API thì restart thoải mái không ảnh hưởng gì.
# Gộp chung nghĩa là mỗi lần sửa một dòng C# rồi chạy lại là phải đi cập nhật Vercel và
# redeploy — một việc mất vài phút, cho một thay đổi không liên quan.

set -euo pipefail

URL="${PMS_URL:-https://localhost:7264}"
LOG="$(mktemp -t pms-tunnel)"

cleanup() { rm -f "$LOG"; }
trap cleanup EXIT

command -v cloudflared >/dev/null 2>&1 || {
  echo "Không tìm thấy cloudflared. Cài: brew install cloudflared" >&2
  exit 1
}

# Cảnh báo sớm nếu API chưa chạy. Tunnel vẫn dựng được và vẫn cấp domain, nhưng mọi
# request qua nó sẽ trả 502 — và triệu chứng đó rất dễ bị đọc nhầm thành "tunnel hỏng".
if ! curl -sk --max-time 3 -o /dev/null "$URL/health"; then
  echo "⚠️  Chưa thấy API ở $URL — chạy ./backend/run-production.sh ở terminal khác trước."
  echo "   (Vẫn mở tunnel, nhưng mọi request sẽ là 502 cho tới khi API lên.)"
  echo
fi

cloudflared tunnel --url "$URL" --no-tls-verify > "$LOG" 2>&1 &
TUNNEL_PID=$!
trap 'kill $TUNNEL_PID 2>/dev/null; cleanup' EXIT INT TERM

echo "Đang chờ Cloudflare cấp domain…"

DOMAIN=""
for _ in $(seq 1 60); do
  # cloudflared in domain trong một khung ASCII; chỉ cần bắt đúng chuỗi hostname.
  DOMAIN=$(grep -oE 'https://[a-z0-9-]+\.trycloudflare\.com' "$LOG" | head -1 || true)
  [[ -n "$DOMAIN" ]] && break
  if ! kill -0 $TUNNEL_PID 2>/dev/null; then
    echo "cloudflared thoát sớm:" >&2
    cat "$LOG" >&2
    exit 1
  fi
  sleep 1
done

if [[ -z "$DOMAIN" ]]; then
  echo "Hết 60s vẫn chưa thấy domain. Output của cloudflared:" >&2
  cat "$LOG" >&2
  exit 1
fi

cat <<EOF

────────────────────────────────────────────────────────────────
  DOMAIN TUNNEL:  $DOMAIN

  Dán lên Vercel (Project Settings → Environment Variables) rồi REDEPLOY:

      BACKEND_ORIGIN=$DOMAIN
      NEXT_PUBLIC_API_BASE_URL=/api/v1

  ⚠️  BACKEND_ORIGIN được next.config.ts đọc lúc build và đóng băng vào bản
      build đó — đổi biến mà không Redeploy thì không có tác dụng gì.
────────────────────────────────────────────────────────────────

EOF

echo "Kiểm tra nhanh qua tunnel:"
CODE=$(curl -s -o /dev/null -w '%{http_code}' --max-time 10 "$DOMAIN/health" || echo "000")
echo "  HTTP $CODE  $DOMAIN/health"
case "$CODE" in
  200) echo "  ✅ API đã ra được Internet." ;;
  000) echo "  ⏳ Cloudflare thường cần thêm vài giây để domain propagate — thử lại lệnh curl trên sau ~10s." ;;
  502) echo "  ⚠️  Tunnel sống nhưng không tới được API. Kiểm tra ./backend/run-production.sh đang chạy ở $URL." ;;
  *)   echo "  ⚠️  Mã lạ — xem log của cloudflared." ;;
esac
echo
echo "Ctrl-C để đóng tunnel. Đóng rồi mở lại = domain MỚI, phải cập nhật Vercel lần nữa."
echo

wait $TUNNEL_PID
