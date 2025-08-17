using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using ThirdOpinion.Common.FunctionalTests.Infrastructure;
using Xunit.Abstractions;
using Shouldly;
using System.Text;

namespace ThirdOpinion.Common.FunctionalTests.Tests;

[Collection("S3")]
public class S3FunctionalTests : BaseIntegrationTest
{
    private readonly string _testPrefix;
    private readonly List<string> _createdBuckets = new();
    private readonly Dictionary<string, List<string>> _createdObjects = new();
    
    public S3FunctionalTests(ITestOutputHelper output) : base(output)
    {
        _testPrefix = Configuration.GetValue<string>("TestSettings:TestResourcePrefix") ?? "functest";
    }

    protected override async Task CleanupTestResourcesAsync()
    {
        try
        {
            foreach (var bucket in _createdBuckets)
            {
                try
                {
                    // Delete all objects in bucket first
                    if (_createdObjects.ContainsKey(bucket))
                    {
                        foreach (var objectKey in _createdObjects[bucket])
                        {
                            try
                            {
                                await S3Client.DeleteObjectAsync(bucket, objectKey);
                            }
                            catch (Exception ex)
                            {
                                WriteOutput($"Warning: Failed to delete object {objectKey} in bucket {bucket}: {ex.Message}");
                            }
                        }
                    }

                    // Delete the bucket
                    await S3Client.DeleteBucketAsync(bucket);
                    WriteOutput($"Deleted bucket: {bucket}");
                }
                catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchBucket")
                {
                    // Bucket already deleted
                }
                catch (Exception ex)
                {
                    WriteOutput($"Warning: Failed to delete bucket {bucket}: {ex.Message}");
                }
            }
        }
        finally
        {
            await base.CleanupTestResourcesAsync();
        }
    }

    [Fact]
    public async Task CreateBucket_WithValidName_ShouldSucceed()
    {
        // Arrange
        var bucketName = GenerateTestResourceName("create-test").ToLowerInvariant();
        
        // Act
        var response = await S3Client.PutBucketAsync(new PutBucketRequest
        {
            BucketName = bucketName
        });
        _createdBuckets.Add(bucketName);

        // Assert
        response.HttpStatusCode.ShouldBe(System.Net.HttpStatusCode.OK);

        // Verify bucket exists
        var listResponse = await S3Client.ListBucketsAsync();
        listResponse.Buckets.ShouldContain(b => b.BucketName == bucketName);
        
        WriteOutput($"Successfully created bucket: {bucketName}");
    }

    [Fact]
    public async Task PutAndGetObject_WithTextContent_ShouldSucceed()
    {
        // Arrange
        var bucketName = await CreateTestBucketAsync("put-get-test");
        var objectKey = "test-file.txt";
        var content = "This is test content for S3 functional testing.";
        
        var putRequest = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            ContentBody = content,
            ContentType = "text/plain"
        };

        // Act - Put object
        var putResponse = await S3Client.PutObjectAsync(putRequest);
        TrackObject(bucketName, objectKey);

        // Act - Get object
        var getResponse = await S3Client.GetObjectAsync(bucketName, objectKey);

        // Assert
        putResponse.HttpStatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        putResponse.ETag.ShouldNotBeNullOrEmpty();

        getResponse.HttpStatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        getResponse.Key.ShouldBe(objectKey);
        getResponse.Headers.ContentType.ShouldBe("text/plain");

        using var reader = new StreamReader(getResponse.ResponseStream);
        var retrievedContent = await reader.ReadToEndAsync();
        retrievedContent.ShouldBe(content);
        
