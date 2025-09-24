using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Textract;
using Amazon.Textract.Model;
using System.Text.Json;
using System.Text.Json.Serialization;
using ThirdOpinion.Common.Textract.Models;
using ThirdOpinion.Common.Textract.Services;

namespace ThirdOpinion.Common.Textract.Helpers;

public static class TextractHelpers
{
    // JSON settings for serialization
    public static readonly JsonSerializerOptions DefaultIndented = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static readonly JsonSerializerOptions DefaultRaw = new JsonSerializerOptions
    {
        PropertyNamingPolicy = null, // This ensures names aren't transformed
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Textract the file and save the result to an S3 object
    /// </summary>
    /// <param name="bucketName">S3 bucket name</param>
    /// <param name="textractService">TextractTextDetectionService instance</param>
    /// <param name="s3Client">AmazonS3Client instance</param>
    /// <param name="sourceKey">S3 key of the source file</param>
    /// <param name="resultObject">ResultObject to track processing</param>
    /// <returns>GetDocumentTextDetectionResponse or null if failed</returns>
    public static async Task<GetDocumentTextDetectionResponse?> TextractFile(string bucketName,
        TextractTextDetectionService textractService,
        IAmazonS3 s3Client, string sourceKey, ResultObject resultObject)
    {
        var dateTimeNow = DateTime.Now;
        resultObject.CreatedAt = dateTimeNow;

        // Start the job
        var jobId = await textractService.StartDocumentTextDetectionPollingAsync(bucketName, sourceKey);
        resultObject.SourceObjectKey = sourceKey;

        Console.WriteLine($"Starting Job ID={jobId}");
        resultObject.JobId = jobId;
        textractService.WaitForJobCompletion(jobId);

        // Get the job results
        var jobResult = textractService.GetJobResults(jobId);
        if (jobResult.Count > 1)
        {
            Console.WriteLine($"Multiple job results found for jobId={jobId}");
            throw new Exception($"Multiple job results found for jobId={jobId}");
        }

        if (jobResult.First().JobStatus == JobStatus.FAILED)
        {
            Console.WriteLine($"Job failed for file={sourceKey}");
            return null;
        }

        Console.WriteLine($"Job Completed, job Status={jobResult.First().JobStatus}");

        resultObject.JobStatus = jobResult.First().JobStatus;
        resultObject.TextractOutputObjectKey = $"{sourceKey}-textract-{dateTimeNow:yyMMddHHmmss}.json";
        resultObject.TextractOutputFilteredObjectKey = $"{sourceKey}-textract-filtered-{dateTimeNow:yyMMddHHmmss}.json";

        GetDocumentTextDetectionResponse? result = null;

        if (jobResult.First().Blocks.Count > 0)
        {
            result = jobResult.First();

            // Save the original textract output to an S3 object
            await s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucketName,
                Key = resultObject.TextractOutputObjectKey,
                ContentBody = JsonSerializer.Serialize(result, DefaultIndented)
            });

            Console.WriteLine(
                $"Original TextractOutput successfully written to TextractOutputObjectKey={resultObject.TextractOutputObjectKey}");
        }
        else
        {
            Console.WriteLine($"No textract output found for file: imageFile={sourceKey}");
        }

