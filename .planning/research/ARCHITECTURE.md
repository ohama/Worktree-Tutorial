# Architecture Research

**Domain:** F# Giraffe REST API tutorial with git worktree parallel development
**Researched:** 2026-03-04
**Confidence:** HIGH

## Standard Architecture

### System Overview

The project has two distinct structural concerns: the F# API source code (in `src/`) and the tutorial documents (in `tutorial/`). They are intentionally separate because the API must be independently buildable and the tutorial documents reference the API code without being coupled to it.

```
┌─────────────────────────────────────────────────────────────┐
│                    Tutorial Documents (tutorial/)            │
│  ┌────────────┐ ┌────────────┐ ┌────────────┐ ┌──────────┐  │
│  │ 01-intro   │ │ 02-setup   │ │ 03-parallel│ │ 04-merge │  │
│  └────────────┘ └────────────┘ └────────────┘ └──────────┘  │
└─────────────────────────────────────────────────────────────┘
                        (references)
┌─────────────────────────────────────────────────────────────┐
│                    F# API Source (src/)                      │
├─────────────────────────────────────────────────────────────┤
│                  HTTP Layer (Program.fs + routes)            │
│  ┌────────────┐ ┌────────────┐ ┌────────────┐               │
│  │  Users     │ │  Products  │ │  Orders    │               │
│  │ /handlers  │ │ /handlers  │ │ /handlers  │               │
│  └─────┬──────┘ └─────┬──────┘ └─────┬──────┘               │
├────────┼──────────────┼──────────────┼────────────────────  │
│        │           Domain Layer       │                      │
│  ┌─────▼──────┐ ┌─────▼──────┐ ┌─────▼──────┐               │
│  │  Users     │ │  Products  │ │  Orders    │               │
│  │ /domain    │ │ /domain    │ │ /domain    │               │
│  └─────┬──────┘ └─────┬──────┘ └─────┬──────┘               │
├────────┼──────────────┼──────────────┼────────────────────  │
│        └──────────────┴──────────────┘                      │
│                  Core (shared types only)                    │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  Core.fs — CommonTypes, ApiResponse, DbConnection    │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | Typical Implementation |
|-----------|----------------|------------------------|
| `Core.fs` | Shared types, DB connection, ApiResponse wrapper | F# types + module, compiled first |
| `Users/Domain.fs` | User business logic, validation, pure functions | F# discriminated unions + functions |
| `Users/Handlers.fs` | HTTP handler functions for user endpoints | Giraffe `HttpHandler` functions |
| `Products/Domain.fs` | Product business logic, independent of Users/Orders | F# types + functions |
| `Products/Handlers.fs` | HTTP handlers for product endpoints | Giraffe `HttpHandler` functions |
| `Orders/Domain.fs` | Order business logic; references User/Product types via Core | F# types + functions |
| `Orders/Handlers.fs` | HTTP handlers for order endpoints | Giraffe `HttpHandler` functions |
| `Program.fs` | Route composition, DI registration, app startup | ASP.NET Core host + Giraffe `choose` |

## Recommended Project Structure

### Source Code (src/)

```
src/
├── Api.fsproj              # Project file — compilation order is authoritative
├── Core.fs                 # Shared types: ApiResponse, DbConfig, common errors
├── Users/
│   ├── Domain.fs           # User types, validation, pure business logic
│   └── Handlers.fs         # GET /users, POST /users, GET /users/:id, etc.
├── Products/
│   ├── Domain.fs           # Product types, validation, pure business logic
│   └── Handlers.fs         # GET /products, POST /products, etc.
├── Orders/
│   ├── Domain.fs           # Order types; may reference User/Product IDs from Core
│   └── Handlers.fs         # GET /orders, POST /orders, etc.
└── Program.fs              # Route composition + app startup (compiled last)
```

The `.fsproj` compilation order must be:

```xml
<ItemGroup>
  <Compile Include="Core.fs" />
  <Compile Include="Users/Domain.fs" />
  <Compile Include="Users/Handlers.fs" />
  <Compile Include="Products/Domain.fs" />
  <Compile Include="Products/Handlers.fs" />
  <Compile Include="Orders/Domain.fs" />
  <Compile Include="Orders/Handlers.fs" />
  <Compile Include="Program.fs" />
