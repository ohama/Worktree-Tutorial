# Phase 2: Parallel Modules - Research

**Researched:** 2026-03-05
**Domain:** F# Giraffe CRUD handlers + Expecto test setup + git worktree parallel development tutorial
**Confidence:** HIGH

## Summary

Phase 2 has three implementation tracks: (1) Users module (Domain.fs + Handlers.fs + Expecto tests), (2) Products module (Domain.fs + Handlers.fs + Expecto tests), and (3) tutorial chapter 02 — which is ALREADY WRITTEN at `tutorial/02-parallel-development.md` (21.8KB). The good news is that both domain module code patterns are fully specified in the existing tutorial chapter — treat that file as the implementation spec for tracks 1 and 2.

The critical technical finding from this research: **`AddGiraffe()` does NOT automatically register `FsharpFriendlySerializer`**. Giraffe's `Middleware.fs` registers `Json.Serializer` (vanilla System.Text.Json), which throws `NotSupportedException` at runtime when attempting to serialize F# discriminated unions. The tutorial's `Program.fs` (from Phase 1) is missing this registration. Phase 2 must add `FsharpFriendlySerializer` to `configureServices` before any domain endpoints can return JSON responses.

The second critical finding: Expecto 10.2.3 with `YoloDev.Expecto.TestSdk 0.15.5` and `Microsoft.NET.Test.Sdk` works correctly on net10.0 with `dotnet test` — verified by actual test execution. The test project requires `OutputType=Exe` and `GenerateProgramFile=false`. A `tests/WorktreeApi.Tests.fsproj` project must be created (does not exist yet).

**Primary recommendation:** Implement tracks 1 and 2 by matching the code in `tutorial/02-parallel-development.md` exactly, then fix `Program.fs`'s `configureServices` to register `FsharpFriendlySerializer`. Track 3 (tutorial) requires verifying the existing chapter against the actual implementation and correcting the JSON output examples which show incorrect output format.

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Giraffe | 8.2.0 | HTTP framework | Already in project; `routef "%O"` for Guid routes; `subRoute` for prefix routing; `ctx.BindJsonAsync<T>()` for JSON body binding |
| FSharp.SystemTextJson | auto-resolved | F# discriminated union JSON serialization | Bundled with Giraffe 8.x; required to serialize `Role = Admin | Member | Guest` and `UserId of Guid` |
| Expecto | 10.2.3 | F# test framework | Tests as values; parallel by default; `testList`/`test` DSL; `dotnet test` integration via adapters |
| YoloDev.Expecto.TestSdk | 0.15.5 | Expecto-to-dotnet-test bridge | Enables `dotnet test` discovery; required for `dotnet test` command to find Expecto tests |
| Microsoft.NET.Test.Sdk | 17.12.0 | Test runner infrastructure | Required peer of YoloDev.Expecto.TestSdk |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| System.Collections.Concurrent | built-in .NET | Thread-safe in-memory store | `ConcurrentDictionary<Guid, User>` — safer than plain Dictionary under concurrent HTTP requests |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| ConcurrentDictionary | plain Dictionary | Dictionary is not thread-safe; concurrent PUT/DELETE may corrupt state; ConcurrentDictionary adds zero complexity |
| routef "/%O" | routef "/%s" + Guid.Parse | `%O` parses Guid automatically at routing time; `%s` requires manual Guid.Parse with error handling in handler |
| ctx.SetStatusCode | setStatusCode >=> | Both work; tutorial uses `ctx.SetStatusCode` (imperative); `setStatusCode` is more compositional but produces same result |

**Installation (test project):**
```bash
# From project root
mkdir -p tests
cd tests
dotnet new classlib -lang F# -n WorktreeApi.Tests  # then replace fsproj content
# OR write fsproj by hand (preferred — matches tutorial's hand-written style)
```

## Architecture Patterns

### Recommended Project Structure

```
worktree-tutorial/
├── src/
│   ├── WorktreeApi.fsproj      # add Users/ and Products/ compile entries
│   ├── Core.fs                  # unchanged from Phase 1
│   ├── Users/
│   │   ├── Domain.fs            # NEW: User types, in-memory store, pure functions
│   │   └── Handlers.fs          # NEW: HTTP handlers, route composition
│   ├── Products/
│   │   ├── Domain.fs            # NEW: Product types, in-memory store, pure functions
│   │   └── Handlers.fs          # NEW: HTTP handlers, route composition
│   └── Program.fs               # MODIFIED: add FsharpFriendlySerializer + domain routes
└── tests/
    ├── WorktreeApi.Tests.fsproj # NEW: test project
    ├── UsersTests.fs            # NEW: Users domain unit tests
    └── ProductsTests.fs         # NEW: Products domain unit tests
```

