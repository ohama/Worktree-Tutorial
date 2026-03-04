# Pitfalls Research

**Domain:** Developer tutorial — F# Giraffe REST API + git worktree parallel development
**Researched:** 2026-03-04
**Confidence:** HIGH

---

## Critical Pitfalls

### Pitfall 1: Creating Worktrees Inside the Repository Working Directory

**What goes wrong:**
Developers clone a repo and then run `git worktree add ./feature-branch feature/x` — creating the worktree as a subdirectory of the main working tree. Git does not prevent this. The result is recursive .git resolution issues, tooling confusion (language servers, file watchers, test runners), and a directory structure that is impossible to navigate cleanly. Editors trying to open "the project" pick up both the parent and the worktree's files simultaneously.

**Why it happens:**
Tutorials show `git worktree add <path> <branch>` without specifying that `<path>` should be outside the repository root. Beginners default to relative paths inside the current directory.

**How to avoid:**
Always create worktrees as siblings to the repository root, not children. The safest pattern is a bare-repository setup:
```bash
# Clone as bare repository
git clone --bare https://example.com/repo.git my-project/.bare
cd my-project
echo "gitdir: ./.bare" > .git

# All worktrees become siblings
git worktree add ./main main
git worktree add ./feature-auth feature/auth
```
Or, with a regular clone, always pass a sibling path:
```bash
# From within ~/projects/my-project
git worktree add ../my-project-feature-auth feature/auth
```

**Warning signs:**
- `ls` inside the main working tree shows a directory that also looks like a full project
- IDE "open recent" shows the same project twice at different paths
- `git status` inside the nested directory shows unexpected files
- Language server reports duplicate symbol definitions

**Phase to address:** Phase 1 (Project Setup) — establish canonical directory layout in the tutorial before any code is written.

---

### Pitfall 2: Deleting a Worktree Directory Manually Without Running `git worktree remove`

**What goes wrong:**
Developer finishes a feature and runs `rm -rf ../my-project-feature-auth` thinking this is equivalent to removing the worktree. Git retains stale administrative metadata in `.git/worktrees/`. Future commands like `git worktree list` show phantom entries. Trying to create a new worktree on the same branch fails with confusing errors. The orphaned metadata also prevents branch deletion: `fatal: '<branch>' is already checked out at '<stale-path>'`.

**Why it happens:**
Worktrees feel like "just folders." The connection between filesystem directories and git's internal `.git/worktrees/` metadata is invisible and non-obvious. Tutorials often demonstrate creation but not cleanup.

**How to avoid:**
Always use git commands to remove worktrees:
```bash
git worktree remove ../my-project-feature-auth
# If worktree has untracked/modified files, use --force
git worktree remove --force ../my-project-feature-auth
# Clean up any remaining stale entries
git worktree prune
```
Teach `git worktree list --porcelain` as a diagnostic command alongside creation commands.

**Warning signs:**
- `git worktree list` shows a path that no longer exists on disk
- `git branch -d feature/auth` returns `fatal: Cannot delete branch 'feature/auth' checked out at '/nonexistent/path'`
- `git worktree add` fails with "already checked out" for a branch you know is unused

**Phase to address:** Phase 1 (Project Setup) — tutorial must cover full worktree lifecycle (create, use, remove, prune) not just creation.

---

### Pitfall 3: F# `.fsproj` Compilation Order Conflicts When Multiple Worktrees Add Files

**What goes wrong:**
F# requires all source files to be listed in the `.fsproj` in strict compilation order — a file can only use types and functions defined in files listed earlier. When two worktrees independently add new `.fs` files and both modify the `<Compile>` entries in `.fsproj`, merging back to main produces conflicts in `.fsproj` XML. Worse: even if the conflict markers are resolved correctly for XML syntax, the compilation order may be wrong, causing `error FS0039: The value or constructor 'X' is not defined` — a cryptic error that does not mention ordering.

**Why it happens:**
C#, JavaScript, and most other languages use filesystem-based or import-based module resolution — developers don't expect XML ordering to be semantically load-bearing. F#'s explicit ordering in `.fsproj` is a language-specific constraint that surprises everyone unfamiliar with F#.

