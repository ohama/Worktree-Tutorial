# Phase 4: Hotfix Parallel (Scenario 3) - Research

**Researched:** 2026-03-05
**Domain:** Git worktree hotfix workflow + F# Giraffe Users handler improvement + tutorial pedagogy (Korean/English)
**Confidence:** HIGH

## Summary

Phase 4 teaches Scenario 3: running a hotfix worktree in parallel while feature development continues uninterrupted. The tutorial chapter `tutorial/04-hotfix-parallel.md` already exists with complete content. This phase has two parallel tracks: Track A creates a `feature/search` worktree that adds `SearchQuery` types to `Core.fs` (represents in-progress feature work), and Track B creates a `hotfix/users-delete-404` worktree that improves the Users delete handler to check existence before deletion. The hotfix is merged to main first, then feature/search is rebased onto updated main.

The critical finding is that the **tutorial chapter is the authoritative spec** for all code changes in this phase — same as Phases 2 and 3. The hotfix code change (`src/Users/Handlers.fs` delete handler improvement) is fully specified in the tutorial's Step 3. The feature/search code change (adding `SearchQuery` type to `Core.fs`) is specified in Step 1. Both changes are minimal and straightforward; no new F# modules, no new test files, no route changes. The worktree cleanup section (Step 4 and the Lifecycle section) teaches the complete cleanup workflow and serves as the tutorial's grand finale.

The second critical finding: Phase 3's summary confirmed that git's `ort` strategy **auto-merged Core.fs without conflict markers** during Phase 3. For Phase 4, the rebase scenario intentionally demonstrates the CLEAN case first (no conflict during rebase) with the conflict case handled as an optional exercise (Challenge 2). The feature/search changes `Core.fs` by adding `SearchQuery`, while the hotfix only changes `src/Users/Handlers.fs` — these are **different files**, so rebase produces no conflict. This is by design: Phase 4 teaches the happy-path rebase workflow, with conflict-on-rebase left as a challenge exercise.

**Primary recommendation:** Implement Phase 4 in three tasks mirroring the tutorial's step structure: (04-01) Set up both worktrees (feature/search + hotfix) and apply the hotfix; (04-02) Merge hotfix to main, rebase feature/search, verify clean rebase; (04-03) Tutorial verification — run the full scenario, verify commands match tutorial output, clean up all worktrees, and document the complete lifecycle section.

## Standard Stack

The established libraries/tools for this domain:

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Git worktree | 2.39+ (macOS bundled) | Parallel branch checkouts | Core mechanism for the entire tutorial; no alternatives considered |
| F# / .NET | net10.0 | Application code | Locked decision from Phase 1 |
| Giraffe | 8.2.0 | HTTP handlers | Already in project; Users/Handlers.fs is the file being modified |
| Expecto | 10.2.3 | Test framework | Already in tests project; no new tests needed for this phase |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `git rebase` | built-in git | Replay feature commits on top of updated main | After hotfix is merged; preferred over merge for feature branches |
| `git worktree remove` | built-in git | Clean worktree removal including metadata | Always use this instead of `rm -rf` |
| `git worktree prune` | built-in git | Clean stale worktree metadata | When a worktree directory was manually deleted |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `git rebase main` | `git merge main` | Rebase creates linear history (tutorial teaches this as preferred); merge creates merge commit |
| Modify Users delete handler | Modify Products or Orders handler | Tutorial specifically uses Users delete to continue the bug story from Step 2's scenario description |
| `git worktree remove` | `rm -rf + git worktree prune` | `remove` is atomic; two-step approach leaves stale metadata if prune is forgotten |

**Installation:** No new packages needed. All tools are standard git operations and existing F# stack.

## Architecture Patterns

### Recommended Project Structure

After Phase 4 is complete:

```
worktree-tutorial/         [main]     — primary worktree
worktree-tutorial-search/  [feature/search]  — created in Step 1, rebased in Step 5
worktree-tutorial-hotfix/  [hotfix/users-delete-404]  — created in Step 2, removed in Step 4
```

Final state (after all cleanup):

```
worktree-tutorial/   [main]   — all changes merged, all other worktrees removed
src/
├── Core.fs                   — UNCHANGED (SearchQuery added in feature/search NOT merged to main yet)
├── Users/
│   ├── Domain.fs             — UNCHANGED
│   └── Handlers.fs           — MODIFIED: improved delete handler (from hotfix)
tutorial/
└── 04-hotfix-parallel.md     — EXISTS: full content, verify output accuracy
```

