/** Soi gương `PMS.Application/Features/Labels/LabelDtos.cs`. */

export interface LabelResponse {
  id: string;
  name: string;
  /** Dạng `#RRGGBB`, luôn có giá trị (backend mặc định `#6B7280`). */
  color: string;
}

export interface CreateLabelRequest {
  name: string;
  /** Bỏ trống/`null` thì backend dùng màu mặc định. */
  color?: string | null;
}

/** ⚠️ Chỉ `SystemAdmin` gọi được — sửa nhãn toàn cục ảnh hưởng mọi project (ADR-037). */
export interface UpdateLabelRequest {
  name: string;
  color: string;
}
