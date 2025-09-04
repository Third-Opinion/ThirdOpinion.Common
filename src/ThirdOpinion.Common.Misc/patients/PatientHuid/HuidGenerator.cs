using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Misc.patients.PatientHuid;

public static class HuidGenerator
{
    public const string HuidPattern
        = @"^P[2346789ADEHJKLMNPQRUVWXYZBCFGT]{3}-[2346789ADEHJKLMNPQRUVWXYZBCFGT]{3}-[2346789ADEHJKLMNPQRUVWXYZBCFGT]{4}$";

    public const int HuidLength = 13;

    // Constants
    private const string DefaultAlphabet = "2346789ADEHJKLMNPQRUVWXYZ";
    private const string DefaultSalt = "ThirdOpinion.io";
    private const string Guards = "BCFGT";
    private const string PatientPrefix = "P";

    public static readonly Regex HuidRegex = new(HuidPattern, RegexOptions.Compiled);

    public static bool IsValidHuid(string huid)
    {
        return Regex.IsMatch(huid, HuidPattern) && huid.Length == HuidLength;
    }

    // Generate a Patient HUID from a patient ID
    public static string GeneratePatientHuid(long patientId)
    {
        string result = EncodeHuid(patientId, 9, true, PatientPrefix);

        // Format with dashes in a P???-???-???? pattern (where ? are encoded chars)
        result = $"{result.Substring(0, 4)}-{result.Substring(4, 3)}-{result.Substring(7)}";
        return result;
    }

    // Main encoding function
    private static string EncodeHuid(long number, int minWidth, bool addChecksum, string prefix)
    {
        string alphabet = DefaultAlphabet;
        string salt = DefaultSalt;

        // Calculate shuffle key
        var shuffleKey = (int)(number % 100);

        // Get lottery character
        char lottery = alphabet[shuffleKey % alphabet.Length];

        // Create buffer for consistent shuffle
        string buffer = lottery + salt + alphabet;
        if (!string.IsNullOrEmpty(prefix))
            buffer = prefix + buffer;

        // Shuffle the alphabet
        string shuffledAlphabet = ConsistentShuffle(alphabet, buffer.Substring(0, alphabet.Length));

        // Hash the number to its base representation
        string hash = Hash(number, shuffledAlphabet);

        // Core part before padding/truncating
        string core = lottery + hash;

        // Ensure core part has exactly minWidth characters
        if (core.Length < minWidth)
            // EnforcePadding ensures the result has length minWidth if padding is needed
            core = EnforcePadding(core, minWidth, alphabet, shuffleKey);
        else if (core.Length > minWidth)
            // Truncate if longer (take the first minWidth characters)
            core = core.Substring(0, minWidth);
        // Now 'core' has length exactly minWidth

        string result = core; // Start building final result from the fixed-length core

        // Add prefix
        if (!string.IsNullOrEmpty(prefix))
            result = prefix + result;

        // Add checksum if requested
        if (addChecksum)
        {
            // Checksum is calculated on the prefixed core string
            char checkDigit = ComputeLuhnChecksum(result, alphabet);
            result += checkDigit;
        }

        return result;
    }

    // Consistent shuffle (Fisher-Yates algorithm variant)
    private static string ConsistentShuffle(string alphabet, string salt)
    {
        if (string.IsNullOrEmpty(salt))
            return alphabet;

        char[] chars = alphabet.ToCharArray();

        var v = 0;
        var p = 0;

        for (int i = chars.Length - 1; i > 0; i--)
        {
            v %= salt.Length;
            int integer = salt[v];
            p += integer;
            int j = (integer + v + p) % i;

            // Swap
            char temp = chars[j];
            chars[j] = chars[i];
            chars[i] = temp;

            v++;
        }

        return new string(chars);
    }

    // Base conversion (similar to hexadecimal but with custom alphabet)
    private static string Hash(long input, string alphabet)
    {
        var hash = "";
        int alphabetLength = alphabet.Length;

        do
        {
            hash = alphabet[(int)(input % alphabetLength)] + hash;
            input = input / alphabetLength;
        } while (input > 0);

        return hash;
    }

    // Enforce minimum padding with guard characters
    private static string EnforcePadding(string result,
        int minWidth,
        string alphabet,
        int shuffleKey)
    {
        // Use guards for padding
        if (result.Length < minWidth)
        {
            int guardIndex = (shuffleKey + result[0]) % Guards.Length;
            char guard = Guards[guardIndex];
            result = guard + result;

            if (result.Length < minWidth)
            {
                guardIndex = (shuffleKey + result[2]) % Guards.Length;
                guard = Guards[guardIndex];
                result = result + guard;
            }
        }

        // Add padding from alphabet if still needed
        int halfLength = alphabet.Length / 2;
        while (result.Length < minWidth)
        {
            alphabet = ConsistentShuffle(alphabet, alphabet);
            result = alphabet.Substring(halfLength) + result + alphabet.Substring(0, halfLength);

            int excess = result.Length - minWidth;
            if (excess > 0)
                result = result.Substring(excess / 2, minWidth);
        }

        return result;
    }

    // Compute Luhn Mod N checksum
    private static char ComputeLuhnChecksum(string input, string alphabet)
    {
        var factor = 2;
        var sum = 0;
        int n = alphabet.Length;

        // Working from right to left is easier
        for (int i = input.Length - 1; i >= 0; i--)
        {
            int codePoint = alphabet.IndexOf(input[i]);
            if (codePoint == -1) continue; // Skip characters not in alphabet

            int addend = factor * codePoint;
            factor = factor == 2 ? 1 : 2;

            // Sum digits of addend as expressed in base n
            addend = addend / n + addend % n;
            sum += addend;
        }

        // Calculate checksum
        int remainder = sum % n;
        int checkCodePoint = (n - remainder) % n;

        return alphabet[checkCodePoint];
    }

    public static string GeneratePatientHuidFromGuid(Guid patientGuid)
    {
        // Convert GUID to a numeric value first
        long numericValue = GetNumericValueFromGuid(patientGuid);

        // Use existing method with the numeric value
        return GeneratePatientHuid(numericValue);
    }

    // Convert GUID to a numeric value (using hashing to avoid collisions)
    private static long GetNumericValueFromGuid(Guid guid)
    {
        // Convert GUID to byte array
        byte[] guidBytes = guid.ToByteArray();

        // Hash the GUID bytes to get better distribution
        using (var sha256 = SHA256.Create())
        {
            byte[] hashBytes = sha256.ComputeHash(guidBytes);

            // Take the first 8 bytes and convert to a long
            // (Being careful about negatives by using ulong first)
            var ulongValue = BitConverter.ToUInt64(hashBytes, 0);

            // Convert to long ensuring it's positive
            return (long)(ulongValue & 0x7FFFFFFFFFFFFFFF);
        }
    }
}