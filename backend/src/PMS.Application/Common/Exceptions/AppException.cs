namespace PMS.Application.Common.Exceptions;
public abstract class AppException : Exception
{
    public abstract int StatusCode { get; }

    /// <summary>
    /// Mã máy đọc được, đi kèm phản hồi lỗi dưới dạng <c>code</c> trong ProblemDetails.
    /// <c>null</c> với hầu hết lỗi — chỉ đặt khi client phải **xử lý khác nhau** giữa hai lỗi
    /// cùng mã HTTP.
    ///
    /// <para>
    /// 🔴 Tồn tại để client khỏi phải **dò chuỗi thông điệp**. Ca đầu tiên cần nó là cổng
    /// duyệt (ADR-062): cả ba nhánh đều là 409, nhưng "đã gửi yêu cầu duyệt giúp bạn" là
    /// tin vui còn "đã bị từ chối" là tin xấu — hiện cả hai thành một toast đỏ là nói dối
    /// người dùng về thứ vừa xảy ra. Khớp theo `title` sẽ là hai nơi cùng dựng một luật, đúng
    /// lớp lỗi ADR-034 đã trả giá.
    /// </para>
    /// </summary>
    public string? Code { get; init; }

    protected AppException(string message) : base(message) { }
}

public class NotFoundException : AppException
{
    public override int StatusCode => 404;
    public NotFoundException(string entity, object key)
        : base($"Không tìm thấy {entity} với định danh '{key}'.") { }
    
    public NotFoundException(string message) : base(message) { }
}

public class UnauthorizedException : AppException
{
    public override int StatusCode => 401;
    public UnauthorizedException(string message) : base(message) { }
}

public class ForbiddenException : AppException
{
    public override int StatusCode => 403;
    public ForbiddenException(string message = "Bạn không có quyền thực hiện thao tác này.")
        : base(message) { }
}

public class ConflictException : AppException
{
    public override int StatusCode => 409;
    public ConflictException(string message) : base(message) { }
}

public class BusinessRuleException : AppException
{
    public override int StatusCode => 400;
    public BusinessRuleException(string message) : base(message) { }
}

/// <summary>
/// File vượt quá kích thước cho phép (ADR-035). Tách khỏi 400 để client phân biệt được
/// "gửi sai" với "gửi đúng nhưng quá to" — chỉ trường hợp sau mới có hành động khắc phục
/// rõ ràng (nén lại hoặc chia nhỏ).
/// </summary>
public class PayloadTooLargeException : AppException
{
    public override int StatusCode => 413;
    public PayloadTooLargeException(string message) : base(message) { }
}

/// <summary>
/// Loại file không nằm trong whitelist (ADR-035).
/// <para>
/// Khác 400 một cách có chủ đích: 415 nghĩa là "định dạng này hệ thống không nhận", còn
/// file <b>nói dối</b> về định dạng của mình (đuôi .png nhưng nội dung là exe) thì trả 400
/// — đó là đầu vào sai lệch, không phải một định dạng hợp lệ chưa được hỗ trợ.
/// </para>
/// </summary>
public class UnsupportedMediaTypeException : AppException
{
    public override int StatusCode => 415;
    public UnsupportedMediaTypeException(string message) : base(message) { }
}