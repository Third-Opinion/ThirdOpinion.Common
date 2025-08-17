# ThirdOpinion.Common Package Build Script (PowerShell)
# This script builds and packs all NuGet packages for local testing

param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "./packages",
    [string]$Version = "1.0.0-local.$(Get-Date -Format 'yyyyMMddHHmmss')"
)

# Configuration
Write-Host "🔨 Building ThirdOpinion.Common NuGet Packages" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Configuration: " -ForegroundColor Yellow -NoNewline
Write-Host $Configuration
Write-Host "Output Directory: " -ForegroundColor Yellow -NoNewline
Write-Host $OutputDir
Write-Host "Version: " -ForegroundColor Yellow -NoNewline
Write-Host $Version
Write-Host ""

try {
    # Clean previous builds
    Write-Host "🧹 Cleaning previous builds..." -ForegroundColor Yellow
    dotnet clean --configuration $Configuration
    if ($LASTEXITCODE -ne 0) { throw "Clean failed" }

    # Create output directory
    if (Test-Path $OutputDir) {
        Remove-Item "$OutputDir/*.nupkg" -Force -ErrorAction SilentlyContinue
    } else {
        New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    }

    # Restore dependencies
    Write-Host "📦 Restoring dependencies..." -ForegroundColor Yellow
    dotnet restore
    if ($LASTEXITCODE -ne 0) { throw "Restore failed" }

    # Build solution
    Write-Host "🔨 Building solution..." -ForegroundColor Yellow
    dotnet build --configuration $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }

    # Run unit tests
    Write-Host "🧪 Running unit tests..." -ForegroundColor Yellow
    
    dotnet test ThirdOpinion.Common.Aws.UnitTests/ --configuration $Configuration --no-build --verbosity normal
    if ($LASTEXITCODE -ne 0) { throw "AWS unit tests failed" }
    
    dotnet test ThirdOpinion.Common.UnitTests/ --configuration $Configuration --no-build --verbosity normal
    if ($LASTEXITCODE -ne 0) { throw "Common unit tests failed" }

    # Package projects
    Write-Host "📦 Creating NuGet packages..." -ForegroundColor Yellow

    $Projects = @(
        "ThirdOpinion.Common.Aws.Cognito",
        "ThirdOpinion.Common.Aws.DynamoDb",
        "ThirdOpinion.Common.Aws.S3",
        "ThirdOpinion.Common.Aws.SQS",
        "ThirdOpinion.Common.Misc"
    )

    foreach ($project in $Projects) {
        Write-Host "  📦 Packing $project..." -ForegroundColor Cyan
        dotnet pack "$project/$project.csproj" `
            --configuration $Configuration `
            --no-build `
            --output $OutputDir `
            -p:PackageVersion=$Version
        if ($LASTEXITCODE -ne 0) { throw "Packing $project failed" }
    }

    # List generated packages
    Write-Host ""
    Write-Host "✅ Package build completed!" -ForegroundColor Green
    Write-Host "Generated packages:" -ForegroundColor Yellow
    Get-ChildItem "$OutputDir/*.nupkg" | ForEach-Object { 
        Write-Host "  $($_.Name)" -ForegroundColor White
    }

    Write-Host ""
    Write-Host "📋 Package Information:" -ForegroundColor Green
    Get-ChildItem "$OutputDir/*.nupkg" | ForEach-Object {
        Write-Host "  $($_.Name)" -ForegroundColor White
    }

    Write-Host ""
    Write-Host "💡 To test locally:" -ForegroundColor Yellow
    $absolutePath = Resolve-Path $OutputDir
    Write-Host "  1. Add local source: dotnet nuget add source `"$absolutePath`" --name local-packages" -ForegroundColor White
    Write-Host "  2. Install package: dotnet add package ThirdOpinion.Common.Aws.DynamoDb --version $Version --source local-packages" -ForegroundColor White
    Write-Host "  3. Remove source: dotnet nuget remove source local-packages" -ForegroundColor White

    Write-Host ""
    Write-Host "🎉 Build script completed successfully!" -ForegroundColor Green

} catch {
    Write-Host ""
    Write-Host "❌ Build failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}