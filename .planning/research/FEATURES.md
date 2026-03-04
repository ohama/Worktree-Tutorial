# Feature Research

**Domain:** Claude Code + git worktree parallel development tutorial with F# REST API example
**Researched:** 2026-03-04
**Confidence:** HIGH

---

## Feature Landscape

This research maps two intersecting feature domains:
1. **Tutorial features** — what content a Claude Code / git worktree tutorial must cover
2. **REST API domain features** — which F# REST API modules and operations demonstrate parallelism best

Both are covered below.

---

## Part 1: Tutorial Content Features

### Table Stakes (Readers Expect These)

Features readers assume the tutorial covers. Missing any of these and readers leave or feel cheated.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Basic worktree setup (`git worktree add`) | Every worktree tutorial starts here — it's the prerequisite | LOW | Must show both manual git command AND `claude --worktree` flag |
| `claude --worktree <name>` flag walkthrough | Primary target audience uses Claude Code; this is the headline feature | LOW | Cover named worktree, auto-named, and mid-session creation |
| Parallel terminal sessions demo | Readers need to see multiple terminals side-by-side to understand the concept | LOW | Show 3 terminals: main + 2 domain worktrees |
| Working F# REST API example codebase | Tutorial requires real code, not pseudocode — readers need to run it | MEDIUM | Giraffe framework, 3 domain modules, in-memory store |
| Worktree cleanup instructions | Readers worry about leftover directories — must close the loop | LOW | Cover `git worktree remove` and Claude's auto-cleanup |
| Merge workflow (worktree branch → main) | Every parallel dev workflow ends with integration — readers need this | MEDIUM | Show `git merge`, including fast-forward case |
| `git worktree list` command | Readers need to orient themselves — what worktrees exist? | LOW | Show at each stage so readers can track state |

### Differentiators (Competitive Advantage)

Features that set this tutorial apart from generic git worktree guides.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| F# + Giraffe as the example domain | Niche language makes modules obviously independent; no "I could just use JavaScript" escape hatch. Readers focus on the worktree pattern, not the language | MEDIUM | Reinforce that pattern is language-agnostic |
| Explicit conflict scenario with resolution walkthrough | Most tutorials only show the happy path — readers need to see conflict handling to trust the workflow in production | HIGH | Intentionally create a conflict in shared `Core.fs` type, resolve it |
| Hotfix-while-feature scenario (Scenario 3) | This is the scenario developers actually fear. Showing it concretely differentiates from academic tutorials | MEDIUM | main branch hotfix while worktree feature work continues |
| CI/CD integration showing per-worktree builds | No other tutorial in the ecosystem currently covers this; shows real-world team usage | HIGH | GitHub Actions matrix strategy or parallel jobs per domain module |
| Korean-English mixed language style (한영 혼용) | Targets Korean developer community specifically; this audience is underserved with Claude Code content | MEDIUM | Korean explanations with English code/commands |
| Efficiency quantification ("parallel vs. sequential") | Core value proposition from PROJECT.md: make the speedup tangible, not just theoretical | MEDIUM | Estimate time savings: 3 modules × 20 min = 60 min sequential vs ~20 min parallel |
| Subagent isolation frontmatter example (`isolation: worktree`) | Shows advanced Claude Code capability beyond basic flag usage | LOW | Add `.claude/agents/` example for automated isolation |
| Session naming with `/rename` | Prevents confusion when juggling 3+ sessions — practical tip not in official docs | LOW | e.g., `/rename users-module`, `/rename products-module` |

### Anti-Features (Commonly Requested, Often Problematic)

