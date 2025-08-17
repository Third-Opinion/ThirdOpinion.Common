using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;

namespace ThirdOpinion.Common.FunctionalTests.Infrastructure;

/// <summary>
/// Utility class for cleaning up AWS resources created during testing
/// </summary>
public class AwsResourceCleaner
{
    private readonly IAmazonCognitoIdentityProvider _cognitoClient;
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly IAmazonS3 _s3Client;
    private readonly IAmazonSQS _sqsClient;
    private readonly ILogger<AwsResourceCleaner> _logger;

    public AwsResourceCleaner(
        IAmazonCognitoIdentityProvider cognitoClient,
        IAmazonDynamoDB dynamoDbClient,
        IAmazonS3 s3Client,
        IAmazonSQS sqsClient,
        ILogger<AwsResourceCleaner> logger)
    {
        _cognitoClient = cognitoClient;
        _dynamoDbClient = dynamoDbClient;
        _s3Client = s3Client;
        _sqsClient = sqsClient;
        _logger = logger;
    }

    /// <summary>
    /// Clean up all test resources created with the specified prefix
    /// </summary>
    public async Task CleanupTestResourcesAsync(string testPrefix)
    {
        try
        {
            await Task.WhenAll(
                CleanupDynamoDbTablesAsync(testPrefix),
                CleanupS3BucketsAsync(testPrefix),
                CleanupSqsQueuesAsync(testPrefix),
                CleanupCognitoUserPoolsAsync(testPrefix)
            );
            
            _logger.LogInformation("Successfully cleaned up all test resources with prefix: {TestPrefix}", testPrefix);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up test resources with prefix: {TestPrefix}", testPrefix);
            throw;
        }
    }

