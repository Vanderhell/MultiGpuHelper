# Versioning and release flow

MultiGpuHelper uses semantic versions. The current version is `1.1.0` and its tag is `v1.1.0`.

Pull requests and pushes to the default `main` branch run restore, Release build, tests, and package creation. A `v*` tag runs the same gates and uploads `.nupkg` and `.snupkg` files as workflow artifacts.

The workflow does not publish to NuGet. Publishing requires a separate, explicit maintainer action after inspecting the tag artifacts. No API key belongs in this repository.

Release preparation:

1. Update the version and changelog.
2. Run restore, Release build, tests, and pack locally.
3. Install the local package into a clean sample and compile the README quick start.
4. Push reviewed code to `main`.
5. Create and push `v1.1.0` only after the commit is approved for release.
6. Download and inspect CI package artifacts before any manual NuGet upload.
