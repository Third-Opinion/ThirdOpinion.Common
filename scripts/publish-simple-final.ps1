param(
    [string]$AwsProfile = "to-prod-admin",
    [string]$Version = "1.1.3",
    [string]$Configuration = "Release"
)

Write-Host "Publishing ThirdOpinion.Common to AWS CodeArtifact" -ForegroundColor Green
Write-Host "AWS Profile: $AwsProfile" -ForegroundColor Yellow
Write-Host "Version: $Version" -ForegroundColor Yellow
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow

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