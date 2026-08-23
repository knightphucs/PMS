/**
 * Query key của mọi dữ liệu thuộc phạm vi MỘT project.
 *
 * Chúng cross-invalidate nhau nhiều tới mức nhớ tay là sai: chuyển một task sang sprint
 * khác làm cũ **cùng lúc** backlog, board "tất cả task", board của sprint nguồn, board
 * của sprint đích, và `taskCount` của cả hai sprint. Có prefix chung thì "quên nhánh nào"
 * biến thành một dòng:
 *
 *     invalidateQueries({ queryKey: projectDataKeys.all(projectId) })
 *
 * Cố ý KHÁC hình dạng phẳng của `projectKeys` trong `use-projects.ts` — bản thân danh
 * sách/chi tiết project có vòng đời khác và không nên bị cuốn theo mỗi lần kéo một thẻ.
 */
export const projectDataKeys = {
  all: (projectId: string) => ['project-data', projectId] as const,
};

/**
 * Cấu hình cột board của project (ADR-052).
 *
 * Nằm DƯỚI `projectDataKeys` vì đổi cột làm cũ cả board lẫn backlog lẫn thống kê — một
 * lệnh `invalidateQueries(projectDataKeys.all)` phải cuốn theo được nó.
 */
export const boardColumnKeys = {
  all: (projectId: string) => [...projectDataKeys.all(projectId), 'columns'] as const,
};

/** Lược đồ trường tuỳ biến của project (ADR-059) — KHÁC với giá trị trên từng task. */
export const fieldDefinitionKeys = {
  all: (projectId: string) => [...projectDataKeys.all(projectId), 'fields'] as const,
};

/** Loại công việc của project (ADR-060). */
export const workItemTypeKeys = {
  all: (projectId: string) => [...projectDataKeys.all(projectId), 'work-item-types'] as const,
};

/** View lưu được của project (ADR-061) — KHÁC với kết quả chạy một view. */
export const savedViewKeys = {
  all: (projectId: string) => [...projectDataKeys.all(projectId), 'views'] as const,
};

/**
 * Kết quả CHẠY một bộ lọc.
 *
 * 🔴 `request` phải nằm TRỌN trong khoá. Endpoint là POST nên hai lượt lọc khác nhau đi
 * cùng một URL — thiếu nó thì đổi bộ lọc sẽ hiện lại kết quả của bộ lọc trước, và người
 * dùng tưởng bộ lọc mới không có kết quả nào.
 *
 * Nằm dưới `projectDataKeys` để một lần sửa task cuốn theo được cả danh sách.
 */
export const taskQueryKeys = {
  all: (projectId: string) => [...projectDataKeys.all(projectId), 'task-query'] as const,
  run: (projectId: string, request: unknown) =>
    [...taskQueryKeys.all(projectId), request] as const,
};

export const boardKeys = {
  all: (projectId: string) => [...projectDataKeys.all(projectId), 'board'] as const,
  /** `sprintId === null` = board "tất cả task", khóa bằng chuỗi 'all' cho ổn định. */
  detail: (projectId: string, sprintId: string | null) =>
    [...boardKeys.all(projectId), sprintId ?? 'all'] as const,
};

export const backlogKeys = {
  all: (projectId: string) => [...projectDataKeys.all(projectId), 'backlog'] as const,
};

export const sprintKeys = {
  all: (projectId: string) => [...projectDataKeys.all(projectId), 'sprints'] as const,
  detail: (projectId: string, sprintId: string) =>
    [...sprintKeys.all(projectId), sprintId] as const,
};

export const memberKeys = {
  all: (projectId: string) => [...projectDataKeys.all(projectId), 'members'] as const,
};

export const taskKeys = {
  all: (projectId: string) => [...projectDataKeys.all(projectId), 'tasks'] as const,
  /** Chi tiết task — nguồn `rowVersion`, luôn nạp mới khi mở form sửa. */
  detail: (projectId: string, taskId: string) => [...taskKeys.all(projectId), taskId] as const,
};

/**
 * Dữ liệu của MỘT task cụ thể, đều nằm dưới `taskKeys.detail` để một lần invalidate task
 * là làm mới cả bảy khối của màn chi tiết.
 */
export const taskDetailKeys = {
  comments: (projectId: string, taskId: string) =>
    [...taskKeys.detail(projectId, taskId), 'comments'] as const,
  attachments: (projectId: string, taskId: string) =>
    [...taskKeys.detail(projectId, taskId), 'attachments'] as const,
  watchers: (projectId: string, taskId: string) =>
    [...taskKeys.detail(projectId, taskId), 'watchers'] as const,
  links: (projectId: string, taskId: string) =>
    [...taskKeys.detail(projectId, taskId), 'links'] as const,
  activity: (projectId: string, taskId: string) =>
    [...taskKeys.detail(projectId, taskId), 'activity'] as const,
  fieldValues: (projectId: string, taskId: string) =>
    [...taskKeys.detail(projectId, taskId), 'field-values'] as const,
  /**
   * Trạng thái duyệt của task (ADR-062).
   *
   * 🔴 Nằm dưới `taskKeys.detail` là **load-bearing**, không phải cho gọn: đổi trạng thái
   * task có thể SINH ra một yêu cầu duyệt (guard tự sinh khi bị chặn), nên một lần
   * invalidate task phải kéo theo cả khối này — nếu không, người dùng kéo thẻ, nhận thông
   * báo "đã gửi yêu cầu duyệt", rồi nhìn xuống thấy khối duyệt vẫn trống.
   */
  approvals: (projectId: string, taskId: string) =>
    [...taskKeys.detail(projectId, taskId), 'approvals'] as const,
};

