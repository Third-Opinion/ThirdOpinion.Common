# S3 Service Patterns and Best Practices

## Overview

The ThirdOpinion.Common S3 service provides a robust abstraction layer over AWS S3, implementing common patterns and best practices for object storage operations.

## Core Features

- Stream-based operations for memory efficiency
- Automatic retry with exponential backoff
- Pre-signed URL generation
- Multi-part upload support for large files
- Metadata and tagging support
- Server-side encryption options

## Common Patterns

### 1. Large File Upload with Progress

```csharp
public class LargeFileUploader
{
    private readonly IS3Service _s3Service;
    private readonly ILogger<LargeFileUploader> _logger;
    
    public async Task UploadLargeFileAsync(
        string bucketName,
        string key,
        Stream fileStream,
        IProgress<long> progress = null)
    {
        const int partSize = 5 * 1024 * 1024; // 5 MB parts
        var uploadId = await _s3Service.InitiateMultipartUploadAsync(bucketName, key);
        
        try
        {
            var parts = new List<CompletedPart>();
            var partNumber = 1;
            var totalBytes = fileStream.Length;
            var uploadedBytes = 0L;
            
            byte[] buffer = new byte[partSize];
            int bytesRead;
            
            while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                using var partStream = new MemoryStream(buffer, 0, bytesRead);
                
                var response = await _s3Service.UploadPartAsync(
                    bucketName,
                    key,
                    uploadId,
                    partNumber,
                    partStream);
                
                parts.Add(new CompletedPart
                {
                    ETag = response.ETag,
                    PartNumber = partNumber
                });
                
                uploadedBytes += bytesRead;
                progress?.Report(uploadedBytes);
                
                _logger.LogInformation(
                    "Uploaded part {PartNumber} ({UploadedMB}/{TotalMB} MB)",
                    partNumber,
                    uploadedBytes / (1024 * 1024),
                    totalBytes / (1024 * 1024));
                
                partNumber++;
            }
            
            await _s3Service.CompleteMultipartUploadAsync(
                bucketName,
                key,
                uploadId,
                parts);
        }
        catch
        {
            await _s3Service.AbortMultipartUploadAsync(bucketName, key, uploadId);
            throw;
        }
    }
}
```

### 2. Secure File Sharing with Pre-signed URLs

```csharp
public class SecureFileSharing
{
    private readonly IS3Service _s3Service;
    
    public async Task<string> GenerateSecureDownloadLinkAsync(
        string bucketName,
        string key,
        TimeSpan expiration,
        string clientIpAddress = null)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucketName,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiration),
            Protocol = Protocol.HTTPS
        };
        
        // Optional: Restrict to specific IP
        if (!string.IsNullOrEmpty(clientIpAddress))
        {
            request.ResponseHeaderOverrides.ContentDisposition = 
                $"attachment; filename=\"{Path.GetFileName(key)}\"";
            request.ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256;
        }
        
        return await _s3Service.GetPreSignedURLAsync(request);
    }
    
    public async Task<string> GenerateSecureUploadLinkAsync(
        string bucketName,
        string key,
        TimeSpan expiration,
        string contentType = null,
        long? maxFileSize = null)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucketName,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expiration),
            Protocol = Protocol.HTTPS
        };
        
        if (!string.IsNullOrEmpty(contentType))
        {
            request.ContentType = contentType;
        }
        
        if (maxFileSize.HasValue)
        {
            request.Headers["Content-Length-Range"] = $"0,{maxFileSize}";
        }
        
        return await _s3Service.GetPreSignedURLAsync(request);
    }
}
```

### 3. Batch Operations with Error Recovery

```csharp
public class S3BatchProcessor
{
    private readonly IS3Service _s3Service;
    private readonly ILogger<S3BatchProcessor> _logger;
    
    public async Task<BatchResult> ProcessBatchAsync(
        string sourceBucket,
        string destinationBucket,
        List<string> keys,
        Func<Stream, Task<Stream>> processor)
    {
        var result = new BatchResult();
        var semaphore = new SemaphoreSlim(5); // Process 5 files concurrently
        
        var tasks = keys.Select(async key =>
        {
            await semaphore.WaitAsync();
            
            try
            {
                // Download
                using var sourceStream = await _s3Service.GetFileStreamAsync(
                    sourceBucket, key);
                
                // Process
                using var processedStream = await processor(sourceStream);
                
                // Upload
                var newKey = $"processed/{key}";
                await _s3Service.UploadFileAsync(
                    destinationBucket,
                    newKey,
                    processedStream);
                
                result.Successful.Add(key);
                _logger.LogInformation("Successfully processed {Key}", key);
            }
            catch (Exception ex)
            {
                result.Failed.Add(new FailedItem
                {
                    Key = key,
                    Error = ex.Message
                });
                
                _logger.LogError(ex, "Failed to process {Key}", key);
            }
            finally
            {
                semaphore.Release();
            }
        });
        
        await Task.WhenAll(tasks);
        return result;
    }
}
```

