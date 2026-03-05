# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-04)

**Core value:** worktree 병렬 개발이 순차 개발보다 얼마나 효율적인지 실제 코드와 함께 체감하게 만드는 것
**Current focus:** Phase 3 — Merge + Conflict Resolution

## Current Position

Phase: 3 of 5 (Merge + Conflict Resolution)
Plan: 2 of 3 in current phase
Status: In progress
Last activity: 2026-03-05 — Completed 03-02-PLAN.md (Pagination worktree with PaginatedResponse)

Progress: [█████░░░░░] 50%

## Performance Metrics

**Velocity:**
- Total plans completed: 5
- Average duration: 1.8 min
- Total execution time: 10 min

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 01-foundation | 1/1 | 2 min | 2 min |
| 02-parallel-modules | 3/3 | 7 min | 2.3 min |
| 03-merge-conflict-resolution | 2/3 | 1 min | <1 min |

**Recent Trend:**
- Last 5 plans: 02-01 (3 min), 02-02 (2 min), 02-03 (2 min), 03-01 (?), 03-02 (1 min)
- Trend: Stable

*Updated after each plan completion*

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- [Init]: F# + Giraffe 8.2.0 on .NET 9.0 — most mature F# web framework, idiomatic handler composition
- [Init]: Domain split (Users/Products/Orders) — each is independently compilable, ideal for parallel worktree demo
- [Init]: In-memory stores per module — eliminates shared DB state conflicts across worktrees
- [Research]: All 6 critical pitfalls are Phase 1 concerns — must be documented before any parallel work begins
- [Research]: Phase 5 (CI/CD) needs deeper research during planning — GitHub Actions matrix YAML for per-worktree builds
- [01-01]: TargetFramework is net10.0 (not net9.0) — only .NET 10.0.2 installed on this machine; tutorial should note readers on .NET 9 use net9.0
- [01-01]: Hand-wrote .fsproj instead of using dotnet new giraffe template — avoids Views/Models/HttpHandlers template artifacts
- [02-01]: FsharpFriendlySerializer requires PropertyNameCaseInsensitive=true — F# record fields are PascalCase but JSON requests use lowercase; constructor takes second JsonSerializerOptions parameter
- [02-01]: Role DU serializes as {"Case":"Admin"} with JsonFSharpOptions.Default() — CONFIRMED by running API
- [02-01]: Test project duplicates domain logic rather than ProjectReference — simpler build, acceptable for tutorial
- [02-02]: Products validation checks Price first, then Stock — error messages are deterministic
- [02-02]: Products module has NO dependency on Users module — only depends on WorktreeApi.Core
- [02-03]: UserId/ProductId (single-case DUs) serialize as plain UUID strings — {"case":"UserId","fields":["..."]} format is INCORRECT
- [02-03]: F# record fields in ApiResponse serialize as PascalCase (Data/Message/Success) — FsharpFriendlySerializer does not apply camelCase by default
- [02-03]: Tutorial JSON output examples updated to reflect actual API behavior
- [03-02]: feature/pagination branches from same base (7a404a5) as feature/orders — both modify Core.fs independently to create the 3-way merge conflict scenario in Plan 03-03

### Pending Todos

None yet.

### Blockers/Concerns

- [Phase 5]: GitHub Actions matrix strategy for per-worktree builds needs verification during phase planning — research flagged this as niche
- [General]: `claude --worktree` exact flag syntax should be verified against live Claude Code CLI before tutorial authoring (CLI flags can change)
- [01-01]: Tutorial references net9.0 but repo uses net10.0 — plan 01-02 (tutorial) should document this discrepancy for readers

## Session Continuity

Last session: 2026-03-05T00:04:36Z
Stopped at: Completed 03-02-PLAN.md — feature/pagination branch created with PaginatedResponse type
Resume file: None
