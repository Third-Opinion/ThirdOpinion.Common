using System.Globalization;
using System.Text;

namespace ThirdOpinion.Common.Aws.S3.StringExtensions;

/// <summary>
///     Provides extension methods for string manipulation and conversion
/// </summary>
public static class StringExtensions
{
    /// <summary>
    ///     Converts a string to a Base64 encoded string
    /// </summary>
    public static string ToBase64(this string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    }

    /// <summary>
    ///     Converts a Base64 encoded string back to its original string
    /// </summary>
    public static string FromBase64(this string value)
    {
        return Encoding.UTF8.GetString(Convert.FromBase64String(value));
    }

    /// <summary>
    ///     Converts a string to a URL-safe Base64 encoded string
    /// </summary>
    public static string? ToBase64UrlSafe(this string value)
    {
        return value.ToBase64()
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    ///     Converts a URL-safe Base64 encoded string back to its original string
    /// </summary>
    public static string FromBase64UrlSafe(this string value)
    {
        string padded = value;
        switch (value.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        padded = padded
            .Replace('-', '+')
            .Replace('_', '/');

        return padded.FromBase64();
    }

    /// <summary>
    ///     Returns the first n characters of a string
    /// </summary>
    /// <param name="value">The string to truncate</param>
    /// <param name="length">The number of characters to return</param>
    /// <returns>The truncated string or the original string if its length is less than the specified length</returns>
    public static string Take(this string value, int length)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value.Length <= length ? value : value.Substring(0, length);
    }

    /// <summary>
    ///     Converts an empty string to null, otherwise returns the original string
    /// </summary>
    /// <param name="value">The string to check</param>
    /// <returns>Null if the string is empty, otherwise the original string</returns>
    public static string? ToNullIfEmpty(this string value)
    {
        return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>
    ///     Converts a string that is null, empty, or contains only whitespace to null
    /// </summary>
    /// <param name="value">The string to check</param>
    /// <returns>Null if the string is null, empty, or whitespace, otherwise the original string</returns>
    public static string? ToNullIfWhiteSpace(this string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    ///     Converts a nullable string that is null, empty, or contains only whitespace to null
    /// </summary>
    /// <param name="value">The nullable string to check</param>
    /// <returns>Null if the string is null, empty, or whitespace, otherwise the original string</returns>
    public static string? ToNullIfWhiteSpaceOrNull(this string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    ///     Capitalizes the first letter of a string. Trims the string first.
    /// </summary>
    /// <param name="value">The string to capitalize</param>
    /// <returns>The capitalized string or the original string if it is null or empty</returns>
    public static string CapitalizeFirstLetter(this string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        value = value.Trim();
        return string.IsNullOrEmpty(value) ? value : char.ToLower(value[0]) + value.Substring(1);
    }

    /// <summary>
    ///     Converts a string to title case, handling commas and spaces appropriately
    /// </summary>
    /// <param name="str">The string to convert to title case</param>
    /// <returns>The string converted to title case</returns>
    public static string ToTitleCase(this string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;

        string strLower = str.ToLower(); //ToTitleCase() assumes all caps are acronyms
        TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;

        if (strLower.Contains(","))
        {
            //store the location of each comma
            var commaLocations = new List<int>();
            for (var i = 0; i < strLower.Length; i++)
                if (strLower[i] == ',')
                    commaLocations.Add(i);

            string[] words = strLower.Split(',');
            IEnumerable<string> capitalizedWords = words.Select(word => textInfo.ToTitleCase(word));
            string result = string.Join(" ", capitalizedWords);

            //insert the commas back in at the correct locations
            foreach (int location in commaLocations) result = result.Insert(location, ",");

            return result;
        }

        if (strLower.Contains(" "))
        {
            string[] words = strLower.Split(' ');
            IEnumerable<string> capitalizedWords = words.Select(word => textInfo.ToTitleCase(word));
            return string.Join(" ", capitalizedWords);
        }

        return textInfo.ToTitleCase(strLower);
    }

    /// <summary>
    ///     Converts the first character of a string to uppercase
    /// </summary>
    /// <param name="input">The string to modify</param>
    /// <returns>The string with the first character converted to uppercase</returns>
    public static string FirstCharToUpper(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return char.ToUpperInvariant(input[0]) + input.Substring(1);
    }

    /// <summary>
    ///     Converts the first character of a string to lowercase
    /// </summary>
    /// <param name="input">The string to modify</param>
    /// <returns>The string with the first character converted to lowercase</returns>
    public static string FirstCharToLower(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return char.ToLowerInvariant(input[0]) + input.Substring(1);
    }
}