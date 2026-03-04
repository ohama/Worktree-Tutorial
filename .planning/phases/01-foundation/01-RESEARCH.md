# Phase 1: Foundation - Research

**Researched:** 2026-03-05
**Domain:** F# Giraffe project scaffolding + git worktree lifecycle documentation
**Confidence:** HIGH

## Summary

Phase 1 has two parallel tracks: (1) creating the F# Giraffe project scaffold that all later phases depend on, and (2) verifying that the existing tutorial chapter content covers the required worktree lifecycle and `claude --worktree` documentation. The good news is that both tracks have complete, verified patterns available — the tutorial chapter 01 (already written at `tutorial/01-introduction.md`) contains the exact scaffold commands, `.fsproj` zone comment syntax, `Core.fs` type definitions, and `Program.fs` skeleton that FOUND-01 through FOUND-05 require. The critical insight is that **the implementation task is to CREATE `src/` files that match what the tutorial chapter already documents** — tutorial and code must stay in sync.

The project-level research (`.planning/research/STACK.md`, `ARCHITECTURE.md`, `PITFALLS.md`) is comprehensive and HIGH confidence. Phase 1 research confirmed that all standard patterns are already documented there. This phase-level research adds only what the project-level research did not cover: the exact giraffe-template scaffold output vs. the simplified hand-written structure the tutorial uses, the exact `.fsproj` zone comment syntax already embedded in `tutorial/01-introduction.md`, and the specific `Core.fs` type design (Guid-based IDs, not int-based).

The existing `tutorial/01-introduction.md` is the authoritative specification for what Phase 1 must build. The planner should treat that file as the requirements document, not just documentation.

**Primary recommendation:** Create `src/` files that exactly match the code blocks in `tutorial/01-introduction.md` — do not deviate from what the tutorial shows.

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET SDK | 9.0 | Runtime + build toolchain | Giraffe 8.x explicitly targets net9.0; .NET 9 STS supported until Nov 2026 |
| F# | 9.0 | Language (ships with .NET 9 SDK) | No separate install; F# 9 ships with .NET 9 SDK |
| Giraffe | 8.2.0 | HTTP framework | Latest stable (2025-11-12); idiomatic F# handler composition; bundles FSharp.SystemTextJson |
| Fantomas | 7.0.5 | Code formatter (dotnet local tool) | Pinned version ensures consistent formatting across all worktrees |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| FSharp.SystemTextJson | auto-resolved by Giraffe | F# discriminated union JSON serialization | Auto-bundled by Giraffe 8.x — no explicit reference needed |
| giraffe-template | latest (`*`) | dotnet new scaffold | Install once, use to create base project structure |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Giraffe 8.2.0 | Saturn | Saturn is higher-level MVC-style on top of Giraffe; less transparent for teaching F# handler composition |
| Giraffe 8.2.0 | Falco | Falco is newer and lighter but has fewer tutorials; Giraffe has more community reference material |
| net9.0 | net10.0 | Giraffe 8.2.0 only lists net10.0 as "computed" compatibility, not an explicit target |

**Installation:**

```bash
# Verify .NET 9 SDK
dotnet --version  # must be 9.x.x

# Install Giraffe template
dotnet new install "giraffe-template::*"

# Install Fantomas as dotnet local tool
dotnet new tool-manifest   # creates .config/dotnet-tools.json
dotnet tool install fantomas --version 7.0.5
```

## Architecture Patterns

### Recommended Project Structure

```
worktree-tutorial/          # git repository root
├── .config/
│   └── dotnet-tools.json   # Fantomas pinned version
├── .gitignore
├── src/
│   ├── WorktreeApi.fsproj  # compilation order is authoritative
│   ├── Core.fs             # shared types — compiled first
│   └── Program.fs          # skeleton entry point — compiled last
└── tutorial/               # already exists — do not modify
    ├── README.md
    ├── 01-introduction.md  # THE SPEC for Phase 1's src/ files
    └── ...
```

**Phase 1 creates only:** `src/WorktreeApi.fsproj`, `src/Core.fs`, `src/Program.fs`, `.config/dotnet-tools.json`, `.gitignore`

**Phase 1 does NOT create:** `src/Users/`, `src/Products/`, `src/Orders/` — those belong to later phases

