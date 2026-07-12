# Stiletto — contributor & agent guide

Stiletto is a compile-time dependency injector for .NET, ported from Square's
Dagger. The core is a Roslyn source generator (`src/Stiletto.Generator`) that
emits binding code at build time; `src/Stiletto` is the runtime library that
ships alongside it in a single NuGet package.

## Commit messages: Conventional Commits (required)

Releases are automated with [release-please](https://github.com/googleapis/release-please),
which reads commit history to decide the next version and to build the
changelog. That only works if commits follow the
[Conventional Commits](https://www.conventionalcommits.org/) format, so **every
commit that lands on `master` must be conventional.** PRs are squash-merged, so
in practice it's the **PR title** that has to conform — and the
`Lint PR title` workflow (`.github/workflows/pr-title.yml`) enforces it as a
status check.

Format:

```
<type>[optional scope][!]: <description>

[optional body]

[optional footer(s)]
```

Common `type`s and how they affect the version:

| Type        | Meaning                                          | Version bump |
| ----------- | ------------------------------------------------ | ------------ |
| `feat`      | A new feature                                    | minor        |
| `fix`       | A bug fix                                         | patch        |
| `perf`      | A performance improvement                        | patch        |
| `docs`      | Documentation only                               | none         |
| `refactor`  | Code change that neither fixes a bug nor adds a feature | none  |
| `test`      | Adding or fixing tests                           | none         |
| `build`     | Build system, packaging, or dependency changes   | none         |
| `ci`        | CI configuration changes                         | none         |
| `chore`     | Anything else not user-facing                    | none         |

### Breaking changes

Signal a breaking change either with a `!` after the type/scope or with a
`BREAKING CHANGE:` footer. Either one triggers a **major** bump.

```
feat!: replace the reflection loader with a registry-only entry point

BREAKING CHANGE: Container.Create no longer falls back to reflection.
```

### Examples

```
feat(generator): emit bindings for record types
fix: don't crash on modules with no provider methods
docs: document the AOT feature switch in the README
build: modernize NuGet packaging, drop Fody
```

## How releases work

1. Land conventional commits on `master`.
2. `release-please.yml` opens/maintains a **release PR** that bumps the version,
   updates `CHANGELOG.md`, `version.txt`, and the `<Version>` in
   `src/Stiletto/Stiletto.csproj`.
3. Merge the release PR when you're ready to cut a release. release-please then
   creates the git tag and GitHub release, and the `publish` job packs and
   pushes the package to nuget.org via Trusted Publishing (OIDC — no API key
   secret).

The version lives in three release-please–owned places; **don't hand-edit them**:
`.release-please-manifest.json`, `version.txt`, and the annotated `<Version>`
line in `src/Stiletto/Stiletto.csproj` (marked `x-release-please-version`).

The package baseline is `1.0.0-alpha.1`. release-please treats that as a
prerelease and, left alone, keeps advancing the prerelease line
(`fix:` → `1.0.1-alpha.1`, etc.) rather than dropping the `-alpha`. To
graduate to a stable release, add a `Release-As:` footer to a commit:

```
Release-As: 1.0.0
```

That overrides the next version for one release only; normal semver
(`feat`→minor, `fix`→patch, `feat!`→major) resumes afterward. The
config-level `release-as` key does the same but pins *every* release until
removed, so prefer the commit footer for one-off graduations.

## Build & test

The solution is `Stiletto.slnx`; it builds on the .NET SDK pinned in
`global.json`.

```
dotnet restore Stiletto.slnx
dotnet build   Stiletto.slnx --configuration Release
dotnet test    Stiletto.slnx --configuration Release
```

Only the SDK-style projects under `src/`, `test/`, and `samples/` are part of
`Stiletto.slnx`. The legacy net4.0 projects (`Stiletto.Fody`, the mobile heads,
`ValidateBuilds`, `Example`) are kept only as reference for the source-generator
port and do not build on this toolchain.
