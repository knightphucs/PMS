'use client';

import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { createProject, listProjects } from '@/lib/api/endpoints/projects';
import type { PagedRequest } from '@/types/common';
import type { CreateProjectRequest } from '@/types/project';

export const projectKeys = {
  all: ['projects'] as const,
  list: (params: PagedRequest) => [...projectKeys.all, 'list', params] as const,
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
