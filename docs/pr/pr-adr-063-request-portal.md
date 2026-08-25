# Mô tả PR — dán tay vào GitHub

> `gh` CLI chưa cài trên máy nên PR phải mở tay.
> **Base:** `dev` ← **Compare:** `feat/adr-063-request-portal`
> **Tiêu đề:** `Cổng yêu cầu — người ngoài project gửi việc vào (ADR-063)`

---

## Tóm tắt

Hệ thống nay có **cả hai nửa của một service desk**: người ngoài phòng ban gửi được yêu cầu
vào (cổng tiếp nhận), và yêu cầu đó đi qua được luật duyệt của ADR-062.

Đây là hạng mục áp chót của Giai đoạn 2.5. Còn đúng một cái nữa: **ADR-064 — khuôn dự án**.

**673 test backend** (261 unit + 412 integration) + 72 frontend, 0 đỏ. Drift `Up()` rỗng.

## 🔴 Quyết định chặn của ADR-063 đã bị LẬT — kèm bằng chứng

Bản ADR soạn 2026-08-17 khuyến nghị đường **(a)**: thêm `RoleInProject.Requester` rồi lọc
theo hàng. Khảo sát code **trước khi gõ dòng đầu tiên** (đúng ràng buộc mục đó tự đặt ra)
tìm ra hai dữ kiện mà bản 08-17 chưa có:

**1. (a) đắt hơn con số đã ước lượng.** `ProjectPermissions.IsAllowed` hiện trả
`ProjectAction.View => true` cho **MỌI** vai trò. Thêm một vai trò mới là **mở** board ·
backlog · comment · attachment · activity · saved view cho người ngoài **ngay tại lần build
đầu tiên**, và chỉ đóng lại khi rà hết **35 lời gọi `View` trên 16 service** (tổng 70 lời
gọi `AuthorizeAsync`). Tệ nhất: nó **build sạch và test xanh** — không test nào hiện có kiểm
"Requester KHÔNG đọc được board", vì vai trò đó chưa từng tồn tại.

**2. (a) có một vòng luẩn quẩn bản cũ chưa nêu.** `Requester` là một hàng `ProjectMembers`
thật. Để **gửi** yêu cầu thì phải là thành viên; để là thành viên thì phải có người trong
project thêm vào — đúng cái rào mà "người ngoài gửi vào" sinh ra để phá.

**3. Cái giá của (b) thì dự án ĐÃ TRẢ RỒI.** Bản cũ gán cho (b) cái giá *"một đường authz
thứ hai chạy song song"*. Nhưng `TaskService.GetMyWorkAsync` (ADR-053) **không gọi `_authz`
một lần nào**, và XML doc của nó nói thẳng vì sao điều đó hợp lệ:

> *"Quyền nằm trong chính điều kiện truy vấn, không phải trong một lượt kiểm thêm."*

### → Chọn đường (b′): cổng HẸP

Nhóm route `/request-portal/*` riêng, authz **LÀ chính vị từ truy vấn** `ReporterId == me`.

| | Kết quả |
|---|---|
| `RoleInProject` | **không đổi một dòng** |
| `ProjectPermissions` | **không đổi một dòng** |
| Call site phải rà | **0** |

Bản khuyến nghị cũ được giữ nguyên văn trong `<details>` ở ADR-063 — một khuyến nghị bị lật
**có bằng chứng** là hồ sơ đáng giữ, xoá đi thì phiên sau sẽ đề xuất lại (a).

## Thay đổi

### Backend

- `WorkItemType.IsRequestable` + `RequestInstructions` — migration `AddRequestPortal`
- enum ĐÓNG `TaskApprovalState` + `TaskField.ApprovalState` — **trả nốt lời hứa còn treo của
  ADR-061**: hàng đợi duyệt nay là một `SavedView` lưu được
- `RequestPortalController` — 5 endpoint, `[Authorize]` trần, không policy quyền hệ thống nào
- `NotificationType`/`ActivityAction.RequestSubmitted`