        WriteOutput($"Successfully put and retrieved object: {objectKey} in bucket: {bucketName}");
    }

    [Fact]
    public async Task PutObject_WithBinaryContent_ShouldSucceed()
    {
        // Arrange
        var bucketName = await CreateTestBucketAsync("binary-test");
        var objectKey = "test-binary.dat";
        var binaryData = TestDataBuilder.CreateBinaryTestData(1024); // 1KB of test data
        
        var putRequest = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            InputStream = new MemoryStream(binaryData),
            ContentType = "application/octet-stream"
        };

        // Act
        var putResponse = await S3Client.PutObjectAsync(putRequest);
        TrackObject(bucketName, objectKey);

        var getResponse = await S3Client.GetObjectAsync(bucketName, objectKey);

        // Assert
        putResponse.HttpStatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        getResponse.ContentLength.ShouldBe(binaryData.Length);

        using var memoryStream = new MemoryStream();
        await getResponse.ResponseStream.CopyToAsync(memoryStream);
        var retrievedData = memoryStream.ToArray();
        
        retrievedData.ShouldBe(binaryData);
        
        WriteOutput($"Successfully put and retrieved binary object: {objectKey} ({binaryData.Length} bytes)");
    }

    [Fact]
    public async Task ListObjects_WithMultipleObjects_ShouldReturnAll()
    {
        // Arrange
        var bucketName = await CreateTestBucketAsync("list-test");
        var objectCount = 5;
        var objectKeys = new List<string>();

        for (int i = 0; i < objectCount; i++)
        {
            var objectKey = $"test-object-{i:D3}.txt";
            objectKeys.Add(objectKey);
            
            await S3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucketName,
                Key = objectKey,
                ContentBody = $"Content for object {i}",
                ContentType = "text/plain"
            });
            TrackObject(bucketName, objectKey);
        }

        // Act
        var listResponse = await S3Client.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = bucketName
        });

        // Assert
        listResponse.S3Objects.Count.ShouldBe(objectCount);
        foreach (var expectedKey in objectKeys)
        {
            listResponse.S3Objects.ShouldContain(obj => obj.Key == expectedKey);
        }
        
        WriteOutput($"Successfully listed {listResponse.S3Objects.Count} objects in bucket: {bucketName}");
    }

    [Fact]
    public async Task CopyObject_BetweenKeys_ShouldSucceed()
    {
        // Arrange
        var bucketName = await CreateTestBucketAsync("copy-test");
        var sourceKey = "source-file.txt";
        var destinationKey = "destination-file.txt";
        var content = "Content to be copied";

        // Create source object
        await S3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucketName,
            Key = sourceKey,
            ContentBody = content,
            ContentType = "text/plain"
        });
        TrackObject(bucketName, sourceKey);

        // Act
        var copyResponse = await S3Client.CopyObjectAsync(new CopyObjectRequest
        {
            SourceBucket = bucketName,
            SourceKey = sourceKey,
            DestinationBucket = bucketName,
            DestinationKey = destinationKey
        });
        TrackObject(bucketName, destinationKey);

        // Assert
        copyResponse.HttpStatusCode.ShouldBe(System.Net.HttpStatusCode.OK);

        // Verify destination object exists and has correct content
        var getResponse = await S3Client.GetObjectAsync(bucketName, destinationKey);
        using var reader = new StreamReader(getResponse.ResponseStream);
        var copiedContent = await reader.ReadToEndAsync();
        copiedContent.ShouldBe(content);
        
        WriteOutput($"Successfully copied object from {sourceKey} to {destinationKey}");
    }

    [Fact]
    public async Task DeleteObject_ExistingObject_ShouldSucceed()
    {
        // Arrange
        var bucketName = await CreateTestBucketAsync("delete-test");
        var objectKey = "file-to-delete.txt";

        await S3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            ContentBody = "Content to be deleted",
            ContentType = "text/plain"
        });

        // Verify object exists
        var getResponse = await S3Client.GetObjectAsync(bucketName, objectKey);
        getResponse.HttpStatusCode.ShouldBe(System.Net.HttpStatusCode.OK);

        // Act
        var deleteResponse = await S3Client.DeleteObjectAsync(bucketName, objectKey);

        // Assert
        deleteResponse.HttpStatusCode.ShouldBe(System.Net.HttpStatusCode.NoContent);

        // Verify object no longer exists
        await Should.ThrowAsync<AmazonS3Exception>(async () =>
        {
            await S3Client.GetObjectAsync(bucketName, objectKey);
        });
        
        WriteOutput($"Successfully deleted object: {objectKey} from bucket: {bucketName}");
    }

    [Fact]
    public async Task GetObjectMetadata_WithCustomMetadata_ShouldReturnMetadata()
    {
        // Arrange
        var bucketName = await CreateTestBucketAsync("metadata-test");
        var objectKey = "metadata-file.txt";
        var customMetadata = new Dictionary<string, string>
        {
            ["test-metadata-1"] = "value1",
            ["test-metadata-2"] = "value2"
        };

        var putRequest = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            ContentBody = "Content with metadata",
            ContentType = "text/plain"
        };

        foreach (var metadata in customMetadata)
        {
            putRequest.Metadata.Add(metadata.Key, metadata.Value);
        }

        await S3Client.PutObjectAsync(putRequest);
        TrackObject(bucketName, objectKey);

        // Act
        var metadataResponse = await S3Client.GetObjectMetadataAsync(bucketName, objectKey);

        // Assert
        metadataResponse.HttpStatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        metadataResponse.Headers.ContentType.ShouldBe("text/plain");

        foreach (var expectedMetadata in customMetadata)
        {
            var expectedKey = $"x-amz-meta-{expectedMetadata.Key}";
            metadataResponse.Metadata.Keys.ShouldContain(expectedKey);
            metadataResponse.Metadata[expectedKey].ShouldBe(expectedMetadata.Value);
        }
        
        WriteOutput($"Successfully retrieved metadata for object: {objectKey}");
    }

    [Fact]
    public async Task GeneratePresignedUrl_ForGetOperation_ShouldAllowAccess()
    {
        // Arrange
        var bucketName = await CreateTestBucketAsync("presigned-test");
        var objectKey = "presigned-file.txt";
        var content = "Content accessible via presigned URL";

        await S3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            ContentBody = content,
            ContentType = "text/plain"
        });
        TrackObject(bucketName, objectKey);

        // Act
        var presignedUrl = S3Client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(15)
        });

        // Assert
        presignedUrl.ShouldNotBeNullOrEmpty();
        presignedUrl.ShouldContain(bucketName);
        presignedUrl.ShouldContain(objectKey);
        presignedUrl.ShouldContain("X-Amz-Signature");
        
        WriteOutput($"Successfully generated presigned URL for object: {objectKey}");
    }

    [Fact]
    public async Task MultipartUpload_LargeFile_ShouldSucceed()
    {
        // Arrange
        var bucketName = await CreateTestBucketAsync("multipart-test");
        var objectKey = "large-file.dat";
        var partSize = 5 * 1024 * 1024; // 5MB minimum part size
        var totalSize = partSize * 2; // 10MB total
        var testData = TestDataBuilder.CreateBinaryTestData(totalSize);

        // Act - Initiate multipart upload
        var initiateResponse = await S3Client.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            ContentType = "application/octet-stream"
        });
        TrackObject(bucketName, objectKey);

        var uploadId = initiateResponse.UploadId;
        var parts = new List<PartETag>();

        try
        {
            // Upload parts
            var partNumber = 1;
            for (int offset = 0; offset < totalSize; offset += partSize)
            {
                var currentPartSize = Math.Min(partSize, totalSize - offset);
                var partData = new byte[currentPartSize];
                Array.Copy(testData, offset, partData, 0, currentPartSize);

                var uploadResponse = await S3Client.UploadPartAsync(new UploadPartRequest
                {
                    BucketName = bucketName,
                    Key = objectKey,
                    UploadId = uploadId,
                    PartNumber = partNumber,
                    InputStream = new MemoryStream(partData)
                });

                parts.Add(new PartETag
                {
                    PartNumber = partNumber,
                    ETag = uploadResponse.ETag
                });

                partNumber++;
            }

            // Complete multipart upload
            var completeResponse = await S3Client.CompleteMultipartUploadAsync(new CompleteMultipartUploadRequest
            {
                BucketName = bucketName,
                Key = objectKey,
                UploadId = uploadId,
                PartETags = parts
            });

            // Assert
            completeResponse.HttpStatusCode.ShouldBe(System.Net.HttpStatusCode.OK);

            // Verify uploaded object
            var metadataResponse = await S3Client.GetObjectMetadataAsync(bucketName, objectKey);
            metadataResponse.ContentLength.ShouldBe(totalSize);
            
            WriteOutput($"Successfully completed multipart upload for {totalSize} bytes in {parts.Count} parts");
        }
        catch
        {
            // Abort multipart upload on failure
            await S3Client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest
            {
                BucketName = bucketName,
                Key = objectKey,
                UploadId = uploadId
            });
            throw;
        }
    }

    private async Task<string> CreateTestBucketAsync(string testName)
    {
        var bucketName = GenerateTestResourceName(testName).ToLowerInvariant();
        
        await S3Client.PutBucketAsync(new PutBucketRequest
        {
            BucketName = bucketName
        });
        _createdBuckets.Add(bucketName);
        
        // Wait for bucket to be available
        await Task.Delay(1000);
        
        return bucketName;
    }

    private void TrackObject(string bucketName, string objectKey)
    {
        if (!_createdObjects.ContainsKey(bucketName))
        {
            _createdObjects[bucketName] = new List<string>();
        }
        _createdObjects[bucketName].Add(objectKey);
    }
}