/** Luật duyệt của project (ADR-062) — cấu hình, KHÁC với yêu cầu duyệt trên từng task. */
export const approvalPolicyKeys = {
  all: (projectId: string) => [...projectDataKeys.all(projectId), 'approval-policies'] as const,
};

export const projectActivityKeys = {
  all: (projectId: string) => [...projectDataKeys.all(projectId), 'activity'] as const,
};

export const statisticsKeys = {
  all: (projectId: string) => [...projectDataKeys.all(projectId), 'statistics'] as const,
};

/** Nhóm báo cáo kiểu Jira — backlog insight + velocity. */
export const reportKeys = {
  backlogInsight: (projectId: string) =>
    [...projectDataKeys.all(projectId), 'reports', 'backlog-insight'] as const,
  velocity: (projectId: string) =>
    [...projectDataKeys.all(projectId), 'reports', 'velocity'] as const,
  timeline: (projectId: string) =>
    [...projectDataKeys.all(projectId), 'reports', 'timeline'] as const,
};

export const projectAttachmentKeys = {
  all: (projectId: string) => [...projectDataKeys.all(projectId), 'attachments'] as const,
};

/**
 * Nhãn là dữ liệu TOÀN CỤC, cố ý nằm NGOÀI `projectDataKeys`: kéo một thẻ trong project A
 * không có lý do gì làm cũ danh sách nhãn dùng chung cho mọi project.
 */
export const labelKeys = {
  all: ['labels'] as const,
};

/**
 * Xem trước một lời mời qua TOKEN trong link (`/invitations/{token}`) — public, theo token
 * thô, không cần đăng nhập. Đây là hộp khóa DUY NHẤT còn lại cho lời mời kể từ ADR-057:
 * lời mời trong-app (`invitationKeys`) đã bị gỡ cùng ba endpoint của nó.
 */
export const invitationPreviewKeys = {
  detail: (token: string) => ['invitation-preview', token] as const,
};

/** Nhật ký cấp hệ thống — chỉ SystemAdmin, không thuộc project nào (ADR-042). */
export const systemAuditKeys = {
  all: ['system-audit-logs'] as const,
};

/**
 * Nhân sự cấp hệ thống. Khóa PHẲNG, cố ý nằm ngoài `projectDataKeys`: đây là dữ liệu toàn
 * hệ thống, khóa/mở một tài khoản không liên quan gì tới cache của một project cụ thể.
 */
export const adminEmployeeKeys = {
  all: ['admin-employees'] as const,
  list: (request: unknown) => [...adminEmployeeKeys.all, 'list', request] as const,
};

/**
 * Tra nhân sự cho ô gợi ý — `GET /employees?search=` (ADR-048).
 *
 * Cố ý TÁCH khỏi `adminEmployeeKeys`: hai endpoint khác nhau, khác quyền, và DTO ở đây chỉ
 * có 3 trường. Dùng chung khóa thì một lần invalidate ở màn quản trị sẽ kéo theo cache của
 * ô gợi ý với hình dạng dữ liệu khác hẳn.
 */
export const employeeLookupKeys = {
  all: ['employee-lookup'] as const,
  search: (keyword: string) => [...employeeLookupKeys.all, keyword] as const,
};

/**
 * Phân quyền vai trò (ADR-045).
 *
 * `catalog` và `matrix` là hai nhánh con của `all` để một lần
 * `invalidateQueries(rolePermissionKeys.all)` sau khi lưu làm mới cả hai — ma trận mà lệch
 * với danh mục thì ô tích hiện sai và người quản trị không có cách nào biết.
 */
export const rolePermissionKeys = {
  all: ['role-permissions'] as const,
  catalog: () => [...rolePermissionKeys.all, 'catalog'] as const,
  matrix: () => [...rolePermissionKeys.all, 'matrix'] as const,
};

/**
 * Thông báo nằm NGOÀI `projectDataKeys` — hộp thông báo là của một CON NGƯỜI, không của
 * một project, và endpoint không nhận `projectId` ở đâu cả (ADR-023).
 *
 * `unreadCount` là nhánh con của `all` để một lần `invalidateQueries(notificationKeys.all)`
 * làm mới cả badge lẫn danh sách — hai thứ luôn phải khớp nhau.
 */
export const notificationKeys = {
  all: ['notifications'] as const,
  list: (request: unknown) => [...notificationKeys.all, 'list', request] as const,
  unreadCount: () => [...notificationKeys.all, 'unread-count'] as const,
};