### Pattern 1: Zone-Commented `.fsproj` Compilation Order

**What:** F# requires all source files listed in `.fsproj` in strict dependency order. Zone comments partition the file into named sections so parallel worktrees know exactly where to insert their new files without creating conflicts.

**When to use:** From project inception — set this up before any parallel work begins.

**Example (exact syntax from `tutorial/01-introduction.md`):**

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <!-- === CORE (shared types — compile first) === -->
    <Compile Include="Core.fs" />

    <!-- === DOMAIN MODULES (independent — add in any order) === -->
    <!-- Users module -->
    <!-- Products module -->
    <!-- Orders module -->

    <!-- === ENTRY POINT (compile last) === -->
    <Compile Include="Program.fs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Giraffe" Version="8.2.0" />
  </ItemGroup>

</Project>
```

### Pattern 2: Core.fs — Shared Types with Guid-Based IDs

**What:** Core.fs defines all shared types that domain modules reference. IDs use `Guid` (not `int`) for opaque identity without coupling. The `ApiResponse<'T>` wrapper standardizes all API responses.

**When to use:** Core.fs is the dependency ceiling. Any type needed by two or more domain modules lives here.

**Example (exact code from `tutorial/01-introduction.md`):**

```fsharp
namespace WorktreeApi

open System

module Core =

    // === Shared ID Types ===
    type UserId = UserId of Guid
    type ProductId = ProductId of Guid
    type OrderId = OrderId of Guid

    // === API Response Wrapper ===
    type ApiResponse<'T> =
        { Data: 'T option
          Message: string
          Success: bool }

    module ApiResponse =
        let success data =
            { Data = Some data
              Message = "OK"
              Success = true }

        let error msg =
            { Data = None
              Message = msg
              Success = false }

        let noContent () =
            { Data = None
              Message = "No Content"
              Success = true }
```

**Critical design choices:**
- `UserId of Guid` not `UserId of int` — opaque type prevents accidental cross-domain ID mixing
- `ApiResponse<'T>` has `Data: 'T option` not `Data: 'T` — allows empty responses without nullability
- `Success: bool` field for quick consumer-side success checking without HTTP status parsing
- The `ApiResponse` module with `success`, `error`, `noContent` constructors — idiomatic F# over direct record construction

### Pattern 3: Skeleton Program.fs with Route Composition Point

**What:** Program.fs starts with only the health check endpoint and an annotated zone for domain routes. The zone comment `// === DOMAIN ROUTES ===` is where all worktrees will add their `subRoute` calls in later phases — this creates the intentional merge conflict the tutorial teaches.

**When to use:** Set up the composition point with the zone comment from day one.

**Example (exact code from `tutorial/01-introduction.md`):**

```fsharp
module WorktreeApi.App

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Giraffe

// === Health Check ===
let healthCheck: HttpHandler =
    fun next ctx ->
        json
            {| status = "healthy"
               timestamp = System.DateTime.UtcNow |}
            next
            ctx

// === Route Composition ===
let webApp: HttpHandler =
    choose
        [ GET >=> route "/health" >=> healthCheck

          // === DOMAIN ROUTES ===
          // (각 worktree에서 여기에 route를 추가합니다)

          RequestErrors.NOT_FOUND "Not Found" ]

// === Server Configuration ===
let configureApp (app: IApplicationBuilder) = app.UseGiraffe webApp

let configureServices (services: IServiceCollection) =
    services.AddGiraffe() |> ignore

[<EntryPoint>]
let main args =
    Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(fun webHost ->
            webHost
                .Configure(configureApp)
                .ConfigureServices(configureServices)
            |> ignore)
        .Build()
        .Run()

    0
```

**Note:** `module WorktreeApi.App` not `module App` — namespace-qualified module name ensures no name collisions when domain modules are added. `namespace WorktreeApi` in Core.fs + `module WorktreeApi.App` in Program.fs creates the coherent namespace.

### Pattern 4: Tutorial Chapter Content Structure (TUTC requirements)

**What:** Tutorial content follows 한영 혼용 style — Korean for explanations, English for all code blocks, commands, and technical terms. Chapters are numbered Markdown files with a README.md index.

