# Phase 3: Merge + Conflict Resolution - Research

**Researched:** 2026-03-05
**Domain:** F# Giraffe Orders module + Git merge conflict pedagogy (Core.fs intentional conflict scenario)
**Confidence:** HIGH

## Summary

Phase 3 has two parallel tracks. Track A builds the Orders module (Domain.fs + Handlers.fs + Expecto tests) and adds `OrderStatus` to Core.fs — following the same structural pattern as Users/Products from Phase 2. Track B simulates a concurrent change to Core.fs (adding `PaginatedResponse`) from a separate worktree, deliberately creating a merge conflict that the tutorial reader resolves step-by-step. The tutorial chapter `03-merge-conflicts.md` already exists (full content) and is the authoritative spec for both Orders module code and the conflict scenario.

The critical technical finding: **the tutorial chapter already contains complete, ready-to-implement code** for `src/Orders/Domain.fs`, `src/Orders/Handlers.fs`, Core.fs changes, and `.fsproj` / `Program.fs` updates. The implementation tracks should treat this chapter as the spec — copy code verbatim. The same JSON output verification problem from Phase 2 applies: the tutorial's curl output examples (Step 6) may not match actual serialization behavior and must be verified after implementation.

The second critical finding: **Orders module uses `PATCH` for status updates** (not `PUT`), which is the correct HTTP semantic for partial resource updates. This is already in the tutorial's `Handlers.fs` code. The success criteria requirement for 200/201/204/400/404 does not cover PATCH specifically — updateStatus returns 200 on success and 400 on invalid status. The `delete` handler returns 204 on success. This is consistent with Users/Products patterns.

**Primary recommendation:** Implement Orders via three worktree-based tasks mirroring Phase 2 structure: (03-01) Orders worktree with Core.fs OrderStatus + Orders module + fsproj/Program.fs updates; (03-02) Pagination worktree with Core.fs PaginatedResponse only; (03-03) Main merge — trigger conflict, resolve it, add OrdersTests to test project, verify tutorial chapter JSON output.

## Standard Stack

The established libraries/tools for this domain:

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Giraffe | 8.2.0 | HTTP framework | Already in project; `PATCH >=> routef "/%O"` for status update endpoint |
| FSharp.SystemTextJson (via FsharpFriendlySerializer) | bundled with Giraffe 8.x | DU JSON serialization | `OrderStatus` is a DU; `UserId`/`ProductId`/`OrderId` are single-case DUs; both must serialize correctly |
| Expecto | 10.2.3 | F# test framework | Already in tests project; add `OrdersTests.fs` to existing project |
| YoloDev.Expecto.TestSdk | 0.15.5 | dotnet test bridge | Already in tests project; no new package needed |
| Microsoft.NET.Test.Sdk | 17.12.0 | Test runner infrastructure | Already in tests project |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| System.Collections.Concurrent | built-in .NET | Thread-safe in-memory store | `ConcurrentDictionary<Guid, Order>` — same pattern as Users/Products |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| PATCH for status update | PUT for full order update | PATCH is semantically correct for partial update; tutorial uses PATCH; requirements list 200/201/204/400/404 which are all covered by PATCH returning 200/400 |
| In-memory Orders store per module | Shared store with Users/Products | In-memory per module is the locked decision; eliminates shared DB state conflicts across worktrees |

**Installation:** No new packages needed — all packages already exist in `tests/WorktreeApi.Tests.fsproj`. New `.fs` files only.

## Architecture Patterns

### Recommended Project Structure

After Phase 3 is complete:

```
src/
├── Core.fs              # MODIFIED: add OrderStatus + PaginatedResponse types
├── Users/               # unchanged
│   ├── Domain.fs
│   └── Handlers.fs
├── Products/            # unchanged
│   ├── Domain.fs
│   └── Handlers.fs
├── Orders/              # NEW (built in Orders worktree)
│   ├── Domain.fs
│   └── Handlers.fs
├── WorktreeApi.fsproj   # MODIFIED: Orders module entries uncommented
└── Program.fs           # MODIFIED: Orders route added
tests/
├── WorktreeApi.Tests.fsproj  # MODIFIED: OrdersTests.fs added
├── UsersTests.fs             # unchanged
├── ProductsTests.fs          # unchanged
├── OrdersTests.fs            # NEW
└── TestMain.fs               # MODIFIED: OrdersTests.ordersTests added
tutorial/
└── 03-merge-conflicts.md     # VERIFY: JSON output examples must match actual API
```

### Pattern 1: Orders Domain Module

**What:** `src/Orders/Domain.fs` follows the exact same structural pattern as Users/Products: namespace, in-memory ConcurrentDictionary store, pure domain functions returning `Result<_, string>` or `option`.

**Key difference from Users/Products:** Orders reference BOTH `UserId` and `ProductId` from Core.fs. The `Order` type contains an `OrderItem list` with `ProductId`, and the order-level `UserId`. These are stored as IDs only (ORDR-02 — no value copying across modules). Orders does NOT import Users.Domain or Products.Domain — it only depends on `WorktreeApi.Core`.

