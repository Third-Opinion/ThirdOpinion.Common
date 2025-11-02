using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Configuration;
using ThirdOpinion.Common.FunctionalTests.Infrastructure;
using Xunit.Abstractions;

namespace ThirdOpinion.Common.FunctionalTests.Tests;

[Collection("DynamoDB")]
public class DynamoDbFunctionalTests : BaseIntegrationTest
{
    private readonly List<string> _createdTables = new();
    private readonly string _testPrefix;

    public DynamoDbFunctionalTests(ITestOutputHelper output) : base(output)
    {
        _testPrefix = Configuration.GetValue<string>("TestSettings:TestResourcePrefix") ??
                      "functest";
    }

    protected override async Task CleanupTestResourcesAsync()
    {
        try
        {
            foreach (string tableName in _createdTables)
                try
                {
                    await DynamoDbClient.DeleteTableAsync(tableName);
                    WriteOutput($"Deleted table: {tableName}");
                }
                catch (ResourceNotFoundException)
                {
                    // Table already deleted
                }
                catch (Exception ex)
                {
                    WriteOutput($"Warning: Failed to delete table {tableName}: {ex.Message}");
                }
        }
        finally
        {
            await base.CleanupTestResourcesAsync();
        }
    }

    [Fact]
    public async Task CreateTable_WithValidSchema_ShouldSucceed()
    {
        // Arrange
        string tableName = GenerateTestResourceName("create-test");

        var createRequest = new CreateTableRequest
        {
            TableName = tableName,
            KeySchema = new List<KeySchemaElement>
            {
                new() { AttributeName = "Id", KeyType = KeyType.HASH }
            },
            AttributeDefinitions = new List<AttributeDefinition>
            {
                new() { AttributeName = "Id", AttributeType = ScalarAttributeType.S }
            },
            BillingMode = BillingMode.PAY_PER_REQUEST
        };

        // Act
        CreateTableResponse? response = await DynamoDbClient.CreateTableAsync(createRequest);
        _createdTables.Add(tableName);

        // Assert
        response.TableDescription.ShouldNotBeNull();
        response.TableDescription.TableName.ShouldBe(tableName);
        response.TableDescription.TableStatus.ShouldBe(TableStatus.CREATING);

        // Wait for table to be active
        await WaitForTableActiveAsync(tableName);

        WriteOutput($"Successfully created table: {tableName}");
    }

    [Fact]
    public async Task PutAndGetItem_WithComplexData_ShouldSucceed()
    {
        // Arrange
        string tableName = await CreateTestTableAsync("put-get-test");
        Dictionary<string, object> testData = TestDataBuilder.CreateDynamoDbTestData();

        var putRequest = new PutItemRequest
        {
            TableName = tableName,
            Item = ConvertToDynamoDbAttributes(testData)
        };

        // Act - Put item
        await DynamoDbClient.PutItemAsync(putRequest);

        // Act - Get item
        var getRequest = new GetItemRequest
        {
            TableName = tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new() { S = testData["Id"].ToString()! }
            }
        };

        GetItemResponse? getResponse = await DynamoDbClient.GetItemAsync(getRequest);

        // Assert
        getResponse.Item.ShouldNotBeEmpty();
        getResponse.Item["Id"].S.ShouldBe(testData["Id"].ToString());
        getResponse.Item["Name"].S.ShouldBe(testData["Name"].ToString());
        getResponse.Item["Email"].S.ShouldBe(testData["Email"].ToString());