**Existing state:** `tutorial/01-introduction.md` (15.6KB) is COMPLETE and covers:
- TUT1-01: git worktree lifecycle (`add`/`list`/`remove`/`prune`) — fully documented
- TUT1-02: `claude --worktree` flag walkthrough — Method 1 and Method 2 both documented
- TUTC-01: numbered Markdown chapters — 5 chapters exist
- TUTC-02: README.md index — exists with chapter table
- TUTC-03: Korean-English mixed style — implemented throughout

**Phase 1's tutorial task:** Tutorial content is already written. Phase 1 only needs to create the `src/` code that matches what the tutorial documents.

### Anti-Patterns to Avoid

- **`giraffe-template` default output as-is:** The `dotnet new giraffe -o src` template generates a more complex scaffold (with Views, Models directories). The tutorial uses a hand-crafted minimal structure. Do NOT use the template output directly — hand-write the minimal files matching `tutorial/01-introduction.md`.
- **`int` IDs instead of `Guid` IDs:** The tutorial uses `UserId of Guid`. Do not use `int` even though it's simpler — the tutorial chapter is the spec.
- **`module App` without namespace prefix:** Using `module WorktreeApi.App` in Program.fs while `namespace WorktreeApi` in Core.fs creates the correct unified namespace. Plain `module App` would conflict.
- **`OutputType` in `.fsproj`:** The hand-written `.fsproj` in the tutorial does NOT include `<OutputType>Exe</OutputType>` — ASP.NET Core SDK infers this from `Microsoft.NET.Sdk.Web`. Do not add it.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| JSON serialization | Custom serializer | FSharp.SystemTextJson via Giraffe | Already bundled; handles F# discriminated unions correctly |
| HTTP handler composition | Custom middleware pipeline | Giraffe's `>=>` fish operator and `choose` | Idiomatic F#; handles `HttpFunc -> HttpContext -> Task<HttpContext option>` correctly |
| Code formatting | Custom formatter config | Fantomas 7.0.5 as dotnet local tool | Pinned via `dotnet-tools.json`; consistent across all worktrees |
| Route not found | Custom 404 handler | `RequestErrors.NOT_FOUND` (Giraffe built-in) | Already implemented in the example |

**Key insight:** For Phase 1, the "don't hand-roll" principle primarily means: do not over-engineer the scaffold. The tutorial requires a deliberately minimal structure that demonstrates clean composition — resist adding authentication, databases, or complex middleware.

## Common Pitfalls

### Pitfall 1: Scaffold Divergence from Tutorial Chapter

**What goes wrong:** The `src/` files are created but don't match the code blocks in `tutorial/01-introduction.md`. Readers following the tutorial get build errors because the actual code differs from what's documented.

**Why it happens:** Implementer creates files from memory or from the giraffe-template output rather than matching the tutorial chapter exactly.

**How to avoid:** Treat `tutorial/01-introduction.md` as the specification. Copy code blocks from the tutorial into the actual files verbatim. Run `diff` mentally against the tutorial after creating each file.

**Warning signs:** `Core.fs` uses `int` IDs instead of `Guid`; `Program.fs` module name doesn't match tutorial; `.fsproj` zone comments differ from tutorial's exact wording.

### Pitfall 2: F# Namespace vs. Module Confusion

**What goes wrong:** `Core.fs` uses `module Core` instead of `namespace WorktreeApi` + `module Core`, causing `open Core` in Program.fs to fail with unresolved namespace errors.

**Why it happens:** F# namespace and module declarations look similar but have different scoping semantics. `namespace X` + `module Y` creates `X.Y`. Just `module X.Y` also creates `X.Y` but requires different open syntax in consumers.

**How to avoid:** Use the exact declarations from the tutorial:
- Core.fs: `namespace WorktreeApi` then `module Core =` (note the `=` for nested module)
- Program.fs: `module WorktreeApi.App` (top-level module, no `=`)

**Warning signs:** `error FS0039: The value or constructor 'Core' is not defined` or `error FS1125: The instantiation of the generic type 'ApiResponse' is missing`.

### Pitfall 3: giraffe-template Generates Different Structure

**What goes wrong:** Running `dotnet new giraffe -o src` generates files (Views/, Models/, HttpHandlers.fs) that do not match the tutorial's minimal two-file structure (Core.fs + Program.fs).