**How to avoid:**
1. Design the module structure before parallel work begins: place shared/foundational modules (Domain, Types, Database) early in the file list; place feature-specific handlers late.
2. Establish a module ownership rule: each worktree (feature) adds files only to its own designated section of the file list — never in a shared zone.
3. Commit a clear file ordering comment in `.fsproj`:
```xml
<!-- ORDERING RULES:
  1. Domain types first (no dependencies)
  2. Infrastructure (Database, Config)
  3. Feature modules (can add here per feature)
  4. Router / Program.fs last
-->
```
4. Validate compilation order in CI immediately after any `.fsproj` change: `dotnet build` catches order violations before merge.

**Warning signs:**
- `error FS0039` on a symbol that definitely exists in the codebase
- `error FS0191: The type is not defined` pointing to a module added in a different worktree
- Merge conflict markers inside `<ItemGroup>` containing `<Compile>` entries in `.fsproj`

**Phase to address:** Phase 1 (Project Setup) — establish file ordering conventions and annotate `.fsproj` before parallel phases begin.

---

### Pitfall 4: Shared Database / SQLite State Across Worktrees

**What goes wrong:**
Both worktrees start ASP.NET Core dev servers that connect to the same SQLite file (e.g., `app.db` in the repo root or a fixed path in `appsettings.Development.json`). Worktree A's migrations create tables or seed data; Worktree B's migrations conflict or assume a different schema baseline. SQLite file-level locking means one server may fail to write while the other holds the lock. Tutorial readers become confused: their API returns errors that are caused by the other running worktree, not their code.

**Why it happens:**
Default ASP.NET Core/Giraffe project templates hardcode a single database connection string. The concept of per-environment database isolation is not built-in to the template. Tutorial readers assume dev servers are independent because they run on separate branches.

**How to avoid:**
Use environment variables or per-worktree `.env` files to point each worktree to its own database:
```bash
# .env.worktree-feature-auth (in worktree root, gitignored)
DATABASE_PATH=../my-project-feature-auth.db
ASPNETCORE_URLS=http://localhost:5001
```
Or use an in-memory database (SQLite `:memory:` or EF Core InMemory) for worktrees that are only doing tutorial exercises.
For the tutorial, explicitly set a different SQLite path per worktree as a setup step, not an afterthought.

**Warning signs:**
- `SqliteException: database is locked` errors in one worktree's logs
- Migrations fail with "table already exists" or "column does not exist" when run in a second worktree
- Data from one worktree's test requests appears in the other worktree's responses

**Phase to address:** Phase 1 (Project Setup) — the tutorial's "run the project" step must include worktree-specific environment setup. Phase 2+ — any phase that introduces database migrations must include isolation verification.

---

### Pitfall 5: Port Conflicts Between Concurrent Worktree Dev Servers

**What goes wrong:**
Both worktrees run `dotnet run` and both try to bind to `http://localhost:5000`. The second server to start fails with `System.IO.IOException: Failed to bind to address http://0.0.0.0:5000: address already in use`. Less obviously: if developers use `launchSettings.json` with a hardcoded port, this conflict is invisible until they try to run both worktrees simultaneously. Tutorial readers think the project is broken when the problem is simply a port collision.

**Why it happens:**
`launchSettings.json` defaults to a fixed port for all profiles. Worktrees share the file (it is committed to the repo), so they inherit the same port. Developers are not used to thinking about port assignment when working on a single branch.

**How to avoid:**
Override ports via environment variable, not via `launchSettings.json`:
```bash
# Worktree 1 (main)
ASPNETCORE_URLS=http://localhost:5000 dotnet run

# Worktree 2 (feature/auth)
ASPNETCORE_URLS=http://localhost:5001 dotnet run
```
Or provide a `Makefile`/shell script per worktree that exports the correct port. Document the port assignment scheme at the top of the tutorial clearly.

**Warning signs:**
- `address already in use` on `dotnet run`
- `dotnet run` starts immediately with no output (fails silently in some configurations)
- HTTP requests return `Connection refused` even though `dotnet run` appeared to succeed

**Phase to address:** Phase 1 (Project Setup) — explicit port assignment must be part of worktree setup instructions.

---

### Pitfall 6: Tutorial "Follows Along" Passively Without Forcing Reader to Break and Fix Things

**What goes wrong:**
Readers watch the tutorial, copy commands, see green output, and close the browser feeling they "learned worktrees." Two days later they cannot set up a worktree on their own project. The tutorial demonstrated concepts but never activated learning. This is "tutorial hell" at the module level: the reader mistakes successful copying for understanding.