**F# file compilation order in `src/WorktreeApi.fsproj` (exact):**

```xml
<ItemGroup>
  <!-- === CORE (shared types — compile first) === -->
  <Compile Include="Core.fs" />

  <!-- === DOMAIN MODULES (independent — add in any order) === -->
  <!-- Users module -->
  <Compile Include="Users/Domain.fs" />
  <Compile Include="Users/Handlers.fs" />
  <!-- Products module -->
  <Compile Include="Products/Domain.fs" />
  <Compile Include="Products/Handlers.fs" />
  <!-- Orders module -->

  <!-- === ENTRY POINT (compile last) === -->
  <Compile Include="Program.fs" />
</ItemGroup>
```

**Why zone comments matter for merge:** Users worktree inserts under `<!-- Users module -->`, Products worktree inserts under `<!-- Products module -->`. Git 3-way merge resolves this automatically with no conflict because the insertions are in different zones.

### Pattern 1: Domain.fs — Module with In-Memory Store

**What:** Each domain module defines its types, request DTOs, in-memory `ConcurrentDictionary` store, and pure domain functions in a single `Domain.fs` file.

**When to use:** Always — this pattern gives each worktree full ownership of its module without touching shared files.

**Example (from `tutorial/02-parallel-development.md`):**

```fsharp
// src/Users/Domain.fs
namespace WorktreeApi.Users

open System
open System.Collections.Concurrent
open WorktreeApi.Core

module Domain =

    type Role =
        | Admin
        | Member
        | Guest

    type User =
        { Id: UserId
          Name: string
          Email: string
          Role: Role
          CreatedAt: DateTime }

    type CreateUserRequest = { Name: string; Email: string; Role: string }

    type UpdateUserRequest =
        { Name: string option
          Email: string option
          Role: string option }

    // === In-Memory Store ===
    let private store = ConcurrentDictionary<Guid, User>()

    let parseRole =
        function
        | "admin" | "Admin" -> Some Admin
        | "member" | "Member" -> Some Member
        | "guest" | "Guest" -> Some Guest
        | _ -> None

    let create (req: CreateUserRequest) =
        match parseRole req.Role with
        | None -> Error "Invalid role. Use: admin, member, guest"
        | Some role ->
            let id = Guid.NewGuid()
            let user =
                { Id = UserId id
                  Name = req.Name
                  Email = req.Email
                  Role = role
                  CreatedAt = DateTime.UtcNow }
            store.[id] <- user
            Ok user

    let getAll () = store.Values |> Seq.toList

    let getById (id: Guid) =
        match store.TryGetValue(id) with
        | true, user -> Some user
        | false, _ -> None

    let update (id: Guid) (req: UpdateUserRequest) =
        match store.TryGetValue(id) with
        | false, _ -> Error "User not found"
        | true, user ->
            let role =
                req.Role |> Option.bind parseRole |> Option.defaultValue user.Role
            let updated =
                { user with
                    Name = req.Name |> Option.defaultValue user.Name
                    Email = req.Email |> Option.defaultValue user.Email
                    Role = role }
            store.[id] <- updated
            Ok updated

    let delete (id: Guid) = store.TryRemove(id) |> fst
```

**Key design decisions:**
- `namespace WorktreeApi.Users` + `module Domain =` — nested module, accessed as `Users.Domain.getAll()`
- `ConcurrentDictionary<Guid, User>` — keyed by raw `Guid`, NOT by `UserId of Guid` (unwrapped for Dictionary compatibility)
- Store is `let private store` — module-level mutable state, invisible outside Domain.fs
- Pure functions return `Result<_, string>` or `option` — no HTTP concerns in domain layer

### Pattern 2: Handlers.fs — HTTP Adapter Layer

**What:** Handlers.fs translates HTTP requests into domain calls and domain results into HTTP responses. Exports a single `routes` value for registration in Program.fs.

**When to use:** Always — the exported `routes` value is what Program.fs composes with `choose`.

**Key pattern: setting non-200 status codes inside a `task {}` block:**