Note: The `feature/search` branch with `SearchQuery` is NOT merged to main in this phase. It is created, used to demonstrate rebase, and left open (or cleaned up as part of the lifecycle section). The tutorial's Step 6 shows merging it: `git merge feature/search` — but this is presented as "what you would do after completing the feature." Whether to actually merge it to main is a planner decision (see Open Questions).

### Pattern 1: Hotfix Worktree From Main

**What:** Create a hotfix worktree branching from current main HEAD. The hotfix branch is specifically named with the bug being fixed. This is distinct from feature worktrees which use `feature/` prefix.

**When to use:** When an urgent bug fix is needed while other feature development is ongoing.

**Example (from `tutorial/04-hotfix-parallel.md` Step 2):**
```bash
# From main worktree (Terminal 1)
git worktree add ../worktree-tutorial-hotfix -b hotfix/users-delete-404

# Verify three-worktree state
git worktree list
# /path/to/worktree-tutorial        abc1234 [main]
# /path/to/worktree-tutorial-search def5678 [feature/search]
# /path/to/worktree-tutorial-hotfix ghi9012 [hotfix/users-delete-404]
```

### Pattern 2: Improved Delete Handler (Hotfix Code Change)

**What:** The hotfix improves `src/Users/Handlers.fs` delete handler from simple `TryRemove` result check to explicit existence-check-before-delete pattern. This is the actual code change introduced in the hotfix.

**Before (current state in codebase):**
```fsharp
// src/Users/Handlers.fs — current delete handler
let delete (id: Guid) : HttpHandler =
    fun next ctx ->
        if Domain.delete id then
            ctx.SetStatusCode 204
            next ctx
        else
            ctx.SetStatusCode 404
            json (ApiResponse.error "User not found") next ctx
```

**After (hotfix change — from `tutorial/04-hotfix-parallel.md` Step 3):**
```fsharp
// src/Users/Handlers.fs — improved delete handler
let delete (id: Guid) : HttpHandler =
    fun next ctx ->
        match Domain.getById id with
        | None ->
            ctx.SetStatusCode 404
            json (ApiResponse.error (sprintf "User %O not found" id)) next ctx
        | Some _ ->
            Domain.delete id |> ignore
            ctx.SetStatusCode 204
            next ctx
```

Key differences:
- Uses `Domain.getById` to check existence before deletion
- Error message includes the ID: `sprintf "User %O not found" id`
- `Domain.delete` result is `|> ignore` (we already know it exists)
- `match` expression replaces `if/else` for F# idiom consistency

### Pattern 3: SearchQuery Type Addition (Feature Work)

**What:** The feature/search worktree adds `SearchQuery` type to `Core.fs`. This represents the in-progress feature work that must NOT be interrupted by the hotfix.

**Addition to `src/Core.fs` (from `tutorial/04-hotfix-parallel.md` Step 1):**
```fsharp
    // === Search ===
    type SearchQuery =
        { Query: string
          Page: int
          PageSize: int }

    module SearchQuery =
        let defaultQuery q =
            { Query = q; Page = 1; PageSize = 20 }
```

**Important:** This change is made in the `feature/search` worktree only. It is NOT committed to main directly. The tutorial's Step 1 explicitly states "이 시점에서 작업이 진행 중입니다. commit하지 않았습니다." (At this point the work is in progress. It has not been committed.)

**When to commit:** For the rebase step to work, the feature/search changes must be committed. The tutorial's Step 5 shows committing the WIP before rebasing: `git add -A && git commit -m "wip: search query types"`.

### Pattern 4: Rebase Feature Branch Onto Updated Main

**What:** After the hotfix is merged to main, the feature/search branch (which branched off the pre-hotfix main) is rebased onto the updated main.

**When to use:** Standard workflow for keeping feature branches current with main after hotfixes land.

**Example (from `tutorial/04-hotfix-parallel.md` Step 5):**
```bash
# In feature/search worktree (Terminal 2)

# First, commit in-progress work
git add -A
git commit -m "wip: search query types"

# Verify main has the hotfix
git log --oneline main
# ddd4444 (main) fix: improve Users delete handler
# ccc3333 merge: resolve Core.fs conflict

# Rebase onto updated main
git rebase main
# Successfully rebased and updated refs/heads/feature/search.

# Verify linear history
git log --oneline
# fff6666 (HEAD -> feature/search) wip: search query types
# ddd4444 (main) fix: improve Users delete handler
# ccc3333 merge: resolve Core.fs conflict
```

