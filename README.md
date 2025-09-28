# ThirdOpinion Common Libraries

Common utilities and libraries for ThirdOpinion applications, providing reusable components for AWS services, testing, and general functionality.

## Libraries

### AWS Integration
- **ThirdOpinion.Common.Aws.Cognito** - Amazon Cognito authentication and authorization utilities
- **ThirdOpinion.Common.Aws.DynamoDb** - DynamoDB repository patterns and utilities  
- **ThirdOpinion.Common.Aws.S3** - S3 storage abstractions and helpers
- **ThirdOpinion.Common.Aws.SQS** - SQS messaging and queue management

### Utilities
- **ThirdOpinion.Common.Misc** - General utility functions and helpers

## Development

This repository uses **GitFlow** for branch management and development workflow.

- 📋 See [GITFLOW.md](GITFLOW.md) for detailed development guidelines
- 🔧 Main development happens on `develop` branch
- 🚀 Features are developed on `feature/*` branches
- 📦 Releases are prepared on `release/*` branches
- 🚨 Critical fixes use `hotfix/*` branches

## Getting Started

### Prerequisites
- .NET 8.0 SDK
- Git Flow tools (`brew install git-flow` on macOS)

### Quick Start
```bash
# Clone the repository
git clone https://github.com/Third-Opinion/ThirdOpinion.Common.git
cd ThirdOpinion.Common

# Initialize GitFlow (use defaults)
git flow init -d

# Start a new feature
git flow feature start my-feature-name

# Build the solution
dotnet build

# Run tests
dotnet test
```

## CI/CD Pipeline

The repository includes a comprehensive CI/CD pipeline that:
- ✅ Builds and tests on all branches
- 📦 Creates NuGet packages for integration branches
- 🚀 Publishes to NuGet.org on releases
- 🔒 Runs security scans and code analysis
- ☁️ Tests against real AWS services (functional tests)

## Contributing

1. Follow the [GitFlow workflow](GITFLOW.md)
2. Create feature branches for new work: `git flow feature start feature-name`
3. Write tests for new functionality
4. Ensure all CI/CD checks pass
5. Submit pull requests to `develop` branch

## Package Management

Packages are automatically versioned and published:
- **Development builds**: `1.0.0-dev.YYYYMMDD.{commit}`
- **Release builds**: Semantic versioning (`1.2.0`, `1.2.1`, etc.)

## Architecture

The libraries follow clean architecture principles with:
- Dependency injection support
- Configuration-based setup
- Comprehensive logging
- Testable abstractions
- AWS SDK v4 integration

## Testing

- **Unit Tests**: Fast, isolated tests for business logic
- **Integration Tests**: Test AWS service integrations  
- **Functional Tests**: End-to-end testing with real AWS resources

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test ThirdOpinion.Common.Aws.UnitTests
```

## Documentation

- [GitFlow Workflow](GITFLOW.md) - Development process and branch management
- [API Documentation](docs/) - Auto-generated API docs (coming soon)

## License

This project is proprietary to ThirdOpinion. All rights reserved.