**Why it happens:** The giraffe-template includes optional features (View Engine, models directory) that the tutorial deliberately omits.

**How to avoid:** After running `dotnet new giraffe -o src`, delete the generated files and replace them with the hand-written versions matching the tutorial. Or skip the template entirely and write the `.fsproj`, `Core.fs`, and `Program.fs` by hand.

**Warning signs:** `src/` contains `Views/`, `Models/`, or `HttpHandlers.fs` directories/files not mentioned in the tutorial.

### Pitfall 4: Missing `dotnet tool restore` Step

**What goes wrong:** Fantomas is installed in CI or in a fresh clone but `dotnet fantomas .` fails because `dotnet tool restore` was not run.

**Why it happens:** `dotnet-tools.json` declares the tool but does not install it. Each new clone/CI environment must run `dotnet tool restore` once.

**How to avoid:** The `.gitignore` must NOT exclude `.config/dotnet-tools.json` (it should be committed). Document `dotnet tool restore` in the README as a post-clone step.

**Warning signs:** `dotnet fantomas .` returns `Tool 'fantomas' is not installed`.

### Pitfall 5: Worktree Created Inside Repository Root

**What goes wrong:** Reader runs `git worktree add ./test-worktree -b test/branch` — creating the worktree as a subdirectory of the repo. Git allows it but language servers, file watchers, and Ionide get confused. `.fsproj` resolution becomes ambiguous.

**Why it happens:** The default mental model is "create inside the current directory." Tutorial must explicitly show `../` prefix before any worktree creation.

**How to avoid:** Tutorial `01-introduction.md` already documents this correctly (Step 10 shows `git worktree add ../worktree-tutorial-test`). The implementation task is to make sure the tutorial's instruction is unambiguous.

**Warning signs:** `ls` inside the repo shows a directory that also contains `.git`-related files; Ionide shows duplicate symbol definitions.

## Code Examples

Verified patterns from `tutorial/01-introduction.md` (the authoritative source):

### .gitignore for F# + Giraffe Projects

```gitignore
# .NET build output
bin/
obj/

# IDE
.vs/
.vscode/
.idea/
*.user
*.suo

# Fantomas
.fantomas-ignore

# OS
.DS_Store
Thumbs.db
```

### Verify Build and Health Check

```bash
cd src
dotnet build
# Expected: Build succeeded. 0 Warning(s) 0 Error(s)

dotnet run &
curl http://localhost:5000/health
# Expected: {"status":"healthy","timestamp":"2026-03-..."}

kill %1
```

### Fantomas Setup (dotnet local tool)

```bash
# From project root (not src/)
dotnet new tool-manifest
dotnet tool install fantomas --version 7.0.5
# Produces: .config/dotnet-tools.json

# Verify
dotnet fantomas src/Core.fs
```

### git worktree Practice Lifecycle (from tutorial Step 10)

