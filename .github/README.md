# ThirdOpinion.Common CI/CD Pipeline

This repository includes a comprehensive CI/CD pipeline for building, testing, and publishing the ThirdOpinion.Common NuGet packages.

## Workflows

### 1. CI/CD Pipeline (`ci-cd.yml`)

The main CI/CD workflow that handles building, testing, packaging, and publishing NuGet packages.

#### Recent Changes

##### Tag-Based Versioning System

The workflow now uses a **tag-based versioning system** instead of manual version management. Versions are automatically calculated from Git tags:

- **For `develop` branch**: 
  - Finds the latest `v*-preview` tag (e.g., `v1.1.11-preview`)
  - Increments the patch version (e.g., `1.1.11-preview` → `1.1.12-preview`)
  - If no preview tags exist, uses the latest stable tag and starts from the next patch version with `-preview` suffix

- **For `main`/`master` branch**:
  - Finds the latest stable tag (excluding preview tags)
  - Increments the minor version and resets patch to 0 (e.g., `1.1.12` → `1.2.0`)
  - If no stable tags exist, starts from `1.0.0`

- **For release events**:
  - Uses the version from the GitHub release tag directly

- **For other branches**:
  - Uses the latest tag as a base with branch name and run number suffix

##### Automatic Tag Creation

After successful package publishing, the workflow automatically creates and pushes version tags:

- **Development builds** (`develop` branch): Creates `v*-preview` tags (e.g., `v1.1.12-preview`)
  - Tag message: "Preview version X.Y.Z"
  
- **Production builds** (`main`/`master` branch): Creates stable `v*` tags (e.g., `v1.2.0`)
  - Tag message: "Release version X.Y.Z"

Tags are only created if they don't already exist, preventing duplicate tags.

##### GitHub Packages Publishing

The workflow now publishes packages to **GitHub Packages** in addition to AWS CodeArtifact:

- **Development packages**: Published to GitHub Packages for `develop` branch builds
- **Production packages**: Published to GitHub Packages for `main`/`master` branch builds
- **Authentication**: Uses `GITHUB_TOKEN` (automatically provided by GitHub Actions)
- **Security**: No credentials are stored in NuGet config; authentication uses API key directly
- **Package visibility**: Packages are private by default, tied to the repository/organization

**Package endpoint**: `https://nuget.pkg.github.com/Third-Opinion/index.json`

#### Workflow Triggers

- **Push to `main`/`master`**: Full CI/CD with stable version publishing
- **Push to `develop`**: Full CI/CD with preview version publishing
- **Push to `feature/**`**: Build and test only (no publishing)
- **Pull requests**: Build and test only
- **GitHub releases**: Full pipeline with release version publishing
- **Manual dispatch**: Optional functional tests and package publishing

#### Jobs

1. **Build and Test**
   - Builds the entire solution
   - Runs unit tests for both AWS and Common projects
   - Generates code coverage reports
   - Uploads test results and coverage as artifacts

2. **Functional Tests** (optional, manual trigger)
   - Spins up LocalStack for AWS service emulation
   - Runs comprehensive functional tests against AWS services
   - Only runs when explicitly requested

3. **Package Creation**
   - Creates NuGet packages with automatic versioning
   - Sets assembly versions using `-p:AssemblyVersion` and `-p:FileVersion` parameters
   - Uploads packages as artifacts

4. **Publishing**
   - Publishes to **AWS CodeArtifact** (development and production repositories)
   - Publishes to **GitHub Packages** (private packages)
   - Creates version tags automatically after successful publishing

### 2. Version Tags (`version-tags.yml`)

A standalone workflow for testing and managing version tags. This workflow demonstrates the tag-based versioning logic and can be manually triggered for testing purposes.

**Note**: Version tag creation is now integrated into the main `ci-cd.yml` workflow, so this workflow is primarily for testing and validation.

## Versioning Strategy

### Semantic Versioning

