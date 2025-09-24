using System.Text.RegularExpressions;

namespace ThirdOpinion.Common.Textract.Helpers;

public class FileHelpers
{
    public static List<FileInformation[]> GroupDocuments(List<string> filesToTextract)
    {
        return Group(filesToTextract, FileInformation.ExtractFileElementsFromDocument);
    }

    public static List<FileInformation[]> GroupLLmJson(List<string> filesToTextract)
    {
        return Group(filesToTextract, FileInformation.ExtractFileElementsFromLlmJson);
    }

    public static List<FileInformation[]> GroupSummmaryByPatient(List<string> filesToTextract)
    {
        return Group(filesToTextract, FileInformation.ExtractFileElementsFromLlmJson);
    }

    private static List<FileInformation[]> Group(List<string> filesToTextract,
        Func<string, FileInformation> extractFileInfo)
    {
        var fileComparer = new FileInformationComparer();

        var result = new List<FileInformation[]>();
        var groupedFiles = new Dictionary<FileInformation, List<FileInformation>>(fileComparer);

        foreach (var file in filesToTextract)
        {
            var fileInfo = extractFileInfo(file);
            if (fileInfo == null)
                continue;

            var found = false;
            foreach (var key in groupedFiles.Keys)
                if (fileComparer.Equals(key, fileInfo))
                {
                    groupedFiles[key].Add(fileInfo);
                    found = true;
                    break;
                }

            if (!found) groupedFiles.Add(fileInfo, new List<FileInformation> { fileInfo });
        }

        foreach (var group in groupedFiles.Values) result.Add(group.ToArray());

        return result;
    }

    public static List<FileInformation[]> GroupTexttract(List<string> filesToTextract)
    {
        return Group(filesToTextract, FileInformation.ExtractFileElementsFromTextractJson);
    }

    public static void WriteFileInformationToCsv(string filePath, List<string> files)
    {
        try
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("Name,DOB,ID,GUID,Page,FullKey");
                foreach (var file in files)
                {
                    var fileInfo = FileInformation.ExtractFileElementsFromDocument(file);
                    if (fileInfo != null)
                        writer.WriteLine(
                            $"{fileInfo.Name},{fileInfo.DOB},{fileInfo.Id},{fileInfo.Guid},{fileInfo.PageStart},{fileInfo.KeyFull}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing CSV file: {ex.Message}");
        }
    }
}

public class FileInformation
{
    public string Name { get; set; }
    public string DOB { get; set; }
    public string Id { get; set; }
    public string Guid { get; set; }
    public string PageStart { get; set; }

    public string? PageEnd { get; set; }

    public string? LlmType { get; set; }
    public string KeyFull { get; set; }
    public int FullSize { get; set; } = 0;
    public string KeyNoGeo { get; set; }
    public int NoGeoSize { get; set; } = 0;
    public string KeyNoGeoNoRelationships { get; set; }
    public int NoGeoNoRelationshipsSize { get; set; } = 0;

    public string? KeyReportPrefix { get; set; }
    public string? KeyReportSuffix { get; set; }

    public static FileInformation ExtractFileElementsFromDocument(string filename)
    {
        var pattern =
            @"^(?:.*/)?([\w\s\.-]+(?: Jr\.)?(?:-[\w\s]+)*)-((\d{8})~\d+~\d+)~([0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12})~pg(\d+)(?:[ \.].*)?$";
        var regex = new Regex(pattern);

        var match = regex.Match(filename);

        if (match.Success)
        {
            var name = match.Groups[1].Value;
            var dob = match.Groups[3].Value;
            var id = match.Groups[2].Value;
            var guid = match.Groups[4].Value;
            var page = match.Groups[5].Value;

            return new FileInformation
            {
                Name = name,
                DOB = dob,
                Id = id,
                Guid = guid,
                PageStart = page,
                KeyFull = filename
            };
        }

        Console.WriteLine(filename);

        return null;
    }