</ItemGroup>
```

**Why this order matters for worktrees:** Each domain module pair (`Domain.fs` + `Handlers.fs`) is a self-contained unit. Adding a new domain module only requires inserting two files into the `.fsproj` and adding route registration in `Program.fs`. Domain modules do not import each other — only `Core.fs`. This is what enables true parallel development: three worktrees can write `Users/`, `Products/`, and `Orders/` simultaneously with zero file-level conflicts.

### Tutorial Documents (tutorial/)

```
tutorial/
├── README.md               # Tutorial index, prerequisites, how to use
├── 01-introduction.md      # What worktrees are, why they matter for AI dev
├── 02-project-setup.md     # Clone repo, build src/, understand structure
├── 03-parallel-development.md  # Core scenario: fan-out to 3 worktrees
├── 04-merge-conflicts.md   # Merge worktrees back, handle conflicts
├── 05-hotfix-parallel.md   # Hotfix on main while features run in worktrees
├── 06-cicd-integration.md  # Run tests/builds per worktree in CI
└── assets/                 # Diagrams, screenshots for tutorials
    ├── worktree-flow.png
    └── merge-sequence.png
```

**Why numbered files:** Sequential numbering enforces reading order without a build tool. Each chapter stands alone but references previous chapters explicitly.

### Structure Rationale

- **`Core.fs` is the only shared file:** Domain modules reference shared types (like `UserId`, `ApiResponse`) from `Core.fs` but never from each other. This is the isolation guarantee that makes worktrees safe.
- **Domain + Handlers paired per module:** Keeping domain logic and HTTP handlers in the same folder makes each module cognitively complete. A developer working in a `Users` worktree never leaves the `Users/` directory.
- **`Program.fs` compiled last:** Route composition is the only integration point across all domain modules. Conflicts in `Program.fs` are expected and intentional — they are the merge story of the tutorial.
- **`tutorial/` entirely separate from `src/`:** Tutorial documents can be edited in a worktree without ever touching the F# source, and vice versa.

## Architectural Patterns

### Pattern 1: Ports and Adapters (Onion Architecture)

**What:** Domain logic is pure F# — no HTTP, no database concerns. Handlers are adapters that translate HTTP requests into domain calls and domain results into HTTP responses. `Core.fs` defines the ports (shared data shapes).

**When to use:** Always in this project. This is what makes domain modules independently testable.

**Trade-offs:** Adds a thin translation layer (Handlers.fs), but this is minimal in F# and makes the domain logic 100% testable without an HTTP stack.

**Example:**

```fsharp
// Users/Domain.fs — pure, no Giraffe dependency
module Users.Domain

type User = { Id: int; Name: string; Email: string }

type CreateUserRequest = { Name: string; Email: string }

let createUser (req: CreateUserRequest) : Result<User, string> =
    if req.Name = "" then Error "Name is required"
    elif req.Email = "" then Error "Email is required"
    else Ok { Id = 0; Name = req.Name; Email = req.Email }
```

```fsharp
// Users/Handlers.fs — adapter layer, depends on Domain.fs
module Users.Handlers

open Giraffe
open Microsoft.AspNetCore.Http
open Users.Domain

let createUserHandler : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) -> task {
        let! req = ctx.BindJsonAsync<CreateUserRequest>()
        match createUser req with
        | Ok user -> return! json user next ctx
        | Error msg -> return! (setStatusCode 400 >=> json {| error = msg |}) next ctx
    }
```

### Pattern 2: SubRoute Composition in Program.fs

**What:** Each domain's routes are defined in their Handlers module and composed in `Program.fs` using `subRoute`. This is the single integration point.

**When to use:** Always. Route composition in one place makes the full API surface visible at a glance.

**Trade-offs:** `Program.fs` becomes a merge conflict zone when multiple worktrees add new domain routes. This is intentional — the tutorial uses this to teach conflict resolution.

**Example:**

```fsharp
// Program.fs — integration point only
let webApp =
    choose [
        subRoute "/api/users"    Users.Handlers.routes
        subRoute "/api/products" Products.Handlers.routes
        subRoute "/api/orders"   Orders.Handlers.routes
        route "/health" >=> text "OK"
    ]
```

### Pattern 3: Core.fs as the Dependency Ceiling

**What:** `Core.fs` is the only file that every other module can depend on. No domain module may import another domain module. Cross-domain references (e.g., Orders referencing a UserId) go through shared types defined in `Core.fs`.

**When to use:** Enforced from project start. Any violation breaks the isolation guarantee.

**Trade-offs:** Orders cannot call Users.Domain functions directly. If Orders needs user data, it must receive a `UserId` (defined in Core) and look up the user itself. This is actually good design — it prevents tight coupling between domains.

**Example:**

```fsharp
// Core.fs — shared types only, no business logic
module Core

type UserId = UserId of int
type ProductId = ProductId of int
type OrderId = OrderId of int

type ApiResponse<'T> = {
    Data: 'T option
    Error: string option
}

// Orders/Domain.fs — references Core types, not Users.Domain
open Core

