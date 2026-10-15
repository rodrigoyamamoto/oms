namespace Oms.Orders.Domain.Common;

internal readonly record struct Error
{
    public Error()
    {
        Code = string.Empty;
        Message = string.Empty;
    }

    public Error(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Code = code;
        Message = message;
    }

    public static Error None => new();

    public string Code { get; }

    public string Message { get; }

    public bool IsNone => string.IsNullOrEmpty(Code);
}
