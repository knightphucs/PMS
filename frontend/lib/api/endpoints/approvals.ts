import type {
  ApprovalPolicyResponse,
  ApprovalResponse,
  CreateApprovalDecisionRequest,
  CreateApprovalPolicyRequest,
  TaskApprovalsResponse,
  UpdateApprovalPolicyRequest,
} from '@/types/approval';

import { apiFetch } from '../http';

/**
 * Phê duyệt (ADR-062).
 *
 * 🔴 **Không có `createApproval`, và đó là chủ ý.** Yêu cầu duyệt tự sinh ở
 * `PATCH /tasks/{id}/status` khi task chạm một cổng — thêm một nút "Gửi duyệt" là thêm một
 * bề mặt lên màn làm việc (luật 5 Doctrine) và bắt người dùng phải *biết trước* rằng loại
 * việc này có cổng.
 */

// ---------- luật duyệt (cấu hình) ----------

/** Đọc mở cho mọi thành viên — cả đội cần biết luật đang áp lên việc của họ. */
export function listApprovalPolicies(projectId: string, signal?: AbortSignal) {
  return apiFetch<ApprovalPolicyResponse[]>(`/projects/${projectId}/approval-policies`, { signal });
}

/**
 * **409** đã có luật cho cùng cặp (loại việc, cột đích) · **404** loại/cột thuộc project
 * khác · **400** quorum lớn hơn số người duyệt, hoặc người duyệt không phải thành viên.
 */
export function createApprovalPolicy(projectId: string, body: CreateApprovalPolicyRequest) {
  return apiFetch<ApprovalPolicyResponse>(`/projects/${projectId}/approval-policies`, {
    method: 'POST',
    body,
  });
}

export function updateApprovalPolicy(policyId: string, body: UpdateApprovalPolicyRequest) {
  return apiFetch<ApprovalPolicyResponse>(`/approval-policies/${policyId}`, {
    method: 'PUT',
    body,
  });
}

/** Xoá luật cũng xoá theo mọi yêu cầu duyệt thuộc nó — cổng mở lại ngay. */
export function deleteApprovalPolicy(policyId: string) {
  return apiFetch<void>(`/approval-policies/${policyId}`, { method: 'DELETE' });
}

// ---------- yêu cầu duyệt (dữ liệu chạy) ----------

export function getTaskApprovals(taskId: string, signal?: AbortSignal) {
  return apiFetch<TaskApprovalsResponse>(`/tasks/${taskId}/approvals`, { signal });
}

/**
 * Bỏ một lá phiếu.
 *
 * ⚠️ **403** khi người gọi không nằm trong danh sách ký — kể cả khi họ là PM, nếu luật dùng
 * chế độ `NamedApprovers`. Quyền ký KHÔNG theo vai trò trong project (ADR-062).
 * **409** khi yêu cầu đã chốt, hoặc khi chính người này đã bỏ phiếu rồi.
 */
export function decideApproval(approvalId: string, body: CreateApprovalDecisionRequest) {
  return apiFetch<ApprovalResponse>(`/approvals/${approvalId}/decisions`, {
    method: 'POST',
    body,
  });
}

/** Rút yêu cầu về — người gửi HOẶC PM. Lối thoát duy nhất khỏi một yêu cầu đã bị từ chối. */
export function cancelApproval(approvalId: string) {
  return apiFetch<ApprovalResponse>(`/approvals/${approvalId}/cancel`, { method: 'POST' });
}
