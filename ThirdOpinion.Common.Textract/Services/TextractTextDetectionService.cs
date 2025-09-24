using Amazon.Textract;
using Amazon.Textract.Model;
using S3Object = Amazon.Textract.Model.S3Object;

namespace ThirdOpinion.Common.Textract.Services;

public class TextractTextDetectionService(IAmazonTextract textract)
{
    private readonly IAmazonTextract _textract = textract ?? throw new ArgumentNullException(nameof(textract));


    public async Task<string> StartDocumentAnalysisWithNotificationAsync(string s3Arn, string snsTopicArn,
        string roleArn)
    {
        ValidateStringParameter(s3Arn, nameof(s3Arn));
        ValidateStringParameter(snsTopicArn, nameof(snsTopicArn));
        ValidateStringParameter(roleArn, nameof(roleArn));

        var request = new StartDocumentAnalysisRequest
        {
            DocumentLocation = new DocumentLocation
            {
                S3Object = new S3Object
                {
                    Bucket = s3Arn.Split(':')[5],
                    Name = s3Arn.Split('/')[1]
                }
            },
            NotificationChannel = new NotificationChannel
            {
                RoleArn = roleArn,
                SNSTopicArn = snsTopicArn
            }
        };

        var response = await _textract.StartDocumentAnalysisAsync(request);
        return response.JobId;
    }

    public async Task<string> StartDocumentTextDetectionPollingAsync(string bucketName, string key)
    {
        ValidateStringParameter(bucketName, nameof(bucketName));
        ValidateStringParameter(key, nameof(key));

        var request = new StartDocumentTextDetectionRequest
        {
            DocumentLocation = new DocumentLocation
            {
                S3Object = new S3Object
                {
                    Bucket = bucketName,
                    Name = key
                }
            },
            OutputConfig = new OutputConfig { S3Bucket = bucketName, S3Prefix = "output/" }
        };

        var response = await _textract.StartDocumentTextDetectionAsync(request);
        return response.JobId;
    }

    public async Task<DetectDocumentTextResponse> DetectTextLocalAsync(string localPath)
    {
        ValidateStringParameter(localPath, nameof(localPath));

        if (!File.Exists(localPath))
        {
            Console.WriteLine($"File: {localPath} doesn't exist");
            return new DetectDocumentTextResponse();
        }

        var request = new DetectDocumentTextRequest
        {
            Document = new Document
            {
                Bytes = new MemoryStream(await File.ReadAllBytesAsync(localPath))
            }
        };

        return await _textract.DetectDocumentTextAsync(request);
    }

    public async Task<DetectDocumentTextResponse> DetectTextS3Async(string bucketName, string key)
    {
        ValidateStringParameter(bucketName, nameof(bucketName));
        ValidateStringParameter(key, nameof(key));

        var request = new DetectDocumentTextRequest
        {
            Document = new Document
            {
                S3Object = new S3Object
                {
                    Bucket = bucketName,
                    Name = key
                }
            }
        };

        return await _textract.DetectDocumentTextAsync(request);
    }

    public void WaitForJobCompletion(string jobId, int delayMs = 5000)
    {
        ValidateStringParameter(jobId, nameof(jobId));
        ValidatePositiveDelay(delayMs);

        while (!IsJobComplete(jobId)) Wait(delayMs);
    }

    public bool IsJobComplete(string jobId)
    {
        ValidateStringParameter(jobId, nameof(jobId));

        var response = _textract.GetDocumentTextDetectionAsync(new GetDocumentTextDetectionRequest
        {
            JobId = jobId
        }).Result;

        return response.JobStatus != JobStatus.IN_PROGRESS;
    }

    public List<GetDocumentTextDetectionResponse> GetJobResults(string jobId)
    {
        ValidateStringParameter(jobId, nameof(jobId));

        var result = new List<GetDocumentTextDetectionResponse>();
        string? nextToken = null;

        do
        {
            var response = _textract.GetDocumentTextDetectionAsync(new GetDocumentTextDetectionRequest
            {
                JobId = jobId,
                NextToken = nextToken
            }).Result;

            result.Add(response);
            nextToken = response.NextToken;

            if (nextToken != null) Wait();
        } while (nextToken != null);

        return result;
    }

    public void Print(DetectDocumentTextResponse response)
    {
        ValidateNotNull(response, nameof(response));
        PrintBlocks(response.Blocks);
    }

    public void Print(List<GetDocumentTextDetectionResponse> responses)
    {
        ValidateNotNull(responses, nameof(responses));
        if (responses.Count == 0)
            throw new ArgumentException("Response list cannot be empty.", nameof(responses));

        responses.ForEach(r => PrintBlocks(r.Blocks));
    }

    public List<string> GetLines(DetectDocumentTextResponse result)
    {
        ValidateNotNull(result, nameof(result));

        return result.Blocks
            .Where(block => block.BlockType == "LINE")
            .Select(block => block.Text)
            .ToList();
    }

    private void PrintBlocks(List<Block> blocks)
    {
        blocks.ForEach(block =>
        {
            if (block.BlockType == "LINE") Console.WriteLine(block.Text);
        });
    }

    private void Wait(int delayMs = 5000)
    {
        Task.Delay(delayMs).Wait();
        Console.Write(".");
    }

    private void ValidateStringParameter(string parameter, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(parameter))
            throw new ArgumentException($"{parameterName} cannot be null or empty.", parameterName);
    }

    private void ValidateNotNull(object parameter, string parameterName)
    {
        if (parameter == null)
            throw new ArgumentNullException(parameterName);
    }

    private void ValidatePositiveDelay(int delayMs)
    {
        if (delayMs <= 0)
            throw new ArgumentException("Delay must be greater than zero.", nameof(delayMs));
    }

}