**Why it happens:**
Tutorial authors optimize for a smooth, error-free experience. They scaffold everything in advance so readers never get stuck. The scaffolded experience removes the cognitive struggle that creates durable memory. Readers mistake "I followed along without errors" for "I understand this."

**How to avoid:**
Structure each major concept with a deliberate break/fix exercise:
- Show the wrong pattern first (e.g., create a worktree inside the repo), observe the problem, then fix it.
- Include a "Now try this yourself" section with a different branch name — no copy-paste allowed.
- Have readers make a merge conflict intentionally, then resolve it.
- End each phase with a "What would break if..." challenge question.

**Warning signs:**
- Tutorial has zero intentional error demonstrations
- All commands produce only success output
- Reader never has to reason about *why* a step is done, only *how*
- No exercises that require reader to adapt, only copy

**Phase to address:** All tutorial phases — build deliberate friction into the tutorial's pedagogical design from Phase 1.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Hardcode SQLite path in `appsettings.Development.json` | No setup script needed | Immediate worktree DB conflicts | Never in a parallel-dev tutorial |
| Put all F# code in `Program.fs` | No ordering complexity | Cannot run parallel worktrees on separate features; one file = one conflict zone | Only for single-file "hello world" demo |
| Skip `git worktree remove`, just `rm -rf` | Faster cleanup | Stale metadata blocks future branch operations | Never |
| Use fixed port 5000 for all dev profiles | Zero configuration | Port collision stops second worktree cold | Never in a multi-worktree tutorial |
| Use shared `.env` committed to repo | Easy for reader to start | All worktrees share same secrets/ports | Only for initial "does it work" verification |
| Skip CI validation of `.fsproj` order | Faster setup | Ordering errors appear at merge time, not commit time | Never for a team/parallel-dev context |

---

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| SQLite + multiple worktrees | Single `DATABASE_PATH` pointing to `./app.db` | Per-worktree env var: `DATABASE_PATH=../../worktree-name.db` |
| ASP.NET Core `launchSettings.json` | All profiles use port 5000 | Teach `ASPNETCORE_URLS` env override; document port-per-worktree convention |
| `dotnet watch` + multiple worktrees | Two `dotnet watch` processes both pick up `src/` changes | Ensure each watch process runs from its own worktree working directory |
| F# `.fsproj` + IDE (VS Code) | VS Code's Ionide extension reloads on `.fsproj` change and may reorder files | Keep Ionide enabled but validate order in terminal with `dotnet build` before committing |
| `git push` from wrong worktree | Developer in worktree A pushes worktree B's branch due to confused working directory | Always verify `git branch --show-current` before push; use shell prompt that shows branch name |

---

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Restore dependencies (NuGet) per worktree | First `dotnet build` in new worktree takes 3-5 minutes | NuGet cache is global by default — no action needed; just explain the first-run delay | Only hurts UX if not documented |
| Running `dotnet build` in repo root (not worktree) | Builds main branch files, not the feature branch files | Always run `dotnet` commands from the worktree directory | Immediately confusing for beginners |
| Build artifacts pollute other worktrees | Stale `.dll` from main branch used in feature worktree | Each worktree has its own `bin/` and `obj/` directories — no sharing | Rarely; MSBuild paths are relative |
| `git worktree list` on large repos with many stale worktrees | Command hangs briefly; confusing output | Run `git worktree prune` regularly; teach it as a hygiene step | When 5+ stale worktrees accumulate |

---

## "Looks Done But Isn't" Checklist

- [ ] **Worktree setup:** Reader has run both worktrees simultaneously — not just one at a time — verify both endpoints respond on different ports
- [ ] **F# file ordering:** Tutorial adds at least one new `.fs` file and verifies `dotnet build` succeeds after adding it to `.fsproj` in the correct position
- [ ] **Branch lifecycle:** Tutorial demonstrates full cycle: `git worktree add` → work → commit → `git worktree remove` → `git worktree prune` → `git branch -d`
- [ ] **Merge conflict:** Tutorial includes at least one deliberately created merge conflict in `.fsproj` or source file, and shows resolution
- [ ] **Database isolation:** Both worktrees use different DB files; verified by inserting data in one and confirming it does NOT appear in the other
- [ ] **Port isolation:** Both dev servers start successfully; verified by running curl against both ports simultaneously
- [ ] **Cleanup verification:** After a worktree is removed, `git worktree list` shows no stale entries and the branch can be deleted cleanly

