# Project Research Summary

**Project:** Claude Code + git worktree parallel development tutorial (F# REST API)
**Domain:** Developer tutorial — parallel AI-assisted development workflow
**Researched:** 2026-03-04
**Confidence:** HIGH

## Executive Summary

This project is a developer tutorial teaching Claude Code's `--worktree` flag and git worktree parallel development patterns, using an F# Giraffe REST API as the working example domain. Expert tutorial authors in this space agree the key design principle is domain independence: the example codebase must be architected so that separate modules (Users, Products, Orders) can be developed simultaneously in isolated worktrees with zero file overlap — except at deliberate integration points. The F# choice is strategically correct because F# module isolation is structurally enforced, making the independence requirement obvious rather than optional.

The recommended approach is a four-phase tutorial structure that mirrors the actual development workflow: establish shared foundations first (Core.fs + skeleton Program.fs), fan out to parallel domain module development in separate worktrees, merge back with conflict resolution, then tackle advanced scenarios (hotfix-while-feature, CI/CD). The API itself uses Giraffe 8.2.0 on .NET 9.0 with in-memory stores per module — database tooling is deliberately excluded to keep focus on the worktree pattern, not the persistence layer.

The primary risks are operational rather than technical: worktrees created inside the repo root cause tooling confusion; manually deleted worktrees leave stale git metadata; F#'s `.fsproj` compilation order creates non-obvious merge conflicts; and concurrent dev servers collide on ports. All six critical pitfalls identified in research are Phase 1 concerns — they must be addressed in setup instructions before any parallel work begins. The tutorial also risks "tutorial hell" if readers are only shown happy-path output without deliberate break-and-fix exercises.

## Key Findings

### Recommended Stack

The stack is stable and well-documented. Giraffe 8.2.0 on .NET 9.0 is the clear choice for an F# REST API tutorial today — it wraps ASP.NET Core with idiomatic F# handler composition (the fish operator `>=>`) and bundles FSharp.SystemTextJson for correct discriminated union serialization. Expecto 10.2.3 is the right test framework because tests are plain F# values, matching the functional style the tutorial promotes. Fantomas 7.0.5 enforces consistent formatting as a dotnet local tool.

See `.planning/research/STACK.md` for full version table and alternatives analysis.

**Core technologies:**
- .NET 9.0 + F# 9.0: Runtime and language — current STS release, explicitly targeted by Giraffe 8.x
- Giraffe 8.2.0: HTTP framework — idiomatic F# handler pipeline, highest community adoption, bundles serialization
- FSharp.SystemTextJson: JSON serialization — auto-resolved by Giraffe, handles F# discriminated unions correctly
- Expecto 10.2.3: Test framework — tests-as-values, idiomatic F#, better DX than xUnit for teaching
- Fantomas 7.0.5: Code formatter — enforces consistent style across parallel worktrees
- git (built-in worktree): Parallel branch management — no third-party tool required for the tutorial itself

**Critical version constraints:**
- Do not target .NET 6/7 (EOL; Giraffe 8.x dropped them)
- Do not target .NET 10 as primary target yet (Giraffe 8.2.0 only lists net10.0 as "computed" compatibility)
- Use `giraffe-template` for scaffolding (`dotnet new install "giraffe-template::*"`)

### Expected Features

The tutorial has two feature domains: the tutorial content scenarios and the REST API domain features that serve as the example. Both must be right for the tutorial to work.

See `.planning/research/FEATURES.md` for full feature matrix and competitor analysis.

**Must have (table stakes) — Tutorial content:**
- Basic worktree setup: `git worktree add`, `git worktree list`, `git worktree remove` lifecycle
- `claude --worktree` flag walkthrough — the headline Claude Code feature for this tutorial
- Parallel terminal session demo with 3 sessions (main + 2 domain worktrees)
- Working F# REST API codebase (Core + Users + Products at minimum) that readers can run
- Merge workflow including conflict resolution in Program.fs
- Worktree cleanup instructions (the lifecycle loop must close)

**Must have (table stakes) — REST API:**
- Core.fs with shared types (UserId, ProductId, ApiResponse) — required before parallel work
- Users module: full CRUD on `/users` with F# discriminated union Role field
- Products module: full CRUD on `/products`
- Orders module: full CRUD on `/orders` referencing Users and Products by ID only (not by value)
- In-memory store per domain module, NOT a shared database
- Proper HTTP status codes (200, 201, 204, 400, 404)

**Should have (competitive differentiators):**
- Explicit conflict scenario with resolution walkthrough (Scenario 2) — most tutorials skip this
- Hotfix-while-feature scenario (Scenario 3) — the scenario developers actually fear
- Efficiency quantification: sequential vs. parallel time comparison (makes the value proposition concrete)
- Intentionally shared type in Core.fs that both worktrees plausibly modify (teaching tool for conflicts)
- Korean-English mixed language style targeting the underserved Korean developer community
- Session naming with `/rename` tips for managing 3+ concurrent sessions

**Defer (v2+):**
- CI/CD integration showing per-worktree parallel builds (Scenario 4) — requires CI setup knowledge
- Advanced subagent `isolation: worktree` frontmatter example — power user content
- Korean localization polish — get content right first
- Korean localization polish — get content right first

**Hard anti-features (do not build):**
- Real database (PostgreSQL/SQLite) — distracts from worktree pattern, creates per-worktree setup complexity
- Authentication/JWT — couples Users to all modules, destroys the independence that makes parallel demo work
- Docker, monorepo tooling, production deployment — all scope creep that obscures the tutorial goal
- Shopping Cart module — inherently coupled to Users and Products simultaneously, cannot be developed in isolation

### Architecture Approach

The architecture has two cleanly separated concerns: the F# API source code in `src/` and the tutorial documents in `tutorial/`. The source code follows a strict layering rule enforced by F#'s compilation order: `Core.fs` (shared types only) is compiled first, then each domain pair (`Domain.fs` + `Handlers.fs`) in sequence, then `Program.fs` last as the single route composition and integration point. This ordering is the structural guarantee that enables true parallel worktree development — three teams can write `Users/`, `Products/`, and `Orders/` simultaneously with zero file-level conflicts.

See `.planning/research/ARCHITECTURE.md` for full component diagram and pattern examples.

**Major components:**
1. `Core.fs` — shared types (UserId, ProductId, OrderId, ApiResponse); the dependency ceiling; no domain module may import another
2. `Users/Domain.fs` + `Users/Handlers.fs` — pure business logic and HTTP adapter layer; self-contained unit
3. `Products/Domain.fs` + `Products/Handlers.fs` — independent domain pair; no dependency on Users or Orders
4. `Orders/Domain.fs` + `Orders/Handlers.fs` — references Core types only, not Users/Products domain modules directly
5. `Program.fs` — route composition only (`subRoute "/api/users" Users.Handlers.routes`); the intentional merge conflict zone
6. `tutorial/` — numbered Markdown chapters (01-introduction through 06-cicd-integration) with `README.md` index

**Key architectural patterns:**
- Ports and Adapters: Domain.fs is pure F# (no Giraffe), Handlers.fs is the adapter; enables 100% testable domain logic
- Core.fs as dependency ceiling: enforced rule that no domain module imports another; cross-domain references go through Core types
- SubRoute composition in Program.fs: single integration point, deliberately creates merge conflicts for tutorial teaching value
- Per-module in-memory store: each Domain.fs holds its own `let mutable` store; avoids shared mutable state conflicts across worktrees

### Critical Pitfalls

All six critical pitfalls map to Phase 1 (Project Setup). They must all be addressed before parallel development begins.

See `.planning/research/PITFALLS.md` for full recovery strategies and integration gotchas.

1. **Worktrees created inside the repo root** — always use sibling paths (`../project-feature-x`) or bare repo pattern; teach this as the first command in the tutorial, before anything else
2. **Manual `rm -rf` of worktree directories** — always use `git worktree remove` + `git worktree prune`; stale metadata blocks future branch operations with cryptic errors; demonstrate the full lifecycle
3. **F# `.fsproj` compilation order conflicts** — annotate `.fsproj` with ordering zone comments before parallel work; run `dotnet build` in CI on every `.fsproj` change; teach that ordering is semantically load-bearing (unlike C# or JS)
4. **Port collisions between concurrent dev servers** — never use fixed port 5000; teach `ASPNETCORE_URLS` environment variable override; document port assignment scheme (e.g., main=5000, users-worktree=5001, products-worktree=5002) at the top of the tutorial
5. **Shared database state across worktrees** — use in-memory stores (not SQLite) to eliminate this class of problem entirely; if SQLite is used, per-worktree `.env` with different `DATABASE_PATH` is mandatory
6. **Passive tutorial learning (tutorial hell)** — every phase must include a deliberate break-and-fix exercise; readers must adapt commands to a different branch name at minimum; include "What would break if..." challenge questions at phase ends

## Implications for Roadmap

Based on research, the architecture's build order and the tutorial's pedagogical structure align directly into a 5-phase roadmap. The F# compiler enforces the dependency order; the roadmap should reflect it.

### Phase 1: Foundation — Core API + Project Setup
**Rationale:** F# compilation order requires Core.fs to exist before any domain module branches. All 6 critical pitfalls are Phase 1 concerns. The tutorial cannot teach worktrees until readers have a working project to parallelize. This phase is the prerequisite for everything else and must be solid before any fan-out.
**Delivers:** Working Giraffe project scaffold (`giraffe-template`), annotated `.fsproj` with compilation order zones, `Core.fs` with shared types, skeleton `Program.fs`, Fantomas formatter configured, tutorial chapters 01 (introduction) and 02 (project setup) with explicit worktree lifecycle instructions including all 6 pitfall preventions.
**Addresses:** All table-stakes features for project setup; REST API skeleton; worktree setup section from FEATURES.md
**Avoids:** All 6 critical pitfalls — this phase IS the pitfall prevention phase

**Research flag:** Standard patterns — well-documented Giraffe scaffolding and git worktree setup; no additional research needed.

### Phase 2: Parallel Module Development (Scenario 1 — Happy Path)
**Rationale:** With Core.fs established and worktree conventions set, this is the tutorial's core teaching moment. Users and Products modules are independent (no cross-domain imports) and can be developed in parallel worktrees simultaneously. Orders is held for Phase 3 because it depends on Users + Products being merged first (it references their IDs). This phase produces the clean, no-conflict merge story.
**Delivers:** Users module (CRUD + Role DU), Products module (CRUD + stock field), parallel worktree session demo with 3 terminals, clean merge workflow (fast-forward), tutorial chapter 03 (parallel development). Efficiency quantification: 3 modules × 20 min sequential vs ~20 min parallel.
**Uses:** Giraffe 8.2.0 handler patterns, Expecto tests per module, `ctx.BindJsonAsync<T>()` for request binding, `json result` for responses, `routef` for parameterized routes
**Implements:** Users/Domain.fs + Handlers.fs, Products/Domain.fs + Handlers.fs, per-module in-memory stores

**Research flag:** Standard patterns — Giraffe CRUD handler patterns are well-documented; skip phase research.

### Phase 3: Merge + Conflict Resolution (Scenario 2) + Orders Module
**Rationale:** Once readers have done a clean merge in Phase 2, they're ready to face the intentional conflict scenario. Both Users and Products worktrees modify a shared field in `Core.fs` (e.g., adding a field to `ApiResponse`), creating a merge conflict. This is the tutorial's highest-value differentiator: most tutorials skip conflict resolution. Orders module is added in this phase because it depends on Users and Products being merged into main; it demonstrates the fan-out → merge → extend pattern.
**Delivers:** Intentional conflict in Core.fs, merge conflict resolution walkthrough, Orders module (CRUD + OrderItem embedded array), tutorial chapter 04 (merge conflicts), completed three-module API.
**Addresses:** Conflict scenario differentiator from FEATURES.md; Orders "natural integrator" feature; Program.fs as intentional merge conflict zone from ARCHITECTURE.md

**Research flag:** Standard patterns — git merge conflict resolution is well-documented; conflict in Program.fs is architecture-by-design.

### Phase 4: Advanced Scenarios — Hotfix + Feature Parallel (Scenario 3)
**Rationale:** After readers master the happy path and conflict resolution, the hotfix-while-feature scenario is the next fear to address. This phase demonstrates that worktrees enable genuinely parallel workflows: a production bug can be fixed on main while feature work continues in worktrees without interruption. The rebase-onto-updated-main workflow is taught here.
**Delivers:** Hotfix worktree branching from main, patch applied while feature worktrees continue, rebase of feature branches onto updated main, tutorial chapter 05 (hotfix parallel).
**Addresses:** Hotfix-while-feature differentiator from FEATURES.md; Hotfix flow from ARCHITECTURE.md data flow section; branch deletion blocked by checked-out worktree pitfall from PITFALLS.md

**Research flag:** Standard patterns — git rebase and hotfix workflows are well-documented.

### Phase 5: CI/CD Integration (Scenario 4)
**Rationale:** CI/CD is a v2 differentiator with the highest reader value for teams but the most setup overhead. It belongs last because it requires all domain modules to be merged (Phases 1-4 complete) and requires CI/CD knowledge that not all tutorial readers will have. It is the "real-world team usage" proof point.
**Delivers:** GitHub Actions workflow showing per-module parallel builds (matrix strategy), tutorial chapter 06 (CI/CD integration).
**Addresses:** CI/CD integration differentiator from FEATURES.md
**Avoids:** N/A — no new pitfalls at this phase; pitfall 3 (.fsproj CI validation) is already established in Phase 1

**Research flag:** Needs deeper research — GitHub Actions matrix strategy for per-worktree builds is not standard documentation; actual YAML patterns for this specific use case should be verified during phase planning.

### Phase Ordering Rationale

- Phases 1 → 2 → 3 → 4 → 5 mirror the dependency graph: Core first, then domain modules, then merge, then advanced patterns
- F# compiler order is the structural justification: you cannot write domain modules before Core.fs exists
- All 6 critical pitfalls are front-loaded into Phase 1 so they don't surface as surprises in later phases
- Orders module is placed in Phase 3 (not Phase 2) because it references Users and Products IDs — those modules must exist before Orders can be written and tested
- CI/CD is Phase 5 not Phase 3 because it requires the complete merged codebase; it cannot be demonstrated meaningfully on partial module sets
- Tutorial chapters map 1:1 to phases: readers can stop after any phase and have a coherent, complete learning unit

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 5 (CI/CD):** GitHub Actions matrix strategy for per-worktree parallel builds is niche. The specific YAML patterns, caching strategy for NuGet across matrix jobs, and worktree checkout behavior in CI need verification before implementation.

Phases with standard patterns (skip research-phase):
- **Phase 1 (Foundation):** Giraffe template scaffolding and git worktree setup are fully documented in official sources.
- **Phase 2 (Parallel Modules):** Giraffe CRUD patterns are documented in multiple tutorials; Expecto test patterns are stable.
- **Phase 3 (Merge + Orders):** Git conflict resolution and Orders CRUD are straightforward extensions of Phase 2 patterns.
- **Phase 4 (Hotfix):** Git hotfix + rebase workflow is textbook git; no novel patterns.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | All versions verified against NuGet and GitHub release notes; explicit compatibility matrix confirmed; no inferred data |
| Features | HIGH | Feature landscape sourced from official Claude Code docs, multiple practitioner articles, and ecosystem tutorials; competitor gap analysis based on direct content review |
| Architecture | HIGH | Architecture patterns sourced from Giraffe official docs and F# for Fun and Profit; module isolation rules are compiler-enforced (not convention); worktree isolation patterns confirmed across multiple independent sources |
| Pitfalls | HIGH | All 6 critical pitfalls sourced from practitioner experience articles and official git documentation; recovery strategies verified against git-scm.com official docs |

**Overall confidence:** HIGH

### Gaps to Address

- **Giraffe net10.0 support timeline:** Giraffe 8.2.0 lists net10.0 as "computed" compatibility only. If the tutorial has a long shelf life (past late 2026), verify whether Giraffe 8.3+ or 9.x explicitly targets net10.0 before recommending an upgrade path.
- **`claude --worktree` exact flag behavior:** Feature research cites Claude Code official docs as the authority, but the exact flag syntax (named worktree vs. auto-named vs. mid-session creation) should be verified against the live Claude Code CLI during tutorial authoring — CLI flags can change across Claude Code releases.
- **Korean developer audience size validation:** The tutorial targets Korean developers, but no data on actual audience size or existing Claude Code content in Korean was gathered. This is a target assumption, not a validated market. Validate via Korean developer community feedback before investing in Korean localization polish.
- **In-memory store thread safety:** The architecture uses module-level `let mutable` dictionaries per domain. Under concurrent test load this is not thread-safe. For the tutorial this is acceptable and documented as a simplification, but the tutorial must explicitly state this limitation rather than leaving readers to discover it.

## Sources

### Primary (HIGH confidence)
- [NuGet: Giraffe 8.2.0](https://www.nuget.org/packages/Giraffe) — version, targets, dependencies verified
- [GitHub: giraffe-fsharp/Giraffe](https://github.com/giraffe-fsharp/Giraffe) — architecture, handler patterns, RELEASE_NOTES
- [Claude Code Common Workflows — Official Docs](https://code.claude.com/docs/en/common-workflows) — `--worktree` flag behavior, subagent isolation frontmatter
- [Giraffe Official Documentation](https://giraffe.wiki/docs) — HttpHandler composition, routing, subRoute patterns
- [Official git-worktree documentation — git-scm.com](https://git-scm.com/docs/git-worktree) — worktree lifecycle, prune behavior
- [F# for Fun and Profit: Organizing Modules](https://fsharpforfunandprofit.com/posts/recipe-part3/) — compilation order rules
- [F# for Fun and Profit: Cyclic Dependencies](https://fsharpforfunandprofit.com/posts/cyclic-dependencies/) — cross-module import anti-patterns

### Secondary (MEDIUM confidence)
- [Claude Code Worktrees Guide — claudefa.st](https://claudefa.st/blog/guide/development/worktree-guide) — scenario-based decision matrix
- [Running Multiple Claude Code Sessions in Parallel — dev.to](https://dev.to/datadeer/part-2-running-multiple-claude-code-sessions-in-parallel-with-git-worktree-165i) — token cost concern, parallel session workflow
- [Git Worktrees for Parallel AI Coding Agents — Upsun](https://devcenter.upsun.com/posts/git-worktrees-for-parallel-ai-coding-agents/) — isolation principles, file state model
- [Building REST APIs in Giraffe Pt 1 + Pt 2 — functionalsoftware.se](https://functionalsoftware.se/posts/building-a-rest-api-in-giraffe-pt1) — CRUD handler patterns
- [How to use git worktree in a clean way — Morgan Cugerone](https://morgan.cugerone.com/blog/how-to-use-git-worktree-and-in-a-clean-way/) — bare repo pattern, sibling directory layout
- [Git Worktree: Pros, Cons, and the Gotchas Worth Knowing — Josh Tune](https://joshtune.com/posts/git-worktree-pros-cons/) — manual deletion pitfall, stale metadata recovery
- [endoflife.date: .NET](https://endoflife.date/dotnet) — .NET 9 STS support until Nov 2026, .NET 10 LTS until Nov 2028

### Tertiary (LOW confidence)
- Efficiency quantification (3 modules × 20 min = ~60 min sequential vs ~20 min parallel) — estimated from general parallel workflow research, not empirically measured; validate with timed runs during tutorial authoring

---
*Research completed: 2026-03-04*
*Ready for roadmap: yes*