### 4. Lifecycle Management and Archival

```csharp
public class S3LifecycleManager
{
    private readonly IS3Service _s3Service;
    
    public async Task SetupLifecycleRulesAsync(string bucketName)
    {
        var configuration = new LifecycleConfiguration
        {
            Rules = new List<LifecycleRule>
            {
                // Archive logs after 30 days
                new LifecycleRule
                {
                    Id = "archive-logs",
                    Status = LifecycleRuleStatus.Enabled,
                    Filter = new LifecycleFilter
                    {
                        LifecycleFilterPredicate = new LifecyclePrefixPredicate
                        {
                            Prefix = "logs/"
                        }
                    },
                    Transitions = new List<LifecycleTransition>
                    {
                        new LifecycleTransition
                        {
                            Days = 30,
                            StorageClass = S3StorageClass.StandardInfrequentAccess
                        },
                        new LifecycleTransition
                        {
                            Days = 90,
                            StorageClass = S3StorageClass.Glacier
                        }
                    }
                },
                // Delete temp files after 7 days
                new LifecycleRule
                {
                    Id = "cleanup-temp",
                    Status = LifecycleRuleStatus.Enabled,
                    Filter = new LifecycleFilter
                    {
                        LifecycleFilterPredicate = new LifecyclePrefixPredicate
                        {
                            Prefix = "temp/"
                        }
                    },
                    Expiration = new LifecycleRuleExpiration
                    {
                        Days = 7
                    }
                }
            }
        };
        
        await _s3Service.PutLifecycleConfigurationAsync(bucketName, configuration);
    }
}
```

### 5. Content Caching and CDN Integration

```csharp
public class S3CdnService
{
    private readonly IS3Service _s3Service;
    
    public async Task UploadWithCacheControlAsync(
        string bucketName,
        string key,
        Stream content,
        string contentType,
        CacheSettings cacheSettings)
    {
        var metadata = new Dictionary<string, string>
        {
            ["Cache-Control"] = cacheSettings.ToCacheControlHeader(),
            ["Content-Type"] = contentType
        };
        
        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            InputStream = content,
            Metadata = metadata,
            CannedACL = S3CannedACL.PublicRead // If serving via CloudFront
        };
        
        // Add ETag for cache validation
        if (cacheSettings.GenerateETag)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            var hash = md5.ComputeHash(content);
            request.MD5Digest = Convert.ToBase64String(hash);
            content.Position = 0; // Reset stream position
        }
        
        await _s3Service.PutObjectAsync(request);
    }
}

public class CacheSettings
{
    public int MaxAgeSeconds { get; set; } = 3600;
    public bool Public { get; set; } = true;
    public bool Immutable { get; set; } = false;
    public bool GenerateETag { get; set; } = true;
    
    public string ToCacheControlHeader()
    {
        var parts = new List<string>();
        
        parts.Add(Public ? "public" : "private");
        parts.Add($"max-age={MaxAgeSeconds}");
        
        if (Immutable)
            parts.Add("immutable");
        
        return string.Join(", ", parts);
    }
}
```

## Performance Optimization

### Parallel Downloads

