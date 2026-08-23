/**
 * Phê duyệt là dữ liệu (ADR-062) — ĐỘNG TỪ đầu tiên của hệ thống.
 *
 * 🔴 **Không có endpoint nào tạo một yêu cầu duyệt.** Nó tự sinh khi người dùng kéo task
 * vào một cột có cổng: `PATCH /tasks/{id}/status` trả **409** kèm thông điệp "đã gửi yêu cầu
 * duyệt", và hàng `Approval` ra đời trong chính lần gọi đó. Đừng dựng nút "Gửi duyệt" —
 * xem ADR-062 quyết định (a).
 */

export type ApprovalStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled';

/** `ProjectManagers` = mọi PM ký được · `NamedApprovers` = chỉ người có tên, kể cả PM cũng không. */
export type ApproverMode = 'ProjectManagers' | 'NamedApprovers';

export type DecisionKind = 'Approve' | 'Reject';

// ---------- luật duyệt (cấu hình) ----------

export interface ApproverDto {
  employeeId: string;
  name: string;
}

export interface ApprovalPolicyResponse {
  id: string;
  projectId: string;
  workItemTypeId: string;
  workItemTypeName: string;
  targetColumnId: string;
  targetColumnName: string;
  approverMode: ApproverMode;
  /** Số phiếu thuận cần có. `1` là ca thường, `2` là ca CAB điển hình. */
  minApprovals: number;
  order: number;
  /** Chỉ có nghĩa khi `approverMode === 'NamedApprovers'`. */
  approvers: ApproverDto[];
}

export interface CreateApprovalPolicyRequest {
  workItemTypeId: string;
  targetColumnId: string;
  approverMode: ApproverMode;
  minApprovals: number;
  approverIds?: string[];
}

export type UpdateApprovalPolicyRequest = CreateApprovalPolicyRequest;

// ---------- yêu cầu duyệt (dữ liệu chạy) ----------

export interface ApprovalDecisionDto {
  id: string;
  approverId: string;
  approverName: string;
  decision: DecisionKind;
  comment: string | null;
  decidedAt: string;
}

export interface ApprovalResponse {
  id: string;
  taskId: string;
  approvalPolicyId: string;
  targetColumnName: string;
  status: ApprovalStatus;
  requestedById: string;
  requestedByName: string;
  requestedAt: string;
  decidedAt: string | null;
  /**
   * `null` = yêu cầu còn hiệu lực và đang chặn cổng.
   *
   * Có giá trị = đã dùng xong hoặc đã huỷ, hàng chỉ còn là lịch sử. ⚠️ Một hàng `Approved`
   * **đã** `consumedAt` không mở được cổng nữa — task rời cột đích rồi quay lại thì phải
   * duyệt LẠI (ADR-062 quyết định b).
   */
  consumedAt: string | null;
  approveCount: number;
  minApprovals: number;
  decisions: ApprovalDecisionDto[];
  /** Backend trả lời thay vì FE tự suy — luật có bốn vế mà FE chỉ nhìn thấy hai (ADR-034). */
  canDecide: boolean;
  canCancel: boolean;
}

export interface TaskApprovalsResponse {
  /**
   * Loại việc của task này có cổng duyệt nào không.
   *
   * 🔴 Đây là thứ nuôi việc **TỰ ẨN** khối duyệt (luật 3 của Doctrine). Không có nó thì một
   * task chưa từng đi qua cổng và một task thuộc loại không có cổng trông giống hệt nhau —
   * và khối sẽ hiện tiêu đề trống trên mọi task của mọi project chưa dùng tính năng.
   */
  hasGate: boolean;
  active: ApprovalResponse | null;
  history: ApprovalResponse[];
}

export interface CreateApprovalDecisionRequest {
  decision: DecisionKind;
  comment?: string | null;
}