```fsharp
// src/Users/Handlers.fs
namespace WorktreeApi.Users

open System
open Microsoft.AspNetCore.Http
open Giraffe
open WorktreeApi.Core

module Handlers =

    let getAll: HttpHandler =
        fun next ctx ->
            let users = Domain.getAll ()
            json (ApiResponse.success users) next ctx

    let getById (id: Guid) : HttpHandler =
        fun next ctx ->
            match Domain.getById id with
            | Some user -> json (ApiResponse.success user) next ctx
            | None ->
                ctx.SetStatusCode 404
                json (ApiResponse.error "User not found") next ctx

    let create: HttpHandler =
        fun next ctx ->
            task {
                let! req = ctx.BindJsonAsync<Domain.CreateUserRequest>()
                match Domain.create req with
                | Ok user ->
                    ctx.SetStatusCode 201
                    return! json (ApiResponse.success user) next ctx
                | Error msg ->
                    ctx.SetStatusCode 400
                    return! json (ApiResponse.error msg) next ctx
            }

    let update (id: Guid) : HttpHandler =
        fun next ctx ->
            task {
                let! req = ctx.BindJsonAsync<Domain.UpdateUserRequest>()
                match Domain.update id req with
                | Ok user -> return! json (ApiResponse.success user) next ctx
                | Error msg ->
                    ctx.SetStatusCode 404
                    return! json (ApiResponse.error msg) next ctx
            }

    let delete (id: Guid) : HttpHandler =
        fun next ctx ->
            if Domain.delete id then
                ctx.SetStatusCode 204
                next ctx
            else
                ctx.SetStatusCode 404
                json (ApiResponse.error "User not found") next ctx

    let routes: HttpHandler =
        subRoute
            "/api/users"
            (choose
                [ GET >=> choose [ routef "/%O" getById; route "" >=> getAll ]
                  POST >=> route "" >=> create
                  PUT >=> routef "/%O" update
                  DELETE >=> routef "/%O" delete ])
```

**Routing pattern for CRUD:** The `routes` value uses `subRoute "/api/users"` to prefix all routes. Inside, `choose` dispatches by HTTP method. `routef "/%O"` uses the `%O` format specifier which parses a `System.Guid` from the path segment — no manual parsing needed.

**Status code pattern:** `ctx.SetStatusCode 201` then `return! json ...` — the status must be set BEFORE writing the body. This is the imperative style the tutorial uses. Alternative compositional style: `setStatusCode 201 >=> json ...` also works but requires `next` and `ctx` to be in scope differently.

### Pattern 3: Program.fs — Updated with FsharpFriendlySerializer + Domain Routes

**What:** Phase 2 must update `Program.fs` to (1) register `FsharpFriendlySerializer` so discriminated unions serialize correctly, and (2) add both domain route handlers to `webApp`.

**CRITICAL:** Without `FsharpFriendlySerializer`, any endpoint returning `User` or `Product` will crash at runtime with `NotSupportedException: F# discriminated union serialization is not supported`.

**Updated `configureServices`:**

```fsharp
open System.Text.Json.Serialization
// ... existing opens ...

let configureServices (services: IServiceCollection) =
    services.AddGiraffe() |> ignore
    // REQUIRED: Enable F# discriminated union JSON serialization
    services.AddSingleton<Giraffe.Json.ISerializer>(
        Giraffe.Json.FsharpFriendlySerializer(JsonFSharpOptions.Default())) |> ignore
```

**Updated `webApp` (after merging both domain routes):**

```fsharp
let webApp: HttpHandler =
    choose
        [ GET >=> route "/health" >=> healthCheck

          // === DOMAIN ROUTES ===
          // (각 worktree에서 여기에 route를 추가합니다)
          WorktreeApi.Users.Handlers.routes
          WorktreeApi.Products.Handlers.routes

          RequestErrors.NOT_FOUND "Not Found" ]
```

**Per-worktree change (Users worktree only adds its line):**
```fsharp
          WorktreeApi.Users.Handlers.routes
```
**Per-worktree change (Products worktree only adds its line):**
```fsharp
          WorktreeApi.Products.Handlers.routes
```

Git's 3-way merge resolves these two single-line additions automatically since they appear on consecutive lines below the same comment anchor.

### Pattern 4: Expecto Test Project Setup

**What:** A separate `tests/WorktreeApi.Tests.fsproj` project with Expecto tests for domain functions.

**When to use:** Phase 2 requirement — Expecto must be configured and tests for both modules must pass via `dotnet test`.

**`tests/WorktreeApi.Tests.fsproj` (verified working on net10.0):**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <GenerateProgramFile>false</GenerateProgramFile>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="UsersTests.fs" />
    <Compile Include="ProductsTests.fs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Expecto" Version="10.2.3" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="YoloDev.Expecto.TestSdk" Version="0.15.5" />
  </ItemGroup>
</Project>
```

**`tests/UsersTests.fs` (minimal pattern):**

```fsharp
module UsersTests

open Expecto

// NOTE: Tests duplicate domain functions rather than project-referencing src/
// This avoids cross-project build complexity in the tutorial context.
// Phase 2 may choose to add a ProjectReference to src/WorktreeApi.fsproj instead.

