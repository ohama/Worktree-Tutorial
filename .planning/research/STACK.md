# Stack Research

**Domain:** F# REST API tutorial with git worktree parallel development
**Researched:** 2026-03-04
**Confidence:** HIGH

## Recommended Stack

### Core Technologies

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| .NET | 9.0 (net9.0) | Runtime and SDK | .NET 9 is the current STS release (supported until Nov 2026); .NET 10 LTS released Nov 2025 but Giraffe 8.2.0 only lists net9.0 as an explicit target (net10.0 is "computed" only). For a tutorial shipping now, net9.0 is the safest verified target. Use net9.0 and plan a net10.0 upgrade once Giraffe explicitly targets it. |
| F# | 9.0 (ships with .NET 9 SDK) | Language | Ships with the .NET 9 SDK; no separate install needed. |
| Giraffe | 8.2.0 | HTTP framework | Latest stable (released 2025-11-12). Wraps ASP.NET Core with an idiomatic F# handler pipeline. Native System.Text.Json support via FSharp.SystemTextJson. Active maintainer, highest NuGet download count among F# web frameworks. |
| ASP.NET Core | 9.0 (ships with .NET 9) | HTTP server runtime | Giraffe is middleware on ASP.NET Core — no separate package; provided by `Microsoft.NET.Sdk.Web`. |

### Supporting Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| FSharp.SystemTextJson | >= 1.3.13 (Giraffe dependency, auto-resolved) | Serialize/deserialize F# discriminated unions and records with System.Text.Json | Always — Giraffe's `Json.FsharpFriendlySerializer` uses this under the hood. Register it in `configureServices` to get correct DU serialization. |
| Giraffe.ViewEngine | >= 1.4.0 (Giraffe dependency, auto-resolved) | DSL for HTML generation | Only if you need HTML responses. Skip for a pure REST API that returns JSON. |
| Microsoft.IO.RecyclableMemoryStream | >= 3.0.1 (auto-resolved) | Memory-efficient streaming for request/response bodies | Auto-resolved by Giraffe — no action needed. |
| Expecto | 10.2.3 | F# test framework | Testing F# code. Tests are plain values; parallel by default; works without a test runner binary (just `dotnet run`). Better DX than xUnit for F# because tests compose like functions. |
| Expecto.FsCheck | matches Expecto 10.x | Property-based testing for Expecto | When testing domain invariants (e.g., Order totals always positive). Add to test projects that validate business rules. |
| FsCheck | 3.3.2 | Property-based test data generation | Underlying generator for Expecto.FsCheck. Pull in explicitly if you need custom generators. |
| Microsoft.AspNetCore.TestHost | 9.x (ships with .NET 9) | In-process integration testing | Integration tests that exercise the full HTTP pipeline without a real port. Combine with Expecto for clean F# integration tests. |
| Fantomas | 7.0.5 | F# code formatter | Enforce consistent formatting. Install as a dotnet local tool (`dotnet tool install fantomas`). Configure via `.editorconfig`. |

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| dotnet CLI | Build, run, test, and scaffold projects | `dotnet new giraffe` scaffolds a starter project (template v1.5.002). Use `dotnet run`, `dotnet test`, `dotnet watch run`. |
| giraffe-template | Project scaffolding | Install with `dotnet new install "giraffe-template::*"` then `dotnet new giraffe`. Generates `Program.fs`, `.fsproj`, optional test project. Use `--Solution` flag to get a solution with test project included. |
| git (built-in worktree) | Parallel branch management | `git worktree add <path> <branch>` — no third-party tool required for the tutorial. Core workflow demonstrated in the tutorial itself. |
| par (optional) | Worktree + tmux session manager for AI workflows | Install globally: `npm install -g @coplane/par`. Creates worktrees + tmux sessions in one command. Good for advanced tutorial bonus section. Not required for the tutorial. |
| gwq (optional) | Fuzzy-finder UI for git worktrees | `go install github.com/d-kuro/gwq@latest`. Interactive worktree switching via fzf. Good alternative to manual `cd` between worktrees. |
| Fantomas dotnet tool | Code formatting | `dotnet tool install fantomas` → add to `.config/dotnet-tools.json` so all contributors use the same version. |
| Ionide (VS Code extension) | F# language support | The standard F# IDE experience: syntax highlighting, type checking, go-to-definition, run/debug. Required for VS Code users. |

## Installation

