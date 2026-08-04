namespace PMS.Application.Features.Labels;

public record CreateLabelRequest(string Name, string? Color);

public record UpdateLabelRequest(string Name, string Color);

/// <summary>
/// Nhãn dùng cho cả danh sách nhãn toàn cục lẫn chip trên thẻ Kanban — cùng một hình dạng
/// nên frontend chỉ cần một kiểu và một component chip.
/// </summary>
public record LabelResponse(Guid Id, string Name, string Color);