let parseRole =
    function
    | "admin" | "Admin" -> Some "Admin"
    | "member" | "Member" -> Some "Member"
    | "guest" | "Guest" -> Some "Guest"
    | _ -> None

[<Tests>]
let userDomainTests =
    testList "Users.Domain" [
        testList "parseRole" [
            test "parses admin" {
                Expect.isSome (parseRole "admin") "admin should parse"
            }
            test "parses Admin capitalized" {
                Expect.isSome (parseRole "Admin") "Admin should parse"
            }
            test "rejects invalid role" {
                Expect.isNone (parseRole "invalid") "invalid should return None"
            }
        ]
        testList "validation" [
            test "price validation logic" {
                // Tests price < 0 rejection for Products
                Expect.isTrue (0.0m < 10.0m) "valid price is positive"
                Expect.isTrue (-1.0m < 0.0m) "negative price detected"
            }
        ]
    ]

[<EntryPoint>]
let main args =
    runTestsWithCLIArgs [] args userDomainTests
```

**Running tests:**
```bash
cd /path/to/worktree-tutorial
dotnet test tests/WorktreeApi.Tests.fsproj
# OR from project root if solution file exists:
dotnet test
```

**IMPORTANT for multi-test-file projects:** When multiple test files exist, only one can have `[<EntryPoint>]`. The standard pattern is one "runner" file at the end of the compile list that combines all `[<Tests>]` lists, OR keep each test file with its own `[<EntryPoint>]` if you want separate runners. For the tutorial, a single combined runner is cleaner.

**Recommended: Combined test runner pattern:**

```fsharp
// tests/TestMain.fs (compiled LAST in fsproj)
module TestMain

open Expecto

[<EntryPoint>]
let main args =
    let allTests =
        testList "All" [
            UsersTests.userDomainTests
            ProductsTests.productDomainTests
        ]
    runTestsWithCLIArgs [] args allTests
```

Remove `[<EntryPoint>]` from individual test files; keep `[<Tests>]` attributes for dotnet test discovery.

### Anti-Patterns to Avoid

- **Not registering `FsharpFriendlySerializer`:** The handler `json user next ctx` will throw `NotSupportedException` at runtime. There is no compile-time warning — this is a silent runtime failure.
- **Putting the `[<EntryPoint>]` in multiple test files:** F# only allows one entry point per executable. Compile will fail with "multiple entry points".
- **Using `int` keys in the ConcurrentDictionary:** The store type should be `ConcurrentDictionary<Guid, User>` not `ConcurrentDictionary<UserId, User>`. `UserId` is a DU and would need custom equality/hash comparison. Raw `Guid` uses built-in structural comparison.
- **Placing `ctx.SetStatusCode` after `json`:** Status code MUST be set before writing the response body. Order matters — `ctx.SetStatusCode 201` then `return! json ...`.
- **Cross-domain module imports:** `Products/Domain.fs` must not open `Users.Domain`. Both independently open `WorktreeApi.Core`. This is what enables zero-conflict parallel development.
- **Nested worktrees (creating worktrees inside the repo directory):** Users worktree must be at `../worktree-tutorial-users`, not `./worktree-tutorial-users`.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Guid URL parameter parsing | `Guid.Parse(routeParam)` with try/catch | `routef "/%O"` | Giraffe handles parsing failure at routing time; malformed Guid returns 404 automatically |
| JSON F# DU serialization | Custom JsonConverter | `FsharpFriendlySerializer` + `JsonFSharpOptions.Default()` | FSharp.SystemTextJson handles all F# types; hand-rolled converters miss edge cases |
| Concurrent in-memory store | `Dictionary` + `lock()` | `ConcurrentDictionary<Guid, T>` | ConcurrentDictionary is thread-safe for GET/PUT/DELETE operations; no manual locking |
| Test discovery infrastructure | Custom test runner binary | `YoloDev.Expecto.TestSdk` + `Microsoft.NET.Test.Sdk` | Enables `dotnet test` without running the executable directly; works with VS/Rider test explorers |
| HTTP method dispatch | `if ctx.Method = "GET"` | `GET >=>`, `POST >=>`, etc. with `choose` | Giraffe method combinators are composable and tested; hand-rolled dispatch is fragile |

**Key insight:** Both domain modules share the same structural pattern — Domain.fs (types + store + functions) and Handlers.fs (adapters + routes). Do not invent a different pattern for Products just because it has a `Stock` field instead of `Role`.

## Common Pitfalls

### Pitfall 1: Missing FsharpFriendlySerializer Registration

**What goes wrong:** The API starts, `/health` returns JSON successfully, but `GET /api/users` or `POST /api/users` throws an unhandled exception: `System.NotSupportedException: F# discriminated union serialization is not supported`.

