# Versioning and release flow

MultiGpuHelper uses semantic versions. The current version is `1.1.0` and its tag is `v1.1.0`.

Pull requests and pushes to the default `main` branch run restore, Release build, and tests through `ci.yml`. That workflow has no package-publishing permission.

Publishing is isolated in `release.yml`. It runs only when a `v*` tag is pushed or through an explicit manual invocation referencing an existing stable version tag. The release job rebuilds and tests the tagged source, creates `.nupkg` and `.snupkg` artifacts, and uses NuGet Trusted Publishing to obtain a short-lived credential through GitHub OIDC. No long-lived NuGet API key belongs in this repository.

The nuget.org Trusted Publishing policy must match:

- Repository owner: `Vanderhell`
- Repository: `MultiGpuHelper`
- Workflow file: `release.yml`
- GitHub environment: `release`

The GitHub `release` environment must define the `NUGET_USER` secret containing the nuget.org profile name, not an email address. Environment approval rules are recommended.

Release preparation:

1. Update the version and changelog.
2. Run restore, Release build, tests, and pack locally.
3. Install the local package into a clean sample and compile the README quick start.
4. Push reviewed code to `main`.
5. Create and push `v1.1.0` only after the commit is approved for release.
6. Pushing the tag starts `release.yml`; alternatively, manually run it with an existing tag.
7. Approve the protected `release` environment, if configured; the workflow then builds, tests, packs, and publishes through Trusted Publishing.