**Example (from `tutorial/03-merge-conflicts.md`):**

```fsharp
// src/Orders/Domain.fs
namespace WorktreeApi.Orders

open System
open System.Collections.Concurrent
open WorktreeApi.Core

module Domain =

    type OrderItem =
        { ProductId: ProductId
          Quantity: int
          UnitPrice: decimal }

    type Order =
        { Id: OrderId
          UserId: UserId
          Items: OrderItem list
          Status: OrderStatus
          TotalAmount: decimal
          CreatedAt: DateTime }

    type CreateOrderItemRequest =
        { ProductId: string
          Quantity: int
          UnitPrice: decimal }

    type CreateOrderRequest =
        { UserId: string
          Items: CreateOrderItemRequest list }

    type UpdateOrderStatusRequest = { Status: string }

    // === In-Memory Store ===
    let private store = ConcurrentDictionary<Guid, Order>()

    let parseStatus =
        function
        | "pending" | "Pending" -> Some Pending
        | "confirmed" | "Confirmed" -> Some Confirmed
        | "shipped" | "Shipped" -> Some Shipped
        | "delivered" | "Delivered" -> Some Delivered
        | "cancelled" | "Cancelled" -> Some Cancelled
        | _ -> None

    let create (req: CreateOrderRequest) =
        match Guid.TryParse(req.UserId) with
        | false, _ -> Error "Invalid user ID"
        | true, userGuid ->
            let items =
                req.Items
                |> List.choose (fun item ->
                    match Guid.TryParse(item.ProductId) with
                    | true, prodGuid ->
                        Some
                            { ProductId = ProductId prodGuid
                              Quantity = item.Quantity
                              UnitPrice = item.UnitPrice }
                    | false, _ -> None)

            if items.IsEmpty then
                Error "No valid items"
            else
                let id = Guid.NewGuid()
                let total = items |> List.sumBy (fun i -> i.UnitPrice * decimal i.Quantity)

                let order =
                    { Id = OrderId id
                      UserId = UserId userGuid
                      Items = items
                      Status = Pending
                      TotalAmount = total
                      CreatedAt = DateTime.UtcNow }

                store.[id] <- order
                Ok order

    let getAll () = store.Values |> Seq.toList

    let getById (id: Guid) =
        match store.TryGetValue(id) with
        | true, order -> Some order
        | false, _ -> None

    let updateStatus (id: Guid) (req: UpdateOrderStatusRequest) =
        match store.TryGetValue(id) with
        | false, _ -> Error "Order not found"
        | true, order ->
            match parseStatus req.Status with
            | None -> Error "Invalid status. Use: pending, confirmed, shipped, delivered, cancelled"
            | Some status ->
                let updated = { order with Status = status }
                store.[id] <- updated
                Ok updated

    let delete (id: Guid) = store.TryRemove(id) |> fst
```

**Key design decisions:**
- `CreateOrderRequest.UserId` and `CreateOrderItemRequest.ProductId` are `string` (not typed IDs) — parsed with `Guid.TryParse` in the `create` function. This is the correct cross-module boundary approach (ORDR-02).
- Items with invalid `ProductId` GUIDs are silently filtered via `List.choose`. If all items are filtered, returns `Error "No valid items"`.
- `TotalAmount` is computed at creation time from `UnitPrice * Quantity` sum.
- Status update uses `PATCH` semantics — only updates the `Status` field.

### Pattern 2: Orders Handlers Module

**What:** `src/Orders/Handlers.fs` uses PATCH (not PUT) for the status update endpoint. This is the only difference from Users/Products handler patterns.

**Example (from `tutorial/03-merge-conflicts.md`):**

```fsharp
// src/Orders/Handlers.fs
namespace WorktreeApi.Orders

open System
open Microsoft.AspNetCore.Http
open Giraffe
open WorktreeApi.Core

module Handlers =

    let getAll: HttpHandler =
        fun next ctx ->
            let orders = Domain.getAll ()
            json (ApiResponse.success orders) next ctx

    let getById (id: Guid) : HttpHandler =
        fun next ctx ->
            match Domain.getById id with
            | Some order -> json (ApiResponse.success order) next ctx
            | None ->
                ctx.SetStatusCode 404
                json (ApiResponse.error "Order not found") next ctx

    let create: HttpHandler =
        fun next ctx ->
            task {
                let! req = ctx.BindJsonAsync<Domain.CreateOrderRequest>()

                match Domain.create req with
                | Ok order ->
                    ctx.SetStatusCode 201
                    return! json (ApiResponse.success order) next ctx
                | Error msg ->
                    ctx.SetStatusCode 400
                    return! json (ApiResponse.error msg) next ctx
            }

    let updateStatus (id: Guid) : HttpHandler =
        fun next ctx ->
            task {
                let! req = ctx.BindJsonAsync<Domain.UpdateOrderStatusRequest>()

                match Domain.updateStatus id req with
                | Ok order -> return! json (ApiResponse.success order) next ctx
                | Error msg ->
                    ctx.SetStatusCode 400
                    return! json (ApiResponse.error msg) next ctx
            }

    let delete (id: Guid) : HttpHandler =
        fun next ctx ->
            if Domain.delete id then
                ctx.SetStatusCode 204
                next ctx
            else
                ctx.SetStatusCode 404
                json (ApiResponse.error "Order not found") next ctx

    let routes: HttpHandler =
        subRoute
            "/api/orders"
            (choose
                [ GET >=> choose [ routef "/%O" getById; route "" >=> getAll ]
                  POST >=> route "" >=> create
                  PATCH >=> routef "/%O" updateStatus
                  DELETE >=> routef "/%O" delete ])
```