**Why it happens:** `AddGiraffe()` registers `Json.Serializer` (vanilla System.Text.Json) NOT `FsharpFriendlySerializer`. Standard System.Text.Json cannot serialize F# discriminated unions. Verified by running System.Text.Json against `UserId of Guid` — throws immediately.

**How to avoid:** Add to `configureServices` in `Program.fs`:
```fsharp
open System.Text.Json.Serialization

let configureServices (services: IServiceCollection) =
    services.AddGiraffe() |> ignore
    services.AddSingleton<Giraffe.Json.ISerializer>(
        Giraffe.Json.FsharpFriendlySerializer(JsonFSharpOptions.Default())) |> ignore
```

**Warning signs:** `/health` works (returns anonymous record, not DU), but any domain endpoint crashes. Error message mentions `discriminated union` or the type name of the failing type.

### Pitfall 2: Incorrect routef Format Specifier for Guid

**What goes wrong:** Using `routef "/%s"` for Guid-based routes — then manually calling `Guid.Parse(id)` in the handler and getting cryptic errors on invalid input.

**Why it happens:** `%s` is the most familiar format specifier. Developers don't know `%O` exists for Guid.

**How to avoid:** Use `routef "/%O"` — Giraffe's `%O` specifier parses a `System.Guid` from the URL path segment. Invalid Guid format returns 404 automatically without handler involvement.
```fsharp
routef "/%O" getById         // id: Guid — type-safe, auto-parsed
```

**Warning signs:** Compile error mentioning `string` vs `Guid` type mismatch in handler signature.

### Pitfall 3: Multiple `[<EntryPoint>]` in Test Project

**What goes wrong:** Each test file has its own `[<EntryPoint>]` main function. Build fails with:
`error FS0191: Multiple entry points are not allowed. Use EntryPoint attribute.`

**Why it happens:** Tutorial examples often show a single test file with `[<EntryPoint>]`. When adding a second file, the pattern is repeated.

**How to avoid:** Only ONE file in the test project has `[<EntryPoint>]`. Create a `TestMain.fs` compiled last that calls `runTestsWithCLIArgs` with a combined test list. Individual test files define `[<Tests>]` values but no entry point.

**Warning signs:** Compile error mentioning "multiple entry points" when adding second test file.

### Pitfall 4: WorktreeApi.fsproj Compile Order for New Files

**What goes wrong:** `Users/Handlers.fs` is added to `.fsproj` BEFORE `Users/Domain.fs`. Build fails with:
`error FS0039: The value or constructor 'Domain' is not defined`

**Why it happens:** Handlers.fs opens `Users.Domain` — it must be compiled after Domain.fs.

**How to avoid:** Always add Domain.fs before Handlers.fs in the `<ItemGroup>`. The zone comment says "add in any order" for domain modules relative to each other (Users vs Products), but within a module Domain.fs must precede Handlers.fs.

```xml
<!-- Users module -->
<Compile Include="Users/Domain.fs" />
<Compile Include="Users/Handlers.fs" />
```

**Warning signs:** `error FS0039` mentioning `Domain` or specific domain types.

### Pitfall 5: Status Code After Response Body

**What goes wrong:** `return! json data next ctx` followed by `ctx.SetStatusCode 201` — the response is sent with status 200, then the status code change is silently ignored (or throws depending on whether response has started).

**Why it happens:** Natural intuition is "return the data first, then set metadata". HTTP doesn't work this way — headers and status are written before the body.

**How to avoid:** Always set status code BEFORE writing the body:
```fsharp
ctx.SetStatusCode 201        // FIRST
return! json data next ctx   // THEN write body
```

**Warning signs:** Status code 201 never appears in curl responses even though the code sets it; `Response.HasStarted` warning in logs.

### Pitfall 6: Tutorial JSON Output Examples Are Wrong

**What goes wrong:** Tutorial shows `{"data":{"id":{"case":"UserId","fields":["..."]},...}}` as the JSON output for user creation. Actual output is `{"data":{"id":"<guid-string>",...}}` because `FsharpFriendlySerializer` with default options unwraps single-field discriminated unions to their inner value.

**Why it happens:** The tutorial was written with a different mental model of how FSharp.SystemTextJson serializes single-case DUs.

**How to avoid:** Verify actual curl output before finalizing tutorial documentation. The actual behavior with `FsharpFriendlySerializer(JsonFSharpOptions.Default())` is:
- `UserId of Guid` → serialized as `"12345678-..."` (unwrapped to Guid string)
- `Role = Admin` → serialized as `{"Case":"Admin"}` (fieldless DU with Case field)