    /// <summary>
    /// Clean up DynamoDB tables
    /// </summary>
    private async Task CleanupDynamoDbTablesAsync(string testPrefix)
    {
        try
        {
            var tablesResponse = await _dynamoDbClient.ListTablesAsync();
            var testTables = tablesResponse.TableNames.Where(name => name.StartsWith(testPrefix, StringComparison.OrdinalIgnoreCase));

            foreach (var tableName in testTables)
            {
                try
                {
                    await _dynamoDbClient.DeleteTableAsync(tableName);
                    _logger.LogInformation("Deleted DynamoDB table: {TableName}", tableName);
                }
                catch (Amazon.DynamoDBv2.Model.ResourceNotFoundException)
                {
                    // Table already deleted, ignore
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete DynamoDB table: {TableName}", tableName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up DynamoDB tables");
        }
    }

    /// <summary>
    /// Clean up S3 buckets
    /// </summary>
    private async Task CleanupS3BucketsAsync(string testPrefix)
    {
        try
        {
            var bucketsResponse = await _s3Client.ListBucketsAsync();
            var testBuckets = bucketsResponse.Buckets.Where(bucket => bucket.BucketName.StartsWith(testPrefix, StringComparison.OrdinalIgnoreCase));

            foreach (var bucket in testBuckets)
            {
                try
                {
                    // Delete all objects in the bucket first
                    await DeleteAllObjectsInBucketAsync(bucket.BucketName);
                    
                    // Delete the bucket
                    await _s3Client.DeleteBucketAsync(bucket.BucketName);
                    _logger.LogInformation("Deleted S3 bucket: {BucketName}", bucket.BucketName);
                }
                catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchBucket")
                {
                    // Bucket already deleted, ignore
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete S3 bucket: {BucketName}", bucket.BucketName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up S3 buckets");
        }
    }

    /// <summary>
    /// Delete all objects in an S3 bucket
    /// </summary>
    private async Task DeleteAllObjectsInBucketAsync(string bucketName)
    {
        try
        {
            var listRequest = new ListObjectsV2Request { BucketName = bucketName };
            ListObjectsV2Response listResponse;

            do
            {
                listResponse = await _s3Client.ListObjectsV2Async(listRequest);
                
                if (listResponse.S3Objects.Count > 0)
                {
                    var deleteRequest = new DeleteObjectsRequest
                    {
                        BucketName = bucketName,
                        Objects = listResponse.S3Objects.Select(obj => new KeyVersion { Key = obj.Key }).ToList()
                    };
                    
                    await _s3Client.DeleteObjectsAsync(deleteRequest);
                }
                
                listRequest.ContinuationToken = listResponse.NextContinuationToken;
            } while (listResponse.IsTruncated == true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete objects in S3 bucket: {BucketName}", bucketName);
        }
    }

    /// <summary>
    /// Clean up SQS queues
    /// </summary>
    private async Task CleanupSqsQueuesAsync(string testPrefix)
    {
        try
        {
            var queuesResponse = await _sqsClient.ListQueuesAsync(new Amazon.SQS.Model.ListQueuesRequest());
            var testQueues = queuesResponse.QueueUrls.Where(url => 
            {
                var queueName = url.Split('/').Last();
                return queueName.StartsWith(testPrefix, StringComparison.OrdinalIgnoreCase);
            });

            foreach (var queueUrl in testQueues)
            {
                try
                {
                    await _sqsClient.DeleteQueueAsync(queueUrl);
                    _logger.LogInformation("Deleted SQS queue: {QueueUrl}", queueUrl);
                }
                catch (QueueDoesNotExistException)
                {
                    // Queue already deleted, ignore
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete SQS queue: {QueueUrl}", queueUrl);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up SQS queues");
        }
    }

    /// <summary>
    /// Clean up Cognito User Pools
    /// </summary>
    private async Task CleanupCognitoUserPoolsAsync(string testPrefix)
    {
        try
        {
            var userPoolsResponse = await _cognitoClient.ListUserPoolsAsync(new ListUserPoolsRequest { MaxResults = 60 });
            var testUserPools = userPoolsResponse.UserPools.Where(pool => pool.Name.StartsWith(testPrefix, StringComparison.OrdinalIgnoreCase));

            foreach (var userPool in testUserPools)
            {
                try
                {
                    await _cognitoClient.DeleteUserPoolAsync(new DeleteUserPoolRequest { UserPoolId = userPool.Id });
                    _logger.LogInformation("Deleted Cognito User Pool: {UserPoolName} ({UserPoolId})", userPool.Name, userPool.Id);
                }
                catch (Amazon.DynamoDBv2.Model.ResourceNotFoundException)
                {
                    // User pool already deleted, ignore
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete Cognito User Pool: {UserPoolName} ({UserPoolId})", userPool.Name, userPool.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up Cognito User Pools");
        }
    }

    /// <summary>
    /// Check AWS service health
    /// </summary>
    public async Task<Dictionary<string, bool>> CheckServiceHealthAsync()
    {
        var healthStatus = new Dictionary<string, bool>();

        // Check DynamoDB
        try
        {
            await _dynamoDbClient.ListTablesAsync();
            healthStatus["DynamoDB"] = true;
        }
        catch
        {
            healthStatus["DynamoDB"] = false;
        }

        // Check S3
        try
        {
            await _s3Client.ListBucketsAsync();
            healthStatus["S3"] = true;
        }
        catch
        {
            healthStatus["S3"] = false;
        }

        // Check SQS
        try
        {
            await _sqsClient.ListQueuesAsync(new Amazon.SQS.Model.ListQueuesRequest());
            healthStatus["SQS"] = true;
        }
        catch
        {
            healthStatus["SQS"] = false;
        }

        // Check Cognito
        try
        {
            await _cognitoClient.ListUserPoolsAsync(new ListUserPoolsRequest { MaxResults = 1 });
            healthStatus["Cognito"] = true;
        }
        catch
        {
            healthStatus["Cognito"] = false;
        }

        return healthStatus;
    }
}