type Order = {
    Id: OrderId
    UserId: UserId        // UserId from Core, not from Users.Domain
    ProductId: ProductId  // ProductId from Core, not from Products.Domain
    Quantity: int
}
```

### Pattern 4: Giraffe Fish Operator Composition

**What:** HttpHandlers are composed with `>=>` (fish operator) to form pipelines. Authentication, validation, and response formatting chain together naturally.

**When to use:** Any time multiple concerns apply to a single endpoint.

**Trade-offs:** F# newcomers may find `>=>` unfamiliar. The tutorial should explain it early in `02-project-setup.md`.

**Example:**

```fsharp
let requiresAuth : HttpHandler = // checks Authorization header
    fun next ctx -> task {
        if ctx.Request.Headers.ContainsKey("Authorization")
        then return! next ctx
        else return! setStatusCode 401 next ctx
    }

let getUser id : HttpHandler =
    requiresAuth >=> (fun next ctx -> task {
        // handler body
        return! json someUser next ctx
    })
```

## Data Flow

### Request Flow

```
HTTP Request
    ↓
ASP.NET Core Middleware (logging, CORS, auth)
    ↓
Giraffe choose [ ... ] in Program.fs
    ↓
subRoute "/api/users" → Users.Handlers.routes
    ↓
routef "/api/users/%i" → Users.Handlers.getUser id
    ↓
ctx.BindJsonAsync<T>()  — deserialize request
    ↓
Users.Domain.someFunction req  — pure business logic
    ↓
json result next ctx  — serialize response
    ↓
HTTP Response
```

### Worktree Development Data Flow

This is the key flow that the tutorial teaches:

```
main branch (Core.fs + Program.fs skeleton)
    ↓ git worktree add
    ├── worktree-users   (feature/users branch)  → Users/Domain.fs + Handlers.fs
    ├── worktree-products (feature/products branch) → Products/Domain.fs + Handlers.fs
    └── worktree-orders  (feature/orders branch)  → Orders/Domain.fs + Handlers.fs
                ↓ (parallel, simultaneous Claude Code sessions)
    merge feature/users → main
    merge feature/products → main     ← likely clean (no file overlap)
    merge feature/orders → main       ← conflict expected in Program.fs
                ↓
    main branch (complete API)