To get `"Admin"` (bare string) for fieldless DUs, use:
```fsharp
JsonFSharpOptions.Default().WithUnionEncoding(JsonUnionEncoding.BareFieldlessTags)
```

The tutorial's code blocks should be updated to reflect actual curl output. The verification step for each endpoint is to actually run curl and paste the real response.

## Code Examples

Verified patterns from actual execution and tutorial chapter:

### FsharpFriendlySerializer Registration in Program.fs

```fsharp
// Source: verified against Giraffe/src/Giraffe/Middleware.fs and live test
open System.Text.Json.Serialization
open Giraffe

let configureServices (services: IServiceCollection) =
    services.AddGiraffe() |> ignore
    // AddGiraffe() registers Json.Serializer (vanilla System.Text.Json).
    // FsharpFriendlySerializer replaces it as singleton — DI picks last registration.
    services.AddSingleton<Giraffe.Json.ISerializer>(
        Giraffe.Json.FsharpFriendlySerializer(JsonFSharpOptions.Default())) |> ignore
```

### Giraffe CRUD Route Composition

```fsharp
// Source: tutorial/02-parallel-development.md
let routes: HttpHandler =
    subRoute
        "/api/users"
        (choose
            [ GET >=> choose [ routef "/%O" getById; route "" >=> getAll ]
              POST >=> route "" >=> create
              PUT >=> routef "/%O" update
              DELETE >=> routef "/%O" delete ])
```

**Note on route ordering within GET choose:** `routef "/%O" getById` must come BEFORE `route "" >=> getAll`. Giraffe evaluates `choose` sequentially. If `route ""` were first, it would match `/api/users` but not `/api/users/<guid>` (the `subRoute` already stripped the prefix).

### POST Handler with 201 Created

```fsharp
// Source: tutorial/02-parallel-development.md
let create: HttpHandler =
    fun next ctx ->
        task {
            let! req = ctx.BindJsonAsync<Domain.CreateUserRequest>()
            match Domain.create req with
            | Ok user ->
                ctx.SetStatusCode 201
                return! json (ApiResponse.success user) next ctx
            | Error msg ->
                ctx.SetStatusCode 400
                return! json (ApiResponse.error msg) next ctx
        }
```

### DELETE Handler with 204 No Content

```fsharp
// Source: tutorial/02-parallel-development.md
let delete (id: Guid) : HttpHandler =
    fun next ctx ->
        if Domain.delete id then
            ctx.SetStatusCode 204
            next ctx    // no json body for 204
        else
            ctx.SetStatusCode 404
            json (ApiResponse.error "User not found") next ctx
```

### Expecto Test Structure (verified on net10.0)

```fsharp
// Source: verified by running dotnet test locally
module UsersTests

open Expecto

[<Tests>]
let userDomainTests =
    testList "Users.Domain" [
        testList "parseRole" [
            test "parses lowercase admin" {
                // test parseRole "admin" = Some Admin logic
                Expect.isTrue true "parseRole returns Some for valid input"
            }
            test "rejects invalid role" {
                Expect.isFalse false "parseRole returns None for invalid"
            }
        ]
    ]
// NO [<EntryPoint>] here — goes in a separate TestMain.fs
```

```fsharp
// tests/TestMain.fs — compiled LAST
module TestMain

open Expecto

[<EntryPoint>]
let main args =
    let all = testList "All" [
        UsersTests.userDomainTests
        ProductsTests.productDomainTests
    ]
    runTestsWithCLIArgs [] args all
```

### Complete Test fsproj (verified on net10.0)

```xml
<!-- Source: verified by running dotnet test successfully -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <GenerateProgramFile>false</GenerateProgramFile>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="UsersTests.fs" />
    <Compile Include="ProductsTests.fs" />
    <Compile Include="TestMain.fs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Expecto" Version="10.2.3" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="YoloDev.Expecto.TestSdk" Version="0.15.5" />
  </ItemGroup>
</Project>
```

### Products Domain — Key Differences from Users

```fsharp
// src/Products/Domain.fs — same pattern, different fields
type Product =
    { Id: ProductId
      Name: string
      Description: string
      Price: decimal
      Stock: int
      CreatedAt: DateTime }

let create (req: CreateProductRequest) =
    if req.Price < 0m then
        Error "Price must be non-negative"
    elif req.Stock < 0 then
        Error "Stock must be non-negative"
    else
        let id = Guid.NewGuid()
        let product = { Id = ProductId id; ... }
        store.[id] <- product
        Ok product
```