**Why rebase instead of merge:** The tutorial explicitly teaches this distinction with a diagram:
```
rebase:  main ── A ── B (hotfix) ── C (feature commit, rebased)

merge:   main ── A ── B (hotfix) ──── M (merge commit)
                                  ╱
         feature ─── C ──────────╯
```

### Pattern 5: Worktree Cleanup (Full Lifecycle)

**What:** The cleanup pattern uses `git worktree remove` (NOT `rm -rf`) followed by `git branch -d`. The tutorial's lifecycle section summarizes all five stages: Create → List → Work → Sync → Cleanup.

**Example (from `tutorial/04-hotfix-parallel.md` Step 4):**
```bash
# After hotfix is merged to main
git worktree remove ../worktree-tutorial-hotfix
# Removing worktree '/path/to/worktree-tutorial-hotfix'

git branch -d hotfix/users-delete-404
# Deleted branch hotfix/users-delete-404 (was ddd4444).
```

**If directory was manually removed (stale metadata):**
```bash
git worktree prune
```

**Full lifecycle commands reference (from tutorial's Lifecycle section):**
```bash
# Create
git worktree add ../project-feature -b feature/name       # new branch
git worktree add ../project-feature feature/name          # existing branch

# List
git worktree list

# Work
cd ../project-feature && git add -A && git commit -m "feat: ..."

# Sync
git rebase main      # in worktree: replay commits on updated main
# OR
git merge feature/name  # in main: incorporate worktree's changes

# Cleanup
git worktree remove ../project-feature
git branch -d feature/name
git worktree prune   # if stale metadata remains
```

### Anti-Patterns to Avoid

- **Using `rm -rf` instead of `git worktree remove`:** `rm -rf` deletes the directory but leaves stale metadata in `.git/worktrees/`. Git worktree list will still show the removed worktree until `git worktree prune` is run.
- **Committing SearchQuery to the main worktree's Core.fs:** SearchQuery must only be added in the `feature/search` worktree. Adding it to main would skip the pedagogical point about feature work continuing in parallel.
- **Merging feature/search to main before the rebase demonstration:** The tutorial demonstrates rebase specifically because the feature branch is BEHIND main (hotfix landed on main). If feature/search were merged before the hotfix, the scenario breaks.
- **Rebasing without committing in-progress work first:** `git rebase main` with uncommitted changes will fail (or behave unexpectedly). Always commit or stash before rebasing.
- **Naming the hotfix branch `fix/` instead of `hotfix/`:** The tutorial uses `hotfix/` prefix specifically — this is a common convention for production hotfixes that distinguishes them from regular `fix/` bug branches.

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Existence-check before delete | Custom pre-check logic with locks | `Domain.getById` then `Domain.delete` | The pattern is already in Domain module; no new functions needed |
| Including ID in error message | Custom error formatter | `sprintf "User %O not found" id` | F# `%O` format specifier calls `ToString()` on Guid — produces standard UUID string |
| Rebasing with uncommitted changes | Complex stash-rebase-pop dance | `git add -A && git commit -m "wip:"` then rebase | WIP commits are valid; tutorial uses this explicitly; cleaner than stash |
| Worktree metadata cleanup | Manual `.git/worktrees/` directory edits | `git worktree prune` | Built-in command handles stale entries safely |

**Key insight:** This phase is primarily a workflow demonstration, not a complex code implementation. The F# code changes are intentionally minimal — the pedagogical value is in the git operations, not the code.

## Common Pitfalls

### Pitfall 1: Feature/Search and Hotfix Touch the Same File

**What goes wrong:** If the `feature/search` changes and the `hotfix/users-delete-404` changes both modify `src/Users/Handlers.fs`, then `git rebase main` in the feature/search worktree will produce a conflict. The tutorial's main scenario expects a clean rebase.

**Why it happens:** Tutorial designers put SearchQuery in `Core.fs` (a shared types file) while the hotfix targets `src/Users/Handlers.fs`. These are different files — no conflict on rebase. But if a student adds search functionality to `src/Users/Handlers.fs` at the same time as the hotfix modifies it, conflict occurs.

**How to avoid:** Strictly follow the tutorial's Step 1 spec: SearchQuery addition goes to `Core.fs` ONLY, not to any `Users/Handlers.fs` or other handlers. The conflict scenario is reserved for Challenge 2 in the exercises section.

**Warning signs:** `git rebase main` outputs `CONFLICT (content): Merge conflict in src/Users/Handlers.fs` when it should say `Successfully rebased`.

### Pitfall 2: SearchQuery Added With Uncommitted Changes — Rebase Fails

**What goes wrong:** `git rebase main` fails with "error: cannot rebase: You have unstaged changes" or "Please commit or stash them."

**Why it happens:** Step 1 explicitly says the feature work is "진행 중입니다. commit하지 않았습니다" (in progress, not committed). But Step 5 commits first with `git add -A && git commit -m "wip: search query types"` BEFORE rebasing. If the rebase is attempted without the commit, it fails.

**How to avoid:** Always commit (or stash) feature/search changes before running `git rebase main`. The tutorial shows this exact sequence in Step 5.

**Warning signs:** `git rebase main` outputs `error: cannot rebase: You have unstaged changes`.

### Pitfall 3: Hotfix Worktree Created From Wrong Branch

**What goes wrong:** Hotfix worktree is created while the main worktree's HEAD is pointing to an older commit (e.g., during a rebase or after a `git checkout`). The hotfix then branches from the wrong base.

**Why it happens:** `git worktree add ../path -b hotfix/name` creates the new branch from the CURRENT HEAD of the repo the command is run from. If run from the feature/search worktree instead of main, the hotfix branches from feature/search HEAD — not main.

**How to avoid:** Always create the hotfix worktree from the main worktree (Terminal 1). The tutorial explicitly marks each command with which Terminal runs it: `**Terminal 1** (main)`.

**Warning signs:** `git log --oneline hotfix/users-delete-404..main` shows commits on main that the hotfix doesn't have, or vice versa.

### Pitfall 4: Hotfix delete Handler Uses `|> fst` Instead of `|> ignore`

**What goes wrong:** The improved delete handler calls `Domain.delete id |> fst` instead of `|> ignore`. This is a type error because `Domain.delete` returns `bool`, not a tuple — `fst` would be undefined.

**Why it happens:** Copy-paste from the ConcurrentDictionary pattern `store.TryRemove(id) |> fst` used in `Domain.delete`. But `Domain.delete` wraps the TryRemove — it already returns `bool`, not `(bool * 'a)`.

**How to avoid:** Use `Domain.delete id |> ignore` in the handler — as specified in the tutorial's Step 3 code. The `ignore` discards the bool return value after we've already verified existence via `Domain.getById`.

**Warning signs:** F# compiler error: `error FS0001: The type 'bool' does not support member 'Item1'` or similar tuple-member error.

### Pitfall 5: `sprintf "User %O not found"` Doesn't Include ID

**What goes wrong:** Error message is `"User not found"` (without ID) instead of `"User <uuid> not found"`.

**Why it happens:** Developer copies the old error message from the getById handler (`ApiResponse.error "User not found"`) instead of using the tutorial's improved version with the ID.

**How to avoid:** Use `ApiResponse.error (sprintf "User %O not found" id)` exactly as specified in the tutorial's Step 3.

**Warning signs:** DELETE on non-existent UUID returns `{"data":null,"message":"User not found","success":false}` without the UUID in the message.

### Pitfall 6: Feature/Search's SearchQuery Causes Rebase Conflict in Core.fs

**What goes wrong:** After the hotfix is merged to main, rebasing feature/search produces a conflict in `Core.fs` even though the hotfix didn't change `Core.fs`.

**Why it happens:** This should NOT happen — the hotfix only changes `src/Users/Handlers.fs`. If a conflict appears in `Core.fs`, it means the hotfix also modified `Core.fs` (unexpected), or the feature/search branch and main's base `Core.fs` diverged in a way that causes conflict.

**How to avoid:** Verify the hotfix commit only contains changes to `src/Users/Handlers.fs` (one file). Run `git show hotfix/users-delete-404` to confirm. If clean, rebase should not conflict.

**Warning signs:** `git rebase main` shows conflict in `src/Core.fs` despite hotfix only touching `src/Users/Handlers.fs`.

### Pitfall 7: Tutorial Commit Hash Placeholders Don't Match Actual Hashes

**What goes wrong:** Tutorial output shows `ccc3333 merge: resolve Core.fs conflict` as the base commit, but the actual repo has a different commit hash from Phase 3's merge commit.

**Why it happens:** The tutorial uses placeholder hashes (`ccc3333`, `ddd4444`, `eee5555`, `fff6666`) as examples. Actual hashes will differ.

**How to avoid:** When verifying tutorial output accuracy, replace placeholder hashes with actual `git log --oneline` output. The tutorial's pedagogical meaning (sequence of commits) is what matters, not the exact hashes.

**Warning signs:** `git log --oneline` shows `aa56b45 docs(03): complete Merge...` instead of `ccc3333 merge: resolve Core.fs conflict` — this is expected; the tutorial shows idealized output.

## Code Examples

Verified patterns from official sources (tutorial chapter and existing codebase):

### Improved Delete Handler (Hotfix)

```fsharp
// Source: tutorial/04-hotfix-parallel.md Step 3
// Replaces current Users/Handlers.fs delete handler

let delete (id: Guid) : HttpHandler =
    fun next ctx ->
        match Domain.getById id with
        | None ->
            ctx.SetStatusCode 404
            json (ApiResponse.error (sprintf "User %O not found" id)) next ctx
        | Some _ ->
            Domain.delete id |> ignore
            ctx.SetStatusCode 204
            next ctx
```

### SearchQuery Type Addition (Feature Work)

```fsharp
// Source: tutorial/04-hotfix-parallel.md Step 1
// Add to src/Core.fs in feature/search worktree ONLY

    // === Search ===
    type SearchQuery =
        { Query: string
          Page: int
          PageSize: int }

    module SearchQuery =
        let defaultQuery q =
            { Query = q; Page = 1; PageSize = 20 }
```

### Full Hotfix Workflow Command Sequence

```bash
# Source: tutorial/04-hotfix-parallel.md Steps 1-5

# --- STEP 1: Create feature/search worktree ---
# Terminal 1 (main):
git worktree add ../worktree-tutorial-search -b feature/search

# Terminal 2 (feature/search):
cd ../worktree-tutorial-search
# Add SearchQuery to src/Core.fs (do NOT commit yet — simulates in-progress work)

# --- STEP 2: Create hotfix worktree (without stopping feature work) ---
# Terminal 1 (main):
git worktree add ../worktree-tutorial-hotfix -b hotfix/users-delete-404

# Verify three worktrees:
git worktree list

# --- STEP 3: Apply hotfix ---
# Terminal 3 (hotfix worktree):
cd ../worktree-tutorial-hotfix
# Edit src/Users/Handlers.fs — improved delete handler
cd src && dotnet build
cd ..
git add src/Users/Handlers.fs
git commit -m "fix: improve Users delete handler — check existence before delete, include ID in error"

# --- STEP 4: Merge hotfix to main + cleanup hotfix worktree ---
# Terminal 1 (main):
git merge hotfix/users-delete-404
# Fast-forward merge

git worktree remove ../worktree-tutorial-hotfix
git branch -d hotfix/users-delete-404

# --- STEP 5: Rebase feature/search onto updated main ---
# Terminal 2 (feature/search):
git add -A
git commit -m "wip: search query types"

git log --oneline main   # verify hotfix is visible

git rebase main
# Successfully rebased and updated refs/heads/feature/search.

git log --oneline   # verify linear history
```

### dotnet build Verification

```bash
# After applying hotfix (in hotfix worktree):
cd /path/to/worktree-tutorial-hotfix/src
dotnet build
# Build succeeded.

# After rebase (in feature/search worktree):
cd /path/to/worktree-tutorial-search/src
dotnet build
# Build succeeded.

# After merging hotfix to main (in main worktree):
cd /path/to/worktree-tutorial/src
dotnet build
# Build succeeded.

# Tests should still pass on main after hotfix:
cd /path/to/worktree-tutorial
dotnet test tests/
# All tests passed (21 Expecto tests)
```

### Worktree Lifecycle Commands (Tutorial Reference)

```bash
# Source: tutorial/04-hotfix-parallel.md — Worktree Lifecycle section

# Create
git worktree add ../project-feature -b feature/name       # new branch
git worktree add ../project-feature feature/name          # existing branch

# List
git worktree list

# Sync (from worktree — rebase)
git rebase main

# Sync (from main — merge)
git merge feature/name

# Cleanup
git worktree remove ../project-feature     # preferred
git branch -d feature/name
git worktree prune                          # stale metadata cleanup
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `git stash` to context-switch for hotfix | `git worktree` for parallel hotfix | Git 2.5 (2015) | No stash/pop, no lost context, feature work continues |
| `git merge main` to update feature branch | `git rebase main` for feature sync | Standard practice | Linear history; cleaner than merge commits for feature work |
| `rm -rf + git worktree prune` | `git worktree remove` (atomic) | Git 2.17 (2018) | Single command handles both directory and metadata cleanup |
| Generic error message "User not found" | ID-specific error "User <uuid> not found" | This phase | Diagnostic improvement; matches hotfix narrative |

**Deprecated/outdated:**
- `git stash` for hotfix context switching: Still valid but pedagogically inferior to worktrees for this scenario. The tutorial explicitly contrasts the old stash-based workflow with the worktree approach.
- `git merge main` for feature sync: Not deprecated, but the tutorial explicitly prefers `git rebase main` for feature branches due to linear history.

## Open Questions

1. **Whether to merge feature/search to main at the end of the phase**
   - What we know: The tutorial's Step 6 shows `git merge feature/search` as the final step after feature completion. But it also says "실제로는 여기서 검색 기능을 완성하고 commit" (actually, complete the feature here and commit). SearchQuery alone is not a complete feature.
   - What's unclear: Whether Phase 4 should actually merge the incomplete feature/search to main, or leave it open and clean up with `git worktree remove` without merging.
   - Recommendation: Do NOT merge feature/search to main. Remove the worktree without merging (`git worktree remove ../worktree-tutorial-search && git branch -D feature/search`). The phase's purpose is to demonstrate the hotfix + rebase workflow, not to deliver a search feature. Using `git branch -D` (force delete) instead of `git branch -d` because feature/search was never merged.

2. **Exact commit hash format in tutorial output**
   - What we know: Tutorial shows placeholder hashes like `ccc3333`, `ddd4444`. The actual repo from Phase 3 has real hashes (e.g., `aa56b45`).
   - What's unclear: Whether to update the tutorial's `git log --oneline` output to show real-looking hashes or leave placeholders.
   - Recommendation: Leave placeholders as-is — they communicate the sequence clearly. If verifying tutorial accuracy, note that hash values will differ and that's expected.

3. **Whether the existing test suite still passes after the hotfix**
   - What we know: The hotfix changes `src/Users/Handlers.fs` delete handler. Tests are in `tests/UsersTests.fs` but they test domain logic (not HTTP handlers) using duplicated functions. The handler change should not break existing tests.
   - What's unclear: Whether any test in `UsersTests.fs` tests the delete behavior specifically.
   - Recommendation: Run `dotnet test tests/` after applying the hotfix to main. Expect all 21 tests to pass (the handler change doesn't affect domain-level test functions which duplicate logic inline).

## Sources

### Primary (HIGH confidence)
- `tutorial/04-hotfix-parallel.md` — authoritative spec for all code changes, git commands, and tutorial flow. Complete file read directly from codebase.
- `src/Users/Handlers.fs` — verified current state of delete handler (uses simple `if Domain.delete id then...` pattern — the "before" state the hotfix improves)
- `src/Users/Domain.fs` — verified `Domain.getById` and `Domain.delete` function signatures; hotfix handler uses both
- `src/Core.fs` — verified current state (has OrderStatus, PaginatedResponse, ApiResponse — SearchQuery not yet present)
- `src/Program.fs` — verified current route composition (Users + Products + Orders routes)
- `.planning/phases/03-merge-conflict-resolution/03-03-SUMMARY.md` — confirmed git ort strategy auto-merged Core.fs in Phase 3; relevant for understanding merge/rebase behavior

### Secondary (MEDIUM confidence)
- `src/Products/Handlers.fs` — verified that current delete handler pattern is identical to Users (both use `if Domain.delete id then...`); confirms the "before" state is accurate
- `tests/TestMain.fs` + `tests/WorktreeApi.Tests.fsproj` — verified test project structure; 21 tests expected to pass after hotfix

### Tertiary (LOW confidence)
- Git worktree behavior with `ort` merge strategy — Phase 3 confirmed auto-merge on Core.fs. For Phase 4, rebase of feature/search (modifying Core.fs) onto main (where only Users/Handlers.fs changed via hotfix) should be conflict-free. This is inferred from different-file changes, not from a live test.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages; all tooling verified from Phase 1-3 infrastructure
- Architecture: HIGH — tutorial chapter is complete and authoritative; all code changes are directly copied from tutorial spec
- Pitfalls: HIGH — delete handler pattern verified in live codebase; rebase conflict analysis based on file-level diff analysis; tutorial hash placeholder issue is obvious from comparison

**Research date:** 2026-03-05
**Valid until:** 2026-06-05 (90 days — stable stack; git worktree behavior is well-established)

---

## Phase 4 Implementation Summary for Planner

### What Already Exists (do not recreate)

| Asset | Status | Action |
|-------|--------|--------|
| `tutorial/04-hotfix-parallel.md` | EXISTS — full content | Verify command output accuracy only |
| `src/Users/Handlers.fs` | EXISTS — old delete pattern | Improve in hotfix worktree (Step 3) |
| `src/Core.fs` | EXISTS — no SearchQuery | Add SearchQuery in feature/search worktree (Step 1) |
| `src/Users/Domain.fs` | EXISTS — has `getById` and `delete` | No changes needed; hotfix handler uses these |
| All tests (21) | PASSING | No new tests needed; verify they still pass after hotfix |

### What Needs to Be Built

| Asset | Where | Action |
|-------|-------|--------|
| `feature/search` worktree | worktree-tutorial-search/ | Create, add SearchQuery to Core.fs (uncommitted initially) |
| `hotfix/users-delete-404` worktree | worktree-tutorial-hotfix/ | Create, improve Users delete handler, build, commit |
| Improved delete handler | hotfix worktree src/Users/Handlers.fs | Match-based existence check before delete, ID in error msg |
| SearchQuery type in Core.fs | feature/search worktree src/Core.fs | Add SearchQuery type + module after PaginatedResponse section |

### Recommended Task Breakdown

```
Task 04-01 (Set up worktrees + apply hotfix):
  1. git worktree add ../worktree-tutorial-search -b feature/search
  2. In feature/search: add SearchQuery to src/Core.fs (DO NOT commit — simulates in-progress work)
  3. git worktree add ../worktree-tutorial-hotfix -b hotfix/users-delete-404
  4. In hotfix worktree: improve src/Users/Handlers.fs delete handler (existence check + ID in error)
  5. In hotfix worktree: dotnet build — must succeed
  6. In hotfix worktree: git add src/Users/Handlers.fs && git commit -m "fix: improve Users delete handler..."

Task 04-02 (Merge hotfix + rebase feature):
  1. In main worktree: git merge hotfix/users-delete-404 — fast-forward expected
  2. In main worktree: dotnet test tests/ — all 21 tests must pass
  3. In main worktree: git worktree remove ../worktree-tutorial-hotfix && git branch -d hotfix/users-delete-404
  4. In feature/search worktree: git add -A && git commit -m "wip: search query types"
  5. In feature/search worktree: git log --oneline main — verify hotfix commit visible
  6. In feature/search worktree: git rebase main — must succeed with no conflicts
  7. In feature/search worktree: git log --oneline — verify linear history (wip commit on top of hotfix)

Task 04-03 (Tutorial verification + cleanup):
  1. Verify tutorial Step 3 hotfix handler code matches actual src/Users/Handlers.fs
  2. Verify tutorial Step 5 rebase output matches actual git log output (modulo placeholder hashes)
  3. Verify tutorial lifecycle section commands are accurate against current git version behavior
  4. In feature/search worktree: dotnet build — must succeed (SearchQuery compiles in Core.fs context)
  5. Clean up feature/search: git worktree remove ../worktree-tutorial-search && git branch -D feature/search
  6. Final state: main has hotfix merge, no other worktrees, 21 tests passing
  7. git commit final state
```

### Success Verification Commands

```bash
# After hotfix merge to main:
cd /path/to/worktree-tutorial/src && dotnet build   # 0 errors
cd .. && dotnet test tests/                          # 21 passed

# After rebase:
cd /path/to/worktree-tutorial-search
git log --oneline | head -3
# wip commit should be on top of hotfix commit, which is on top of Phase 3 merge commit

# After all cleanup (final state):
git worktree list   # should show only main worktree
git branch          # should show only main branch
```