        WriteOutput($"Successfully put and retrieved item from table: {tableName}");
    }

    [Fact]
    public async Task BatchWriteItems_WithMultipleItems_ShouldSucceed()
    {
        // Arrange
        string tableName = await CreateTestTableAsync("batch-write-test");
        var itemCount = 25;
        var testItems = new List<Dictionary<string, object>>();

        for (var i = 0; i < itemCount; i++)
            testItems.Add(TestDataBuilder.CreateDynamoDbTestData($"batch-item-{i}"));

        // Act
        List<WriteRequest> writeRequests = testItems.Select(item => new WriteRequest
        {
            PutRequest = new PutRequest
            {
                Item = ConvertToDynamoDbAttributes(item)
            }
        }).ToList();

        // Process in batches of 25 (DynamoDB limit)
        var batchRequest = new BatchWriteItemRequest
        {
            RequestItems = new Dictionary<string, List<WriteRequest>>
            {
                [tableName] = writeRequests
            }
        };

        BatchWriteItemResponse? response = await DynamoDbClient.BatchWriteItemAsync(batchRequest);

        // Assert
        response.UnprocessedItems.ShouldBeEmpty();

        // Verify items were written by scanning the table
        ScanResponse? scanResponse = await DynamoDbClient.ScanAsync(new ScanRequest
        {
            TableName = tableName
        });

        scanResponse.Items.Count.ShouldBe(itemCount);

        WriteOutput($"Successfully batch wrote {itemCount} items to table: {tableName}");
    }

    [Fact]
    public async Task QueryWithGSI_OnIndexedAttribute_ShouldReturnFilteredResults()
    {
        // Arrange
        string tableName = await CreateTestTableWithGSIAsync("query-gsi-test");

        // Insert test data with various ages
        var testItems = new List<Dictionary<string, object>>();
        var targetAge = 30;

        for (var i = 0; i < 10; i++)
        {
            Dictionary<string, object> item
                = TestDataBuilder.CreateDynamoDbTestData($"query-item-{i}");
            item["Age"] = i < 5 ? targetAge : targetAge + 10; // 5 items with target age
            testItems.Add(item);
        }

        foreach (Dictionary<string, object> item in testItems)
            await DynamoDbClient.PutItemAsync(new PutItemRequest
            {
                TableName = tableName,
                Item = ConvertToDynamoDbAttributes(item)
            });

        // Act - Query using GSI
        var queryRequest = new QueryRequest
        {
            TableName = tableName,
            IndexName = "AgeIndex",
            KeyConditionExpression = "Age = :age",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":age"] = new() { N = targetAge.ToString() }
            }
        };

        QueryResponse? queryResponse = await DynamoDbClient.QueryAsync(queryRequest);

        // Assert
        queryResponse.Items.Count.ShouldBe(5);
        queryResponse.Items.ShouldAllBe(item => item["Age"].N == targetAge.ToString());

        WriteOutput(
            $"Successfully queried GSI and found {queryResponse.Items.Count} items with age {targetAge}");
    }

    [Fact]
    public async Task ScanWithFilter_OnLargeDataset_ShouldReturnFilteredResults()
    {
        // Arrange
        string tableName = await CreateTestTableAsync("scan-filter-test");
        var totalItems = 50;
        var activeItemsCount = 0;

        // Insert test data
        for (var i = 0; i < totalItems; i++)
        {
            Dictionary<string, object> item
                = TestDataBuilder.CreateDynamoDbTestData($"scan-item-{i}");
            bool isActive = i % 3 == 0; // Every 3rd item is active
            item["IsActive"] = isActive;
            if (isActive) activeItemsCount++;

            await DynamoDbClient.PutItemAsync(new PutItemRequest
            {
                TableName = tableName,
                Item = ConvertToDynamoDbAttributes(item)
            });
        }

        // Act - Scan with filter
        var scanRequest = new ScanRequest
        {
            TableName = tableName,
            FilterExpression = "IsActive = :active",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":active"] = new() { BOOL = true }
            }
        };

        ScanResponse? scanResponse = await DynamoDbClient.ScanAsync(scanRequest);

        // Assert
        scanResponse.Items.Count.ShouldBe(activeItemsCount);
        scanResponse.Items.ShouldAllBe(item => item["IsActive"].BOOL == true);

        WriteOutput(
            $"Successfully scanned table and found {scanResponse.Items.Count} active items out of {totalItems} total");
    }

    [Fact]
    public async Task UpdateItem_WithConditionalExpression_ShouldSucceed()
    {
        // Arrange
        string tableName = await CreateTestTableAsync("update-test");
        Dictionary<string, object> testData = TestDataBuilder.CreateDynamoDbTestData();
        var itemId = testData["Id"].ToString()!;

        // Put initial item
        await DynamoDbClient.PutItemAsync(new PutItemRequest
        {
            TableName = tableName,
            Item = ConvertToDynamoDbAttributes(testData)
        });

        // Act - Update with condition
        var updateRequest = new UpdateItemRequest
        {
            TableName = tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new() { S = itemId }
            },
            UpdateExpression = "SET #name = :newName, #age = #age + :increment",
            ConditionExpression = "attribute_exists(Id)",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#name"] = "Name",
                ["#age"] = "Age"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":newName"] = new() { S = "Updated Name" },
                [":increment"] = new() { N = "1" }
            },
            ReturnValues = ReturnValue.ALL_NEW
        };

        UpdateItemResponse? updateResponse = await DynamoDbClient.UpdateItemAsync(updateRequest);

        // Assert
        updateResponse.Attributes.ShouldNotBeEmpty();
        updateResponse.Attributes["Name"].S.ShouldBe("Updated Name");
        updateResponse.Attributes["Age"].N.ShouldBe(((int)testData["Age"] + 1).ToString());

        WriteOutput($"Successfully updated item {itemId} with conditional expression");
    }

    [Fact]
    public async Task TransactWriteItems_WithMultipleOperations_ShouldSucceed()
    {
        // Arrange
        string tableName = await CreateTestTableAsync("transact-write-test");
        Dictionary<string, object> item1Data
            = TestDataBuilder.CreateDynamoDbTestData("transact-item-1");
        Dictionary<string, object> item2Data
            = TestDataBuilder.CreateDynamoDbTestData("transact-item-2");

        // Act - Transactional write
        var transactRequest = new TransactWriteItemsRequest
        {
            TransactItems = new List<TransactWriteItem>
            {
                new()
                {
                    Put = new Put
                    {
                        TableName = tableName,
                        Item = ConvertToDynamoDbAttributes(item1Data),
                        ConditionExpression = "attribute_not_exists(Id)"
                    }
                },
                new()
                {
                    Put = new Put
                    {
                        TableName = tableName,
                        Item = ConvertToDynamoDbAttributes(item2Data),
                        ConditionExpression = "attribute_not_exists(Id)"
                    }
                }
            }
        };

        await DynamoDbClient.TransactWriteItemsAsync(transactRequest);

        // Assert - Verify both items were created
        GetItemResponse? getItem1 = await DynamoDbClient.GetItemAsync(new GetItemRequest
        {
            TableName = tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new() { S = item1Data["Id"].ToString()! }
            }
        });

        GetItemResponse? getItem2 = await DynamoDbClient.GetItemAsync(new GetItemRequest
        {
            TableName = tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new() { S = item2Data["Id"].ToString()! }
            }
        });

        getItem1.Item.ShouldNotBeEmpty();
        getItem2.Item.ShouldNotBeEmpty();

        WriteOutput($"Successfully executed transactional write for 2 items in table: {tableName}");
    }

    private async Task<string> CreateTestTableAsync(string testName)
    {
        string tableName = GenerateTestResourceName(testName);

        var createRequest = new CreateTableRequest
        {
            TableName = tableName,
            KeySchema = new List<KeySchemaElement>
            {
                new() { AttributeName = "Id", KeyType = KeyType.HASH }
            },
            AttributeDefinitions = new List<AttributeDefinition>
            {
                new() { AttributeName = "Id", AttributeType = ScalarAttributeType.S }
            },
            BillingMode = BillingMode.PAY_PER_REQUEST
        };

        await DynamoDbClient.CreateTableAsync(createRequest);
        _createdTables.Add(tableName);

        await WaitForTableActiveAsync(tableName);
        return tableName;
    }

    private async Task<string> CreateTestTableWithGSIAsync(string testName)
    {
        string tableName = GenerateTestResourceName(testName);

        var createRequest = new CreateTableRequest
        {
            TableName = tableName,
            KeySchema = new List<KeySchemaElement>
            {
                new() { AttributeName = "Id", KeyType = KeyType.HASH }
            },
            AttributeDefinitions = new List<AttributeDefinition>
            {
                new() { AttributeName = "Id", AttributeType = ScalarAttributeType.S },
                new() { AttributeName = "Age", AttributeType = ScalarAttributeType.N }
            },
            GlobalSecondaryIndexes = new List<GlobalSecondaryIndex>
            {
                new()
                {
                    IndexName = "AgeIndex",
                    KeySchema = new List<KeySchemaElement>
                    {
                        new() { AttributeName = "Age", KeyType = KeyType.HASH }
                    },
                    Projection = new Projection { ProjectionType = ProjectionType.ALL }
                }
            },
            BillingMode = BillingMode.PAY_PER_REQUEST
        };

        await DynamoDbClient.CreateTableAsync(createRequest);
        _createdTables.Add(tableName);

        await WaitForTableActiveAsync(tableName);
        return tableName;
    }

    private async Task WaitForTableActiveAsync(string tableName)
    {
        TimeSpan timeout = TimeSpan.FromMinutes(2);
        DateTime start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < timeout)
        {
            try
            {
                DescribeTableResponse? describeResponse
                    = await DynamoDbClient.DescribeTableAsync(tableName);
                if (describeResponse.Table.TableStatus == TableStatus.ACTIVE) return;
            }
            catch (ResourceNotFoundException)
            {
                // Table still creating
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException($"Table {tableName} did not become active within timeout");
    }

    private static Dictionary<string, AttributeValue> ConvertToDynamoDbAttributes(
        Dictionary<string, object> data)
    {
        var attributes = new Dictionary<string, AttributeValue>();

        foreach (KeyValuePair<string, object> kvp in data)
            attributes[kvp.Key] = kvp.Value switch
            {
                string s => new AttributeValue { S = s },
                int i => new AttributeValue { N = i.ToString() },
                bool b => new AttributeValue { BOOL = b },
                DateTime dt => new AttributeValue { S = dt.ToString("O") },
                List<string> list => new AttributeValue { SS = list },
                Dictionary<string, object> dict => new AttributeValue
                    { M = ConvertToDynamoDbAttributes(dict) },
                _ => new AttributeValue { S = JsonSerializer.Serialize(kvp.Value) }
            };

        return attributes;
    }
}