    public static FileInformation ExtractFileElementsFromLlmJson(string filename)
    {
        var pattern = @"^(?:.*/)?([\w\s\.-]+(?: Jr\.)?(?:-[\w\s]+)*)-((\d{8})~\d+~\d+)~([0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12})~pg(\d+)-pg(\d+).*-(\w+).json$";
        var regex = new Regex(pattern);

        var match = regex.Match(filename);

        if (match.Success)
        {
            var name = match.Groups[1].Value;
            var dob = match.Groups[3].Value;
            var id = match.Groups[2].Value;
            var guid = match.Groups[4].Value;
            var pageStart = match.Groups[5].Value;
            var pageEnd = match.Groups[6].Value;
            var llmToken = match.Groups[7].Value;

            Regex regex2 = new Regex(@"^(.*?~pg)[0-9]+-pg[0-9]+(\.[a-z]+)-textract");
            Match match2 = regex2.Match(filename);
            string? keyReportPrefix = null;
            string? keyReportSuffix = null;

            if (match2.Success)
            {
               keyReportPrefix = match2.Groups[1].Value;
               keyReportSuffix = match2.Groups[2].Value;
            }
            else
            {
                Console.WriteLine($"Error parsing Llm JSON file: {filename} - could not parse keyReport");
            }

            return new FileInformation
            {
                Name = name,
                DOB = dob,
                Id = id,
                Guid = guid,
                PageStart = pageStart,
                PageEnd = pageEnd,
                LlmType = llmToken,
                KeyFull = filename,
                KeyReportPrefix = keyReportPrefix,
                KeyReportSuffix = keyReportSuffix
            };
        }

        return null;
    }

    public static FileInformation ExtractFileElementsFromTextractJson(string filename)
    {
        var pattern =
            @"^(?:.*\/)?([\w\s\.-]+(?: Jr\.)?(?:-[\w\s]+)*)-((\d{8})~\d+~\d+)~([0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12})~pg(\d+)\..*$";
        var regex = new Regex(pattern);

        var match = regex.Match(filename);

        if (match.Success)
        {
            var name = match.Groups[1].Value;
            var dob = match.Groups[3].Value;
            var id = match.Groups[2].Value;
            var guid = match.Groups[4].Value;
            var page = match.Groups[5].Value;

            return new FileInformation
            {
                Name = name,
                DOB = dob,
                Id = id,
                Guid = guid,
                PageStart = page,
                KeyFull = filename
            };
        }

        Console.WriteLine(filename);

        return null;
    }

    public static FileInformation ExtractFileElementsFromMergedTextractJson(string filename)
    {
        var pattern =
            @"^(?:.*\/)?([\w\s\.-]+(?: Jr\.)?(?:-[\w\s]+)*)-((\d{8})~\d+~\d+)~([0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12})~pg(\d+)-pg(\d+)\..*$";
        var regex = new Regex(pattern);

        var match = regex.Match(filename);

        if (match.Success)
        {
            var name = match.Groups[1].Value;
            var dob = match.Groups[3].Value;
            var id = match.Groups[2].Value;
            var guid = match.Groups[4].Value;

            return new FileInformation
            {
                Name = name,
                DOB = dob,
                Id = id,
                Guid = guid,
                PageStart = match.Groups[5].Value,
                PageEnd = match.Groups[6].Value,
                KeyFull = filename
            };
        }

        Console.WriteLine(filename);

        return null;
    }

    public static bool IsSamePerson(FileInformation fileInfo1, FileInformation fileInfo2)
    {
        if (fileInfo1 == null || fileInfo2 == null)
            return false;

        return fileInfo1.Name == fileInfo2.Name && fileInfo1.DOB == fileInfo2.DOB &&
               fileInfo1.Guid == fileInfo2.Guid;
    }
}

public class FileInformationComparer : IEqualityComparer<FileInformation>
{
    public bool Equals(FileInformation x, FileInformation y)
    {
        if (x is null || y is null)
            return false;

        return x.Name == y.Name &&
               x.DOB == y.DOB &&
               x.Id == y.Id &&
               x.Guid == y.Guid;
    }

    public int GetHashCode(FileInformation obj)
    {
        var hash = 17;
        hash = hash * 23 + (obj.Name?.GetHashCode() ?? 0);
        hash = hash * 23 + (obj.DOB?.GetHashCode() ?? 0);
        hash = hash * 23 + (obj.Id?.GetHashCode() ?? 0);
        hash = hash * 23 + (obj.Guid?.GetHashCode() ?? 0);
        hash = hash * 23 + (obj.PageStart?.GetHashCode() ?? 0);
        return hash;
    }
}