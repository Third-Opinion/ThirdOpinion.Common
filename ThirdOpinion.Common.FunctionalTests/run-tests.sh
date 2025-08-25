#!/bin/bash

# Run functional tests with AWS credentials
# This script sets up the AWS profile for running functional tests against real AWS services

# Check if AWS_PROFILE is already set
if [ -z "$AWS_PROFILE" ]; then
    echo "Setting AWS_PROFILE to to-dev-admin..."
    export AWS_PROFILE=to-dev-admin
else
    echo "Using existing AWS_PROFILE: $AWS_PROFILE"
fi

# Verify AWS credentials are valid
echo "Verifying AWS credentials..."
aws sts get-caller-identity > /dev/null 2>&1
if [ $? -ne 0 ]; then
    echo "Error: AWS credentials are not valid or expired."
    echo "Please run: aws sso login --profile to-dev-admin"
    exit 1
fi

echo "AWS credentials verified successfully."
echo ""

# Run tests based on arguments
if [ $# -eq 0 ]; then
    echo "Running all functional tests..."
    dotnet test
elif [ "$1" == "s3" ]; then
    echo "Running S3 functional tests..."
    dotnet test --filter "FullyQualifiedName~S3FunctionalTests"
elif [ "$1" == "cognito" ]; then
    echo "Running Cognito functional tests..."
    dotnet test --filter "FullyQualifiedName~CognitoFunctionalTests"
elif [ "$1" == "dynamodb" ]; then
    echo "Running DynamoDB functional tests..."
    dotnet test --filter "FullyQualifiedName~DynamoDbFunctionalTests"
elif [ "$1" == "sqs" ]; then
    echo "Running SQS functional tests..."
    dotnet test --filter "FullyQualifiedName~SqsFunctionalTests"
else
    echo "Running tests with filter: $@"
    dotnet test "$@"
fi