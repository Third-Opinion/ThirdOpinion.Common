# Debugging ThirdOpinion.Common NuGet Packages

This document explains how to step into the ThirdOpinion.Common library code while debugging.

## What's Configured

The ThirdOpinion.Common projects are now configured to generate debug symbols with:

- **Portable PDB files** - Cross-platform debug symbols
- **Embedded source code** - Source files embedded in PDB for offline debugging
- **SourceLink** - Integration with GitHub for automatic source retrieval
- **Symbol packages (.snupkg)** - Separate symbol packages for NuGet

## Visual Studio Configuration

To enable stepping into ThirdOpinion.Common code in Visual Studio:

1. **Disable "Just My Code"**
   - Go to Tools → Options → Debugging → General
   - Uncheck "Enable Just My Code"

2. **Enable Source Link Support**
   - In the same dialog, check "Enable Source Link support"

3. **Enable Symbol Server**
   - Go to Tools → Options → Debugging → Symbols
   - If publishing to a symbol server, add the symbol server URL
   - For local development, ensure "Load only specified modules" is unchecked

4. **Enable Source Server Support**
   - Go to Tools → Options → Debugging → General
   - Check "Enable source server support"

## Visual Studio Code / Rider Configuration

### VS Code (launch.json)

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": ".NET Core Launch",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/bin/Debug/net8.0/YourApp.dll",
      "args": [],
      "cwd": "${workspaceFolder}",
      "stopAtEntry": false,
      "console": "internalConsole",
      "justMyCode": false,
      "enableStepFiltering": false,
      "symbolOptions": {
        "searchPaths": [],
        "searchMicrosoftSymbolServer": true,
        "searchNuGetOrgSymbolServer": true
      },
      "sourceFileMap": {
        "/Path/To/Sources": "${workspaceFolder}"
      }
    }
  ]
}
```

### JetBrains Rider

1. Go to Settings → Build, Execution, Deployment → Debugger
2. Uncheck "Enable 'Just My Code'"
3. Check "Enable source server support"
4. Check "Enable SourceLink support"

## Local Development

When working with local project references (not NuGet packages), debugging is automatic. The configuration in `Directory.Build.props` ensures:

- Debug symbols are always generated
- Source code is embedded in PDBs
- Deterministic builds for consistency

## NuGet Package Debugging

When consuming ThirdOpinion.Common as NuGet packages:

1. **Install the packages** normally via NuGet
2. **Symbols are automatically restored** if you have symbol servers configured
3. **Sources are embedded** in the PDB, so you don't need source code access

### Manual Symbol Loading

If automatic symbol loading doesn't work:

1. Download the `.snupkg` file for the package version
2. Extract the PDB file from the `.snupkg` (it's just a zip file)
3. Place the PDB next to the DLL in your bin folder

Example:
```bash
# Extract symbol package
unzip ThirdOpinion.Common.Aws.Bedrock.1.0.0-alpha.5.snupkg

# Copy PDB to your output directory
cp lib/net8.0/ThirdOpinion.Common.Aws.Bedrock.pdb ./bin/Debug/net8.0/
```

## Verifying Symbol Configuration

To verify that symbols are working:

1. Set a breakpoint in your code that calls a ThirdOpinion.Common method
2. Start debugging
3. When the breakpoint hits, try to "Step Into" (F11) the method call
4. You should be able to see the source code and step through it

## Publishing Symbol Packages

When publishing to NuGet.org or a private feed:

```bash
# Build with symbols
dotnet pack -c Release

# This creates both:
# - ThirdOpinion.Common.Aws.Bedrock.1.0.0-alpha.5.nupkg (main package)
# - ThirdOpinion.Common.Aws.Bedrock.1.0.0-alpha.5.snupkg (symbol package)

# Push both to NuGet.org
dotnet nuget push ThirdOpinion.Common.Aws.Bedrock.1.0.0-alpha.5.nupkg --api-key YOUR_KEY --source https://api.nuget.org/v3/index.json

# Symbol package is automatically detected and pushed if it exists in the same directory
```

## Build Configuration Reference

The debug symbol configuration is defined in `Directory.Build.props`:

```xml
<PropertyGroup>
    <DebugType>portable</DebugType>
    <DebugSymbols>true</DebugSymbols>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <EmbedAllSources>true</EmbedAllSources>
    <Deterministic>true</Deterministic>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
</PropertyGroup>
```

## Troubleshooting

### "Cannot step into method" / "Source not available"

1. Verify PDB files exist next to DLLs in your bin folder
2. Check that "Just My Code" is disabled
3. Try cleaning and rebuilding: `dotnet clean && dotnet build`
4. Check Module window in debugger to see if symbols are loaded

### Symbols not loading from NuGet

1. Verify symbol server is configured in IDE
2. Check network connectivity to symbol server
3. Use embedded sources by disabling source server support temporarily
4. Manually copy PDB files to your bin folder

### Sources show as "decompiled"

If sources show as decompiled despite embedded sources:
1. This might be a caching issue - restart your IDE
2. Verify the PDB was built with `EmbedAllSources=true`
3. Use a tool like `dotnet-symbol` to verify PDB contents

## Additional Resources

- [Microsoft SourceLink Documentation](https://github.com/dotnet/sourcelink)
- [NuGet Symbol Packages](https://docs.microsoft.com/en-us/nuget/create-packages/symbol-packages-snupkg)
- [Debugging with .NET Core](https://learn.microsoft.com/en-us/visualstudio/debugger/debug-dotnet-core-in-wsl2)