```bash
# Prerequisites: .NET 9 SDK
dotnet --version  # should be 9.x.x

# Install Giraffe template
dotnet new install "giraffe-template::*"

# Scaffold a new Giraffe project with solution + tests
dotnet new giraffe --Solution -o MyApi
cd MyApi

# Install Fantomas as a local tool (pinned version for the team)
dotnet new tool-manifest   # creates .config/dotnet-tools.json
dotnet tool install fantomas --version 7.0.5
dotnet tool restore         # teammates run this once after clone

# Run the API
dotnet run --project src/MyApi

# Run tests
dotnet test

# Format code
dotnet fantomas .
```

For the tutorial's F# REST API example project, the `src/` package references will look like:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <OutputType>Exe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Update="FSharp.Core" Version="9.0.*" />
    <PackageReference Include="Giraffe" Version="8.2.0" />
  </ItemGroup>

  <ItemGroup>
    <Compile Include="Domain/Users.fs" />
    <Compile Include="Domain/Products.fs" />
    <Compile Include="Domain/Orders.fs" />
    <Compile Include="Program.fs" />
  </ItemGroup>
</Project>
```

For the test project:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <OutputType>Exe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Expecto" Version="10.2.3" />
    <PackageReference Include="Expecto.FsCheck" Version="10.2.3" />
    <PackageReference Include="FsCheck" Version="3.3.2" />
    <PackageReference Include="Microsoft.AspNetCore.TestHost" Version="9.0.*" />
  </ItemGroup>
</Project>
```

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| Giraffe 8.x | Saturn | Saturn is a higher-level MVC-style framework built on top of Giraffe. Use Saturn if you want convention over configuration (routing auto-generated from controllers). For a tutorial demonstrating F# fundamentals, Giraffe's explicit handler composition is clearer. |
| Giraffe 8.x | Falco | Falco is a newer, more minimal F# web framework (also ASP.NET Core-based). Use Falco for greenfield projects where you want a lighter abstraction. Giraffe has more tutorials, ecosystem articles, and community history — better for a learner-facing tutorial. |
| Giraffe 8.x | ASP.NET Core Minimal APIs (C#-style) | Use if you want C# parity or are targeting F# beginners who already know C# minimal APIs. Giraffe's functional composition better demonstrates F# idioms. |
| Expecto | xUnit + FsUnit | Use xUnit+FsUnit if your team already uses xUnit and wants familiar syntax. Expecto is more idiomatic F# (tests as values, no attributes), making it the better teaching choice. |
| System.Text.Json via FSharp.SystemTextJson | Newtonsoft.Json (Json.NET) | Newtonsoft still works with Giraffe via a custom serializer. Use Newtonsoft only if you need features not in System.Text.Json (e.g., very complex polymorphic contracts). System.Text.Json is faster and is now Giraffe's default. |
| Thoth.Json | Thoth.Json | Thoth provides a functional encoder/decoder API loved by Elm-background devs. Skip for this tutorial — it adds a second serialization concept when FSharp.SystemTextJson (already bundled with Giraffe) covers the needs. |
| native git worktree | par CLI | Use par if you want tmux-based session management built in. For the tutorial, native `git worktree` commands are clearer for teaching — no extra tool install. |
| native git worktree | gwq | Use gwq if interactive fuzzy-finder navigation matters more than explicit commands. Again, native commands are better for tutorial clarity. |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| Giraffe < 7.0 | Versions before 7 use Newtonsoft.Json by default and lack the `FsharpFriendlySerializer`. Breaking changes in the serialization layer make migration painful. | Giraffe 8.2.0 |
| .NET 6 or .NET 7 target framework | Both are end-of-life as of November 2024. Giraffe 8.x dropped explicit net6/net7 targets. | net9.0 |
| .NET 10 (net10.0) as the primary target | Giraffe 8.2.0 lists net10.0 only as "computed" compatibility, not an explicit target. Wait for Giraffe 8.3+ or 9.x to confirm net10.0 support before targeting it. | net9.0 for now |
| Suave | Suave is a standalone F# web server (no ASP.NET Core). It's largely dormant and doesn't benefit from ASP.NET Core's ecosystem (middleware, DI, auth). | Giraffe |
| NancyFx / FancyFx | Archived. No active development. | Giraffe or Falco |
| fantomas-tool (old package name) | Renamed to `fantomas`. `fantomas-tool` is an old package that is no longer updated. | `fantomas` 7.0.5 |

## Stack Patterns by Variant

**If building the tutorial's example API only (F# REST API with Giraffe):**
- Use Giraffe 8.2.0 on net9.0
- FSharp.SystemTextJson is auto-bundled — no explicit reference needed
- Expecto for tests

**If adding a database layer (out of scope for tutorial, but useful to document):**
- Use Npgsql.FSharp (PostgreSQL) or SQLite with Microsoft.Data.Sqlite
- Do NOT use Entity Framework Core unless you want C# ORM patterns. For F#, prefer Donald (thin F# wrapper over ADO.NET) or SQLProvider.

**If the tutorial needs auth (out of scope but common next question):**
- Use ASP.NET Core Identity via `Microsoft.AspNetCore.Authentication.JwtBearer` — it integrates into Giraffe's pipeline as standard ASP.NET Core middleware.

**For git worktree parallel workflow patterns:**
- Bare repository approach: `git clone --bare <url> .git` then `git worktree add <path> <branch>` — cleaner separation
- Sibling directory approach: standard repo with `git worktree add ../worktrees/<feature> <branch>` — simpler for tutorial learners
- Recommend the sibling directory approach for the tutorial because it doesn't require learners to understand bare repos first

## Version Compatibility

| Package | Compatible With | Notes |
|---------|-----------------|-------|
| Giraffe 8.2.0 | .NET 8.0, .NET 9.0 (explicit); net10.0 computed | Do not target net6/net7 — removed from 8.x |
| FSharp.SystemTextJson >= 1.3.13 | Auto-resolved by Giraffe 8.x | Do not pin separately unless you need a specific feature not yet in Giraffe's minimum version |
| Expecto 10.2.3 | .NET 6.0+ (net9.0 confirmed compatible) | Pre-release 11.0.0-alpha is available; stay on 10.2.3 for stability |
| FsCheck 3.3.2 | .NET 6.0+ | FsCheck 3.x dropped .NET Standard; confirmed .NET 9 compatible |
| Fantomas 7.0.5 | .NET 8.0 (but runs on net9.0 toolchain) | Install as dotnet tool, not a project reference |
| Microsoft.AspNetCore.TestHost | Must match your .NET SDK version | Use `9.0.*` wildcard to stay on patch releases |

## Sources

- [NuGet: Giraffe 8.2.0](https://www.nuget.org/packages/Giraffe) — version, targets, dependencies verified
- [GitHub: giraffe-fsharp/Giraffe](https://github.com/giraffe-fsharp/Giraffe) — architecture overview, handler pattern
- [GitHub: giraffe-fsharp/giraffe-template](https://github.com/giraffe-fsharp/giraffe-template) — template v1.5.002, generated project structure
- [NuGet: Expecto 10.2.3](https://www.nuget.org/packages/Expecto) — version verified 2025-03-30
- [NuGet: FsCheck 3.3.2](https://www.nuget.org/packages/FsCheck) — version verified 2025-11-09
- [NuGet: Fantomas 7.0.5](https://www.nuget.org/packages/fantomas) — version verified 2025-12-05
- [GitHub: giraffe-fsharp/Giraffe RELEASE_NOTES](https://raw.githubusercontent.com/giraffe-fsharp/giraffe/master/RELEASE_NOTES.md) — 8.x breaking changes, net6/7 removal confirmed
- [GitHub: Tarmil/FSharp.SystemTextJson](https://github.com/Tarmil/FSharp.SystemTextJson) — serialization customization patterns
- [hamy.xyz: Single-File Web API with F#/Giraffe (2024)](https://hamy.xyz/blog/2024-09_single-file-webapi-fsharp-giraffe) — .fsproj structure, handler patterns
- [GitHub: coplane/par](https://github.com/coplane/par) — parallel worktree + tmux session manager commands
- [GitHub: d-kuro/gwq](https://github.com/d-kuro/gwq) — fuzzy-finder worktree manager
- [Git Worktree Best Practices Gist (ChristopherA)](https://gist.github.com/ChristopherA/4643b2f5e024578606b9cd5d2e6815cc) — bare repo pattern, shell functions
- [Steve Kinney: Git Worktrees for Parallel AI Development](https://stevekinney.com/courses/ai-development/git-worktrees) — Claude Code + worktree workflow
- [endoflife.date: .NET](https://endoflife.date/dotnet) — .NET 9 STS support until Nov 2026; .NET 10 LTS until Nov 2028
- [Microsoft: .NET Support Policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) — LTS vs STS policy confirmed

---
*Stack research for: F# REST API tutorial with git worktree parallelism*
*Researched: 2026-03-04*
