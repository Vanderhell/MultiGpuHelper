# MultiGpuHelper Versioning

This project follows **Semantic Versioning (SemVer)** principles.

## Version Format

```
MAJOR.MINOR.PATCH[-PRERELEASE][+BUILD]
```

Example: `1.2.3`, `2.0.0-beta`, `1.0.0-rc.1`

## Versioning Rules

### MAJOR (Breaking Changes)
Increment when you make incompatible API changes:
- Remove public methods or types
- Change method signatures in breaking ways
- Change return types
- Modify enum values
- Remove support for a platform/framework

Example: `1.0.0` → `2.0.0`

### MINOR (New Features)
Increment when you add new functionality in a backwards-compatible way:
- Add new public methods
- Add new types
- Add optional parameters (with defaults)
- Extend enums with new values
- Improve performance without API changes

Example: `1.0.0` → `1.1.0`

### PATCH (Bug Fixes)
Increment for backwards-compatible bug fixes:
- Fix bugs in existing functionality
- Internal refactoring
- Documentation updates
- Performance improvements

Example: `1.0.0` → `1.0.1`

### Pre-release versions
Use for alpha/beta releases:
- `1.0.0-alpha` - early development
- `1.0.0-beta` - feature-complete, testing phase
- `1.0.0-rc.1` - release candidate

### Build metadata
Optional build metadata (not part of version precedence):
- `1.0.0+build.20240107`

## Release Workflow

### 1. Update Version Number

Edit `src/MultiGpuHelper/MultiGpuHelper.csproj`:

```xml
<Version>1.2.3</Version>
```

### 2. Update CHANGELOG (if applicable)

Document changes in a `CHANGELOG.md` file:

```markdown
## [1.2.3] - 2024-01-07

### Added
- New GPU refresh API
- Support for async work item prioritization

### Fixed
- VRAM budget calculation edge case
- Memory leak in GpuDispatcher

### Changed
- Improved error messages for GPU selection
```

### 3. Commit Changes

```bash
git add src/MultiGpuHelper/MultiGpuHelper.csproj CHANGELOG.md
git commit -m "Bump version to 1.2.3"
```

### 4. Create Git Tag

```bash
git tag -a v1.2.3 -m "Release version 1.2.3"
```

### 5. Push Tag

```bash
git push origin v1.2.3
```

### 6. Build and Pack

```bash
dotnet build -c Release
dotnet pack src/MultiGpuHelper/MultiGpuHelper.csproj -c Release -o ./artifacts
```

The NuGet package will be generated as:
- `MultiGpuHelper.1.2.3.nupkg`
- `MultiGpuHelper.1.2.3.snupkg` (symbols)

### 7. Publish to NuGet (Manual)

```bash
# For internal testing/local nuget server
dotnet nuget push ./artifacts/MultiGpuHelper.1.2.3.nupkg -s https://your-nuget-server
```

Or via GitHub Actions:
- Tag on `main` branch triggers CI
- CI builds and uploads package as artifact
- Maintainers publish manually to nuget.org

## Breaking Change Policy

Before releasing a MAJOR version:

1. **Deprecation Period**: If possible, deprecate old APIs first (MINOR release)
2. **Documentation**: Clearly document breaking changes in README and CHANGELOG
3. **Migration Guide**: Provide examples showing how to update code
4. **Communication**: Announce breaking changes in advance (if applicable)

## Current Version

Current version is defined in: `src/MultiGpuHelper/MultiGpuHelper.csproj`

View version:
```bash
dotnet nuget locals all --list
# or
cat src/MultiGpuHelper/MultiGpuHelper.csproj | grep -i "<Version>"
```

## Version History

- **1.0.0** (2024-01-07): Initial release
  - GPU device management
  - Multi-GPU work dispatching
  - VRAM budgeting
  - Device selection policies (RoundRobin, MostFreeVram, SpecificDevice)
  - Concurrency control via semaphores
  - NVIDIA GPU detection via nvidia-smi

## References

- [Semantic Versioning (semver.org)](https://semver.org)
- [NuGet Versioning](https://docs.microsoft.com/en-us/nuget/concepts/package-versioning)