The project follows [Semantic Versioning](https://semver.org/) (SemVer):

- **Major.Minor.Patch** (e.g., `1.2.3`)
- **Pre-release versions** use `-preview` suffix (e.g., `1.1.12-preview`)

### Version Increment Rules

| Branch | Version Pattern | Increment Rule | Example |
|--------|----------------|----------------|---------|
| `develop` | `X.Y.Z-preview` | Patch + 1 | `1.1.11-preview` → `1.1.12-preview` |
| `main`/`master` | `X.Y.0` | Minor + 1, Patch = 0 | `1.1.12` → `1.2.0` |
| `feature/**` | `X.Y.Z-branchname.N` | Latest tag + branch suffix | `1.1.12-feature-xyz.123` |
| Release | `X.Y.Z` | From release tag | `1.2.0` |

### Tag Format

- **Preview tags**: `v1.1.12-preview` (for develop branch)
- **Release tags**: `v1.2.0` (for main/master branch)
- Tags are created automatically after successful package publishing

## Package Publishing

### Package Structure

The workflow publishes this NuGet package:

- `ThirdOpinion.Common` - Complete package including AWS services integration (S3, DynamoDB, SQS, Cognito), FHIR R4 healthcare integration, and utility functions

### AWS CodeArtifact

Packages are published to AWS CodeArtifact for both development and production:

- **Development repository**: Used for `develop` branch builds
- **Production repository**: Used for `main`/`master` branch builds and releases

**Configuration**: Uses repository variables:
- `AWS_CODEARTIFACT_DOMAIN`
- `AWS_CODEARTIFACT_DOMAIN_OWNER`
- `AWS_CODEARTIFACT_REPOSITORY`

**Authentication**: Uses AWS IAM role configured in repository secrets:
- `AWS_CODEARTIFACT_ROLE_ARN`

**Installing from CodeArtifact**:

```bash
# Configure AWS CLI with appropriate credentials
# Add CodeArtifact as a NuGet source
aws codeartifact login --tool dotnet --domain YOUR_DOMAIN --repository YOUR_REPOSITORY

# Install package
dotnet add package ThirdOpinion.Common
```

### GitHub Packages

Packages are published to GitHub Packages (private by default):

- **Endpoint**: `https://nuget.pkg.github.com/Third-Opinion/index.json`
- **Authentication**: Uses `GITHUB_TOKEN` (automatically provided, no configuration needed)
- **Security**: No credentials stored; API key passed directly to `dotnet nuget push`

**Installing from GitHub Packages**:

```bash
# Add GitHub Packages as a NuGet source
dotnet nuget add source https://nuget.pkg.github.com/Third-Opinion/index.json \
  --name github \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_GITHUB_TOKEN \
  --store-password-in-clear-text

# Install package
dotnet add package ThirdOpinion.Common
```

## Setup Requirements

### Repository Variables

Add these variables in your GitHub repository settings:

```
AWS_CODEARTIFACT_DOMAIN           # CodeArtifact domain name
AWS_CODEARTIFACT_DOMAIN_OWNER     # AWS account ID that owns the domain
AWS_CODEARTIFACT_REPOSITORY       # CodeArtifact repository name
```

### Repository Secrets

Add these secrets in your GitHub repository settings:

```
AWS_CODEARTIFACT_ROLE_ARN         # IAM role ARN for CodeArtifact access
AWS_FUNCTIONAL_TEST_ROLE_ARN      # IAM role ARN for functional tests (optional)
```

**Note**: `GITHUB_TOKEN` is automatically provided by GitHub Actions and does not need to be configured.

### Environment Protection

The workflow uses a `production` environment for publishing release packages. Configure this in your repository settings:

1. Go to **Settings** → **Environments**
2. Create `production` environment
3. Add protection rules (e.g., required reviewers)

## Usage Examples

### Automatic Publishing

**Development builds** (`develop` branch):
- Push to `develop` → Automatically builds, tests, packages, and publishes preview version
- Creates `v*-preview` tag automatically

**Production builds** (`main`/`master` branch):
- Merge to `main` → Automatically builds, tests, packages, and publishes stable version
- Creates `v*` tag automatically

**Release builds**:
- Create GitHub release → Automatically builds, tests, packages, and publishes release version

### Manual Publishing

1. Go to **Actions** tab in GitHub
2. Select **CI/CD Pipeline** workflow
3. Click **Run workflow**
4. Check **Publish NuGet packages**
5. Click **Run workflow**

### Running Functional Tests Manually

1. Go to **Actions** tab in GitHub
2. Select **CI/CD Pipeline** workflow
3. Click **Run workflow**
4. Check **Run functional tests**
5. Click **Run workflow**

### Testing Version Tags

To test the version tag calculation logic:

1. Go to **Actions** tab in GitHub
2. Select **Version Tags** workflow
3. Click **Run workflow**
4. Select the branch to test
5. Review the version calculation output

## Local Development

### Running Tests Locally

```bash
# Unit tests only
dotnet test ThirdOpinion.Common.Aws.UnitTests/
dotnet test ThirdOpinion.Common.UnitTests/

# Build packages locally
dotnet pack --configuration Release --output ./packages/
```

## Monitoring and Maintenance

### Code Quality

- All PRs require passing tests
- Code coverage reports are generated for each build

## Troubleshooting

### Version Calculation Issues

- **Issue**: Version not incrementing correctly
  - **Solution**: Ensure tags exist in the repository. For `develop`, check for `v*-preview` tags. For `main`/`master`, check for stable `v*` tags.

- **Issue**: Tag already exists error
  - **Solution**: This is expected if the tag was already created. The workflow will skip tag creation and continue.

### Publishing Issues

- **Issue**: GitHub Packages publish fails
  - **Solution**: Ensure `GITHUB_TOKEN` has `packages: write` permission (automatically provided in GitHub Actions)

- **Issue**: CodeArtifact publish fails
  - **Solution**: Verify AWS credentials and IAM role permissions. Check that repository variables are correctly configured.

- **Issue**: Duplicate package error
  - **Solution**: The workflow uses `--skip-duplicate` flag, so duplicate packages are automatically skipped without failing the workflow.

### Common Issues

1. **Functional tests fail**: Ensure LocalStack is properly started and health checks pass
2. **Package publishing fails**: Check that secrets are properly configured
3. **Version conflicts**: Ensure package versions are properly incremented
4. **Test timeouts**: Functional tests may need longer timeouts in CI environment

### Debugging

- Check the **Actions** tab for detailed logs
- Download test results and coverage artifacts

## Migration Notes

### From Manual Versioning

The workflow previously used manual version management from `.csproj` files. The new tag-based system:

- ✅ Eliminates manual version updates
- ✅ Provides automatic version incrementing
- ✅ Creates version tags automatically
- ✅ Supports multiple package registries (CodeArtifact + GitHub Packages)

### Old Version Determination (Deprecated)

The old version determination logic has been commented out in `ci-cd.yml` (lines 182-240). It used:
- Base version from `.csproj` file
- Run number suffixes for development builds
- Manual version management

This has been replaced by the tag-based system that calculates versions from Git tags.

## Related Documentation

- [Versioning Strategy](../../VERSIONING.md)
- [CodeArtifact Setup](../../README-CODEARTIFACT.md)