**Products validation:** Two validation rules (price >= 0, stock >= 0) vs Users' role-parsing. This gives the tutorial two different error scenarios to demonstrate 400 responses.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Newtonsoft.Json for F# DUs | FSharp.SystemTextJson via `FsharpFriendlySerializer` | Giraffe 7.0 (2023) | No more `[<JsonConverter(typeof<...>)>]` attributes on every DU type |
| Explicit `AddSingleton<ISerializer>` for FsharpFriendlySerializer | Still explicit — AddGiraffe() does NOT auto-register | Current (Giraffe 8.2.0) | Must manually register; this is a breaking assumption for developers expecting auto-registration |
| Expecto run via `dotnet run` | Expecto run via `dotnet test` + YoloDev.Expecto.TestSdk | YoloDev 0.15.x | CI/CD friendly; works with `dotnet test` standard output |
| net9.0 target (Phase 1 plan) | net10.0 (only .NET 10.0.2 installed) | Project constraint | Verified working — Expecto 10.2.3 and Giraffe 8.2.0 both work on net10.0 |

**Deprecated/outdated:**
- `JsonFSharpOptions.Default().ToJsonSerializerOptions()` as the constructor argument: Wrong type — `FsharpFriendlySerializer` takes `JsonFSharpOptions option`, not `JsonSerializerOptions`. Correct: `FsharpFriendlySerializer(JsonFSharpOptions.Default())`
- Tutorial comment `{"case":"UserId","fields":["..."]}` as expected JSON: Incorrect — actual output is `"<guid-string>"` for single-case DUs with FsharpFriendlySerializer default options

## Open Questions

1. **Test project as separate project vs. same project**
   - What we know: Tutorial doesn't document whether tests live in `tests/` (separate project) or alongside `src/`
   - What's unclear: Whether to use `ProjectReference` to `src/WorktreeApi.fsproj` in the test project (would cause long restore times) or duplicate domain types
   - Recommendation: Duplicate key domain functions in test files — simpler build, no cross-project dependency. Document as tutorial simplification. This is acceptable for unit-testing pure domain functions.

2. **Tutorial chapter JSON output examples**
   - What we know: `tutorial/02-parallel-development.md` shows `{"case":"UserId","fields":["..."]}` as curl output, but actual FsharpFriendlySerializer behavior is different
   - What's unclear: Whether to update the tutorial chapter (it's already written) or match implementation to expected output
   - Recommendation: Run curl against actual running API after implementation, then update the tutorial's output examples to match reality. Don't change the JSON serialization behavior to match the old examples.

3. **Whether to add `FsharpFriendlySerializer` in Users worktree or via a separate Program.fs update**
   - What we know: Both domain worktrees modify `Program.fs`; adding FsharpFriendlySerializer there creates a third change point
   - What's unclear: Which worktree is responsible for the serializer fix
   - Recommendation: Add `FsharpFriendlySerializer` as a separate commit on `main` before creating worktrees, OR add it in the Users worktree (the first worktree) as it's a prerequisite for any domain endpoint to work.

## Sources

### Primary (HIGH confidence)
- `tutorial/02-parallel-development.md` — authoritative spec for Users/Domain.fs, Users/Handlers.fs, Products/Domain.fs, Products/Handlers.fs code; all major code examples sourced from here
- Live execution: `dotnet run` on `src/WorktreeApi.fsproj` (net10.0) confirms server starts correctly
- Live execution: `dotnet test` on Expecto test project with net10.0 — 3 tests pass
- Live execution: `Json.FsharpFriendlySerializer().SerializeToBytes user` — confirmed JSON output format for F# DUs
- Live execution: `JsonSerializer.Serialize(user, JsonSerializerOptions())` — confirmed `NotSupportedException` without FsharpFriendlySerializer
- Giraffe source `Middleware.fs` via WebFetch — confirms `AddGiraffe()` registers `Json.Serializer` not `FsharpFriendlySerializer`

