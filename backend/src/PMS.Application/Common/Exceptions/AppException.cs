namespace PMS.Application.Common.Exceptions;
public abstract class AppException : Exception
{
    public abstract int StatusCode { get; }
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