### Pattern 3: Core.fs — OrderStatus Type Addition (Conflict Source)

**What:** The Orders worktree adds `OrderStatus` DU to `Core.fs` between the ID types and `ApiResponse`. The Pagination worktree adds `PaginatedResponse<'T>` to `Core.fs` after `ApiResponse`. Both touch Core.fs at adjacent locations, causing a content conflict.

**Orders worktree — modified Core.fs section:**

```fsharp
module Core =

    // === Shared ID Types ===
    type UserId = UserId of Guid
    type ProductId = ProductId of Guid
    type OrderId = OrderId of Guid

    // === Order Status ===       ← ADDED BY ORDERS WORKTREE
    type OrderStatus =
        | Pending
        | Confirmed
        | Shipped
        | Delivered
        | Cancelled

    // === API Response Wrapper ===
    type ApiResponse<'T> = ...
```

**Pagination worktree — modified Core.fs section (adds PaginatedResponse after ApiResponse):**

```fsharp
    // === API Response Wrapper ===
    type ApiResponse<'T> = ...

    // === Paginated Response ===  ← ADDED BY PAGINATION WORKTREE
    type PaginatedResponse<'T> =
        { Data: 'T list
          Page: int
          PageSize: int
          TotalCount: int
          TotalPages: int }

    module PaginatedResponse =
        let create (items: 'T list) (page: int) (pageSize: int) (totalCount: int) =
            { Data = items
              Page = page
              PageSize = pageSize
              TotalCount = totalCount
              TotalPages = (totalCount + pageSize - 1) / pageSize }
```

**Why a conflict occurs:** Both worktrees start from the same base `Core.fs`. Orders inserts 8 lines between ID types and ApiResponse. Pagination inserts lines after ApiResponse. When Orders is merged first (fast-forward), the base changes. Git's 3-way merge then sees Pagination branching off the OLD base (without OrderStatus). The conflict region is the area around `ApiResponse` which appears in both diffs at overlapping line positions.

### Pattern 4: Conflict Resolution — Keep Both Changes

**What:** The correct resolution of the Core.fs conflict is to include BOTH `OrderStatus` (from Orders/HEAD) AND `PaginatedResponse` (from feature/pagination). Neither change should be discarded.

**Resolved Core.fs (from `tutorial/03-merge-conflicts.md`):**

```fsharp
namespace WorktreeApi

open System

module Core =

    // === Shared ID Types ===
    type UserId = UserId of Guid
    type ProductId = ProductId of Guid
    type OrderId = OrderId of Guid

    // === Order Status ===
    type OrderStatus =
        | Pending
        | Confirmed
        | Shipped
        | Delivered
        | Cancelled

    // === API Response Wrapper ===
    type ApiResponse<'T> =
        { Data: 'T option
          Message: string
          Success: bool }

    // === Paginated Response ===
    type PaginatedResponse<'T> =
        { Data: 'T list
          Page: int
          PageSize: int
          TotalCount: int
          TotalPages: int }

    module ApiResponse =
        let success data = { Data = Some data; Message = "OK"; Success = true }
        let error msg = { Data = None; Message = msg; Success = false }
        let noContent () = { Data = None; Message = "No Content"; Success = true }

    module PaginatedResponse =
        let create (items: 'T list) (page: int) (pageSize: int) (totalCount: int) =
            { Data = items
              Page = page
              PageSize = pageSize
              TotalCount = totalCount
              TotalPages = (totalCount + pageSize - 1) / pageSize }
```

**Resolution principle:** Remove all conflict markers (`<<<<<<<`, `=======`, `>>>>>>>`). Keep OrderStatus (from HEAD/main after Orders merge) AND PaginatedResponse (from feature/pagination). F# requires type definitions to precede their usage — OrderStatus goes before ApiResponse, PaginatedResponse goes after. Both module helpers follow their respective types.

### Pattern 5: .fsproj Orders Module Zone

**What:** The `.fsproj` already has a `<!-- Orders module -->` zone comment placeholder (verified in `src/WorktreeApi.fsproj`). The Orders worktree fills it in.

**Current .fsproj (existing placeholder):**
```xml
    <!-- Orders module -->
    <!-- (empty — Orders worktree fills this in) -->
```

**After Orders worktree:**
```xml
    <!-- Orders module -->
    <Compile Include="Orders/Domain.fs" />
    <Compile Include="Orders/Handlers.fs" />
```

**No conflict with Pagination worktree:** The Pagination worktree does not modify `.fsproj` (PaginatedResponse lives in Core.fs, not in a separate module file). So `.fsproj` merge is clean.

