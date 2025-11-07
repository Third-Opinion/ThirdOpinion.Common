# ThirdOpinion.Common - Versioning Strategy

## Overview

ThirdOpinion.Common uses a hybrid versioning system that supports both manual version control and automatic pre-release
versioning. This approach provides flexibility for different development workflows while maintaining semantic versioning
standards.

## Versioning Rules

### 1. **Manual Version Control** (Recommended for Releases)

For stable releases, update the version manually in the project file:

```xml
<!-- In src/ThirdOpinion.Common.csproj -->
<Version>2.1.0</Version>
```

**Process:**

1. Update the `<Version>` in `src/ThirdOpinion.Common.csproj`
2. Commit the version change
3. Create a git tag: `git tag v2.1.0`
4. Push the tag: `git push origin v2.1.0`
5. Create a GitHub release using the tag

### 2. **Automatic Pre-Release Versioning**

The CI/CD system automatically generates pre-release versions based on the branch:

| Branch Type     | Version Pattern                 | Example                    | Use Case            |
|-----------------|---------------------------------|----------------------------|---------------------|
| `develop`       | `{base}-beta.{build}`           | `2.0.0-beta.123`           | Development builds  |
| `feature/*`     | `{base}-alpha.{build}.{branch}` | `2.0.0-alpha.124.new-auth` | Feature development |
| `main`/`master` | `{base}` or auto-increment      | `2.0.0` or `1.0.{build}`   | Stable releases     |
| Other branches  | `{base}-{branch}.{build}`       | `2.0.0-hotfix.125`         | Custom branches     |

## Version Sources

The system reads versions from multiple sources in this priority order:

1. **Release Tags** - For GitHub releases (highest priority)
2. **Project File** - Base version from `src/ThirdOpinion.Common.csproj`
3. **Auto-Generation** - Fallback for compatibility

## Semantic Versioning

We follow [Semantic Versioning 2.0.0](https://semver.org/):

- **MAJOR** (`X.0.0`) - Breaking changes
- **MINOR** (`1.X.0`) - New features (backward compatible)
- **PATCH** (`1.1.X`) - Bug fixes (backward compatible)

### Pre-Release Identifiers

- **alpha** - Early development, unstable
- **beta** - Feature-complete, testing phase
- **rc** - Release candidate, final testing

## Common Workflows

### Creating a New Major Release

```bash
# 1. Update version in project file
sed -i 's/<Version>.*<\/Version>/<Version>3.0.0<\/Version>/' src/ThirdOpinion.Common.csproj

# 2. Commit and tag
git add src/ThirdOpinion.Common.csproj
git commit -m "Release version 3.0.0"
git tag v3.0.0
git push origin main
git push origin v3.0.0

# 3. Create GitHub release
gh release create v3.0.0 --title "Release 3.0.0" --notes "Major release with breaking changes"
```

### Creating a Minor Release

```bash
# Update to next minor version
sed -i 's/<Version>2\.0\.0<\/Version>/<Version>2.1.0<\/Version>/' src/ThirdOpinion.Common.csproj
git add src/ThirdOpinion.Common.csproj
git commit -m "Release version 2.1.0 - new features"
git tag v2.1.0
git push origin main
git push origin v2.1.0
```

### Creating a Patch Release

```bash
# Update to next patch version
sed -i 's/<Version>2\.1\.0<\/Version>/<Version>2.1.1<\/Version>/' src/ThirdOpinion.Common.csproj
git add src/ThirdOpinion.Common.csproj
git commit -m "Release version 2.1.1 - bug fixes"
git tag v2.1.1
git push origin main
git push origin v2.1.1
```

## Development Workflow

### Feature Development

1. Create feature branch: `git checkout -b feature/new-authentication`
2. Develop and commit changes
3. Push branch: `git push origin feature/new-authentication`
4. CI/CD automatically creates: `2.0.0-alpha.{build}.new-authentication`

### Beta Testing (Develop Branch)

1. Merge features to develop: `git checkout develop && git merge feature/new-authentication`
2. Push develop: `git push origin develop`
3. CI/CD automatically creates: `2.0.0-beta.{build}`

### Production Release

1. Merge develop to main: `git checkout main && git merge develop`
2. Update version in project file (if needed)
3. Create release tag and GitHub release

## Version Information in CI/CD

The CI/CD pipeline provides detailed version information:

```yaml
- Base version: 2.0.0 (from project file)
- Final version: 2.0.0-beta.123
- Version source: auto_develop
- Assembly version: 2.0.0.0
```

## NuGet Package Versioning

### Package Feed Strategy

- **Stable Releases**: Published to NuGet.org (future)
- **Pre-Releases**: Published to GitHub Packages
- **Development Builds**: Available as CI artifacts

### Version Validation

The system validates versions for semantic versioning compliance:

- ✅ `2.0.0` - Valid stable
- ✅ `2.0.0-beta.1` - Valid pre-release
- ✅ `2.0.0-alpha.1.feature` - Valid development
- ❌ `2.0` - Invalid (missing patch)
- ❌ `v2.0.0` - Invalid (extra prefix)

## Troubleshooting

### Common Issues

**Issue**: Version not updating in CI/CD
**Solution**: Ensure the version in `src/ThirdOpinion.Common.csproj` follows the exact format:
`<Version>X.Y.Z</Version>`

**Issue**: Pre-release not detected
**Solution**: Check branch naming convention and ensure it matches the patterns in CI/CD

**Issue**: Assembly version mismatch
**Solution**: Assembly versions use only the major.minor.patch without pre-release suffixes

### Manual Override

To force a specific version in CI/CD:

```yaml
# In GitHub Actions workflow dispatch
inputs:
  override_version:
    description: 'Override version (optional)'
    required: false
    default: ''
```

## Examples

| Scenario       | Branch         | Project Version | CI/CD Output           | Package Name                                     |
|----------------|----------------|-----------------|------------------------|--------------------------------------------------|
| Stable release | `main`         | `2.0.0`         | `2.0.0`                | `ThirdOpinion.Common.2.0.0.nupkg`                |
| Beta testing   | `develop`      | `2.0.0`         | `2.0.0-beta.123`       | `ThirdOpinion.Common.2.0.0-beta.123.nupkg`       |
| Feature dev    | `feature/auth` | `2.0.0`         | `2.0.0-alpha.124.auth` | `ThirdOpinion.Common.2.0.0-alpha.124.auth.nupkg` |
| GitHub release | `main`         | `2.0.0`         | `2.1.0` (from tag)     | `ThirdOpinion.Common.2.1.0.nupkg`                |

## Migration from Old System

The old system used:

- `1.0.{build}` for main/master
- `1.0.{build}-beta` for develop

The new system:

- Reads base version from project file
- Applies semantic versioning
- Supports manual version control
- Maintains backward compatibility

## Best Practices

1. **Always update project file version** before creating releases
2. **Use semantic versioning** for all version changes
3. **Create GitHub releases** for important versions
4. **Test pre-release versions** before promoting to stable
5. **Document breaking changes** in release notes
6. **Keep assembly versions** aligned with package versions

---

For questions or issues with versioning, please see the [CI/CD documentation](CICD.md) or create an issue in the
repository.