---

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Worktree created inside repo | MEDIUM | Move directory: `git worktree move ./nested-wt ../correct-path` (Git 2.17+); or remove and re-add |
| Stale worktree after manual `rm -rf` | LOW | `git worktree prune` cleans orphaned metadata; then `git branch -d <branch>` works again |
| Branch locked "already checked out" | LOW | `git worktree list --porcelain` to diagnose; `git worktree remove <path>` then `git worktree prune` |
| `.fsproj` merge conflict | MEDIUM | Resolve by accepting all `<Compile>` entries from both sides; manually reorder to satisfy dependency order; `dotnet build` to verify |
| SQLite file locked | LOW | Stop both dev servers; identify which process holds lock (`lsof app.db`); restart with isolated DB paths |
| Port conflict on `dotnet run` | LOW | Kill the conflicting process (`lsof -ti :5000 | xargs kill`); restart with `ASPNETCORE_URLS` override |
| Reader confused by bare repo `.git` file | LOW | Explain: `.git` as a file (not directory) is a gitdir pointer — `cat .git` shows the path; this is intentional |

---

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Worktree created inside repo | Phase 1: Project Setup | `git worktree list` shows all paths outside repo root |
| Manual worktree deletion / stale metadata | Phase 1: Project Setup | Demonstrate remove + prune cycle before Phase 2 begins |
| F# `.fsproj` ordering conflicts | Phase 1: Project Setup | `.fsproj` has ordering comments; `dotnet build` green before any parallel work |
| Shared SQLite across worktrees | Phase 1: Project Setup | Each worktree's README snippet includes different `DATABASE_PATH` |
| Port conflicts | Phase 1: Project Setup | Both servers run simultaneously; curl to both ports succeeds |
| Tutorial passive learning / tutorial hell | All phases (pedagogy) | Each phase ends with a "now do it yourself" exercise, not just a demo |
| Branch deletion blocked by checked-out worktree | Phase 2+: Feature Development | Teach branch lifecycle at end of each feature phase, not just once at the start |
| Wrong worktree directory for git commands | Phase 2+: Feature Development | Shell prompt shows branch; `git branch --show-current` is taught as a sanity check |

---

## Sources

- [Git Worktree: Pros, Cons, and the Gotchas Worth Knowing — Josh Tune](https://joshtune.com/posts/git-worktree-pros-cons/)
- [Git Worktrees for Parallel AI Coding Agents — Upsun Developer Center](https://devcenter.upsun.com/posts/git-worktrees-for-parallel-ai-coding-agents/)
- [Git Worktree Branch Locked — DevToolbox Blog](https://devtoolbox.dedyn.io/blog/git-worktree-branch-locked-linked-worktree-remote-tracking-guide)
- [How to use git worktree in a clean way — Morgan Cugerone](https://morgan.cugerone.com/blog/how-to-use-git-worktree-and-in-a-clean-way/)
- [Merge conflicts in fsproj files — Paket Issue #2297](https://github.com/fsprojects/Paket/issues/2297)
- [File Order in F# - the most annoying thing for a beginner — DEV Community](https://dev.to/klimcio/file-order-in-f-the-most-annoying-thing-for-a-beginner-38dc)
- [Official git-worktree documentation — git-scm.com](https://git-scm.com/docs/git-worktree)
- [Git Worktrees Explained: Conflict-Free Parallel Development — DEV Community](https://dev.to/aivideotool/git-worktrees-explained-the-secret-to-conflict-free-parallel-development-1mep)
- [portree: Git Worktree Server Manager with automatic port allocation](https://github.com/fairy-pitta/portree)
- [Organizing modules in a project — F# for Fun and Profit](https://fsharpforfunandprofit.com/posts/recipe-part3/)
- [Tutorial Hell to Vibe Coding Hell: Learning to Code in 2025 — Sigma School](https://sigmaschool.co/blogs/from-tutorial-hell-to-vibe-coding-hell)
- [Project-Based Learning vs Tutorials — Frontend Mentor](https://www.frontendmentor.io/articles/project-based-learning-vs-tutorials)

---
*Pitfalls research for: F# Giraffe REST API + git worktree parallel development tutorial*
*Researched: 2026-03-04*