### Pattern 6: Program.fs Orders Route Addition

**What:** The Orders worktree adds `WorktreeApi.Orders.Handlers.routes` to `webApp`. The Pagination worktree does NOT modify `Program.fs`. So no `Program.fs` conflict occurs in this scenario.

**Program.fs webApp after Orders merge:**
```fsharp
let webApp: HttpHandler =
    choose
        [ GET >=> route "/health" >=> healthCheck

          // === DOMAIN ROUTES ===
          WorktreeApi.Users.Handlers.routes
          WorktreeApi.Products.Handlers.routes
          WorktreeApi.Orders.Handlers.routes    // ← added by Orders worktree

          RequestErrors.NOT_FOUND "Not Found" ]
```

### Pattern 7: OrdersTests.fs — Expecto Test Pattern

**What:** `tests/OrdersTests.fs` follows the same pattern as `UsersTests.fs` and `ProductsTests.fs`. Tests duplicate domain functions rather than project-referencing `src/` (locked decision: [02-01]).

**Testable domain logic for Orders:**
- `parseStatus`: string → `OrderStatus option` (same pattern as `parseRole` in Users)
- Order validation logic: empty items list → Error
- TotalAmount calculation: sum of `UnitPrice * Quantity`

**Example OrdersTests.fs:**

```fsharp
module OrdersTests

open Expecto

let parseStatus =
    function
    | "pending" | "Pending" -> Some "Pending"
    | "confirmed" | "Confirmed" -> Some "Confirmed"
    | "shipped" | "Shipped" -> Some "Shipped"
    | "delivered" | "Delivered" -> Some "Delivered"
    | "cancelled" | "Cancelled" -> Some "Cancelled"
    | _ -> None

let calculateTotal (items: (decimal * int) list) =
    items |> List.sumBy (fun (price, qty) -> price * decimal qty)

[<Tests>]
let ordersDomainTests =
    testList "Orders.Domain" [
        testList "parseStatus" [
            test "parses lowercase pending" {
                Expect.isSome (parseStatus "pending") "pending should parse to Some"
            }
            test "parses capitalized Confirmed" {
                Expect.isSome (parseStatus "Confirmed") "Confirmed should parse to Some"
            }
            test "parses shipped" {
                Expect.isSome (parseStatus "shipped") "shipped should parse to Some"
            }
            test "parses delivered" {
                Expect.isSome (parseStatus "delivered") "delivered should parse to Some"
            }
            test "parses cancelled" {
                Expect.isSome (parseStatus "cancelled") "cancelled should parse to Some"
            }
            test "rejects unknown status" {
                Expect.isNone (parseStatus "processing") "unknown status should return None"
            }
            test "rejects empty string" {
                Expect.isNone (parseStatus "") "empty string should return None"
            }
        ]
        testList "total calculation" [
            test "calculates single item total" {
                Expect.equal (calculateTotal [(10m, 2)]) 20m "10 * 2 = 20"
            }
            test "calculates multi-item total" {
                Expect.equal (calculateTotal [(10m, 1); (5m, 3)]) 25m "10 + 15 = 25"
            }
            test "handles zero quantity" {
                Expect.equal (calculateTotal [(99m, 0)]) 0m "zero qty = zero total"
            }
        ]
    ]
```

**TestMain.fs update** — add `OrdersTests.ordersDomainTests` to the combined list:

```fsharp
module TestMain

open Expecto

[<EntryPoint>]
let main args =
    let all =
        testList "All" [
            UsersTests.userDomainTests
            ProductsTests.productDomainTests
            OrdersTests.ordersDomainTests   // ← add this
        ]
    runTestsWithCLIArgs [] args all
```

**WorktreeApi.Tests.fsproj** — add `OrdersTests.fs` before `TestMain.fs`:

```xml
<ItemGroup>
  <Compile Include="UsersTests.fs" />
  <Compile Include="ProductsTests.fs" />
  <Compile Include="OrdersTests.fs" />    <!-- ← add this -->
  <Compile Include="TestMain.fs" />
</ItemGroup>
```

### Anti-Patterns to Avoid