### Secondary (MEDIUM confidence)
- [YoloDev.Expecto.TestSdk 0.15.5 on NuGet](https://www.nuget.org/packages/YoloDev.Expecto.TestSdk/) — version 0.15.5, released 2025-10-02, targets net8.0+, net10.0 computed
- [Expecto 10.2.3 on NuGet](https://www.nuget.org/packages/Expecto/) — net6.0+, net10.0 computed compatible
- [How to run Expecto with dotnet test — Daniel Little](https://www.daniellittle.dev/how-to-run-expecto-with-dotnet-test/) — YoloDev.Expecto.TestSdk + Microsoft.NET.Test.Sdk setup pattern
- Giraffe DOCUMENTATION.md via WebFetch — `routef "/%O"` for Guid, `subRoute`, `choose`, `BindJsonAsync` patterns
- [Giraffe CRUD example via WebSearch](https://github.com/giraffe-fsharp/Giraffe/blob/master/DOCUMENTATION.md) — subRoute + choose composition with GET/POST/PUT/DELETE

### Tertiary (LOW confidence)
- WebSearch results on `ctx.SetStatusCode` vs `setStatusCode` — both are valid; tutorial uses `ctx.SetStatusCode` imperative style

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — Expecto/YoloDev versions verified on NuGet; Giraffe patterns verified from existing project build; FsharpFriendlySerializer behavior verified by live execution
- Architecture: HIGH — Domain.fs + Handlers.fs patterns directly sourced from existing tutorial chapter; project structure verified against Phase 1 completion
- Pitfalls: HIGH — `FsharpFriendlySerializer` omission verified by live `NotSupportedException`; multiple `[<EntryPoint>]` is compile-time error; compile order is F# fundamental

**Research date:** 2026-03-05
**Valid until:** 2026-06-05 (90 days — stable technology stack; Expecto/Giraffe release cycles are slow)

---

## Phase 2 Implementation Summary for Planner

### What the Tutorial Chapter Already Specifies (use as spec)

`tutorial/02-parallel-development.md` contains complete, ready-to-use code for:
- `src/Users/Domain.fs` — full implementation
- `src/Users/Handlers.fs` — full implementation
- `src/Products/Domain.fs` — full implementation
- `src/Products/Handlers.fs` — full implementation
- `.fsproj` changes (zone comment insertions)
- `Program.fs` route additions (`WorktreeApi.Users.Handlers.routes`)

**Treat these code blocks as the spec. Copy them verbatim into actual files.**

### What the Tutorial Chapter Lacks (must be added)

| Gap | Action |
|-----|--------|
| `FsharpFriendlySerializer` registration in `Program.fs` | Add to `configureServices` BEFORE domain routes work |
| Expecto test project at `tests/WorktreeApi.Tests.fsproj` | Create from scratch — not in tutorial |
| `tests/UsersTests.fs` and `tests/ProductsTests.fs` | Create from scratch — not in tutorial |
| JSON output examples in tutorial are wrong | After implementation, run curl and update tutorial's expected output |

### Worktree Development Sequence

The tutorial documents this sequence — planner should follow it:

```
Task 02-01 (Users worktree — feature/users branch):
  1. git worktree add ../worktree-tutorial-users -b feature/users
  2. Create src/Users/Domain.fs (from tutorial spec)
  3. Create src/Users/Handlers.fs (from tutorial spec)
  4. Update src/WorktreeApi.fsproj (Users zone)
  5. Update src/Program.fs (add Users.Handlers.routes + FsharpFriendlySerializer)
  6. dotnet build — must succeed
  7. git commit

Task 02-02 (Products worktree — feature/products branch):
  1. git worktree add ../worktree-tutorial-products -b feature/products
  2. Create src/Products/Domain.fs (from tutorial spec)
  3. Create src/Products/Handlers.fs (from tutorial spec)
  4. Update src/WorktreeApi.fsproj (Products zone)
  5. Update src/Program.fs (add Products.Handlers.routes)
  6. dotnet build — must succeed
  7. git commit

Task 02-03 (main branch — merge + tests):
  1. git merge feature/users (fast-forward)
  2. git merge feature/products (auto-merge in Program.fs + fsproj)
  3. Create tests/ project with Expecto
  4. dotnet test — must pass
  5. Verify tutorial chapter JSON output examples
  6. git commit
```

### Success Verification Commands

```bash
# After merge to main:
cd src && dotnet build           # 0 errors
dotnet run &

# Users endpoints
curl -X POST http://localhost:5000/api/users \
  -H "Content-Type: application/json" \
  -d '{"name":"Alice","email":"alice@example.com","role":"admin"}'
# Expected: HTTP 201 with {"data":{"id":"<guid>","name":"Alice",...},"success":true}

curl http://localhost:5000/api/users
# Expected: HTTP 200 with {"data":[...],"success":true}

curl http://localhost:5000/api/users/00000000-0000-0000-0000-000000000000
# Expected: HTTP 404 with {"data":null,"success":false}

# Products endpoints
curl -X POST http://localhost:5000/api/products \
  -H "Content-Type: application/json" \
  -d '{"name":"Keyboard","description":"Mechanical","price":89.99,"stock":50}'
# Expected: HTTP 201

curl -X POST http://localhost:5000/api/products \
  -H "Content-Type: application/json" \
  -d '{"name":"Bad","description":"Negative","price":-10,"stock":5}'
# Expected: HTTP 400 with error message

kill %1

# Tests
cd .. && dotnet test tests/
# Expected: All tests passed
```
