using System.Globalization;
using System.Text;

namespace Misc.StringExtensions;

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
        var padded = value;
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

    public static string? ToNullIfEmpty(this string value)
    {
        return string.IsNullOrEmpty(value) ? null : value;
    }

    public static string? ToNullIfWhiteSpace(this string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

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

    public static string ToTitleCase(this string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;

        string strLower = str.ToLower();  //ToTitleCase() assumes all caps are acronyms
        TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;

        if (strLower.Contains(","))
        {
            //store the location of each comma
            var commaLocations = new List<int>();
            for (int i = 0; i < strLower.Length; i++)
            {
                if (strLower[i] == ',')
                    commaLocations.Add(i);
            }

            var words = strLower.Split(',');
            var capitalizedWords = words.Select(word => textInfo.ToTitleCase(word));
            var result = string.Join(" ", capitalizedWords);

            //insert the commas back in at the correct locations
            foreach (var location in commaLocations)
            {
                result = result.Insert(location, ",");
            }

            return result;
        } else if (strLower.Contains(" ")) {
            var words = strLower.Split(' ');
            var capitalizedWords = words.Select(word => textInfo.ToTitleCase(word));
            return string.Join(" ", capitalizedWords);
        } else {
            return textInfo.ToTitleCase(strLower);
        }
    }

    public static string FirstCharToUpper(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return char.ToUpperInvariant(input[0]) + input.Substring(1);
    }

    public static string FirstCharToLower(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return char.ToLowerInvariant(input[0]) + input.Substring(1);
    }
}