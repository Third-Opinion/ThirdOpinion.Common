# ThirdOpinion.Common Build & Publishing Scripts

This directory contains scripts for building, packaging, and publishing ThirdOpinion.Common NuGet packages.

## Scripts Overview

### 📦 `build-packages.ps1` / `build-packages.sh`
**Purpose:** Build and package for local testing

**Features:**
- Auto-reads version from `src/ThirdOpinion.Common.csproj`
- Adds timestamp for unique local versions
- Supports custom prerelease tags
- Runs unit tests before packaging
- Creates packages in `./packages` directory

**PowerShell Usage:**
```powershell
# Default - creates package with version {csproj-version}-local.{timestamp}
.\build-packages.ps1

# Custom prerelease tag
.\build-packages.ps1 -PrereleaseTag "dev"
# Result: 1.1.6-dev.20250103120000

# Explicit version
.\build-packages.ps1 -Version "2.0.0-alpha.1"

# Custom output directory
.\build-packages.ps1 -OutputDir "./my-packages"

# Debug configuration
.\build-packages.ps1 -Configuration "Debug"
```

**Bash Usage:**
```bash
# Default - creates package with version {csproj-version}-local.{timestamp}
./build-packages.sh

# Custom configuration
./build-packages.sh Release

# Custom output directory
./build-packages.sh Release ./my-packages

# Explicit version
./build-packages.sh Release ./packages "2.0.0-alpha.1"

# Custom prerelease tag
./build-packages.sh Release ./packages "" "dev"
# Result: 1.1.6-dev.20250103120000
```

---

### 🚀 `publish-simple-final.ps1`
**Purpose:** Publish prerelease packages to AWS CodeArtifact

**Features:**
- **Auto-increments** revision number (1.1.6 → 1.1.7)
- **Always adds prerelease tag** (default: `-preview`)
- Reads version from `src/ThirdOpinion.Common.csproj`
- Publishes to AWS CodeArtifact
- Includes symbol packages (.snupkg)

**Default Behavior:**
1. Reads current version from csproj (e.g., `1.1.6`)
2. Increments revision/patch number (e.g., `1.1.7`)
3. Adds prerelease tag (e.g., `1.1.7-preview`)
4. Publishes to AWS CodeArtifact

**Usage Examples:**

```powershell
# Default - Auto-increment + preview tag
.\publish-simple-final.ps1
# If csproj has 1.1.6 → Publishes as 1.1.7-preview

# Custom prerelease tag
.\publish-simple-final.ps1 -PrereleaseTag "beta"
# If csproj has 1.1.6 → Publishes as 1.1.7-beta

.\publish-simple-final.ps1 -PrereleaseTag "rc"
# If csproj has 1.1.6 → Publishes as 1.1.7-rc

# No auto-increment (use csproj version as-is)
.\publish-simple-final.ps1 -NoAutoIncrement
# If csproj has 1.1.6 → Publishes as 1.1.6-preview

# Explicit version (overrides all auto-increment logic)
.\publish-simple-final.ps1 -Version "2.0.0-rc.1"
# Publishes exactly as 2.0.0-rc.1

# Custom AWS profile
.\publish-simple-final.ps1 -AwsProfile "my-aws-profile"

# Debug configuration
.\publish-simple-final.ps1 -Configuration "Debug"
```

**Parameters:**

| Parameter | Default | Description |
|-----------|---------|-------------|
| `AwsProfile` | `to-prod-admin` | AWS CLI profile to use |
| `Version` | *(auto)* | Explicit version (overrides auto-increment) |
| `Configuration` | `Release` | Build configuration |
| `PrereleaseTag` | `preview` | Prerelease suffix (preview, beta, rc, etc.) |
| `NoAutoIncrement` | *(switch)* | Don't increment version, use csproj as-is |

---

## Version Management

### Understanding Version Auto-Increment

The publish script automatically manages versioning:

| csproj Version | Script Behavior | Published Version |
|----------------|-----------------|-------------------|
| `1.1.6` | Default (auto-increment) | `1.1.7-preview` |
| `1.1.6` | `-PrereleaseTag "beta"` | `1.1.7-beta` |
| `1.1.6` | `-NoAutoIncrement` | `1.1.6-preview` |
| `1.1.6` | `-Version "2.0.0-rc.1"` | `2.0.0-rc.1` |

### When to Update csproj Version

**DO update** `src/ThirdOpinion.Common.csproj` version when:
- Publishing a stable release (remove prerelease tag)
- Moving to a new major or minor version
- After a series of previews, preparing for final release

**DON'T update** csproj version when:
- Publishing regular preview/beta builds (auto-increment handles it)
- Testing changes in development
- Creating experimental builds

### Prerelease Tag Conventions