- **Orders depending on Users.Domain or Products.Domain:** The `Order` type stores `UserId` and `ProductId` (core types) — it does NOT store `User` or `Product` records from other modules. Cross-module imports break independent compilability.
- **Storing UserId as a raw Guid in the ConcurrentDictionary key:** Same as Users/Products — use `ConcurrentDictionary<Guid, Order>` with raw `Guid` key. `OrderId` DU is not a valid dictionary key without custom equality.
- **Resolving the Core.fs conflict by discarding one side:** The tutorial teaches "keep both changes" — discarding OrderStatus breaks the Orders module; discarding PaginatedResponse means the pagination scenario taught nothing.
- **Adding PaginatedResponse to a separate .fs file:** The scenario deliberately adds it to `Core.fs` to create the conflict. Don't put it in a new `Pagination.fs` — that would miss the conflict entirely.
- **Running `dotnet build` after conflict before resolving all markers:** F# compiler fails immediately on `<<<<<<<` syntax. Always resolve ALL conflict markers before running dotnet build.
- **Forgetting to add OrdersTests.fs to .fsproj compile order BEFORE TestMain.fs:** F# compile order is strict. TestMain.fs references `OrdersTests.ordersDomainTests`, so OrdersTests.fs must be compiled first.

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| OrderStatus string parsing | Custom switch with exhaustive match from scratch | Duplicate the `parseStatus` pattern from tutorial chapter | Pattern already designed — inconsistent parsing leads to tutorial inconsistencies |
| Cross-module ID validation | Lookup Users store to verify UserId exists | Accept any valid Guid format as UserId | ORDR-02 explicitly says "ID only, not values"; validation of ID existence would require cross-module coupling |
| PaginatedResponse helper | Manual page math inline | `PaginatedResponse.create` helper module | Already designed in tutorial; page calculation `(total + size - 1) / size` is off-by-one prone |
| Conflict reproduction instructions | Describe the conflict abstractly | Show exact `git status` / file content with conflict markers | Tutorial readers need exact markers to see and resolve; abstract description doesn't teach the skill |

**Key insight:** The Orders module is the third iteration of the same Domain.fs + Handlers.fs pattern. Don't invent new patterns — clone the structure from Products and adapt field names + validation logic.

## Common Pitfalls

### Pitfall 1: OrderStatus Not in Core.fs Before Orders Module Compiles

**What goes wrong:** `src/Orders/Domain.fs` references `OrderStatus` from `WorktreeApi.Core`. If the Orders worktree forgot to add `OrderStatus` to `Core.fs`, the build fails with `error FS0039: The value or constructor 'OrderStatus' is not defined`.

**Why it happens:** Developer adds Orders/Domain.fs and Handlers.fs but forgets Core.fs needs to be updated first (or in the same commit).

**How to avoid:** In the Orders worktree, modify Core.fs to add `OrderStatus` BEFORE creating Orders/Domain.fs. Verify `dotnet build` succeeds with just the Core.fs change, then add the Orders module files.

**Warning signs:** `error FS0039: The value or constructor 'Pending' is not defined` — Pending/Confirmed/etc. are not in scope because OrderStatus isn't in Core.fs.

### Pitfall 2: Conflict Scenario Produces No Conflict

**What goes wrong:** After merging Orders (fast-forward) and then trying to merge Pagination, git merges cleanly with no conflict. The tutorial scenario fails to produce the expected conflict.

**Why it happens:** If the two Core.fs changes are far enough apart in line position, git's 3-way merge resolves them automatically. The conflict only occurs if they overlap in the diff context window (typically 3 lines of context).

**How to avoid:** Follow the tutorial's exact placement — OrderStatus goes BETWEEN the ID types and ApiResponse; PaginatedResponse goes AFTER ApiResponse. This creates overlapping diff regions because both branches touch the area around the `// === API Response Wrapper ===` comment.

**Warning signs:** `git merge feature/pagination` reports "Merge made by the 'ort' strategy" with no CONFLICT message. If this happens, the conflict scenario is broken and the tutorial chapter needs to be redesigned.

### Pitfall 3: Tutorial Step 6 JSON Examples Are Wrong

**What goes wrong:** The tutorial `03-merge-conflicts.md` Step 6 shows `{"data":{"id":{"case":"UserId","fields":["USER-GUID-HERE"]},...}}` as the JSON output for user creation. Actual output with `FsharpFriendlySerializer(JsonFSharpOptions.Default())` wraps single-case DUs differently.

**Why it happens:** Same issue as Chapter 02 (documented in Phase 2 research). `UserId of Guid` serializes as plain `"<guid-string>"`, not `{"case":"UserId","fields":[...]}`. `OrderStatus` serializes as `{"Case":"Pending"}` (fieldless DU with Case field).

**How to avoid:** After running the server in Step 6, capture actual curl output and update the tutorial's code blocks to match. Do not trust the existing Step 6 output examples.

**Warning signs:** The existing Step 6 shows placeholder `USER-GUID-HERE` and `PRODUCT-GUID-HERE` — these are tokens to be replaced, confirming the examples need verification.

### Pitfall 4: Program.fs Conflict When Pagination Worktree Also Modifies Program.fs

**What goes wrong:** If the Pagination worktree accidentally adds a line to `Program.fs` (e.g., registering a middleware), this creates a second conflict in `Program.fs` in addition to the Core.fs conflict.

**Why it happens:** The tutorial scenario ONLY has the Pagination worktree touch `Core.fs`. If Program.fs is modified too, the conflict scenario expands beyond what the tutorial documents.

**How to avoid:** The Pagination worktree makes exactly ONE change: add `PaginatedResponse<'T>` type and `PaginatedResponse` module to `Core.fs`. No `.fsproj` changes. No `Program.fs` changes. Commit only `src/Core.fs`.

**Warning signs:** `git status` during merge shows CONFLICT in both `src/Core.fs` AND `src/Program.fs` simultaneously.

### Pitfall 5: Incorrect F# Compile Order in Resolved Core.fs

