namespace Misc.patients.PatientHuid;

public static class PatientMatcher
{
    public static float CalculateSamePerson(Patient patientBase, Patient patientToCompare)
    {
        // Define weights based on identifying importance
        const float nameWeight = 0.45f; // Names are critically important
        const float birthDateWeight = 0.35f; // Birth date is highly important
        const float sexWeight = 0.15f; // Sex is moderately important
        const float otherWeight = 0.05f; // Other attributes have minor importance

        var totalScore = 0.0f;
        var maxPossibleScore = 0.0f;

        // 1. NAME COMPARISON (First, Last, Middle)
        var nameScore = 0.0f;
        float nameFactors = 0;

        // Last name comparison (most important name factor)
        if (!string.IsNullOrEmpty(patientBase.Demographics.LastName) &&
            !string.IsNullOrEmpty(patientToCompare.Demographics.LastName))
        {
            float lastNameSimilarity = CalculateStringSimilarity(patientBase.Demographics.LastName,
                patientToCompare.Demographics.LastName);

            // Consider common name variations and typos
            if (lastNameSimilarity >= 0.9f)
                lastNameSimilarity = 1.0f; // Likely the same last name with minor variation
            else if
                (lastNameSimilarity >= 0.7f)
                lastNameSimilarity = 0.8f; // Possibly same last name with typo or variant spelling

            nameScore += lastNameSimilarity * 1.5f; // Last name has 1.5x weight
            nameFactors += 1.5f;
        }

        // First name comparison
        if (!string.IsNullOrEmpty(patientBase.Demographics.FirstName) &&
            !string.IsNullOrEmpty(patientToCompare.Demographics.FirstName))
        {
            float firstNameSimilarity = CalculateStringSimilarity(
                patientBase.Demographics.FirstName,
                patientToCompare.Demographics.FirstName);

            // Check for nickname matches (e.g., Robert/Bob, William/Bill)
            if (firstNameSimilarity < 0.7f)
            {
                string baseFirstName = patientBase.Demographics.FirstName.Trim().ToLower();
                string compareFirstName = patientToCompare.Demographics.FirstName.Trim().ToLower();

                if (AreCommonNicknames(baseFirstName, compareFirstName))
                    firstNameSimilarity = 0.9f; // High similarity for nickname matches
            }

            // Handle first initial only
            if ((patientBase.Demographics.FirstName.Length == 1 ||
                 patientToCompare.Demographics.FirstName.Length == 1) &&
                patientBase.Demographics.FirstName[0] == patientToCompare.Demographics.FirstName[0])
                firstNameSimilarity = 0.7f; // First initial matches

            nameScore += firstNameSimilarity;
            nameFactors += 1.0f;
        }

        // Calculate normalized name score based on first and last names only
        float normalizedNameScore = nameFactors > 0 ? nameScore / nameFactors : 0;
        
        // Middle name comparison - provide bonus on top of base score
        float middleNameBonus = 0.0f;
        if (!string.IsNullOrEmpty(patientBase.Demographics.MiddleName) &&
            !string.IsNullOrEmpty(patientToCompare.Demographics.MiddleName))
        {
            // For middle names, initial matches are common in records
            if ((patientBase.Demographics.MiddleName.Length == 1 ||
                 patientToCompare.Demographics.MiddleName.Length == 1) &&
                patientBase.Demographics.MiddleName[0] ==
                patientToCompare.Demographics.MiddleName[0])
            {
                middleNameBonus = 0.2f; // 20% bonus for middle initial match
            }
            else
            {
                float middleNameSimilarity = CalculateStringSimilarity(
                    patientBase.Demographics.MiddleName,
                    patientToCompare.Demographics.MiddleName);
                middleNameBonus = middleNameSimilarity * 0.2f; // Up to 20% bonus for full middle name match
            }
        }
        
        // Apply extra weight for exceptionally good name matches
        if (normalizedNameScore > 0.9f && nameFactors >= 2.0f)
            normalizedNameScore
                = Math.Min(1.0f, normalizedNameScore * 1.1f); // Boost very good matches

        totalScore += normalizedNameScore * nameWeight;
        
        // Apply middle name bonus directly to total score to ensure it's not lost to capping
        totalScore += middleNameBonus * nameWeight;
        maxPossibleScore += nameWeight;

        // 2. BIRTH DATE COMPARISON
        var birthDateScore = 0.0f;
        if (patientBase.Demographics.BirthDate.HasValue &&
            patientToCompare.Demographics.BirthDate.HasValue)
        {
            DateTime base_dob = patientBase.Demographics.BirthDate.Value.Date;
            DateTime compare_dob = patientToCompare.Demographics.BirthDate.Value.Date;

            // Exact match
            if (base_dob == compare_dob)
            {
                birthDateScore = 1.0f;
            }
            // Same month and day, different year (possible transcription error)
            else if (base_dob.Month == compare_dob.Month && base_dob.Day == compare_dob.Day)
            {
                int yearDifference = Math.Abs(base_dob.Year - compare_dob.Year);
                if (yearDifference == 1)
                    birthDateScore = 0.7f; // Likely typo in year
                else if (yearDifference <= 10)
                    birthDateScore = 0.3f; // Possibly decade error
                else
                    birthDateScore = 0.1f; // Different person, same birthday
            }
            // Transposed month/day (common in different date formats)
            else if (base_dob.Month == compare_dob.Day && base_dob.Day == compare_dob.Month &&
                     base_dob.Year == compare_dob.Year)
            {
                birthDateScore = 0.9f; // Very likely same person with format confusion
            }
            // Nearby dates (possible minor errors)
            else
            {
                TimeSpan difference = (base_dob - compare_dob).Duration();

                if (difference.TotalDays <= 3)
                    birthDateScore = 0.8f; // Very close dates, likely error
                else if (difference.TotalDays <= 31)
                    birthDateScore = 0.4f; // Within a month, possibly same person
                else
                    birthDateScore = 0.0f; // Different birth dates
            }

            totalScore += birthDateScore * birthDateWeight;
            maxPossibleScore += birthDateWeight;
        }
        else
        {
            maxPossibleScore
                += birthDateWeight / 2; //let always consider the birth date to some level
        }

        // 3. SEX COMPARISON
        var sexScore = 0.0f;
        if (patientBase.Demographics.Sex.HasValue && patientToCompare.Demographics.Sex.HasValue)
        {
            if (patientBase.Demographics.Sex.Value == patientToCompare.Demographics.Sex.Value)
                sexScore = 1.0f;
            else if (patientBase.Demographics.Sex.Value == Demographics.SexEnum.Unknown ||
                     patientToCompare.Demographics.Sex.Value == Demographics.SexEnum.Unknown)
                sexScore = 0.5f; // Unknown sex should be neutral
            else
                sexScore = 0.0f; // Different sex

            totalScore += sexScore * sexWeight;
            maxPossibleScore += sexWeight;
        }
        else
        {
            maxPossibleScore += sexWeight / 2; //always consider to some level
        }


        // 4. ADDITIONAL FACTORS
        var additionalScore = 0.0f;
        float additionalFactors = 0;

        // Phone number match is a strong indicator
        if (!string.IsNullOrEmpty(patientBase.Demographics.PhoneNumber) &&
            !string.IsNullOrEmpty(patientToCompare.Demographics.PhoneNumber))
        {
            // Normalize phone numbers (remove non-digits)
            var basePhone
                = new string(patientBase.Demographics.PhoneNumber.Where(char.IsDigit).ToArray());
            var comparePhone = new string(patientToCompare.Demographics.PhoneNumber
                .Where(char.IsDigit).ToArray());

            // Compare the last 7 digits (local number without area code)
            if (basePhone.Length >= 7 && comparePhone.Length >= 7)
            {
                string baseLast7 = basePhone.Substring(Math.Max(0, basePhone.Length - 7));
                string compareLast7 = comparePhone.Substring(Math.Max(0, comparePhone.Length - 7));

                if (baseLast7 == compareLast7)
                {
                    additionalScore += 1.0f;
                }
                else
                {
                    // Check for transposition errors in phone numbers
                    var transpositionErrors = 0;
                    for (var i = 0; i < Math.Min(baseLast7.Length, compareLast7.Length) - 1; i++)
                        if (baseLast7[i] == compareLast7[i + 1] &&
                            baseLast7[i + 1] == compareLast7[i])
                            transpositionErrors++;

                    if (transpositionErrors > 0 && transpositionErrors <= 2)
                        additionalScore += 0.7f; // Likely same number with transposition errors
                    else
                        additionalScore += 0.0f; // Different numbers
                }
            }
            else if (basePhone == comparePhone)
            {
                additionalScore += 1.0f;
            }

            additionalFactors += 1.0f;
        }

        // Death date - if both have death dates, they should match
        if (patientBase.Demographics.DeathDate.HasValue &&
            patientToCompare.Demographics.DeathDate.HasValue)
        {
            if (patientBase.Demographics.DeathDate.Value.Date ==
                patientToCompare.Demographics.DeathDate.Value.Date)
                additionalScore += 1.0f;
            else
                // Different death dates strongly indicates different people
                additionalScore -= 1.0f;

            additionalFactors += 1.0f;
        }
        // If one has death date and the other doesn't, likely different people or incomplete record
        else if (patientBase.Demographics.DeathDate.HasValue ||
                 patientToCompare.Demographics.DeathDate.HasValue)
        {
            additionalScore += 0.0f; // Neutral factor
            additionalFactors += 0.5f; // Half weight for missing data
        }

        // Add prefix/suffix match as minor factor
        if (!string.IsNullOrEmpty(patientBase.Demographics.Prefix) &&
            !string.IsNullOrEmpty(patientToCompare.Demographics.Prefix) &&
            string.Equals(patientBase.Demographics.Prefix, patientToCompare.Demographics.Prefix,
                StringComparison.OrdinalIgnoreCase))
        {
            additionalScore += 0.5f;
            additionalFactors += 0.5f;
        }

        if (!string.IsNullOrEmpty(patientBase.Demographics.Suffix) &&
            !string.IsNullOrEmpty(patientToCompare.Demographics.Suffix) &&
            string.Equals(patientBase.Demographics.Suffix, patientToCompare.Demographics.Suffix,
                StringComparison.OrdinalIgnoreCase))
        {
            additionalScore += 0.5f;
            additionalFactors += 0.5f;
        }

        // Calculate normalized additional score
        float normalizedAdditionalScore
            = additionalFactors > 0 ? additionalScore / additionalFactors : 0;
        totalScore += normalizedAdditionalScore * otherWeight;
        maxPossibleScore += otherWeight;

        // Calculate final probability
        float samePerson = maxPossibleScore > 0 ? totalScore / maxPossibleScore : 0;

        // Apply threshold adjustments
        // Very high name and DoB similarity almost certainly means same person
        if (normalizedNameScore > 0.9 && birthDateScore > 0.9 && sexScore > 0.5)
            samePerson = Math.Max(samePerson, 0.95f);

        // Different sex should significantly reduce confidence, especially with other factors
        if (sexScore == 0.0f && patientBase.Demographics.Sex.HasValue &&
            patientToCompare.Demographics.Sex.HasValue)
        {
            // Apply sex difference penalty - more severe if birth dates don't match well too
            if (birthDateScore < 0.3f)
                samePerson = Math.Min(samePerson, 0.1f); // Very low score for sex + birth date mismatch
            else
                samePerson = Math.Min(samePerson, 0.3f); // Moderate penalty for sex difference only
        }

        // Death date mismatch is a strong indicator of different people
        if (patientBase.Demographics.DeathDate.HasValue &&
            patientToCompare.Demographics.DeathDate.HasValue &&
            patientBase.Demographics.DeathDate.Value.Date != patientToCompare.Demographics.DeathDate.Value.Date)
        {
            samePerson = Math.Min(samePerson, 0.5f); // Cap at 50% for death date mismatch
        }

        return Math.Min(1.0f, Math.Max(0.0f, samePerson)); // Ensure result is between 0 and 1
    }

// Helper method to calculate string similarity using Levenshtein distance
    private static float CalculateStringSimilarity(string str1, string str2)
    {
        if (string.IsNullOrEmpty(str1) && string.IsNullOrEmpty(str2)) return 1.0f;
        if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2)) return 0.0f;

        // Normalize the strings for comparison
        str1 = str1.Trim().ToLower();
        str2 = str2.Trim().ToLower();

        // Exact matches get full similarity
        if (str1 == str2) return 1.0f;

        // Calculate normalized edit distance
        int distance = LevenshteinDistance(str1, str2);
        int maxLength = Math.Max(str1.Length, str2.Length);

        return 1.0f - (float)distance / maxLength;
    }

