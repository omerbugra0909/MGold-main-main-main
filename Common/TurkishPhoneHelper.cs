using System.Text.RegularExpressions;

namespace MGold.Common;

public static partial class TurkishPhoneHelper
{
    private const string InvalidPhoneMessage = "Only Turkish mobile phone numbers are supported. Use a 5xx number.";

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigitRegex();

    public static string Normalize(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new ArgumentException(InvalidPhoneMessage);
        }

        var digits = NonDigitRegex().Replace(phoneNumber, string.Empty);

        if (digits.StartsWith("0090", StringComparison.Ordinal))
        {
            digits = digits[4..];
        }
        else if (digits.StartsWith("90", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }
        else if (digits.StartsWith("0", StringComparison.Ordinal))
        {
            digits = digits[1..];
        }

        if (digits.Length != 10 || !digits.StartsWith("5", StringComparison.Ordinal))
        {
            throw new ArgumentException(InvalidPhoneMessage);
        }

        return $"+90{digits}";
    }

    public static bool TryNormalize(string? phoneNumber, out string normalized)
    {
        try
        {
            normalized = Normalize(phoneNumber);
            return true;
        }
        catch
        {
            normalized = string.Empty;
            return false;
        }
    }
}
