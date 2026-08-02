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

/** Lời mời của TÔI — không thuộc project nào cụ thể (tôi còn chưa là thành viên). */
export const invitationKeys = {
  all: ['my-invitations'] as const,
};
