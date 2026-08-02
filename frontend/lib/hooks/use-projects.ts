'use client';

import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  createProject,
  deleteProject,
  getProject,
  listProjects,
  updateProject,
} from '@/lib/api/endpoints/projects';
import type { PagedRequest } from '@/types/common';
import type { CreateProjectRequest, UpdateProjectRequest } from '@/types/project';

export const projectKeys = {
  all: ['projects'] as const,
  list: (params: PagedRequest) => [...projectKeys.all, 'list', params] as const,
  /** Nguồn `rowVersion`. `staleTime: 0` / `gcTime: 0` — CHỈ dành cho form sửa. */
  detail: (id: string) => [...projectKeys.all, 'detail', id] as const,
  /**
   * Cùng endpoint với `detail` nhưng khóa riêng và cache bình thường.
   *
   * Trang chi tiết project cần `name` (breadcrumb, header) và `members` (suy ra vai trò
   * của mình) trong suốt phiên làm việc, không phải chỉ trong lúc một dialog đang mở.
   * Dùng chung khóa với `detail` thì hoặc là trang refetch ở mỗi lần đổi tab, hoặc là
   * cache bị vứt ngay khi đóng dialog — và tệ hơn cả: form sửa sẽ nhận một `rowVersion`
   * cũ tới 5 phút, tức 409 bảo đảm.
   *
   * Giá phải trả là một `GET /projects/{id}` thừa khi mở dialog sửa từ trang chi tiết.
   * Đó là giá đúng của luật ADR-016 "muốn ghi đè thì phải chứng minh đã đọc bản mới nhất".
   */
  overview: (id: string) => [...projectKeys.all, 'overview', id] as const,
};

export function useProjects(params: PagedRequest) {
  return useQuery({
    queryKey: projectKeys.list(params),
    queryFn: ({ signal }) => listProjects(params, signal),
    // Giữ dữ liệu trang cũ trong lúc tải trang mới: bảng không sập xuống skeleton rồi
    // bật lại ở mỗi lần bấm phân trang hay gõ ô tìm kiếm. Dùng `isFetching` để hiện
    // trạng thái đang tải mà không phá bố cục.
    placeholderData: keepPreviousData,
  });
}

export function useCreateProject() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: CreateProjectRequest) => createProject(body),
    onSuccess: () => {
      // Invalidate cả nhánh: không biết project mới rơi vào trang nào sau khi sắp xếp,
      // nên làm mới toàn bộ danh sách thay vì đoán rồi chèn thủ công vào cache.
      void queryClient.invalidateQueries({ queryKey: projectKeys.all });
    },
  });
}

/**
 * Chi tiết project — nguồn DUY NHẤT của `rowVersion` và `description`.
 *
 * Danh sách (`ProjectSummaryResponse`) cố ý không có hai trường đó, nên form sửa buộc
 * phải nạp chi tiết trước. Đúng bản chất của optimistic concurrency: muốn ghi đè thì phải
 * chứng minh mình đã đọc bản mới nhất.
 *
 * `staleTime: 0` để mỗi lần mở dialog là một lượt đọc mới — `rowVersion` cũ trong cache
 * chính là thứ sinh ra 409.
 */
export function useProject(id: string | null) {
  return useQuery({
    queryKey: projectKeys.detail(id ?? ''),
    queryFn: ({ signal }) => getProject(id!, signal),
    enabled: id !== null,
    staleTime: 0,
    gcTime: 0,
  });
}

/**
 * Chi tiết project cho HIỂN THỊ — tên, mô tả, danh sách thành viên.
 *
 * ⚠️ Đừng lấy `rowVersion` từ đây để gửi lên: dữ liệu ở đây được phép cũ vài phút. Form
 * sửa phải dùng `useProject` (khóa `detail`).
 */
export function useProjectOverview(projectId: string | null) {
  return useQuery({
    queryKey: projectKeys.overview(projectId ?? ''),
    queryFn: ({ signal }) => getProject(projectId!, signal),
    enabled: projectId !== null,
    staleTime: 5 * 60_000,
  });
}

export function useUpdateProject(id: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: UpdateProjectRequest) => updateProject(id, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: projectKeys.all });
    },
    onError: () => {
      // Kể cả khi hỏng vẫn phải bỏ chi tiết đang cache. Ca 409 là quan trọng nhất: nó
      // nghĩa là bản trong tay ta đã cũ, nên giữ lại chỉ khiến lần thử tiếp theo gửi lại
      // đúng `rowVersion` hỏng đó và 409 vĩnh viễn.
      void queryClient.invalidateQueries({ queryKey: projectKeys.detail(id) });
    },
  });
}

export function useDeleteProject() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => deleteProject(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: projectKeys.all });
    },
  });
}
