# Chapter 03: Merge + Conflict Resolution (Scenario 2)

이 챕터에서는 **의도적으로 merge conflict를 발생**시키고 해결합니다.
또한 Orders 모듈을 추가하여 3-모듈 REST API를 완성합니다.

## 왜 충돌이 발생하는가?

Chapter 02에서는 Users와 Products가 서로 다른 파일만 수정했기 때문에 충돌 없이 merge되었습니다. 하지만 실제 개발에서는 **공유 파일을 여러 브랜치에서 동시에 수정**하는 일이 흔합니다.

이 챕터에서 만들 충돌:

```
[main] ──── Core.fs (ApiResponse 타입)
   │
   ├── [feature/orders]
   │     Core.fs에 OrderStatus 타입 추가
   │     Program.fs에 Orders route 추가
   │
   ├── [feature/pagination]
   │     Core.fs에 PaginatedResponse 타입 추가
   │     Program.fs는 수정 없음
   │
   ▼
merge 시도 → Core.fs에서 충돌 발생!
```

---

## Step 1: 시나리오 설정

두 개의 worktree를 만듭니다. 하나는 Orders 모듈, 다른 하나는 pagination 지원 타입을 추가합니다.

**Terminal 1** (main):

```bash
# 현재 main에 Users + Products가 합쳐져 있는 상태
$ git log --oneline
>>> xyz9876 Merge branch 'feature/products'
>>> def5678 feat: add Users module with CRUD endpoints
>>> 9ab0123 feat: add Products module with CRUD endpoints
>>> abc1234 feat: initialize F# Giraffe project

# Worktree 생성
$ git worktree add ../worktree-tutorial-orders -b feature/orders
>>> Preparing worktree (new branch 'feature/orders')

$ git worktree add ../worktree-tutorial-pagination -b feature/pagination
>>> Preparing worktree (new branch 'feature/pagination')

$ git worktree list
>>> /path/to/worktree-tutorial             xyz9876 [main]
>>> /path/to/worktree-tutorial-orders      xyz9876 [feature/orders]
>>> /path/to/worktree-tutorial-pagination  xyz9876 [feature/pagination]
```

---

## Step 2: Orders 모듈 개발 (Terminal 2)

**Terminal 2** — `../worktree-tutorial-orders`에서 작업합니다.

### Core.fs에 OrderStatus 추가

`src/Core.fs`를 열고 `module Core =` 안에 OrderStatus 타입을 추가합니다:

```fsharp
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

    // ... (나머지 동일)
```

> **주의**: `OrderStatus`를 ID 타입과 Response 타입 **사이에** 추가합니다. 이 위치가 나중에 충돌의 원인이 됩니다.

### `src/Orders/Domain.fs`

```fsharp
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

### `src/Orders/Handlers.fs`

```fsharp
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
                [ GET
                  >=> choose [ routef "/%O" getById; route "" >=> getAll ]
                  POST >=> route "" >=> create
                  PATCH >=> routef "/%O" updateStatus
                  DELETE >=> routef "/%O" delete ])
```

### `.fsproj` 수정 (Orders worktree에서)

```xml
    <!-- Orders module -->
    <Compile Include="Orders/Domain.fs" />
    <Compile Include="Orders/Handlers.fs" />
```

### `Program.fs` 수정 (Orders worktree에서)

```fsharp
          // === DOMAIN ROUTES ===
          WorktreeApi.Users.Handlers.routes
          WorktreeApi.Products.Handlers.routes
          WorktreeApi.Orders.Handlers.routes    // ← 추가
```

### 빌드 및 커밋

```bash
$ cd src && dotnet build
>>> Build succeeded.