🔴 **`RequestPortalService` KHÔNG gọi `IProjectAuthorizationService`, và đó là thiết kế.**
Đây là **ngoại lệ có chủ đích thứ tư** của mô hình hai tầng (`Notification` ADR-023 ·
`ApproverMode` ADR-062 · `GetMyWorkAsync` ADR-053 · lớp này). Đã ghi trong XML doc của lớp
để phiên sau không "sửa nó về cho nhất quán".

### Frontend

- 3 route: `/requests` · `/requests/new` · `/requests/{id}`
- Switch **"nhận yêu cầu từ bên ngoài"** + ô chỉ dẫn ở dialog loại việc (ô chỉ dẫn **tự ẩn**
  khi chưa bật cổng)
- `ApprovalState` vào ô lọc màn Danh sách
- Mục sidebar **TỰ ẨN** khi chưa đội nào mở cổng — luật 3 Doctrine

## Năm guard, cả năm qua MUTATION TEST

Không phải được tin là đang chạy — đã gỡ ra và đếm test đỏ:

| Guard | Gỡ ra → test đỏ |
|---|---|
| **G1** kiểm `IsRequestable` lúc gửi | **2** |
| **G2** cưỡng chế `IsRequired` lúc gửi | **1** |
| **G3** vị từ `ReporterId == me` | **1** |
| **G4** lọc `IsRequestable` ở danh mục cổng | **3** |
| `ConsumedAt IS NULL` ở bộ lọc `ApprovalState` | **1** |

🔴 **`IsRequired` nay có HAI điểm cưỡng chế khác nhau, cố ý.** Ở task nội bộ nó chỉ chặn
*xoá trắng* giá trị (ADR-060); ở cổng yêu cầu nó chặn *ngay lúc gửi*. Có test
`Diem_cuong_che_IsRequired_nay_KHONG_ap_cho_POST_tasks` sẽ **ĐỎ** nếu ai đem phép kiểm về
`TaskService` — và đó là điều đáng xảy ra.

## Nghiệm thu đường (b′)

`RequestPortalTests.Cong_yeu_cau_KHONG_mo_them_cua_nao_khac` — người gửi **đã gửi thành công**
một yêu cầu vào project (tức CÓ quan hệ thật với nó) mà **11/11 endpoint project-scoped vẫn
trả 404**. Đó chính là điều đường (a) sẽ phá vỡ, và test này canh nó vĩnh viễn.

Đã xác nhận lại bằng tay trên trình duyệt: gõ tay URL board của project vừa gửi yêu cầu →
*"Không tìm thấy Project"*.

## 🪤 Bốn lỗi CÓ SẴN mà việc bấm tay lộ ra

Cả bốn đều **build sạch, test xanh, tài liệu ghi ✅** — đúng lớp lỗi §0 nguyên tắc 3 đặt tên.
Chúng sống sót vì *thứ cần kiểm chứng chưa có ai gọi tới*.

1. 🔴 **Mọi bộ lọc Enum/Boolean ở màn Danh sách hỏng ngay lần đầu dùng** (từ ADR-061).
   `filter-builder` vẽ `value={value || options[0].value}` — **hiện** lựa chọn đầu nhưng
   **không ghi** nó vào state. Người dùng thêm "Độ ưu tiên bằng Cao nhất", nhìn thấy đúng
   chữ đó, bấm chạy và nhận **400 *"thiếu giá trị so sánh"***. Lối thoát là mở ô chọn rồi
   chọn lại đúng cái đang hiện sẵn — không ai đoán ra.
2. 🔴 **`ParseEnumByName` nói dối về tên của chính nó** (từ ADR-061). XML doc tuyên bố
   *"khớp theo TÊN, không theo số"*, nhưng `Enum.TryParse` của .NET **cũng nhận chuỗi số**:
   `TryParse<Priority>("1")` → `High` với `IsDefined == true` (đã kiểm bằng chương trình dò).
   Một `SavedView` lưu `"1"` vẫn chạy và sẽ **âm thầm đổi nghĩa** nếu ai chèn thành viên vào
   giữa enum — đúng bẫy remap ADR-052.
