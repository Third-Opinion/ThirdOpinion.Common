#!/bin/bash

# Script to run tests with coverage reporting
# Usage: ./run-tests-with-coverage.sh

set -e

echo "Running tests with code coverage..."

# Clean previous test results
rm -rf TestResults/
rm -rf coverage/

# Run tests with coverage
dotnet test ThirdOpinion.Common.UnitTests/ThirdOpinion.Common.UnitTests.csproj \
    --settings ThirdOpinion.Common.UnitTests/.runsettings \
    --collect:"XPlat Code Coverage" \
    --results-directory TestResults \
    --logger trx \
    --logger "console;verbosity=detailed"

# Find the coverage file
COVERAGE_FILE=$(find TestResults -name "coverage.cobertura.xml" | head -1)

if [ -f "$COVERAGE_FILE" ]; then
    echo "Coverage report generated: $COVERAGE_FILE"
    
    # Install and run reportgenerator if available
    if command -v reportgenerator &> /dev/null; then
        echo "Generating HTML coverage report..."
        reportgenerator \
            "-reports:$COVERAGE_FILE" \
            "-targetdir:coverage" \
            "-reporttypes:Html"
        echo "HTML coverage report generated in: coverage/index.html"
    else
        echo "Install reportgenerator for HTML reports: dotnet tool install -g dotnet-reportgenerator-globaltool"
    fi
else
    echo "No coverage file found"
fi

echo "Test run completed!"