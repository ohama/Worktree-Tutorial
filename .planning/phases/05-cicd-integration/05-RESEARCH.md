# Phase 5: CI/CD Integration - Research

**Researched:** 2026-03-05
**Domain:** GitHub Actions matrix strategy + Expecto test filtering + .NET 10 CI + git worktree cleanup in CI
**Confidence:** HIGH

## Summary

Phase 5 creates two artifacts: `.github/workflows/ci.yml` (a GitHub Actions workflow with matrix strategy for per-module parallel builds) and `tutorial/05-cicd-integration.md` (a tutorial chapter explaining the YAML line-by-line). The tutorial chapter already exists at `tutorial/05-cicd-integration.md` with full content, but contains version discrepancies that must be corrected: it uses `net9.0` and `dotnet-version: 9.0.x` when the actual project targets `net10.0`. The GitHub Actions workflow file does NOT exist yet (`.github/workflows/` directory is absent).

The critical technical discovery is the exact Expecto CLI flag for per-module test filtering: `--filter-test-list <substring>` matches the test list name (e.g., `--filter-test-list Users` runs only the `Users.Domain` tests). This was verified by running the actual test binary against the live codebase. The existing tutorial's draft uses `--filter "${{ matrix.module }}"` which is the wrong flag (that flag filters by path hierarchy). The correct command is `dotnet run --project tests/WorktreeApi.Tests.fsproj -- --filter-test-list "${{ matrix.module }}"`.

The single `.fsproj` architecture means CI cannot do per-module builds — there is only one project to build. However, per-module test execution IS achievable using Expecto's `--filter-test-list` flag. The correct CI strategy is: one unified build job, then three parallel test jobs (one per module) using matrix strategy.

**Primary recommendation:** Create `.github/workflows/ci.yml` with a two-stage pipeline (build → parallel test matrix), fix the tutorial's net9→net10 version discrepancy, and replace `--filter` with `--filter-test-list` in the matrix test step.

## Standard Stack

The established tools for this domain:

### Core

| Tool | Version | Purpose | Why Standard |
|------|---------|---------|--------------|
| `actions/checkout` | v4 | Checkout repository | Required first step in all jobs |
| `actions/setup-dotnet` | v4 | Install .NET SDK | Official dotnet action, supports `10.0.x` format |
| GitHub Actions matrix strategy | N/A | Run parallel jobs per module | Native GitHub Actions parallelism |
| Expecto | 10.2.3 | F# test framework | Already in project; `--filter-test-list` enables per-module runs |

### Supporting

| Tool | Version | Purpose | When to Use |
|------|---------|---------|-------------|
| `git worktree prune` | built-in git | Clean stale worktree metadata in CI | Cleanup step after worktree-based CI steps |
| `dotnet restore` | built-in | Restore NuGet packages | Before build; separate step for caching clarity |
| `dotnet build --no-restore` | built-in | Compile project | After restore; `--no-restore` avoids redundant work |
| `dotnet run -- --filter-test-list` | built-in | Run module-filtered Expecto tests | In matrix test jobs |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `--filter-test-list` flag | `--filter` flag | `--filter` filters by slash-separated hierarchy path, not test list name; `--filter-test-list` matches test list names which correspond to module names |
| `dotnet run --project tests/` | `dotnet test tests/` | `dotnet run` calls Expecto's CLI directly enabling all Expecto flags; `dotnet test` with YoloDev adapter uses VSTest filter syntax which is different |
| Matrix on `module` only | Matrix on `os` + `module` | Cross-platform builds add value for library code; this is a single-platform API — ubuntu-latest is sufficient |
| `fail-fast: true` | `fail-fast: false` | `fail-fast: false` lets all module tests complete independently, providing full failure info; tutorial teaches this as the parallel value |

**Installation:** No new packages needed. GitHub Actions is hosted; no local install required.

## Architecture Patterns

### Recommended Workflow File Structure

```
.github/
└── workflows/
    └── ci.yml      # Single workflow file for the tutorial
```

### Pattern 1: Two-Stage Pipeline (Build → Parallel Test Matrix)

**What:** One build job compiles the single `.fsproj`. Three parallel test jobs depend on the build job and each run a single module's tests using `--filter-test-list`.

**When to use:** When a single compiled artifact exists but tests are logically organized per module. This is the correct pattern for this codebase — one `.fsproj`, three logical test domains.

**Example (verified against actual codebase):**

