namespace StoresPOS.BLL.Exceptions;

/// <summary>
/// A business-rule violation shown to the user. <see cref="MessageKey"/> is the English format string
/// (also the localization key in the PL); <see cref="Args"/> fill its placeholders.
/// </summary>
public class BusinessException : Exception
{
    public BusinessException(string message) : base(message)
    {
        MessageKey = message;
        Args = [];
    }

    public BusinessException(string messageKey, params object[] args)
        : base(string.Format(System.Globalization.CultureInfo.InvariantCulture, messageKey, args))
    {
        MessageKey = messageKey;
        Args = args;
    }

    public string MessageKey { get; }
    public object[] Args { get; }
}