        return jobResult.First();
    }

    /// <summary>
    /// Read Textract results from S3 instead of processing new files
    /// </summary>
    /// <param name="bucketName">S3 bucket name</param>
    /// <param name="s3Client">AmazonS3Client instance</param>
    /// <param name="sourceKey">S3 key of the existing Textract result file</param>
    /// <param name="resultObject">ResultObject to track processing</param>
    /// <returns>GetDocumentTextDetectionResponse or null if failed</returns>
    public static async Task<GetDocumentTextDetectionResponse?> TextractFileFromS3(string bucketName,
        IAmazonS3 s3Client, string sourceKey, ResultObject resultObject)
    {
        var dateTimeNow = DateTime.Now;
        resultObject.CreatedAt = dateTimeNow;

        // Generate a unique job ID for tracking
        var jobId = Guid.NewGuid().ToString();
        resultObject.SourceObjectKey = sourceKey;

        Console.WriteLine($"Reading Job ID={jobId}");
        resultObject.JobId = jobId;

        // Get the job results from S3
        var resultFromS3 = await s3Client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = bucketName,
            Key = sourceKey
        });

        GetDocumentTextDetectionResponse jobResultFromS3;
        using (var streamReader = new StreamReader(resultFromS3.ResponseStream))
        {
            var jsonContent = await streamReader.ReadToEndAsync();
            jobResultFromS3 = JsonSerializer.Deserialize<GetDocumentTextDetectionResponse>(jsonContent)
                ?? throw new InvalidOperationException("Failed to deserialize Textract response from S3");
        }

        if (jobResultFromS3.Blocks.Count == 0)
        {
            Console.WriteLine($"Job failed for file={sourceKey}");
            return null;
        }

        Console.WriteLine($"Job Completed, job Status={jobResultFromS3.JobStatus}");

        resultObject.JobStatus = jobResultFromS3.JobStatus;
        resultObject.TextractOutputObjectKey = $"{sourceKey}-textract-{dateTimeNow:yyMMddHHmmss}.json";
        resultObject.TextractOutputFilteredObjectKey = $"{sourceKey}-textract-filtered-{dateTimeNow:yyMMddHHmmss}.json";

        return jobResultFromS3;
    }

    /// <summary>
    /// Get all Textract output files from the S3 bucket
    /// </summary>
    /// <param name="bucketName">S3 bucket name</param>
    /// <param name="s3Client">AmazonS3Client instance</param>
    /// <param name="prefix">Optional prefix to filter objects</param>
    /// <returns>List of S3 keys for Textract output files</returns>
    public static async Task<List<string>> GetAllTextTractedFiles(string bucketName, IAmazonS3 s3Client,
        string? prefix = null)
    {
        var filesToTextract = new List<string>();

        var listObjectsRequest = new ListObjectsV2Request
        {
            BucketName = bucketName,
            Prefix = prefix
        };

        try
        {
            ListObjectsV2Response listObjectsResponse;
            do
            {
                listObjectsResponse = await s3Client.ListObjectsV2Async(listObjectsRequest);

                foreach (var entry in listObjectsResponse.S3Objects)
                {
                    if (entry.Key.EndsWith(".json") && entry.Key.Contains("-textract-") &&
                        !entry.Key.Contains("-merged") && !entry.Key.Contains("-filtered"))
                    {
                        filesToTextract.Add(entry.Key);
                    }
                }

                listObjectsRequest.ContinuationToken = listObjectsResponse.NextContinuationToken;
            } while (listObjectsResponse.IsTruncated);
        }
        catch (AmazonS3Exception ex)
        {
            Console.WriteLine($"Error encountered on server. Message:'{ex.Message}' when listing objects");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unknown error encountered. Message:'{ex.Message}' when listing objects");
        }

        return filesToTextract;
    }

    /// <summary>
    /// Get all source document files (images/PDFs) from the S3 bucket that can be processed by Textract
    /// </summary>
    /// <param name="bucketName">S3 bucket name</param>
    /// <param name="s3Client">AmazonS3Client instance</param>
    /// <param name="prefix">Optional prefix to filter objects</param>
    /// <returns>List of S3 keys for source document files</returns>
    public static async Task<List<string>> GetAllReportFiles(string bucketName, IAmazonS3 s3Client,
        string? prefix = null)
    {
        var filesToTextract = new List<string>();

        var listObjectsRequest = new ListObjectsV2Request
        {
            BucketName = bucketName,
            Prefix = prefix
        };

        try
        {
            ListObjectsV2Response listObjectsResponse;
            do
            {
                listObjectsResponse = await s3Client.ListObjectsV2Async(listObjectsRequest);

                foreach (var entry in listObjectsResponse.S3Objects)
                {
                    if (entry.Key.EndsWith(".tiff") || entry.Key.EndsWith(".tif") || entry.Key.EndsWith(".pdf"))
                    {
                        filesToTextract.Add(entry.Key);
                    }
                }

                listObjectsRequest.ContinuationToken = listObjectsResponse.NextContinuationToken;
            } while (listObjectsResponse.IsTruncated);
        }
        catch (AmazonS3Exception ex)
        {
            Console.WriteLine($"Error encountered on server. Message:'{ex.Message}' when listing objects");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unknown error encountered. Message:'{ex.Message}' when listing objects");
        }

        return filesToTextract;
    }
}

/// <summary>
/// Result object for tracking Textract processing operations
/// </summary>
public class ResultObject
{
    public FileInformation? FileInformation { get; set; }
    public string? JobId { get; set; }
    public JobStatus? JobStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? SourceObjectKey { get; set; }
    public string? TextractOutputObjectKey { get; set; }
    public string? TextractOutputFilteredObjectKey { get; set; }
    public TextractOutputExtensions.TextractOutput? TextractResultFull { get; set; }
    public TextractOutputExtensions.TextractOutput? TextractResultNoGeo { get; set; }
    public TextractOutputExtensions.TextractOutput? TextractResultNoGeoNoRelationships { get; set; }
}

