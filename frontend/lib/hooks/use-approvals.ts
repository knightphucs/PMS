'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  cancelApproval,
  createApprovalPolicy,
  decideApproval,
  deleteApprovalPolicy,
  listApprovalPolicies,
  updateApprovalPolicy,
} from '@/lib/api/endpoints/approvals';
import { approvalPolicyKeys, projectDataKeys, taskDetailKeys, taskKeys } from '@/lib/hooks/keys';
import { getTaskApprovals } from '@/lib/api/endpoints/approvals';
import type {
  CreateApprovalDecisionRequest,
  CreateApprovalPolicyRequest,
  UpdateApprovalPolicyRequest,
} from '@/types/approval';

// ---------- luật duyệt (cấu hình) ----------

/**
 * Luật duyệt của project (ADR-062).
 *
 * `staleTime` dài cùng lý do `useWorkItemTypes`/`useBoardColumns`: luật gần như không đổi
 * trong một phiên, mà hook mount ở cả trang Cấu hình lẫn khối duyệt của từng task.
 */
export function useApprovalPolicies(projectId: string | null) {
  return useQuery({
    queryKey: approvalPolicyKeys.all(projectId ?? ''),
    queryFn: ({ signal }) => listApprovalPolicies(projectId!, signal),
    enabled: projectId !== null,
    staleTime: 5 * 60_000,
  });
}

/**
 * 🔴 Đổi luật duyệt chạm tới nhiều thứ hơn vẻ ngoài: nó đổi việc một task có kéo được sang
 * một cột hay không, và đổi cả `hasGate` của khối duyệt trên MỌI task thuộc loại đó. Dọn hẹp
 * chỉ danh sách luật sẽ để lại những khối duyệt vẽ theo một luật không còn tồn tại.
 */
function usePolicyInvalidation(projectId: string) {
  const queryClient = useQueryClient();

  return () => {
    void queryClient.invalidateQueries({ queryKey: approvalPolicyKeys.all(projectId) });
    void queryClient.invalidateQueries({ queryKey: projectDataKeys.all(projectId) });
  };
}

export function useCreateApprovalPolicy(projectId: string) {
  const invalidate = usePolicyInvalidation(projectId);
  return useMutation({
    mutationFn: (body: CreateApprovalPolicyRequest) => createApprovalPolicy(projectId, body),
    onSuccess: invalidate,
  });
}

export function useUpdateApprovalPolicy(projectId: string) {
  const invalidate = usePolicyInvalidation(projectId);
  return useMutation({
    mutationFn: ({ policyId, body }: { policyId: string; body: UpdateApprovalPolicyRequest }) =>
      updateApprovalPolicy(policyId, body),
    onSuccess: invalidate,
  });
}

export function useDeleteApprovalPolicy(projectId: string) {
  const invalidate = usePolicyInvalidation(projectId);
  return useMutation({
    mutationFn: (policyId: string) => deleteApprovalPolicy(policyId),
    onSuccess: invalidate,
  });
}

// ---------- yêu cầu duyệt (dữ liệu chạy) ----------

export function useTaskApprovals(projectId: string, taskId: string | null) {
  return useQuery({
    queryKey: taskDetailKeys.approvals(projectId, taskId ?? ''),
    queryFn: ({ signal }) => getTaskApprovals(taskId!, signal),
    enabled: taskId !== null,
  });
}

/**
 * Sau một lá phiếu, phải làm mới CẢ task chứ không chỉ khối duyệt: phiếu cuối cùng của quorum
 * mở cổng ra, và ô chọn trạng thái ở chi tiết task phải thôi báo lỗi ngay lập tức.
 */
function useApprovalInvalidation(projectId: string, taskId: string) {
  const queryClient = useQueryClient();

  return () => {
    void queryClient.invalidateQueries({ queryKey: taskKeys.detail(projectId, taskId) });
  };
}

export function useDecideApproval(projectId: string, taskId: string) {
  const invalidate = useApprovalInvalidation(projectId, taskId);
  return useMutation({
    mutationFn: ({
      approvalId,
      body,
    }: {
      approvalId: string;
      body: CreateApprovalDecisionRequest;
    }) => decideApproval(approvalId, body),
    onSuccess: invalidate,
  });
}

export function useCancelApproval(projectId: string, taskId: string) {
  const invalidate = useApprovalInvalidation(projectId, taskId);
  return useMutation({
    mutationFn: (approvalId: string) => cancelApproval(approvalId),
    onSuccess: invalidate,
  });
}