$ cd ..
$ git add -A
$ git commit -m "feat: add Orders module with CRUD endpoints and OrderStatus type"
>>> [feature/orders aaa1111] feat: add Orders module with CRUD endpoints and OrderStatus type
```

---

## Step 3: Pagination 타입 추가 (Terminal 3)

**Terminal 3** — `../worktree-tutorial-pagination`에서 **동시에** 작업합니다.

### Core.fs에 PaginatedResponse 추가

`src/Core.fs`를 열고 `ApiResponse` 타입 **바로 아래에** 새 타입을 추가합니다:

```fsharp
module Core =

    // === Shared ID Types ===
    type UserId = UserId of Guid
    type ProductId = ProductId of Guid
    type OrderId = OrderId of Guid

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
        // ... (동일)

    module PaginatedResponse =
        let create (items: 'T list) (page: int) (pageSize: int) (totalCount: int) =
            { Data = items
              Page = page
              PageSize = pageSize
              TotalCount = totalCount
              TotalPages = (totalCount + pageSize - 1) / pageSize }
```

### 커밋

```bash
$ git add -A
$ git commit -m "feat: add PaginatedResponse type for future pagination support"
>>> [feature/pagination bbb2222] feat: add PaginatedResponse type
```

---

## Step 4: Merge — 충돌 발생!

**Terminal 1** (main)에서 두 브랜치를 merge합니다.

```bash
# Orders 먼저 merge (clean)
$ git merge feature/orders
>>> Updating xyz9876..aaa1111
>>> Fast-forward
>>>  src/Core.fs              |  8 ++++
>>>  src/Orders/Domain.fs     | xxx +++
>>>  src/Orders/Handlers.fs   | xxx +++
>>>  src/WorktreeApi.fsproj   |  2 ++
>>>  src/Program.fs           |  1 +

# Pagination merge 시도
$ git merge feature/pagination
>>> Auto-merging src/Core.fs
>>> CONFLICT (content): Merge conflict in src/Core.fs
>>> Automatic merge failed; fix conflicts and then commit the result.
```

**충돌 발생!** 두 브랜치 모두 `Core.fs`에서 ID 타입과 ApiResponse 사이에 새로운 타입을 추가했기 때문입니다.

## Step 5: 충돌 해결

### 충돌 상태 확인

```bash
$ git status
>>> On branch main
>>> You have unmerged paths.
>>>
>>> Unmerged paths:
>>>   (use "git add <file>..." to mark resolution)
>>>       both modified:   src/Core.fs
>>>
>>> no changes added to commit (use "git add" and/or "git commit")
```

### 충돌 파일 확인

`src/Core.fs`를 열면 충돌 마커가 보입니다:

```fsharp
module Core =

    // === Shared ID Types ===
    type UserId = UserId of Guid
    type ProductId = ProductId of Guid
    type OrderId = OrderId of Guid

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
        { Data: 'T option
          Message: string
          Success: bool }

<<<<<<< HEAD
    module ApiResponse =
=======
    // === Paginated Response ===
    type PaginatedResponse<'T> =
        { Data: 'T list
          Page: int
          PageSize: int
          TotalCount: int
          TotalPages: int }

    module ApiResponse =
        // ...

    module PaginatedResponse =
        // ...
>>>>>>> feature/pagination
```

### 충돌 마커 이해하기

```
<<<<<<< HEAD          ← 현재 브랜치 (main, Orders가 이미 merge됨)의 내용
    (main의 코드)
=======               ← 구분선
    (feature/pagination의 코드)
>>>>>>> feature/pagination  ← merge하려는 브랜치의 내용
```

### 해결: 양쪽 모두 유지

두 변경 모두 필요합니다. 충돌 마커를 제거하고 양쪽 코드를 모두 포함합니다:

**`src/Core.fs` (해결 후):**

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
        let success data =
            { Data = Some data
              Message = "OK"
              Success = true }

        let error msg =
            { Data = None
              Message = msg
              Success = false }

        let noContent () =
            { Data = None
              Message = "No Content"
              Success = true }

    module PaginatedResponse =
        let create (items: 'T list) (page: int) (pageSize: int) (totalCount: int) =
            { Data = items
              Page = page
              PageSize = pageSize
              TotalCount = totalCount
              TotalPages = (totalCount + pageSize - 1) / pageSize }
```

핵심 원칙:
1. **충돌 마커 (`<<<<<<<`, `=======`, `>>>>>>>`)를 모두 제거**
2. **양쪽 변경사항을 모두 포함** (OrderStatus도, PaginatedResponse도 필요)
3. **F# 컴파일 순서 확인** — 타입 정의가 사용되는 곳보다 위에 있어야 함

### 빌드로 검증

```bash
$ cd src && dotnet build
>>> Build succeeded.
>>>     0 Warning(s)
>>>     0 Error(s)
```

빌드가 성공하면 충돌이 올바르게 해결된 것입니다.

### 충돌 해결 커밋

```bash
$ cd ..
$ git add src/Core.fs
$ git commit -m "merge: resolve Core.fs conflict — keep both OrderStatus and PaginatedResponse"
>>> [main ccc3333] merge: resolve Core.fs conflict
```

---

## Step 6: 최종 확인

```bash
$ cd src && dotnet run &

# Users API
$ curl http://localhost:5000/api/users
>>> {"data":[],"message":"OK","success":true}

# Products API
$ curl http://localhost:5000/api/products
>>> {"data":[],"message":"OK","success":true}

# Orders API — User와 Product 생성 후 주문
$ curl -X POST http://localhost:5000/api/users \
  -H "Content-Type: application/json" \
  -d '{"name":"Alice","email":"alice@example.com","role":"admin"}'
>>> {"data":{"id":{"case":"UserId","fields":["USER-GUID-HERE"]},...},...}

$ curl -X POST http://localhost:5000/api/products \
  -H "Content-Type: application/json" \
  -d '{"name":"Laptop","description":"MacBook Pro","price":2499.99,"stock":10}'
>>> {"data":{"id":{"case":"ProductId","fields":["PRODUCT-GUID-HERE"]},...},...}

# Order 생성 (위에서 받은 GUID 사용)
$ curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{"userId":"USER-GUID-HERE","items":[{"productId":"PRODUCT-GUID-HERE","quantity":1,"unitPrice":2499.99}]}'
>>> {"data":{"id":...,"status":{"case":"Pending"},"totalAmount":2499.99,...},...}

# Order 상태 변경
$ curl -X PATCH http://localhost:5000/api/orders/ORDER-GUID-HERE \
  -H "Content-Type: application/json" \
  -d '{"status":"confirmed"}'
>>> {"data":{...,"status":{"case":"Confirmed"},...},...}

$ kill %1
```

3개 모듈이 모두 동작합니다!

## Step 7: Worktree 정리

```bash
$ git worktree remove ../worktree-tutorial-orders
$ git worktree remove ../worktree-tutorial-pagination
$ git branch -d feature/orders
$ git branch -d feature/pagination
```

---

## 충돌 해결 체크리스트

실제 프로젝트에서 worktree merge 충돌을 만났을 때:

1. **`git status`로 충돌 파일 확인**
2. **각 충돌 파일을 열어서 마커 확인**
3. **양쪽 변경의 의도 파악** — 뭘 추가/수정했는지
4. **마커 제거 + 양쪽 반영** (또는 한쪽 선택)
5. **`dotnet build`로 컴파일 확인** — F#은 컴파일 순서가 엄격
6. **`git add` + `git commit`**

> **Tip: Claude Code로 충돌 해결하기**
>
> 충돌이 발생한 후 Claude Code에게 도움을 요청할 수 있습니다:
> ```
> "src/Core.fs에서 merge conflict가 발생했어. 양쪽 변경사항을 모두 유지하면서 해결해줘."
> ```
> Claude는 충돌 마커를 인식하고 적절히 해결합니다.

---

## 연습 문제

### Challenge 1: Program.fs 충돌

만약 Orders worktree와 Pagination worktree가 **둘 다** `Program.fs`의 같은 줄을 수정했다면 어떤 충돌이 발생할까요? 직접 시나리오를 만들어보세요.

<details>
<summary>힌트</summary>

```
# 두 worktree에서 각각:
# Orders: webApp에 Orders route 추가
# Pagination: webApp에 middleware 추가

# 같은 위치를 수정하면 충돌 발생
# 해결: 양쪽 코드를 적절한 순서로 배치
```

</details>

### Challenge 2: .fsproj 충돌

만약 zone 주석 없이 `.fsproj`에 파일을 추가했다면 어떤 충돌이 발생하나요?

<details>
<summary>정답</summary>

`.fsproj`에서 두 브랜치가 같은 위치에 `<Compile Include="..." />`를 추가하면:

```xml
<<<<<<< HEAD
    <Compile Include="Orders/Domain.fs" />
    <Compile Include="Orders/Handlers.fs" />
=======
    <Compile Include="Pagination/Helpers.fs" />
>>>>>>> feature/pagination
```

해결할 때 **F# 컴파일 순서를 반드시 고려**해야 합니다.
Orders가 Pagination의 타입을 사용한다면 Pagination이 먼저 와야 합니다.

**이것이 zone 주석이 중요한 이유입니다** — 처음부터 서로 다른 zone에 추가하면 충돌 자체가 발생하지 않습니다.

</details>

---

## 다음 챕터

충돌 해결을 마스터했습니다. 다음 챕터에서는 더 실전적인 시나리오 — feature 개발 중에 **긴급 버그 수정**이 필요한 상황을 다룹니다.

[Chapter 04: Hotfix Parallel →](./04-hotfix-parallel.md)