```

### Key Data Flows

1. **Fan-out:** Main branch contains `Core.fs` and a skeleton `Program.fs`. Three branches fan out from this base.
2. **Domain isolation:** Each worktree only writes to its own module folder. Zero file conflicts across `Users/`, `Products/`, `Orders/`.
3. **Merge conflict zone:** `Program.fs` receives route registrations from all three branches. This is the tutorial's conflict resolution lesson.
4. **Hotfix flow:** A `hotfix/` worktree branches from main while feature worktrees continue. The hotfix merges to main, then feature branches rebase onto updated main.

## Scaling Considerations

| Scale | Architecture Adjustments |
|-------|--------------------------|
| Tutorial (1 developer) | Monolith in single `src/` project, in-memory data store (no real DB) |
| Small team (2-5 devs) | Same structure; worktrees replace the need for separate repos |
| Real production API | Extract each domain into separate .fsproj; Core becomes a shared NuGet package |

### Scaling Priorities

1. **First bottleneck (tutorial scope):** In-memory data store (Dictionary) is not thread-safe across concurrent requests. For the tutorial this is acceptable — use a mutable ref protected by a lock or just note the limitation.
2. **Second bottleneck (post-tutorial):** If domains need to call each other, introduce an event bus or shared service abstraction rather than direct module imports.

## Anti-Patterns

### Anti-Pattern 1: Cross-Domain Module Imports

**What people do:** `Orders/Domain.fs` opens `Users.Domain` to call user validation functions directly.

**Why it's wrong:** Creates a compilation dependency that breaks worktree isolation. The Orders worktree now requires Users code to be complete before it can compile. Parallel development becomes sequential.

**Do this instead:** Define shared ID types in `Core.fs`. Orders holds a `UserId` (an opaque type from Core). If Orders needs to validate a user exists, it does so through a service interface registered in `Program.fs` at startup, not through direct module import.

### Anti-Pattern 2: Route Registration Inside Domain Handlers

**What people do:** Each `Handlers.fs` registers its own routes directly against the ASP.NET Core router, bypassing `Program.fs` composition.

**Why it's wrong:** The tutorial needs a visible merge conflict in `Program.fs` to teach conflict resolution. If routes self-register, merging is trivially conflict-free and the lesson disappears. Also, route registration in one place is genuinely better architecture.

**Do this instead:** Each `Handlers.fs` exports a `routes : HttpHandler` value. `Program.fs` composes them with `subRoute`.

### Anti-Pattern 3: Shared Mutable State Outside Core

**What people do:** A `Database.fs` module holds a mutable in-memory store and each domain module imports it directly.

**Why it's wrong:** Any worktree that touches `Database.fs` forces all other worktrees to rebase. For a tutorial this creates accidental complexity.

**Do this instead:** Each domain module defines its own in-memory store as a module-level `let mutable` within its own `Domain.fs`. This is not production-ready, but it keeps the tutorial's file ownership clean. The tutorial can note this explicitly as a simplification.

### Anti-Pattern 4: Deeply Nested Tutorial Documents Without Index

**What people do:** Tutorial chapters reference each other by relative path without a central `README.md` index.

**Why it's wrong:** Readers arriving at any chapter (e.g., from a search result) have no context about where they are in the series.

**Do this instead:** `tutorial/README.md` is the definitive reading guide with links to every chapter in order. Each chapter ends with a "Next: [chapter title]" link.

## Integration Points

### External Services

| Service | Integration Pattern | Notes |
|---------|---------------------|-------|
| In-memory data store | Module-level mutable Dictionary in each Domain.fs | Tutorial simplification; document explicitly |
| ASP.NET Core DI | `Program.fs` registers services via `services.Add*` | Domain modules receive services via `ctx.GetService<T>()` in handlers |
| JSON serialization | Giraffe built-in `json` handler + `System.Text.Json` | No extra library needed; `[<CLIMutable>]` attribute for request DTOs |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| `Core.fs` → Domain modules | Direct open/import (one-way only) | Core has no dependencies; all domains depend on Core |
| Domain modules → Handlers modules | Direct open (one-way: Handlers opens Domain) | Handlers are always compiled after their Domain |
| Domain modules → each other | FORBIDDEN | Cross-domain imports break worktree isolation |
| Handlers modules → `Program.fs` | Handlers export `routes : HttpHandler`; Program composes them | Program.fs is the single integration point |
| `tutorial/` → `src/` | Code blocks in Markdown reference src/ paths | Tutorial documents do not import or build against src/ |

## Build Order Implications for Roadmap

The F# compiler enforces dependency order, which directly maps to the tutorial phase structure:

| Phase | What Gets Built | Why This Order |
|-------|-----------------|----------------|
| Foundation | `Core.fs` + skeleton `Program.fs` + project file | Must exist before worktrees branch. All domains depend on Core. |
| Fan-out | `Users/`, `Products/`, `Orders/` in parallel worktrees | No inter-domain dependencies; can be built simultaneously |
| Merge-back | Route registration in `Program.fs`, conflict resolution | Integration point; teaches the key worktree skill |
| Advanced | Hotfix worktree + CI/CD patterns | Requires understanding of merge-back first |

**Critical build order constraint:** `Core.fs` must be complete before any domain worktree branches. If Users needs a `UserId` type that does not yet exist in `Core.fs`, the Users worktree cannot compile. The tutorial must establish this dependency clearly in Phase 1.

## Sources

- [Giraffe Official Documentation](https://giraffe.wiki/docs) — HttpHandler composition, routing, subRoute patterns
- [Giraffe GitHub DOCUMENTATION.md](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md) — route types, fish operator, choose combinator
- [F# for Fun and Profit: Organizing Modules](https://fsharpforfunandprofit.com/posts/recipe-part3/) — types-only modules, compilation order, shared type placement
- [F# for Fun and Profit: Cyclic Dependencies](https://fsharpforfunandprofit.com/posts/cyclic-dependencies/) — why cross-module imports are evil in F#
- [Codit: Ports and Adapters in F# Giraffe](https://www.codit.eu/blog/a-f-primitive-giraffe-wearing-lenses-a-ports-and-adapters-story/) — onion architecture with bounded contexts
- [Samuel Eresca: F# Giraffe Web Service](https://samueleresca.net/build-web-service-using-f-and-asp-net-core/) — five-file project structure (Model/DataAccess/RequestModels/Handlers/Program)
- [Viquoc Quan: Creating API with Giraffe](https://vtquan.github.io/fsharp/creating-api-with-giraffe/) — Controllers folder pattern, subRoute organization
- [Sergii Grytsaienko: Parallel AI Development with Git Worktrees](https://sgryt.com/posts/git-worktree-parallel-ai-development/) — isolation principles, focused task decomposition
- [Git Worktrees for Parallel AI Coding Agents (Upsun)](https://devcenter.upsun.com/posts/git-worktrees-for-parallel-ai-coding-agents/) — file state isolation, shared git history model

---
*Architecture research for: F# Giraffe REST API tutorial with git worktree parallel development*
*Researched: 2026-03-04*