```csharp
public async Task<byte[]> DownloadFileInParallelAsync(
    string bucketName,
    string key,
    int parallelism = 4)
{
    var metadata = await _s3Service.GetObjectMetadataAsync(bucketName, key);
    var fileSize = metadata.ContentLength;
    var chunkSize = fileSize / parallelism;
    
    var tasks = new Task<byte[]>[parallelism];
    
    for (int i = 0; i < parallelism; i++)
    {
        var start = i * chunkSize;
        var end = (i == parallelism - 1) ? fileSize - 1 : start + chunkSize - 1;
        
        tasks[i] = DownloadRangeAsync(bucketName, key, start, end);
    }
    
    var chunks = await Task.WhenAll(tasks);
    
    // Combine chunks
    var result = new byte[fileSize];
    var offset = 0;
    
    foreach (var chunk in chunks)
    {
        Buffer.BlockCopy(chunk, 0, result, offset, chunk.Length);
        offset += chunk.Length;
    }
    
    return result;
}

private async Task<byte[]> DownloadRangeAsync(
    string bucketName,
    string key,
    long start,
    long end)
{
    var request = new GetObjectRequest
    {
        BucketName = bucketName,
        Key = key,
        ByteRange = new ByteRange(start, end)
    };
    
    using var response = await _s3Service.GetObjectAsync(request);
    using var memoryStream = new MemoryStream();
    
    await response.ResponseStream.CopyToAsync(memoryStream);
    return memoryStream.ToArray();
}
```

## Security Best Practices

### 1. Encryption at Rest

```csharp
// Server-side encryption with AWS-managed keys
var request = new PutObjectRequest
{
    BucketName = bucketName,
    Key = key,
    ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
};

// Server-side encryption with KMS
var kmsRequest = new PutObjectRequest
{
    BucketName = bucketName,
    Key = key,
    ServerSideEncryptionMethod = ServerSideEncryptionMethod.AWSKMS,
    ServerSideEncryptionKeyManagementServiceKeyId = "arn:aws:kms:..."
};
```

### 2. Access Control

```csharp
// Bucket policy for restricted access
var bucketPolicy = @"{
    ""Version"": ""2012-10-17"",
    ""Statement"": [{
        ""Effect"": ""Allow"",
        ""Principal"": {
            ""AWS"": ""arn:aws:iam::ACCOUNT:role/MyRole""
        },
        ""Action"": [""s3:GetObject""],
        ""Resource"": ""arn:aws:s3:::my-bucket/*"",
        ""Condition"": {
            ""IpAddress"": {
                ""aws:SourceIp"": [""192.168.1.0/24""]
            }
        }
    }]
}";
```

## Error Handling

```csharp
public async Task<T> ExecuteWithRetryAsync<T>(
    Func<Task<T>> operation,
    int maxRetries = 3)
{
    var retryPolicy = Policy
        .Handle<AmazonS3Exception>(ex => 
            ex.StatusCode == HttpStatusCode.ServiceUnavailable ||
            ex.StatusCode == HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(
            maxRetries,
            retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (exception, timeSpan, retryCount, context) =>
            {
                _logger.LogWarning(
                    "Retry {RetryCount} after {TimeSpan}s due to {Exception}",
                    retryCount,
                    timeSpan.TotalSeconds,
                    exception.Message);
            });
    
    return await retryPolicy.ExecuteAsync(operation);
}
```

## Common Gotchas

1. **Stream Position**: Always reset stream position to 0 after reading for hash calculation
2. **Key Naming**: Use forward slashes (/) for folder structure, avoid special characters
3. **Region Consistency**: Ensure bucket region matches your configured AWS region
4. **CORS Configuration**: Configure CORS for browser-based uploads
5. **Request Limits**: S3 has request rate limits - implement exponential backoff

## Testing Patterns

```csharp
// Use LocalStack or MinIO for local S3 testing
public class S3ServiceTests
{
    private readonly IS3Service _s3Service;
    private readonly string _testBucket = "test-bucket";
    
    [Fact]
    public async Task UploadAndDownload_ShouldPreserveContent()
    {
        // Arrange
        var key = $"test/{Guid.NewGuid()}.txt";
        var content = "Test content";
        using var uploadStream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        
        // Act
        await _s3Service.UploadFileAsync(_testBucket, key, uploadStream);
        using var downloadStream = await _s3Service.GetFileStreamAsync(_testBucket, key);
        
        // Assert
        using var reader = new StreamReader(downloadStream);
        var downloadedContent = await reader.ReadToEndAsync();
        
        Assert.Equal(content, downloadedContent);
        
        // Cleanup
        await _s3Service.DeleteFileAsync(_testBucket, key);
    }
}
```

## Related Documentation

- [Getting Started](../getting-started.md)
- [DynamoDB Patterns](dynamodb-patterns.md)
- [Troubleshooting](../troubleshooting.md)