```bash
# Create practice worktree (sibling — NOT inside repo)
git worktree add ../worktree-tutorial-test -b test/practice

# Verify
git worktree list
# Expected: two entries — main and test/practice

# Remove properly (NOT rm -rf)
git worktree remove ../worktree-tutorial-test
git branch -d test/practice

# Verify cleanup
git worktree list
# Expected: only main
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Newtonsoft.Json for F# | FSharp.SystemTextJson (bundled in Giraffe 8.x) | Giraffe 7.0 (2023) | Discriminated unions serialize correctly by default; no custom converters needed |
| `fantomas-tool` package name | `fantomas` | Fantomas 4.x | Old package name is abandoned; must use `fantomas` |
| .NET 6/7 targets | net9.0 | Giraffe 8.x | Giraffe 8.x dropped net6/7 targets; net9.0 is the minimum explicit target |

**Deprecated/outdated:**
- `fantomas-tool`: Old NuGet package name — use `fantomas` 7.0.5 instead
- .NET 7 target framework: EOL November 2024; Giraffe 8.x does not list it as an explicit target
- Suave framework: Standalone F# server with no ASP.NET Core; largely dormant; do not reference

## Open Questions

1. **`claude --worktree` exact current behavior**
   - What we know: The tutorial documents two methods — `claude --worktree feature-users` (auto-creates worktree) and manual `git worktree add` + separate `claude` session
   - What's unclear: The exact current syntax of `claude --worktree` — CLI flags can change across Claude Code releases; the tutorial's method 1 description may be slightly out of date
   - Recommendation: Verify `claude --worktree --help` output against what tutorial chapter 01 documents before committing. If behavior differs, update the tutorial chapter to match current CLI. The tutorial is more important than the flag documentation to get right — do not ship tutorial content that doesn't match the actual CLI.

2. **giraffe-template exact scaffold output in 2026**
   - What we know: Template v1.5.002 generates `Program.fs`, `.fsproj`, optional test project
   - What's unclear: Whether the template's default output still differs significantly from the tutorial's hand-written version (template may have been updated)
   - Recommendation: Run `dotnet new giraffe -o src-test --dry-run` (or into a temp dir) and compare output against tutorial code blocks. Hand-write the final files regardless — the tutorial is the spec.

## Sources

### Primary (HIGH confidence)
- `tutorial/01-introduction.md` — authoritative specification for Phase 1 `src/` files; all code examples above taken directly from this file
- `.planning/research/STACK.md` — verified versions (Giraffe 8.2.0, Fantomas 7.0.5, .NET 9.0, Expecto 10.2.3)
- `.planning/research/ARCHITECTURE.md` — component diagram, `.fsproj` compilation order rules, Core.fs dependency ceiling pattern
- `.planning/research/PITFALLS.md` — all 6 critical pitfalls; Phase 1 is explicitly the prevention phase for all of them

### Secondary (MEDIUM confidence)
- `.planning/research/SUMMARY.md` — confirms "Phase 1: standard patterns, skip research" note; phase research validates this assessment
- `tutorial/README.md` — confirms chapter structure, TUTC requirements are already met by existing tutorial files

### Tertiary (LOW confidence)
- None — all findings are grounded in the existing authoritative sources above

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — versions verified against NuGet in project-level research (2026-03-04); Giraffe 8.2.0, Fantomas 7.0.5, .NET 9.0 confirmed
- Architecture: HIGH — Core.fs patterns, `.fsproj` zone comments, Program.fs skeleton all sourced directly from the existing tutorial chapter which defines the spec
- Pitfalls: HIGH — all 6 pitfalls from project-level research are Phase 1 concerns; specific pitfall about scaffold-tutorial divergence is new and Phase 1 specific

**Research date:** 2026-03-05
**Valid until:** 2026-06-05 (90 days — stable technology; Giraffe release cycle is slow; F# version tied to .NET SDK)

---

## Phase 1 Implementation Summary for Planner

**What must be created:**

| File | Source of Truth | Key Constraint |
|------|-----------------|----------------|
| `src/WorktreeApi.fsproj` | `tutorial/01-introduction.md` Step 3 | Must have zone comments exactly as shown; `Giraffe` version must be `8.2.0` |
| `src/Core.fs` | `tutorial/01-introduction.md` Step 4 | Guid-based IDs; `ApiResponse` module with `success`/`error`/`noContent` constructors |
| `src/Program.fs` | `tutorial/01-introduction.md` Step 5 | Health check on `/health`; zone comment `// === DOMAIN ROUTES ===` for later merge conflict teaching |
| `.config/dotnet-tools.json` | `dotnet new tool-manifest` + `dotnet tool install fantomas --version 7.0.5` | Fantomas 7.0.5 pinned |
| `.gitignore` | `tutorial/01-introduction.md` Step 8 | Excludes `bin/`, `obj/`, `.vs/`, `.DS_Store` |

**What already exists and must NOT be modified:**

| File | Status |
|------|--------|
| `tutorial/01-introduction.md` | COMPLETE — covers TUT1-01, TUT1-02, TUTC-01, TUTC-02, TUTC-03 |
| `tutorial/README.md` | COMPLETE — chapter index exists |
| `tutorial/02-parallel-development.md` through `tutorial/05-cicd-integration.md` | COMPLETE — out of Phase 1 scope |

**Success verification commands:**

```bash
cd src && dotnet build    # must succeed with 0 errors
dotnet run &
curl http://localhost:5000/health  # must return JSON with "status":"healthy"
kill %1
cd .. && dotnet fantomas src/  # must run without errors
```