**What goes wrong:** After resolving the conflict, `PaginatedResponse<'T>` is placed BEFORE `ApiResponse<'T>` in Core.fs. Build succeeds, but ordering is illogical and inconsistent with tutorial documentation.

**Why it happens:** When manually merging conflict markers, order of code blocks depends on which side of `=======` you place first.

**How to avoid:** The correct order is: ID types → OrderStatus → ApiResponse → PaginatedResponse → ApiResponse module → PaginatedResponse module. This order matches the tutorial's "resolved" Core.fs in Step 5.

**Warning signs:** `dotnet build` succeeds (both orderings compile), but the tutorial's Step 5 "해결 후" code block doesn't match what's in the file.

### Pitfall 6: Delete Handler Returns Wrong Status for Missing Order

**What goes wrong:** Orders `delete` handler returns 404 with a JSON body when the order is not found. But `delete` returns a boolean — there's no way to distinguish "not found" from "concurrent delete" in `TryRemove`.

**Why it happens:** `ConcurrentDictionary.TryRemove` returns `(bool * value)`. If `fst` is false, the item wasn't there. Returning 404 is correct behavior.

**How to avoid:** Use the same pattern as Users/Products delete handlers — `if Domain.delete id then 204 next ctx else 404 + json error`. This is exactly what the tutorial specifies.

**Warning signs:** DELETE on non-existent order returns 200 instead of 404, or returns 404 without JSON body.

## Code Examples

Verified patterns from existing codebase and tutorial chapter:

### Core.fs OrderStatus Addition (Orders Worktree Change)

```fsharp
// Source: tutorial/03-merge-conflicts.md Step 2
// Place between ID types and ApiResponse:

    // === Order Status ===
    type OrderStatus =
        | Pending
        | Confirmed
        | Shipped
        | Delivered
        | Cancelled
```

### Core.fs PaginatedResponse Addition (Pagination Worktree Change)

```fsharp
// Source: tutorial/03-merge-conflicts.md Step 3
// Place after ApiResponse type:

    // === Paginated Response ===
    type PaginatedResponse<'T> =
        { Data: 'T list
          Page: int
          PageSize: int
          TotalCount: int
          TotalPages: int }

    module PaginatedResponse =
        let create (items: 'T list) (page: int) (pageSize: int) (totalCount: int) =
            { Data = items
              Page = page
              PageSize = pageSize
              TotalCount = totalCount
              TotalPages = (totalCount + pageSize - 1) / pageSize }
```

### Expected Conflict Markers in Core.fs

```fsharp
// Source: tutorial/03-merge-conflicts.md Step 5 — what git produces
<<<<<<< HEAD
    // === Order Status ===
    type OrderStatus =
        | Pending
        | Confirmed
        | Shipped
        | Delivered
        | Cancelled

    // === API Response Wrapper ===
=======
    // === API Response Wrapper ===
>>>>>>> feature/pagination
    type ApiResponse<'T> =
        ...

<<<<<<< HEAD
    module ApiResponse =
=======
    // === Paginated Response ===
    type PaginatedResponse<'T> = ...

    module ApiResponse =
    ...

    module PaginatedResponse =
    ...
>>>>>>> feature/pagination
```

### Worktree Commands for Conflict Scenario

```bash
# From main branch (after Phase 2 merge is complete):

# Create Orders worktree
git worktree add ../worktree-tutorial-orders -b feature/orders
# (develop Orders module, commit)

# Create Pagination worktree
git worktree add ../worktree-tutorial-pagination -b feature/pagination
# (add PaginatedResponse to Core.fs, commit)

# Back on main — merge sequence:
git merge feature/orders          # Fast-forward (clean)
git merge feature/pagination      # CONFLICT in src/Core.fs

# Resolve conflict in src/Core.fs (keep both)
# Then:
git add src/Core.fs
git commit -m "merge: resolve Core.fs conflict — keep both OrderStatus and PaginatedResponse"

# Verify:
cd src && dotnet build            # must succeed
dotnet test ../tests/             # must pass
```

### Orders curl Test Commands

```bash
# Create a user first (get UUID from response)
curl -X POST http://localhost:5000/api/users \
  -H "Content-Type: application/json" \
  -d '{"name":"Alice","email":"alice@example.com","role":"admin"}'

# Create a product (get UUID from response)
curl -X POST http://localhost:5000/api/products \
  -H "Content-Type: application/json" \
  -d '{"name":"Laptop","description":"MacBook Pro","price":2499.99,"stock":10}'

# Create an order (substitute actual GUIDs)
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{"userId":"<USER-UUID>","items":[{"productId":"<PRODUCT-UUID>","quantity":1,"unitPrice":2499.99}]}'
# Expected: HTTP 201

# Update order status
curl -X PATCH http://localhost:5000/api/orders/<ORDER-UUID> \
  -H "Content-Type: application/json" \
  -d '{"status":"confirmed"}'
# Expected: HTTP 200

# Invalid status
curl -X PATCH http://localhost:5000/api/orders/<ORDER-UUID> \
  -H "Content-Type: application/json" \
  -d '{"status":"INVALID"}'
# Expected: HTTP 400

# Get all orders
curl http://localhost:5000/api/orders
# Expected: HTTP 200 with array

# Delete order
curl -X DELETE http://localhost:5000/api/orders/<ORDER-UUID>
# Expected: HTTP 204

# Delete non-existent order
curl -X DELETE http://localhost:5000/api/orders/00000000-0000-0000-0000-000000000000
# Expected: HTTP 404
```