Features that seem useful for the tutorial but create problems.

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Real database (PostgreSQL/SQLite) | Feels more "real-world" | Adds setup complexity that distracts from the worktree pattern; readers get stuck on DB config, not the tutorial goal | Use in-memory store (`Map<string, T>`); note in tutorial it's production-replaceable |
| Authentication / JWT | Realistic API feature | Users module with auth becomes tightly coupled to all other modules, destroying the independence that makes the parallel demo work | Use simple user records with no auth; flag it as out of scope |
| Monorepo tooling (Nx, Turborepo) | Seems related to parallel builds | Introduces entirely separate ecosystem that overshadows the worktree + Claude Code story | Keep it as a single-solution F# project with logical module separation |
| Docker containerization | Modern best practice | Adds setup overhead; tutorial time is limited, Docker troubleshooting is deep | Skip; note that readers can add it after completing tutorial |
| GSD workflow integration (`/gsd` commands) | PROJECT.md says it's out of scope; some readers will assume it's included | Scope creep, breaks the tutorial's focus | Explicitly state it's a separate topic at the tutorial start |
| Production deployment (Azure, AWS) | Readers ask "how do I deploy this?" | Deployment varies by infrastructure; adds hours of content for zero tutorial value | Add a one-sentence "out of scope" note with pointer to Giraffe deployment docs |
| "Best of N" model comparison | Popular AI dev pattern | Changes the tutorial from "learn worktrees" to "benchmark AI models" — different audience, different goal | Mention as a valid use case in a sidebar, don't build it |
| Real-time conflict detection tooling | Interesting but complex | Conflicts in the tutorial are intentional teaching moments; automated detection removes the learning | Show manual conflict resolution via `git diff` and editor; that's the learning |

---

## Part 2: REST API Domain Module Features

### Table Stakes (Readers Expect These)

Baseline REST API operations that must be present or the example feels incomplete.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Users module: full CRUD (`/users`) | Users is the prototypical REST resource; readers orient around it first | LOW | GET all, GET by id, POST, PUT/PATCH, DELETE |
| Products module: full CRUD (`/products`) | Products domain is universally understood; zero domain knowledge required to participate | LOW | GET all, GET by id, POST, PUT/PATCH, DELETE |
| Orders module: full CRUD (`/orders`) | Completes the classic e-commerce triad; naturally references Users and Products | MEDIUM | GET all, GET by id, POST (create), GET by userId |
| JSON request/response throughout | Standard REST expectation; Giraffe's `BindJsonAsync` and `json` handlers cover this | LOW | Use F# record types for serialization |
| Proper HTTP status codes | Readers judge API quality by status code correctness | LOW | 200, 201, 204, 400, 404 minimum |
| In-memory data store (mutable Map) | Readers need data persistence within a session without DB setup | LOW | One store per domain module; initialized at startup |
| Route organization in separate files | Demonstrates module independence at the code level | LOW | `UsersHandlers.fs`, `ProductsHandlers.fs`, `OrdersHandlers.fs` |

### Differentiators (Domain Module Selection Rationale)

Why these three modules (Users/Products/Orders) are the right choice for demonstrating parallel development, and what features within them serve the tutorial.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Shared Core types module (separate from domain modules) | Makes the fan-out architecture explicit: Core is phase 1, domains are phase 2, merge is phase 3 | MEDIUM | `Core.fs` with shared types (UserId, ProductId, etc.); intentionally create one shared type that needs merging |
| Orders referencing Users and Products by ID (not by value) | Creates a realistic but resolvable dependency: Orders needs UserId and ProductId, but not the full User/Product objects | LOW | Use `UserId = string` type alias rather than embedding User record; keeps modules independent |
| Orders as the "natural integrator" module | Demonstrates the fan-out → merge pattern: develop Users and Products in parallel, then build Orders which references both | MEDIUM | Build in Scenario 1: Users worktree + Products worktree → merge → Orders on main |
| Intentionally shared type in Core that generates a conflict | Without a deliberate conflict, Scenario 2 (conflict resolution) has nothing to teach | MEDIUM | E.g., both worktrees add a new field to `ApiError` type in `Core.fs`; merge creates conflict |
| Products: name, description, price, stock fields | Gives Products module enough variety to be realistic (string, string, decimal, int) without being complex | LOW | Stock level is a good PATCH-only field (partial update demo) |
| Users: email, name, role fields | Role field (enum: Admin/User) demonstrates F# discriminated unions in REST context | LOW | Simple DU: `type Role = Admin | User` |

### Anti-Features (REST Domain Choices to Avoid)