```yaml
# Source: GitHub Actions official docs + verified against local test run
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 10.0
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

  test:
    runs-on: ubuntu-latest
    needs: build
    strategy:
      matrix:
        module: [Users, Products, Orders]
      fail-fast: false

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 10.0
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore
        run: dotnet restore tests/WorktreeApi.Tests.fsproj

      - name: Run ${{ matrix.module }} tests
        run: |
          dotnet run --project tests/WorktreeApi.Tests.fsproj \
            -- --filter-test-list "${{ matrix.module }}"

  cleanup:
    runs-on: ubuntu-latest
    needs: [test]
    if: always()

    steps:
      - uses: actions/checkout@v4

      - name: Prune stale worktree metadata
        run: |
          git worktree list
          git worktree prune -v
          echo "After prune:"
          git worktree list
```

### Pattern 2: Matrix Variable Reference

**What:** Use `${{ matrix.module }}` in step names and run commands to reference the current matrix value. This makes logs readable (each job shows e.g. "Run Users tests") and passes the filter value.

**Example:**
```yaml
strategy:
  matrix:
    module: [Users, Products, Orders]
  fail-fast: false

steps:
  - name: Run ${{ matrix.module }} tests
    run: |
      dotnet run --project tests/WorktreeApi.Tests.fsproj \
        -- --filter-test-list "${{ matrix.module }}"
```

This generates three parallel jobs:
- `test (Users)` — runs `--filter-test-list Users` → 6 tests pass
- `test (Products)` — runs `--filter-test-list Products` → 5 tests pass
- `test (Orders)` — runs `--filter-test-list Orders` → 10 tests pass

### Anti-Patterns to Avoid

- **Using `--filter` instead of `--filter-test-list`:** `--filter` in Expecto filters by slash-separated hierarchy path (e.g., `All/Users.Domain/parseRole`), NOT by test list name. `--filter-test-list Users` correctly matches the `Users.Domain` test list.
- **Running all tests in one job:** Defeats the purpose of demonstrating parallel CI. The tutorial's teaching goal is matrix parallelism.
- **`needs: [build, test]` on cleanup:** Use `needs: [test]` only, and add `if: always()` so cleanup runs even when tests fail.
- **`dotnet-version: 9.0.x` in YAML:** The project uses `net10.0`. Use `dotnet-version: '10.0.x'` to match the actual TargetFramework.
- **Skipping `dotnet restore` in test jobs:** Even with `needs: build`, each runner is a fresh VM. Restore must happen again in test jobs.

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Per-module test selection | Custom script parsing test output | `--filter-test-list <name>` Expecto CLI flag | Built-in, works reliably, matches test list names |
| Parallel CI job coordination | Shell script with `&` and `wait` | GitHub Actions matrix strategy | Native parallelism with per-job logs, status tracking |
| CI worktree cleanup | Manual `rm -rf` in shell | `git worktree prune -v` | Removes stale metadata properly; `rm -rf` leaves git metadata |
| .NET version management | Custom install scripts | `actions/setup-dotnet@v4` with `dotnet-version: '10.0.x'` | Official action, handles PATH, caching, multiple SDK versions |

**Key insight:** GitHub Actions matrix strategy is the correct abstraction for "run the same job for each module." Don't replicate this with shell scripts or separate named jobs.

## Common Pitfalls

### Pitfall 1: Wrong dotnet version in CI YAML

**What goes wrong:** Using `dotnet-version: 9.0.x` when the project targets `net10.0`. The build will fail with "The current .NET SDK does not support targeting .NET 10.0."

**Why it happens:** The existing tutorial draft (written before the project was updated) specifies `net9.0` throughout. The actual `.fsproj` files use `net10.0`.

**How to avoid:** Always use `dotnet-version: '10.0.x'` in the workflow YAML to match `<TargetFramework>net10.0</TargetFramework>`.

**Warning signs:** Tutorial text says "Setup .NET 9.0" but project file has `net10.0`.

### Pitfall 2: Wrong Expecto filter flag

**What goes wrong:** Using `-- --filter "${{ matrix.module }}"` instead of `-- --filter-test-list "${{ matrix.module }}"`. The `--filter` flag uses slash-separated hierarchy (e.g., `All/Users.Domain`) and won't match bare module names like `Users`.

**Why it happens:** The existing tutorial draft uses `--filter` which is a plausible-sounding flag name.

**How to avoid:** Use `--filter-test-list` which matches substrings in test list names. Verified: `dotnet run ... -- --filter-test-list Users` runs 6 tests (the Users.Domain tests only).

**Warning signs:** Running with `--filter Users` produces 0 tests (no hierarchy matches "Users" at the top level of the slash path).

