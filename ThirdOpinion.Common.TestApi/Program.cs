using Amazon.CognitoIdentityProvider;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Extensions.NETCore.Setup;
using Amazon.S3;
using Amazon.SQS;
using ThirdOpinion.Common.Aws.DynamoDb;
using ThirdOpinion.Common.Aws.S3;
using ThirdOpinion.Common.Aws.SQS;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure AWS Options
AWSOptions? awsOptions = builder.Configuration.GetAWSOptions();

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

WebApplication app = builder.Build();

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
public partial class Program
{
}