Domain design decisions that seem helpful but undermine the parallel development tutorial.

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Shopping Cart module | Completes the e-commerce picture | Cart is stateful and inherently coupled to Users and Products simultaneously — cannot be developed in isolation | Leave Cart out; mention it in "next steps" as an exercise |
| Category/Tag system for Products | Makes Products more realistic | Requires a separate entity with relationships, adding a 4th domain module that complicates the fan-out story | Use a simple `category: string` field on Product instead |
| Order line items as separate endpoint (`/orders/{id}/items`) | RESTfully correct | Nested resources introduce complexity and couple Orders more tightly to Products | Embed items as a JSON array on the Order record: `items: OrderItem list` |
| User authentication endpoints (`/auth/login`, `/auth/register`) | Natural Users module extension | Auth coupling means Users cannot be developed independently from all other modules | Exclude auth; note it as production addition |
| Pagination on list endpoints | Production best practice | Increases handler complexity; distracts from the worktree pattern | Use simple `GET /users` returning all records; add pagination as "next steps" |
| Search/filtering endpoints (`/products?category=X`) | Useful feature | Query parameter handling adds handler complexity without demonstrating anything new about parallel development | Skip; stick to simple CRUD |

---

## Feature Dependencies

```
[Shared Core Module (types, error handling)]
    └──required by──> [Users Module]
    └──required by──> [Products Module]
    └──required by──> [Orders Module]

[Users Module]
    └──id-referenced-by──> [Orders Module]

[Products Module]
    └──id-referenced-by──> [Orders Module]

[Basic worktree setup]
    └──required by──> [Scenario 1: Parallel Module Development]
    └──required by──> [Scenario 2: Merge & Conflict Resolution]
    └──required by──> [Scenario 3: Hotfix + Feature Parallel]
    └──required by──> [Scenario 4: CI/CD Integration]

[Scenario 1: Parallel Module Development]
    └──prerequisite for──> [Scenario 2: Merge & Conflict Resolution]
    └──prerequisite for──> [Scenario 3: Hotfix + Feature Parallel]

[Scenario 2: Merge & Conflict Resolution]
    └──prerequisite for──> [Scenario 4: CI/CD Integration]
```

### Dependency Notes

- **Core Module must exist before worktrees fan out:** The tutorial must establish Core types (shared error types, id aliases, etc.) in a main branch before spawning domain worktrees. If Core doesn't exist, domain handlers have no shared contracts and readers can't demonstrate fan-out correctly.
- **Orders depends on Users + Products by ID only:** This is a deliberate design choice to maintain module independence. Orders holds `userId: string` and `productId: string`, not the full objects. This allows Users and Products to be developed in parallel worktrees without Orders needing to wait.
- **Conflict scenario requires shared Core file:** Scenario 2 (conflict resolution) only works if both worktrees touch the same file. Design Core to have a shared type both Users and Products worktrees plausibly modify (e.g., `ApiError` or `Pagination` type). This is intentional, not accidental.
- **CI/CD scenario requires all domain modules to be merged first:** Scenario 4 shows per-module parallel builds; this only makes sense after Scenarios 1-3 establish the merged codebase.

---

## MVP Definition

### Launch With (v1)

Minimum content to validate the tutorial as a teaching artifact.

- [ ] Working F# + Giraffe API with Core + Users + Products modules — establishes the parallel development target
- [ ] Tutorial document covering Scenario 1 (parallel module development) only — the core concept
- [ ] Basic worktree setup section: `git worktree add`, `claude --worktree`, `git worktree list`, cleanup
- [ ] Parallel terminal session visual (screenshot or ASCII diagram showing 2 Claude sessions)
- [ ] Merge workflow for Scenario 1 (no conflict; fast-forward merge to show the happy path)

### Add After Validation (v1.x)

Add once readers confirm Scenario 1 is clear and the F# code runs correctly.

- [ ] Orders module — add after Users and Products are validated, since it depends on both
- [ ] Scenario 2 (merge + conflict resolution) — requires established codebase from Scenario 1
- [ ] Scenario 3 (hotfix + feature parallel) — requires readers to be comfortable with basic worktree flow

### Future Consideration (v2+)

Defer until the core tutorial content is proven valuable.

- [ ] Scenario 4 (CI/CD integration) — requires CI/CD setup knowledge; useful but not core to the worktree pattern teaching goal
- [ ] Korean localization polish — get content right first, then refine language quality
- [ ] Advanced: subagent `isolation: worktree` frontmatter example — useful for power users, not the tutorial's primary audience

