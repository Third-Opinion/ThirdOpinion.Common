# GitFlow Workflow Guide

This repository follows the GitFlow branching model for organized development and releases.

## Branch Structure

### Main Branches

- **`master`** - Production-ready code. Only hotfixes and release merges allowed.
- **`develop`** - Integration branch for features. Latest development state.

### Supporting Branches

- **`feature/*`** - New features and enhancements
- **`release/*`** - Release preparation and bug fixes
- **`hotfix/*`** - Critical production fixes
- **`bugfix/*`** - General bug fixes

## Workflow Commands

### Starting New Work

```bash
# Start a new feature
git flow feature start my-feature-name

# Start a release
git flow release start 1.2.0

# Start a hotfix
git flow hotfix start hotfix-name
```

### Finishing Work

```bash
# Finish a feature (merges to develop)
git flow feature finish my-feature-name

# Finish a release (merges to master and develop, creates tag)
git flow release finish 1.2.0

# Finish a hotfix (merges to master and develop, creates tag)
git flow hotfix finish hotfix-name
```

## CI/CD Pipeline Behavior

### Build & Test Only
- `feature/*` branches
- `bugfix/*` branches

### Full CI/CD (Build, Test, Package)
- `develop` branch
- `release/*` branches  
- `hotfix/*` branches
- `master` branch

### Production Publishing
- `master` branch (on release)
- Manual workflow dispatch

## Development Guidelines

### Feature Development
1. Start feature from `develop`: `git flow feature start feature-name`
2. Develop and commit changes
3. Push feature branch for CI/CD feedback
4. Create pull request to `develop`
5. Finish feature: `git flow feature finish feature-name`

### Release Process
1. Start release from `develop`: `git flow release start 1.2.0`
2. Update version numbers, documentation
3. Test and fix bugs on release branch
4. Finish release: `git flow release finish 1.2.0`
5. Push `master`, `develop`, and tags

### Hotfix Process
1. Start hotfix from `master`: `git flow hotfix start critical-fix`
2. Fix the issue and test
3. Finish hotfix: `git flow hotfix finish critical-fix`
4. Push `master`, `develop`, and tags

## Branch Naming Conventions

- **Features**: `feature/add-user-authentication`
- **Releases**: `release/1.2.0`
- **Hotfixes**: `hotfix/fix-security-vulnerability`
- **Bugfixes**: `bugfix/fix-null-reference`

## Version Tagging

- Semantic versioning: `MAJOR.MINOR.PATCH`
- Release tags: `1.2.0`, `1.2.1`, `2.0.0`
- Pre-release tags: `1.2.0-rc.1`, `1.2.0-beta.1`

## Pull Request Guidelines

### Target Branches
- Features → `develop`
- Releases → `master` (via GitFlow finish)
- Hotfixes → `master` (via GitFlow finish)
- General fixes → `develop`

### Required Checks
- All CI/CD pipeline checks must pass
- Code review approval required
- No merge conflicts
- Updated documentation if needed

## Integration with GitHub

### Protected Branches
- `master` - Requires pull request, status checks, admin enforcement
- `develop` - Requires pull request, status checks

### Branch Policies
- Direct pushes to `master` and `develop` are restricted
- Feature branches allow direct pushes for development
- Release and hotfix branches require careful management

## Best Practices

1. **Keep feature branches small and focused**
2. **Regularly sync with develop** - `git flow feature pull origin`  
3. **Use descriptive branch names** that explain the purpose
4. **Test thoroughly** before finishing features/releases
5. **Update documentation** as part of feature development
6. **Follow semantic versioning** for releases
7. **Use pull requests** for code review even with GitFlow

## Troubleshooting

### Common Issues

**Feature branch out of sync:**
```bash
git checkout feature/my-feature
git merge develop
```

**Need to update from develop:**
```bash
git flow feature pull origin my-feature
```

**Accidentally committed to develop:**
```bash
git checkout develop
git reset --hard origin/develop
git checkout -b feature/fix-from-develop
# Cherry-pick your commits to the feature branch
```

## CI/CD Integration

The pipeline automatically:
- **Builds and tests** all branch types
- **Creates packages** for develop, release, hotfix, and master
- **Publishes to NuGet** only on master releases
- **Runs security scans** on pull requests
- **Generates coverage reports** for all builds

## Getting Started

1. Install git-flow: `brew install git-flow` (macOS) or equivalent
2. Clone repository: `git clone <repo-url>`
3. Initialize GitFlow: `git flow init -d` (use defaults)
4. Start your first feature: `git flow feature start my-first-feature`

For more information, see the [GitFlow original documentation](https://nvie.com/posts/a-successful-git-branching-model/).