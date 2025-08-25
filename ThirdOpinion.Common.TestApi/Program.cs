using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Extensions.NETCore.Setup;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SQS;
using ThirdOpinion.Common.Aws.DynamoDb;
using ThirdOpinion.Common.Aws.S3;
using ThirdOpinion.Common.Aws.SQS;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure AWS Options
var awsOptions = builder.Configuration.GetAWSOptions();

// Use AWS_PROFILE environment variable if set
var awsProfile = Environment.GetEnvironmentVariable("AWS_PROFILE");
if (!string.IsNullOrEmpty(awsProfile))
{
    awsOptions.Credentials = new Amazon.Runtime.CredentialManagement.CredentialProfileStoreChain()
        .TryGetAWSCredentials(awsProfile, out var credentials) 
        ? credentials 
        : FallbackCredentialsFactory.GetCredentials();
}

// Set region from configuration or environment
var region = builder.Configuration["AWS:Region"] ?? 
             Environment.GetEnvironmentVariable("AWS_REGION") ?? 
             "us-east-1";
awsOptions.Region = RegionEndpoint.GetBySystemName(region);

// Register AWS services
builder.Services.AddDefaultAWSOptions(awsOptions);
builder.Services.AddAWSService<IAmazonCognitoIdentityProvider>();
builder.Services.AddAWSService<IAmazonDynamoDB>();
builder.Services.AddAWSService<IAmazonS3>();
builder.Services.AddAWSService<IAmazonSQS>();

// Register DynamoDB context
builder.Services.AddSingleton<IDynamoDBContext>(sp =>
{
    var client = sp.GetRequiredService<IAmazonDynamoDB>();
    return new DynamoDBContext(client);
});

// Register custom services
builder.Services.AddSingleton<IDynamoDbRepository, DynamoDbRepository>();
builder.Services.AddSingleton<IS3Storage, S3Storage>();
builder.Services.AddSingleton<ISqsMessageQueue, SqsMessageQueue>();

// Add logging
builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.AddDebug();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Make the Program class accessible to test projects
public partial class Program { }