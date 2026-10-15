namespace Oms.Orders.Domain.Common;

internal readonly record struct Error
{
    internal static readonly Error None = new(string.Empty, string.Empty, isNone: true);

    public Error(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Code = code;
        Message = message;
    }

    private Error(string code, string message, bool isNone)
    {
        _ = isNone;
        Code = code;
        Message = message;
    }

    public string Code { get; }

    public string Message { get; }

    public bool IsNone => string.IsNullOrEmpty(Code);
}