// Implementation of Levenshtein distance algorithm
    private static int LevenshteinDistance(string s, string t)
    {
        int n = s.Length;
        int m = t.Length;
        var d = new int[n + 1, m + 1];

        if (n == 0) return m;
        if (m == 0) return n;

        // Initialize the first row and column
        for (var i = 0; i <= n; i++) d[i, 0] = i;
        for (var j = 0; j <= m; j++) d[0, j] = j;

        // Fill the distance matrix
        for (var i = 1; i <= n; i++)
            for (var j = 1; j <= m; j++)
            {
                int cost = t[j - 1] == s[i - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }

        return d[n, m];
    }

// Helper method to check common nickname matches
    private static bool AreCommonNicknames(string name1, string name2)
    {
        // Common nickname dictionary (add more as needed)
        var nicknamePairs = new Dictionary<string, HashSet<string>>
        {
            { "robert", new HashSet<string> { "rob", "bob", "bobby" } },
            { "william", new HashSet<string> { "will", "bill", "billy", "liam" } },
            { "james", new HashSet<string> { "jim", "jimmy", "jamie" } },
            { "john", new HashSet<string> { "johnny", "jack", "jon" } },
            { "margaret", new HashSet<string> { "maggie", "meg", "peggy" } },
            { "elizabeth", new HashSet<string> { "liz", "beth", "betty", "eliza", "lisa" } },
            { "katherine", new HashSet<string> { "kate", "kathy", "katie", "kat", "catherine" } },
            { "michael", new HashSet<string> { "mike", "mick", "mikey" } },
            { "richard", new HashSet<string> { "rick", "dick", "richie" } },
            { "thomas", new HashSet<string> { "tom", "tommy" } },
            { "joseph", new HashSet<string> { "joe", "joey", "jose" } },
            { "charles", new HashSet<string> { "chuck", "charlie", "chas" } },
            { "patricia", new HashSet<string> { "pat", "patty", "trish" } },
            { "christopher", new HashSet<string> { "chris", "topher" } },
            { "jennifer", new HashSet<string> { "jen", "jenny" } },
            { "david", new HashSet<string> { "dave", "davey" } }
        };

        // Normalize names
        name1 = name1.ToLower().Trim();
        name2 = name2.ToLower().Trim();

        // Check direct matches
        if (name1 == name2) return true;

        // Check if either name is a known nickname of the other
        foreach (KeyValuePair<string, HashSet<string>> pair in nicknamePairs)
        {
            // Check if name1 is the formal name and name2 is a nickname
            if (pair.Key == name1 && pair.Value.Contains(name2)) return true;
            // Check if name2 is the formal name and name1 is a nickname
            if (pair.Key == name2 && pair.Value.Contains(name1)) return true;
            // Check if both are nicknames of the same formal name
            if (pair.Value.Contains(name1) && pair.Value.Contains(name2)) return true;
        }

        return false;
    }
}