### OrderStatus Serialization — Expected JSON

```
OrderStatus.Pending → {"Case":"Pending"}
OrderStatus.Confirmed → {"Case":"Confirmed"}
```

This is because `OrderStatus` is a fieldless discriminated union. With `JsonFSharpOptions.Default()`, fieldless DUs serialize as `{"Case":"<CaseName>"}`. This is different from single-case DUs like `OrderId of Guid` which serialize as plain `"<guid-string>"`.

**If tutorial shows `{"case":"Pending"}` (lowercase):** Incorrect. Actual output has uppercase `Case` field name (it's the F# field name, not camelCased by default).

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Manual conflict resolution by editing raw file | git conflict markers + resolve + `dotnet build` verification | Standard git workflow | F# compiler immediately catches resolution errors — fast feedback loop |
| Zone comments as optional style | Zone comments as mandatory worktree architecture pattern | Phase 1 design | `.fsproj` zone comments prevent `.fsproj` conflicts; Core.fs has no zones (intentional — creates the conflict) |
| All types in one mega-Core.fs | Only shared cross-module types in Core.fs | Phase 1 architecture | Module-specific types (like OrderItem) stay in module's Domain.fs; only types used by 2+ modules go in Core.fs |

**Deprecated/outdated:**
- `JsonFSharpOptions.Default().ToJsonSerializerOptions()` as constructor arg: Wrong — current `Program.fs` correctly uses `FsharpFriendlySerializer(JsonFSharpOptions.Default(), jsonOpts)` where `jsonOpts = JsonSerializerOptions(PropertyNameCaseInsensitive = true)`. This is already correct in the existing codebase.

## Open Questions

1. **Whether the conflict scenario actually triggers under the exact tutorial setup**
   - What we know: The conflict depends on git's line-level diff algorithm. Orders adds between lines 7-9, Pagination adds after lines 16-18 of Core.fs. The context window overlap is close.
   - What's unclear: Whether git's `ort` merge strategy (default since git 2.34) uses the same 3-line context window as the older `recursive` strategy for this specific case
   - Recommendation: After implementing both worktrees, verify that `git merge feature/pagination` actually produces a CONFLICT. If it auto-merges cleanly, the placement of `OrderStatus` needs to be moved closer to `ApiResponse` in Core.fs to force the conflict.

2. **Tutorial JSON output for OrderId in Order response**
   - What we know: `OrderId of Guid` is a single-case DU, which `FsharpFriendlySerializer.Default()` serializes as plain `"<guid-string>"`. Similarly for `UserId` and `ProductId` in the Order.
   - What's unclear: The existing tutorial Step 6 uses placeholder `ORDER-GUID-HERE` tokens instead of actual JSON — it doesn't show the actual response body format
   - Recommendation: After implementation, run the curl sequence in Step 6 with actual server, capture real JSON output, and update tutorial with verified examples. Expect Order response to show `"id":"<guid>"` not `{"case":"OrderId","fields":[...]}`.

3. **Whether to update the tutorial's conflict marker display for accuracy**
   - What we know: Tutorial Step 5 shows expected conflict markers, but the exact markers depend on git's diff — line numbers and context may differ
   - What's unclear: Whether the displayed conflict markers exactly match what git will produce
   - Recommendation: After triggering the conflict, compare actual conflict markers in Core.fs with what the tutorial shows. Update tutorial if they don't match. The tutorial's pedagogical value depends on showing EXACTLY what the reader will see.

## Sources

### Primary (HIGH confidence)
- `tutorial/03-merge-conflicts.md` — authoritative spec for Orders/Domain.fs, Orders/Handlers.fs, Core.fs changes, conflict scenario flow, and conflict resolution. All major code examples sourced from this file.
- `src/Core.fs` — verified current state (OrderId defined but OrderStatus NOT yet added)
- `src/WorktreeApi.fsproj` — verified `<!-- Orders module -->` zone comment placeholder exists
- `src/Program.fs` — verified current state (Users + Products routes, no Orders route)
- `tests/WorktreeApi.Tests.fsproj` — verified current test project structure (Expecto 10.2.3, net10.0, OutputType=Exe)
- `tests/TestMain.fs` — verified combined runner pattern (UsersTests + ProductsTests, needs OrdersTests added)
- `.planning/phases/02-parallel-modules/02-RESEARCH.md` — prior research confirming serialization behavior, FsharpFriendlySerializer configuration, and test project structure

### Secondary (MEDIUM confidence)
- `tutorial/02-parallel-development.md` — confirms established Domain.fs + Handlers.fs patterns that Orders follows exactly
- `.planning/phases/02-parallel-modules/02-03-PLAN.md` — confirms JSON output verification approach (run curl, update tutorial)

### Tertiary (LOW confidence)
- Git ort strategy merge context window behavior — not verified against specific git version; conflict guarantee depends on implementation detail

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all packages already exist in project; Orders follows verified patterns from Phase 2
- Architecture: HIGH — tutorial chapter specifies complete implementation; conflict scenario structure is documented in detail
- Pitfalls: HIGH — JSON serialization pitfalls confirmed from Phase 2 experience; compile order is F# fundamental; conflict scenario trigger depends on git diff algorithm (only LOW confidence item)

**Research date:** 2026-03-05
**Valid until:** 2026-06-05 (90 days — stable stack; same technology as Phase 2)

---

## Phase 3 Implementation Summary for Planner

### What Already Exists (do not recreate)

| Asset | Status | Action |
|-------|--------|--------|
| `tutorial/03-merge-conflicts.md` | EXISTS — full content | Verify JSON output examples only |
| `src/Core.fs` | EXISTS — has OrderId but NOT OrderStatus | Add OrderStatus in Orders worktree |
| `src/WorktreeApi.fsproj` | EXISTS — has `<!-- Orders module -->` placeholder | Fill in Orders entries in Orders worktree |
| `src/Program.fs` | EXISTS — has Users/Products routes | Add Orders route in Orders worktree |
| `tests/WorktreeApi.Tests.fsproj` | EXISTS — Expecto configured | Add OrdersTests.fs entry |
| `tests/TestMain.fs` | EXISTS — has Users + Products | Add OrdersTests.ordersDomainTests |

### What Needs to Be Built

| Asset | Where | Action |
|-------|-------|--------|
| `src/Orders/Domain.fs` | Orders worktree | Create from tutorial spec (Step 2) |
| `src/Orders/Handlers.fs` | Orders worktree | Create from tutorial spec (Step 2) |
| OrderStatus in Core.fs | Orders worktree | Add to Core.fs (Step 2) |
| PaginatedResponse in Core.fs | Pagination worktree | Add to Core.fs (Step 3) |
| `tests/OrdersTests.fs` | main (post-merge) | Create with parseStatus + calculation tests |

### Recommended Task Breakdown

```
Task 03-01 (Orders worktree — feature/orders branch):
  1. git worktree add ../worktree-tutorial-orders -b feature/orders
  2. Edit src/Core.fs — add OrderStatus between ID types and ApiResponse
  3. Create src/Orders/Domain.fs (from tutorial Step 2 spec)
  4. Create src/Orders/Handlers.fs (from tutorial Step 2 spec)
  5. Update src/WorktreeApi.fsproj — fill Orders zone with Domain.fs + Handlers.fs
  6. Update src/Program.fs — add WorktreeApi.Orders.Handlers.routes
  7. dotnet build — must succeed
  8. git commit -m "feat: add Orders module with CRUD endpoints and OrderStatus type"

Task 03-02 (Pagination worktree — feature/pagination branch):
  1. git worktree add ../worktree-tutorial-pagination -b feature/pagination
  2. Edit src/Core.fs — add PaginatedResponse after ApiResponse
  3. dotnet build — must succeed
  4. git commit -m "feat: add PaginatedResponse type for future pagination support"
  (NOTE: Pagination worktree runs CONCURRENTLY with Orders worktree — same as Phase 2 parallel pattern)

Task 03-03 (main branch — conflict trigger, resolve, tests, tutorial verification):
  1. git merge feature/orders              # fast-forward
  2. git merge feature/pagination          # CONFLICT in src/Core.fs
  3. Resolve Core.fs conflict — keep BOTH OrderStatus AND PaginatedResponse
  4. dotnet build — must succeed after resolution
  5. git add src/Core.fs
  6. git commit -m "merge: resolve Core.fs conflict — keep both OrderStatus and PaginatedResponse"
  7. Create tests/OrdersTests.fs (parseStatus + total calculation tests)
  8. Update tests/WorktreeApi.Tests.fsproj — add OrdersTests.fs before TestMain.fs
  9. Update tests/TestMain.fs — add OrdersTests.ordersDomainTests to combined list
  10. dotnet test tests/ — must pass (all 3 module tests)
  11. Run curl sequence against live server — capture actual JSON output
  12. Update tutorial/03-merge-conflicts.md Step 6 with verified JSON output
  13. Verify conflict marker display in Step 5 matches actual git output
  14. git worktree remove + git branch -d (cleanup)
  15. git commit final state
```

### Success Verification Commands

```bash
# After all merges and resolution:
cd src && dotnet build   # 0 errors, 0 warnings

# Start server
dotnet run &

# 3-module API verification
curl http://localhost:5000/api/users     # 200
curl http://localhost:5000/api/products  # 200
curl http://localhost:5000/api/orders    # 200

# Orders CRUD
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{"userId":"<valid-uuid>","items":[{"productId":"<valid-uuid>","quantity":2,"unitPrice":49.99}]}'
# Expected: 201

kill %1

# Tests
cd .. && dotnet test tests/
# Expected: All tests passed (Users + Products + Orders)
```
