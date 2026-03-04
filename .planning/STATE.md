# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-04)

**Core value:** worktree 병렬 개발이 순차 개발보다 얼마나 효율적인지 실제 코드와 함께 체감하게 만드는 것
**Current focus:** Phase 2 — Parallel Modules

## Current Position

Phase: 2 of 5 (Parallel Modules)
Plan: 2 of 3 in current phase
Status: In progress
Last activity: 2026-03-05 — Completed 02-02-PLAN.md (Products module + Products tests)

Progress: [████░░░░░░] 37%

## Performance Metrics

**Velocity:**
- Total plans completed: 3
- Average duration: 2.3 min
- Total execution time: 7 min

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 01-foundation | 1/1 | 2 min | 2 min |
| 02-parallel-modules | 2/3 | 5 min | 2.5 min |

**Recent Trend:**
- Last 5 plans: 01-01 (2 min), 02-01 (3 min), 02-02 (2 min)
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
- [02-01]: Role DU serializes as {"Case":"Admin"} with JsonFSharpOptions.Default() — tutorial JSON output examples in chapter 02 need updating
- [02-01]: Test project duplicates domain logic rather than ProjectReference — simpler build, acceptable for tutorial
- [02-02]: Products validation checks Price first, then Stock — error messages are deterministic
- [02-02]: Products module has NO dependency on Users module — only depends on WorktreeApi.Core

### Pending Todos

None yet.

### Blockers/Concerns

- [Phase 5]: GitHub Actions matrix strategy for per-worktree builds needs verification during phase planning — research flagged this as niche
- [General]: `claude --worktree` exact flag syntax should be verified against live Claude Code CLI before tutorial authoring (CLI flags can change)
- [01-01]: Tutorial references net9.0 but repo uses net10.0 — plan 01-02 (tutorial) should document this discrepancy for readers

## Session Continuity

Last session: 2026-03-04T22:46:40Z
Stopped at: Completed 02-02-PLAN.md — Products module + Products tests complete
Resume file: None