---

## Feature Prioritization Matrix

### Tutorial Content

| Feature | Reader Value | Implementation Cost | Priority |
|---------|-------------|---------------------|----------|
| Scenario 1: Parallel module development | HIGH | MEDIUM | P1 |
| Working F# + Giraffe codebase (Core + Users + Products) | HIGH | MEDIUM | P1 |
| Basic worktree setup + `claude --worktree` walkthrough | HIGH | LOW | P1 |
| Parallel terminal session demo | HIGH | LOW | P1 |
| Merge workflow (clean, no conflict) | HIGH | LOW | P1 |
| Scenario 2: Conflict resolution | HIGH | MEDIUM | P1 |
| Scenario 3: Hotfix + feature parallel | HIGH | LOW | P2 |
| Orders module | MEDIUM | MEDIUM | P2 |
| Efficiency quantification (time comparison) | MEDIUM | LOW | P2 |
| Scenario 4: CI/CD integration | MEDIUM | HIGH | P3 |
| Session naming (`/rename`) tips | LOW | LOW | P2 |
| Subagent `isolation: worktree` example | LOW | LOW | P3 |

**Priority key:**
- P1: Must have for launch
- P2: Should have, add when possible
- P3: Nice to have, future consideration

---

## Competitor Feature Analysis

Closest comparable content in the ecosystem:

| Feature | Generic git worktree tutorials | Claude Code official docs | This tutorial |
|---------|-------------------------------|--------------------------|---------------|
| `claude --worktree` flag | No (not Claude-specific) | Yes (basic documentation) | Yes (primary focus) |
| Real working codebase to clone | Rarely (pseudocode) | No | Yes (F# REST API) |
| Conflict resolution scenario | Sometimes | No | Yes (intentional conflict in Core) |
| Hotfix + feature parallel | Sometimes | No | Yes (Scenario 3) |
| CI/CD integration | Rarely | No | Yes (Scenario 4) |
| Parallel session management tips | Rarely | No | Yes (naming, context, token cost) |
| Language-specific example (F#) | No | No | Yes |
| Token cost awareness | No | No | Yes (acknowledge from dev.to research) |
| Korean language audience | No | No | Yes |

---

## Sources

- [Claude Code Common Workflows — Official Docs](https://code.claude.com/docs/en/common-workflows) — Authoritative source for `--worktree` flag behavior, subagent isolation frontmatter, cleanup behavior
- [Claude Code Worktrees Guide](https://claudefa.st/blog/guide/development/worktree-guide) — Scenario-based decision matrix; defines when worktrees are/aren't appropriate
- [Running Multiple Claude Code Sessions in Parallel — dev.to](https://dev.to/datadeer/part-2-running-multiple-claude-code-sessions-in-parallel-with-git-worktree-165i) — Identifies token cost as a real concern; validates parallel session workflow steps
- [Git Worktrees Changed My AI Agent Workflow — Nx Blog](https://nx.dev/blog/git-worktrees-ai-agents) — PR review and urgent fix scenarios; "eliminating ceremony" value proposition
- [Claude Code Worktree — motlin.com](https://motlin.com/blog/claude-code-worktree) — Environment file copying, terminal automation, focused context per worktree
- [Embracing Parallel Coding Agents — Simon Willison](https://simonwillison.net/2025/Oct/5/parallel-coding-agents/) — Spec-driven work requirement; code review overhead as anti-pattern blocker
- [Parallel AI Agents — Multi-agent Coding Guide](https://www.digitalapplied.com/blog/multi-agent-coding-parallel-development) — Task decomposition, isolation, coordination patterns
- [Building REST APIs in Giraffe Pt 1](https://functionalsoftware.se/posts/building-a-rest-api-in-giraffe-pt1) + [Pt 2](https://functionalsoftware.se/posts/building-a-rest-api-in-giraffe-pt2) — Giraffe CRUD handler patterns, `BindJsonAsync`, error handling
- [REST API Best Practices — Azure Architecture Center](https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-design) — Module independence via loose coupling, contract-first design for parallel teams

---
*Feature research for: Claude Code + git worktree parallel development tutorial (F# REST API domain)*
*Researched: 2026-03-04*