3. ⚠️ **Cùng một ternary HAI VẾ ở CẢ hai đầu** — danh sách giá trị hợp lệ (backend) và danh
   sách lựa chọn (frontend) đều là `field === 'Priority' ? … : Category`. Đúng khi có hai
   trường Enum, bắt đầu nói dối ở trường thứ ba. *Hai bản sao của một **lỗi** cũng trôi khỏi
   nhau y như hai bản sao của một luật.*
4. ⚠️ **`backlog-insight` thiếu `TAB_LABEL`** nên breadcrumb dừng ở tên dự án. Ghi chú cũ ở
   `frontend-next-session.md` đổ nhầm cho `velocity`/`timeline` — cả hai vốn có sẵn.

## ✅ Nợ kiểm chứng — đã trả hết

Phiên đầu tiên có công cụ trình duyệt.

- **Kéo–thả bằng chuột chạy thật** — nợ treo từ 2026-08-03
- **7/7 bước ADR-062 ĐẠT** — toast lần chạm cổng đầu là `info` trung tính (không đỏ) · task
  không di chuyển · 1/2 rồi 2/2 lượt ký · kéo qua được · kéo ra kéo lại sinh yêu cầu **MỚI**
  với "Lịch sử duyệt" giữ nguyên hai chữ ký cũ (nghiệm thu `ConsumedAt`)
- **5/5 mục ADR-059/060 ĐẠT** — chip Select đổi đúng màu đã cấu hình (`#DC2626`) · ô Date gõ
  15/09 lưu `2026-09-15T00:00:00Z` **không lệch** dù máy ở UTC+7 · khối trường tự ẩn

⚠️ **Vẫn chưa kiểm:** kéo bằng **bàn phím** và **cảm ứng**. Và độ chính xác của cú thả tổng
hợp không tuyệt đối — một lần thẻ rơi sang cột kế bên, nên khi kiểm tay phải xác nhận cột
đích bằng ảnh chụp.

## 📌 Khoảng trống đã biết — ghi thẳng chứ không giấu

**"CR chờ TÔI duyệt" vẫn CHƯA phải một view lưu được.** `SavedViewFilter` lưu **giá trị
literal**, không có sentinel "người đang đăng nhập" — nên một view **chia sẻ** mang điều
kiện đó sẽ nói dối mọi người trừ tác giả của nó. `ApprovalState` giải quyết **hàng đợi của
đội**, không giải quyết **hàng đợi cá nhân**. Lời giải cho phiên sau: cờ
`SavedViewFilter.UseCurrentUser`.

**Người gửi chưa bình luận được trên yêu cầu của mình.** `CommentService` đi qua
`ProjectAction.CreateComment` nên người ngoài nhận 404. Điểm cắm đúng là
`POST /request-portal/requests/{id}/comments` với cùng vị từ. Để lại vì nó là một **quyết
định sản phẩm riêng** (ai đọc được bình luận nội bộ của đội xử lý?), không phải chi tiết
cài đặt.

## Cách kiểm PR này

```bash
cd backend  && dotnet test                       # 673, 0 đỏ
cd frontend && npm run typecheck && npm run lint && npm test && npm run build
```

Kiểm tay (cần 2 tài khoản: một PM, một người **ngoài** project):

1. PM: `/projects/{id}/settings` → Loại công việc → bật **"nhận yêu cầu từ bên ngoài"**
2. Người ngoài: mục **"Yêu cầu của tôi"** xuất hiện ở sidebar → Gửi yêu cầu
3. Bỏ trống một trường bắt buộc → bị chặn kèm **TÊN trường** (G2)
4. Gửi đủ → yêu cầu vào hàng đợi của đội ở màn **Danh sách**
5. 🔴 Người ngoài gõ tay `/projects/{id}/board` → vẫn **404** (nghiệm thu b′)
