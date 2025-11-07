# ThirdOpinion.Common AWS CodeArtifact Publishing Script
#
# This script automatically:
# 1. Reads the current version from src/ThirdOpinion.Common.csproj
# 2. Increments the revision/patch number (e.g., 1.1.6 -> 1.1.7)
# 3. Adds a prerelease tag (default: -preview)
# 4. Publishes to AWS CodeArtifact
#
# Usage Examples:
#   Default (auto-increment + preview tag):
#     .\publish-simple-final.ps1
#     Result: If csproj has 1.1.6, publishes as 1.1.7-preview
#
#   Custom prerelease tag:
#     .\publish-simple-final.ps1 -PrereleaseTag beta
#     Result: If csproj has 1.1.6, publishes as 1.1.7-beta
#
#   No auto-increment (use csproj version as-is):
#     .\publish-simple-final.ps1 -NoAutoIncrement
#     Result: If csproj has 1.1.6, publishes as 1.1.6-preview
#
#   Explicit version (overrides auto-increment):
#     .\publish-simple-final.ps1 -Version "2.0.0-rc.1"
#     Result: Publishes as 2.0.0-rc.1 (ignores csproj version)

param(
    [string]$AwsProfile = "to-prod-admin",
    [string]$Version = "",  # If empty, auto-increments from csproj
    [string]$Configuration = "Release",
    [string]$PrereleaseTag = "preview",  # Prerelease suffix (e.g., preview, beta, rc)
    [switch]$NoAutoIncrement  # If set, uses version from csproj without incrementing
)

# Function to extract version from csproj file
function Get-CsProjVersion {
    param([string]$CsProjPath)

    $content = Get-Content $CsProjPath -Raw
    if ($content -match '<Version>([\d\.]+)</Version>') {
        return $matches[1]
    }
    throw "Could not find version in $CsProjPath"
}

# Function to increment revision number
function Get-IncrementedVersion {
    param([string]$CurrentVersion)

    $parts = $CurrentVersion -split '\.'
    if ($parts.Count -ne 3) {
        throw "Version must be in format Major.Minor.Patch (e.g., 1.1.6)"
    }

    $major = [int]$parts[0]
    $minor = [int]$parts[1]
    $patch = [int]$parts[2]
    $patch++

    return "$major.$minor.$patch"
}

# Determine version to use
if ([string]::IsNullOrWhiteSpace($Version)) {
    Write-Host "Reading version from csproj..." -ForegroundColor Cyan
    $csprojPath = "src/ThirdOpinion.Common.csproj"
    $baseVersion = Get-CsProjVersion -CsProjPath $csprojPath
    Write-Host "Base version from csproj: $baseVersion" -ForegroundColor White

    if ($NoAutoIncrement) {
        $Version = "$baseVersion-$PrereleaseTag"
        Write-Host "Using csproj version with prerelease tag (no increment)" -ForegroundColor Yellow
    } else {
        $incrementedVersion = Get-IncrementedVersion -CurrentVersion $baseVersion
        $Version = "$incrementedVersion-$PrereleaseTag"
        Write-Host "Auto-incremented to: $incrementedVersion" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Publishing ThirdOpinion.Common to AWS CodeArtifact" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Green
Write-Host "AWS Profile: $AwsProfile" -ForegroundColor Yellow
Write-Host "Version: $Version" -ForegroundColor Yellow
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Prerelease Tag: $PrereleaseTag" -ForegroundColor Yellow
Write-Host ""

try {
    # Verify AWS CLI
    Write-Host "Verifying AWS CLI..." -ForegroundColor Cyan
    $awsVersion = aws --version
    Write-Host "AWS CLI: $awsVersion" -ForegroundColor Green

    # Verify AWS credentials
    Write-Host "Verifying AWS credentials..." -ForegroundColor Cyan
    $awsIdentity = aws sts get-caller-identity --profile $AwsProfile --region us-east-2
    Write-Host "AWS credentials verified" -ForegroundColor Green

    # Build and package
    Write-Host "Building and packaging..." -ForegroundColor Cyan
    dotnet clean --configuration $Configuration
    dotnet restore
    dotnet build --configuration $Configuration --no-restore

    Write-Host "  Packing ThirdOpinion.Common (combined package with symbols)..." -ForegroundColor Cyan
    dotnet pack src/ThirdOpinion.Common.csproj `
        --configuration $Configuration `
        --output packages `
        -p:PackageVersion=$Version `
        -p:IncludeSymbols=true `
        -p:SymbolPackageFormat=snupkg
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to pack ThirdOpinion.Common"
    }

    Write-Host "  ✅ Generated packages:" -ForegroundColor Green
    Write-Host "    - ThirdOpinion.Common.$Version.nupkg (main package with embedded PDBs)" -ForegroundColor White
    Write-Host "    - ThirdOpinion.Common.$Version.snupkg (symbol package for symbol servers)" -ForegroundColor White

    # Configure NuGet source with CodeArtifact credential provider
    Write-Host "Configuring NuGet source..." -ForegroundColor Cyan
    $sourceUrl = "https://thirdopinion-442042533707.d.codeartifact.us-east-2.amazonaws.com/nuget/Thirdopinion_Nuget/v3/index.json"
    
    # Remove existing source
    dotnet nuget remove source aws-codeartifact 2>$null
    
    # Add CodeArtifact source (authentication handled by credential provider)
    dotnet nuget add source $sourceUrl --name aws-codeartifact

    # Publish package
    Write-Host "Publishing package..." -ForegroundColor Cyan
    $packageFile = "packages/ThirdOpinion.Common.$Version.nupkg"
    dotnet nuget push $packageFile --source aws-codeartifact --api-key any --skip-duplicate

    # Cleanup
    dotnet nuget remove source aws-codeartifact 2>$null

    Write-Host ""
    Write-Host "Package published successfully!" -ForegroundColor Green
    Write-Host "Package: ThirdOpinion.Common (combined package with all sub-projects)" -ForegroundColor White
    Write-Host "Version: $Version" -ForegroundColor White
    Write-Host "Repository: $sourceUrl" -ForegroundColor White

} catch {
    Write-Host "Publishing failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}