### Pitfall 3: Fresh runner = no cached build

**What goes wrong:** Assuming test jobs can skip restore because the build job already ran. Each GitHub Actions job runs on a fresh VM. The compiled artifacts from the `build` job are NOT available in `test` jobs unless explicitly uploaded/cached.

**Why it happens:** Developers assume job state persists. It does not.

**How to avoid:** Always run `dotnet restore` (and optionally `dotnet build --no-restore`) at the start of each job that needs compiled code. For `dotnet run`, the run command will build automatically if needed.

**Warning signs:** `test` job fails with "project not found" or "assembly not found" after `needs: build` succeeds.

### Pitfall 4: Missing `if: always()` on cleanup job

**What goes wrong:** If tests fail, the cleanup job is skipped because its `needs` dependency failed. Stale state remains.

**Why it happens:** Default GitHub Actions behavior cancels dependent jobs when a dependency fails.

**How to avoid:** Add `if: always()` to any cleanup job. This ensures cleanup runs regardless of test pass/fail.

### Pitfall 5: Tutorial version text inconsistency

**What goes wrong:** Tutorial text says "Setup .NET 9.0" but YAML shows `dotnet-version: '10.0.x'`. Readers notice the inconsistency.

**Why it happens:** Tutorial was drafted before the project TargetFramework was finalized.

**How to avoid:** Update all occurrences of "9.0" in the tutorial to "10.0". Check: Step 2 fsproj example (line 60 uses `<TargetFramework>net9.0</TargetFramework>`), multiple workflow YAML sections using `9.0.x`.

## Code Examples

Verified patterns from actual codebase execution:

### Expecto --filter-test-list in Action

```bash
# Source: verified by running local test binary 2026-03-05

# List all tests (shows naming hierarchy)
dotnet run --project tests/WorktreeApi.Tests.fsproj -- --list-tests
# Output:
# All.Users.Domain.parseRole.parses lowercase admin
# All.Users.Domain.parseRole.parses capitalized Admin
# ... (6 Users tests)
# All.Products.Domain.create validation.accepts valid price and stock
# ... (5 Products tests)
# All.Orders.Domain.parseStatus.parses lowercase pending
# ... (10 Orders tests)

# Run Users module only
dotnet run --project tests/WorktreeApi.Tests.fsproj -- --filter-test-list Users
# EXPECTO! 6 tests run — 6 passed, 0 ignored, 0 failed, 0 errored.

# Run Products module only
dotnet run --project tests/WorktreeApi.Tests.fsproj -- --filter-test-list Products
# EXPECTO! 5 tests run — 5 passed, 0 ignored, 0 failed, 0 errored.

# Run Orders module only
dotnet run --project tests/WorktreeApi.Tests.fsproj -- --filter-test-list Orders
# EXPECTO! 10 tests run — 10 passed, 0 ignored, 0 failed, 0 errored.
```

### Complete ci.yml (Canonical Version)

```yaml
# Source: GitHub Actions official docs + verified Expecto filter flags
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  # Stage 1: Build the entire project once
  build:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 10.0
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

  # Stage 2: Run per-module tests in parallel using matrix strategy
  test:
    runs-on: ubuntu-latest
    needs: build
    strategy:
      matrix:
        module: [Users, Products, Orders]
      fail-fast: false   # All modules run independently

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 10.0
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore
        run: dotnet restore tests/WorktreeApi.Tests.fsproj

      - name: Run ${{ matrix.module }} tests
        run: |
          dotnet run --project tests/WorktreeApi.Tests.fsproj \
            -- --filter-test-list "${{ matrix.module }}"

  # Stage 3: Cleanup worktree metadata (always runs)
  cleanup:
    runs-on: ubuntu-latest
    needs: [test]
    if: always()

    steps:
      - uses: actions/checkout@v4

      - name: Prune stale worktree metadata
        run: |
          echo "Active worktrees before prune:"
          git worktree list
          git worktree prune -v
          echo "Active worktrees after prune:"
          git worktree list
```

### git worktree prune in CI

```bash
# Source: git-scm.com/docs/git-worktree
# Safe to run in fresh checkout — removes stale metadata from previous runs
git worktree list        # Shows only main worktree in fresh CI clone
git worktree prune -v    # No-op if no stale metadata; verbose confirms
git worktree list        # Confirms unchanged state
```

### setup-dotnet for .NET 10

