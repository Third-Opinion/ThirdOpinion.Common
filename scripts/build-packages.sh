#!/bin/bash

# ThirdOpinion.Common Package Build Script
# This script builds and packs all NuGet packages for local testing
#
# Usage Examples:
#   Default (local build with timestamp):
#     ./build-packages.sh
#     Result: Creates package with version from csproj + -local.{timestamp}
#
#   Custom configuration:
#     ./build-packages.sh Release
#     Result: Uses Release configuration with default version
#
#   Custom output directory:
#     ./build-packages.sh Release ./my-packages
#     Result: Outputs to ./my-packages
#
#   Explicit version:
#     ./build-packages.sh Release ./packages "2.0.0-alpha.1"
#     Result: Creates package with version 2.0.0-alpha.1
#
#   Custom prerelease tag:
#     ./build-packages.sh Release ./packages "" "dev"
#     Result: Creates package with csproj version + -dev.{timestamp}

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Function to extract version from csproj file
get_csproj_version() {
    local csproj_path="$1"

    if [ ! -f "$csproj_path" ]; then
        echo -e "${RED}Error: Could not find $csproj_path${NC}" >&2
        exit 1
    fi

    # Extract version using grep and sed
    version=$(grep -oP '<Version>\K[0-9.]+' "$csproj_path" || true)

    if [ -z "$version" ]; then
        echo -e "${RED}Error: Could not find version in $csproj_path${NC}" >&2
        exit 1
    fi

    echo "$version"
}

# Configuration
CONFIGURATION=${1:-Release}
OUTPUT_DIR=${2:-./packages}
VERSION=${3:-""}
PRERELEASE_TAG=${4:-local}

# Determine version to use
if [ -z "$VERSION" ]; then
    echo -e "${CYAN}Reading version from csproj...${NC}"
    CSPROJ_PATH="src/ThirdOpinion.Common.csproj"
    BASE_VERSION=$(get_csproj_version "$CSPROJ_PATH")
    TIMESTAMP=$(date +%Y%m%d%H%M%S)
    VERSION="$BASE_VERSION-$PRERELEASE_TAG.$TIMESTAMP"
    echo -e "${GREEN}Generated version: $VERSION${NC}"
fi

echo ""
echo -e "${GREEN}🔨 Building ThirdOpinion.Common NuGet Packages${NC}"
echo "=================================================="
echo -e "${YELLOW}Configuration:${NC} $CONFIGURATION"
echo -e "${YELLOW}Output Directory:${NC} $OUTPUT_DIR"
echo -e "${YELLOW}Version:${NC} $VERSION"
echo -e "${YELLOW}Prerelease Tag:${NC} $PRERELEASE_TAG"
echo ""

# Clean previous builds
echo -e "${YELLOW}🧹 Cleaning previous builds...${NC}"
dotnet clean --configuration $CONFIGURATION

# Create output directory
mkdir -p $OUTPUT_DIR
rm -f $OUTPUT_DIR/*.nupkg

# Restore dependencies
echo -e "${YELLOW}📦 Restoring dependencies...${NC}"
dotnet restore

# Build solution
echo -e "${YELLOW}🔨 Building solution...${NC}"
dotnet build --configuration $CONFIGURATION --no-restore

# Run unit tests
echo -e "${YELLOW}🧪 Running unit tests...${NC}"
dotnet test --configuration $CONFIGURATION --no-build --verbosity normal --filter "FullyQualifiedName!~IntegrationTests&FullyQualifiedName!~FunctionalTests"

# Package main project (includes all sub-projects)
echo -e "${YELLOW}📦 Creating NuGet package (combined)...${NC}"

echo "  📦 Packing ThirdOpinion.Common (includes all sub-projects)..."
dotnet pack "src/ThirdOpinion.Common.csproj" \
    --configuration $CONFIGURATION \
    --output $OUTPUT_DIR \
    -p:PackageVersion=$VERSION

# List generated packages
echo ""
echo -e "${GREEN}✅ Package build completed!${NC}"
echo -e "${YELLOW}Generated packages:${NC}"
ls -la $OUTPUT_DIR/*.nupkg

echo ""
echo -e "${GREEN}📋 Package Information:${NC}"
for package in $OUTPUT_DIR/*.nupkg; do
    if [ -f "$package" ]; then
        echo "  $(basename "$package")"
    fi
done

echo ""
echo -e "${YELLOW}💡 To test locally:${NC}"
echo "  1. Add local source: dotnet nuget add source $PWD/$OUTPUT_DIR --name local-packages"
echo "  2. Install package: dotnet add package ThirdOpinion.Common.Aws.DynamoDb --version $VERSION --source local-packages"
echo "  3. Remove source: dotnet nuget remove source local-packages"

echo ""
echo -e "${GREEN}🎉 Build script completed successfully!${NC}"