| Tag | Use Case | Example |
|-----|----------|---------|
| `preview` | Default prerelease builds | `1.1.7-preview` |
| `beta` | Beta testing phase | `1.1.7-beta` |
| `rc` | Release candidate | `1.1.7-rc` |
| `alpha` | Early experimental | `1.1.7-alpha` |
| `dev` | Development builds | `1.1.7-dev` |
| `local` | Local testing | `1.1.6-local.{timestamp}` |

---

## Workflow Examples

### Daily Development Cycle

```powershell
# Make code changes...

# Build and test locally
.\scripts\build-packages.ps1

# Publish preview to AWS for team testing
.\scripts\publish-simple-final.ps1
# Result: Auto-increments and publishes as preview
```

### Beta Release Workflow

```powershell
# Ready for beta testing
.\scripts\publish-simple-final.ps1 -PrereleaseTag "beta"
# Result: 1.1.7-beta

# More beta iterations (auto-increments)
.\scripts\publish-simple-final.ps1 -PrereleaseTag "beta"
# Result: 1.1.8-beta
```

### Release Candidate Workflow

```powershell
# Feature complete, testing for release
.\scripts\publish-simple-final.ps1 -PrereleaseTag "rc"
# Result: 1.1.7-rc

# If issues found, fix and republish
.\scripts\publish-simple-final.ps1 -PrereleaseTag "rc"
# Result: 1.1.8-rc
```

### Stable Release Workflow

```powershell
# 1. Update csproj to final version (e.g., 1.2.0)
# Edit src/ThirdOpinion.Common.csproj: <Version>1.2.0</Version>

# 2. Publish without auto-increment, without prerelease tag
.\scripts\publish-simple-final.ps1 -Version "1.2.0"
# Result: 1.2.0 (stable release)

# 3. Resume preview builds
.\scripts\publish-simple-final.ps1
# Result: 1.2.1-preview
```

---

## AWS CodeArtifact Setup

### Prerequisites

1. **AWS CLI** installed and configured
2. **AWS credentials** with CodeArtifact permissions
3. **.NET SDK** 8.0 or later
4. **AWS CodeArtifact Credential Provider** (automatic via .NET SDK)

### Verifying Setup

```powershell
# Check AWS CLI
aws --version

# Check AWS credentials
aws sts get-caller-identity --profile to-prod-admin

# Check .NET SDK
dotnet --version
```

### Installing Packages from AWS CodeArtifact

```bash
# Add AWS CodeArtifact source (one-time setup)
dotnet nuget add source https://thirdopinion-442042533707.d.codeartifact.us-east-2.amazonaws.com/nuget/Thirdopinion_Nuget/v3/index.json --name aws-codeartifact

# Install specific version
dotnet add package ThirdOpinion.Common --version 1.1.7-preview

# List available versions
aws codeartifact list-package-versions \
  --domain thirdopinion \
  --repository Thirdopinion_Nuget \
  --format nuget \
  --package ThirdOpinion.Common \
  --profile to-prod-admin
```

---

## Troubleshooting

### Build Fails

```powershell
# Clean and rebuild
dotnet clean
dotnet restore
.\scripts\build-packages.ps1
```

### Tests Fail

```powershell
# Run tests manually to see detailed errors
dotnet test --verbosity detailed
```

### AWS Publishing Fails

```powershell
# Verify AWS credentials
aws sts get-caller-identity --profile to-prod-admin

# Check CodeArtifact permissions
aws codeartifact list-packages \
  --domain thirdopinion \
  --repository Thirdopinion_Nuget \
  --profile to-prod-admin
```

### Version Already Exists

```powershell
# Auto-increment will create a new version
.\scripts\publish-simple-final.ps1

# Or specify different prerelease tag
.\scripts\publish-simple-final.ps1 -PrereleaseTag "beta.2"
```

---

## Quick Reference

| Task | Command |
|------|---------|
| **Local test build** | `.\scripts\build-packages.ps1` |
| **Publish preview** | `.\scripts\publish-simple-final.ps1` |
| **Publish beta** | `.\scripts\publish-simple-final.ps1 -PrereleaseTag "beta"` |
| **Publish RC** | `.\scripts\publish-simple-final.ps1 -PrereleaseTag "rc"` |
| **Publish stable** | Update csproj, then `.\scripts\publish-simple-final.ps1 -Version "X.Y.Z"` |
| **Skip increment** | `.\scripts\publish-simple-final.ps1 -NoAutoIncrement` |

---

## Notes

- **Always test locally** with `build-packages.ps1` before publishing
- **Auto-increment is enabled by default** for AWS publishing
- **Prerelease tags are required** for all AWS publishes (safety feature)
- **Symbol packages** (.snupkg) are automatically included
- **Skip duplicate** is enabled - republishing same version is safe