```yaml
# Source: github.com/actions/setup-dotnet README — supports A.B.x format
- name: Setup .NET 10.0
  uses: actions/setup-dotnet@v4
  with:
    dotnet-version: '10.0.x'  # Installs latest patch of .NET 10
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `actions/setup-dotnet@v3` | `actions/setup-dotnet@v4` | 2023 | v4 supports `10.0.x` format, better caching |
| `dotnet test` with VSTest filters | `dotnet run -- --filter-test-list` for Expecto | When YoloDev.Expecto.TestSdk is used | Both work; `dotnet run` gives direct Expecto CLI access |
| Per-job build + test combined | Separate build job + matrix test jobs | Standard CI best practice | Avoids redundant builds; clearer job separation |

**Deprecated/outdated:**
- Using `dotnet-version: 9.0.x` in this project: The TargetFramework is `net10.0`. Must use `10.0.x`.
- `--filter` flag for module filtering in this codebase: Use `--filter-test-list` instead. The `--filter` flag requires knowing the full hierarchy path.

## Open Questions

1. **Should the tutorial's existing fsproj example in Step 2 be updated?**
   - What we know: The tutorial draft shows `<TargetFramework>net9.0</TargetFramework>` in a fsproj example (line 60). The actual test project uses `net10.0`. The tutorial also describes creating a test project from scratch ("Step 2: 테스트 프로젝트 설정") which doesn't match reality — the tests already exist.
   - What's unclear: Should the planner update the tutorial chapter to reflect the existing codebase (tests already exist), or keep the "create from scratch" narrative for pedagogical purposes?
   - Recommendation: The planner should treat the existing `tutorial/05-cicd-integration.md` as a draft requiring targeted edits: (1) fix all `net9.0` → `net10.0`, (2) fix `--filter` → `--filter-test-list`, (3) remove the "create test project" section (Step 2) since tests already exist in the codebase, (4) update the main workflow YAML with the canonical version above.

2. **Should the workflow include `dotnet fantomas --check` format checking?**
   - What we know: The tutorial draft includes a `format` job using fantomas. The `.config/dotnet-tools.json` was not checked for fantomas.
   - What's unclear: Whether fantomas is already configured in this project.
   - Recommendation: Check for `.config/dotnet-tools.json` before including the format job. If fantomas is not configured, omit the format job to avoid CI failures. Keep the workflow focused on the tutorial's core teaching: matrix parallel test builds.

3. **Does `git worktree prune` in a fresh CI clone do anything meaningful?**
   - What we know: In a fresh `actions/checkout` clone, there are no worktrees to prune. `git worktree prune -v` will be a no-op.
   - What's unclear: Whether this is still pedagogically useful (teaching the command) or misleading (showing a step that does nothing).
   - Recommendation: Include the cleanup job anyway for tutorial completeness. Explain in the tutorial that this step is a no-op in fresh clones but is essential when CI itself creates worktrees. This teaches readers the command for real-world scenarios.

## Sources

### Primary (HIGH confidence)
- Local Expecto binary execution (`dotnet run -- --help`, `--list-tests`, `--filter-test-list`) — verified 2026-03-05
- `tests/UsersTests.fs`, `tests/ProductsTests.fs`, `tests/OrdersTests.fs`, `tests/TestMain.fs` — actual test naming confirmed
- `src/WorktreeApi.fsproj`, `tests/WorktreeApi.Tests.fsproj` — TargetFramework confirmed as `net10.0`
- [GitHub Actions matrix strategy docs](https://docs.github.com/en/actions/using-jobs/using-a-matrix-for-your-jobs) — matrix syntax, fail-fast, include/exclude verified

### Secondary (MEDIUM confidence)
- [actions/setup-dotnet README](https://github.com/actions/setup-dotnet) — `10.0.x` format confirmed supported
- [GitHub Actions .NET docs](https://docs.github.com/en/actions/automating-builds-and-tests/building-and-testing-net) — standard dotnet CI patterns
- [git-worktree documentation](https://git-scm.com/docs/git-worktree) — `prune -v` behavior

### Tertiary (LOW confidence)
- Tutorial draft `tutorial/05-cicd-integration.md` — used as starting point, known to have version errors requiring correction

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — verified with actual running code and official docs
- Architecture: HIGH — Expecto filter flags tested locally; matrix YAML from official docs
- Pitfalls: HIGH — Version discrepancy confirmed by reading both fsproj files; wrong flag confirmed by testing both `--filter` and `--filter-test-list` locally

**Research date:** 2026-03-05
**Valid until:** 2026-06-05 (stable — GitHub Actions matrix syntax and Expecto CLI are mature; dotnet-version format is stable)
