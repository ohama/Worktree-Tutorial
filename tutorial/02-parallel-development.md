# Chapter 02: Parallel Development (Scenario 1)

이 챕터에서는 Users 모듈과 Products 모듈을 **2개의 worktree에서 동시에** 개발하고, main으로 merge합니다.

## 전체 흐름

```
[main] ─── Core.fs, Program.fs (foundation)
   │
   ├──── [feature/users]    ──── Users 모듈 개발 ──── commit ──┐
   │     (Terminal 2)                                           │
   │                                                            ├── merge to main
   ├──── [feature/products] ──── Products 모듈 개발 ── commit ──┘
   │     (Terminal 3)
   │
   ▼
[main] ─── 두 모듈이 합쳐진 완성된 API
```

---

## Step 1: Worktree 생성

**Terminal 1** (main — 프로젝트 루트에서):

```bash
# 현재 위치 확인
$ pwd
>>> /path/to/worktree-tutorial

# Users worktree 생성
$ git worktree add ../worktree-tutorial-users -b feature/users
>>> Preparing worktree (new branch 'feature/users')
>>> HEAD is now at abc1234 feat: initialize F# Giraffe project

# Products worktree 생성
$ git worktree add ../worktree-tutorial-products -b feature/products
>>> Preparing worktree (new branch 'feature/products')
>>> HEAD is now at abc1234 feat: initialize F# Giraffe project

# 3개의 worktree 확인
$ git worktree list
>>> /path/to/worktree-tutorial           abc1234 [main]
>>> /path/to/worktree-tutorial-users     abc1234 [feature/users]
>>> /path/to/worktree-tutorial-products  abc1234 [feature/products]
```

디렉토리 구조가 이렇게 됩니다:

```
parent-directory/
├── worktree-tutorial/           ← main (Terminal 1)
├── worktree-tutorial-users/     ← feature/users (Terminal 2)
└── worktree-tutorial-products/  ← feature/products (Terminal 3)
```

## Step 2: 각 worktree에서 Claude Code 시작

**3개의 터미널**을 엽니다. iTerm2의 split pane, tmux, 또는 별도 터미널 창을 사용하세요.

```
┌─────────────────────────────────────────────────────────┐
│ Terminal 1 (main)        │ Terminal 2 (users)            │
│ /worktree-tutorial       │ /worktree-tutorial-users      │
│                          │                               │
│ $ # 대기 (merge 담당)     │ $ cd ../worktree-tutorial-users│
│                          │ $ claude                      │
├──────────────────────────┼───────────────────────────────┤
│ Terminal 3 (products)    │                               │
│ /worktree-tutorial-prods │                               │
│                          │                               │
│ $ cd ../worktree-tutorial│                               │
│   -products              │                               │
│ $ claude                 │                               │
└──────────────────────────┴───────────────────────────────┘
```

**Terminal 2** (Users worktree):

```bash
$ cd ../worktree-tutorial-users
$ claude
```

**Terminal 3** (Products worktree):

```bash
$ cd ../worktree-tutorial-products
$ claude
```

> **Tip: 세션 이름 지정**
>
> Claude Code에서 `/rename` 명령으로 세션에 이름을 붙이면 구분하기 쉽습니다:
> - Terminal 2에서: `/rename users-module`
> - Terminal 3에서: `/rename products-module`

이제 **두 개의 Claude Code 세션이 동시에** 실행되고 있습니다. 각각 독립된 worktree에서 작업합니다.

---

## Step 3: Users 모듈 개발

**Terminal 2** (feature/users worktree)에서 Claude Code에게 다음과 같이 요청합니다:

> "Users 모듈을 만들어줘. src/Users/ 디렉토리에 Domain.fs와 Handlers.fs를 작성하고, .fsproj에 추가해줘."

또는 직접 파일을 작성합니다:

### `src/Users/Domain.fs`

```fsharp
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
                req.Role
                |> Option.bind parseRole
                |> Option.defaultValue user.Role

            let updated =
                { user with
                    Name = req.Name |> Option.defaultValue user.Name
                    Email = req.Email |> Option.defaultValue user.Email
                    Role = role }

            store.[id] <- updated
            Ok updated

    let delete (id: Guid) = store.TryRemove(id) |> fst
```

### `src/Users/Handlers.fs`

```fsharp
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
                [ GET
                  >=> choose [ routef "/%O" getById; route "" >=> getAll ]
                  POST >=> route "" >=> create
                  PUT >=> routef "/%O" update
                  DELETE >=> routef "/%O" delete ])
```

### `.fsproj` 수정 (Users worktree에서)

`src/WorktreeApi.fsproj`의 `<!-- Users module -->` 주석을 다음으로 교체:

```xml
    <!-- Users module -->
    <Compile Include="Users/Domain.fs" />
    <Compile Include="Users/Handlers.fs" />
```

### `Program.fs` 수정 (Users worktree에서)

`// === DOMAIN ROUTES ===` 주석 아래에 추가:

```fsharp
          // === DOMAIN ROUTES ===
          WorktreeApi.Users.Handlers.routes
```

### Users 빌드 및 테스트

```bash
$ cd src
$ dotnet build
>>> Build succeeded.

# 서버 실행
$ dotnet run &

# User 생성
$ curl -X POST http://localhost:5000/api/users \
  -H "Content-Type: application/json" \
  -d '{"name":"Alice","email":"alice@example.com","role":"admin"}'
>>> {"Data":{"Id":"a1b2c3d4-e5f6-7890-abcd-ef1234567890","Name":"Alice","Email":"alice@example.com","Role":{"Case":"Admin"},"CreatedAt":"2026-03-04T22:48:06.459146Z"},"Message":"OK","Success":true}

# 전체 조회
$ curl http://localhost:5000/api/users
>>> {"Data":[{"Id":"a1b2c3d4-e5f6-7890-abcd-ef1234567890","Name":"Alice","Email":"alice@example.com","Role":{"Case":"Admin"},"CreatedAt":"2026-03-04T22:48:06.459146Z"}],"Message":"OK","Success":true}

$ kill %1
```

### Users 커밋

```bash
$ git add -A
$ git commit -m "feat: add Users module with CRUD endpoints"
>>> [feature/users def5678] feat: add Users module with CRUD endpoints
>>>  4 files changed, xxx insertions(+)
```

---

## Step 4: Products 모듈 개발 (동시에!)

**Terminal 3** (feature/products worktree)에서 **동시에** Products 모듈을 개발합니다.

> Claude Code에게: "Products 모듈을 만들어줘. src/Products/ 디렉토리에 Domain.fs와 Handlers.fs를 작성하고, .fsproj에 추가해줘."

### `src/Products/Domain.fs`

```fsharp
namespace WorktreeApi.Products

open System
open System.Collections.Concurrent
open WorktreeApi.Core

module Domain =

    type Product =
        { Id: ProductId
          Name: string
          Description: string
          Price: decimal
          Stock: int
          CreatedAt: DateTime }

    type CreateProductRequest =
        { Name: string
          Description: string
          Price: decimal
          Stock: int }

    type UpdateProductRequest =
        { Name: string option
          Description: string option
          Price: decimal option
          Stock: int option }

    // === In-Memory Store ===
    let private store = ConcurrentDictionary<Guid, Product>()

    let create (req: CreateProductRequest) =
        if req.Price < 0m then
            Error "Price must be non-negative"
        elif req.Stock < 0 then
            Error "Stock must be non-negative"
        else
            let id = Guid.NewGuid()

            let product =
                { Id = ProductId id
                  Name = req.Name
                  Description = req.Description
                  Price = req.Price
                  Stock = req.Stock
                  CreatedAt = DateTime.UtcNow }

            store.[id] <- product
            Ok product

    let getAll () = store.Values |> Seq.toList

    let getById (id: Guid) =
        match store.TryGetValue(id) with
        | true, product -> Some product
        | false, _ -> None

    let update (id: Guid) (req: UpdateProductRequest) =
        match store.TryGetValue(id) with
        | false, _ -> Error "Product not found"
        | true, product ->
            let updated =
                { product with
                    Name = req.Name |> Option.defaultValue product.Name
                    Description = req.Description |> Option.defaultValue product.Description
                    Price = req.Price |> Option.defaultValue product.Price
                    Stock = req.Stock |> Option.defaultValue product.Stock }

            store.[id] <- updated
            Ok updated

    let delete (id: Guid) = store.TryRemove(id) |> fst
```

### `src/Products/Handlers.fs`

```fsharp
namespace WorktreeApi.Products

open System
open Microsoft.AspNetCore.Http
open Giraffe
open WorktreeApi.Core

module Handlers =

    let getAll: HttpHandler =
        fun next ctx ->
            let products = Domain.getAll ()
            json (ApiResponse.success products) next ctx

    let getById (id: Guid) : HttpHandler =
        fun next ctx ->
            match Domain.getById id with
            | Some product -> json (ApiResponse.success product) next ctx
            | None ->
                ctx.SetStatusCode 404
                json (ApiResponse.error "Product not found") next ctx

    let create: HttpHandler =
        fun next ctx ->
            task {
                let! req = ctx.BindJsonAsync<Domain.CreateProductRequest>()

                match Domain.create req with
                | Ok product ->
                    ctx.SetStatusCode 201
                    return! json (ApiResponse.success product) next ctx
                | Error msg ->
                    ctx.SetStatusCode 400
                    return! json (ApiResponse.error msg) next ctx
            }

    let update (id: Guid) : HttpHandler =
        fun next ctx ->
            task {
                let! req = ctx.BindJsonAsync<Domain.UpdateProductRequest>()

                match Domain.update id req with
                | Ok product -> return! json (ApiResponse.success product) next ctx
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
                json (ApiResponse.error "Product not found") next ctx

    let routes: HttpHandler =
        subRoute
            "/api/products"
            (choose
                [ GET
                  >=> choose [ routef "/%O" getById; route "" >=> getAll ]
                  POST >=> route "" >=> create
                  PUT >=> routef "/%O" update
                  DELETE >=> routef "/%O" delete ])
```

### `.fsproj` 수정 (Products worktree에서)

`src/WorktreeApi.fsproj`의 `<!-- Products module -->` 주석을 다음으로 교체:

```xml
    <!-- Products module -->
    <Compile Include="Products/Domain.fs" />
    <Compile Include="Products/Handlers.fs" />
```

### `Program.fs` 수정 (Products worktree에서)

`// === DOMAIN ROUTES ===` 주석 아래에 추가:

```fsharp
          // === DOMAIN ROUTES ===
          WorktreeApi.Products.Handlers.routes
```

### Products 빌드 및 테스트

```bash
$ cd src
$ dotnet build
>>> Build succeeded.

$ dotnet run &

# Product 생성
$ curl -X POST http://localhost:5000/api/products \
  -H "Content-Type: application/json" \
  -d '{"name":"Keyboard","description":"Mechanical","price":89.99,"stock":50}'
>>> {"Data":{"Id":"b2c3d4e5-f6a7-8901-bcde-f12345678901","Name":"Keyboard","Description":"Mechanical","Price":89.99,"Stock":50,"CreatedAt":"2026-03-04T22:48:06.508125Z"},"Message":"OK","Success":true}

# 유효성 검사 테스트
$ curl -X POST http://localhost:5000/api/products \
  -H "Content-Type: application/json" \
  -d '{"name":"Bad","description":"Negative","price":-10,"stock":5}'
>>> {"Data":null,"Message":"Price must be non-negative","Success":false}

$ kill %1
```

### Products 커밋

```bash
$ git add -A
$ git commit -m "feat: add Products module with CRUD endpoints"
>>> [feature/products 9ab0123] feat: add Products module with CRUD endpoints
>>>  4 files changed, xxx insertions(+)
```

---

## Step 5: Main으로 Merge

두 worktree에서 모두 개발이 완료되었습니다. **Terminal 1** (main)에서 merge합니다.

```bash
# Terminal 1 — main worktree에서

# 현재 브랜치 확인
$ git branch
>>> * main
>>>   feature/products
>>>   feature/users

# Users merge
$ git merge feature/users
>>> Updating abc1234..def5678
>>> Fast-forward
>>>  src/Users/Domain.fs      | xxx +++
>>>  src/Users/Handlers.fs    | xxx +++
>>>  src/WorktreeApi.fsproj   |   2 ++
>>>  src/Program.fs           |   1 +
>>>  4 files changed, xxx insertions(+)
```

Users merge는 clean (fast-forward)입니다. main에 다른 변경이 없었으니까요.

```bash
# Products merge
$ git merge feature/products
>>> Auto-merging src/Program.fs
>>> Auto-merging src/WorktreeApi.fsproj
>>> Merge made by the 'ort' strategy.
>>>  src/Products/Domain.fs    | xxx +++
>>>  src/Products/Handlers.fs  | xxx +++
>>>  src/WorktreeApi.fsproj    |   2 ++
>>>  src/Program.fs            |   1 +
>>>  4 files changed, xxx insertions(+)
```

> **왜 Products merge에서 충돌이 발생하지 않았나?**
>
> `.fsproj`에서 Users와 Products는 **서로 다른 zone 주석 아래**에 파일을 추가했습니다.
> `Program.fs`에서도 둘 다 `// === DOMAIN ROUTES ===` 주석 아래에 추가했지만,
> git의 3-way merge가 서로 다른 줄에 추가된 것을 인식해서 자동으로 merge합니다.
>
> 만약 zone 주석 없이 같은 위치에 추가했다면 충돌이 발생했을 것입니다.
> **이것이 `.fsproj`에 zone 주석을 넣는 이유입니다.**

### 최종 빌드 확인

```bash
$ cd src
$ dotnet build
>>> Build succeeded.

$ dotnet run &

# Users API 확인
$ curl -X POST http://localhost:5000/api/users \
  -H "Content-Type: application/json" \
  -d '{"name":"Bob","email":"bob@example.com","role":"member"}'
>>> {"Data":{"Id":"c3d4e5f6-a7b8-9012-cdef-012345678902","Name":"Bob","Email":"bob@example.com","Role":{"Case":"Member"},"CreatedAt":"2026-03-04T22:50:00.000000Z"},"Message":"OK","Success":true}

# Products API 확인
$ curl -X POST http://localhost:5000/api/products \
  -H "Content-Type: application/json" \
  -d '{"name":"Mouse","description":"Wireless","price":29.99,"stock":100}'
>>> {"Data":{"Id":"d4e5f6a7-b8c9-0123-defa-123456789003","Name":"Mouse","Description":"Wireless","Price":29.99,"Stock":100,"CreatedAt":"2026-03-04T22:50:00.100000Z"},"Message":"OK","Success":true}

# Health check도 여전히 동작
$ curl http://localhost:5000/health
>>> {"status":"healthy","timestamp":"2026-03-04T22:50:00.200000Z"}

$ kill %1
```

두 모듈이 하나의 API에서 함께 동작합니다!

## Step 6: Worktree 정리

merge가 완료되었으므로 worktree를 정리합니다.

```bash
# worktree 제거
$ git worktree remove ../worktree-tutorial-users
>>> Removing worktree '/path/to/worktree-tutorial-users'

$ git worktree remove ../worktree-tutorial-products
>>> Removing worktree '/path/to/worktree-tutorial-products'

# 브랜치 정리 (선택사항 — 이미 merge했으므로 안전)
$ git branch -d feature/users
>>> Deleted branch feature/users (was def5678).

$ git branch -d feature/products
>>> Deleted branch feature/products (was 9ab0123).

# 확인
$ git worktree list
>>> /path/to/worktree-tutorial  xyz9876 [main]
```

## 효율성 비교

### 순차 개발 (traditional)

```
Users:    [████████████████] 20분
Products: [████████████████] 20분
Merge:    [██]               2분
                              ─────
                         총 42분
```

### 병렬 개발 (worktree)

```
Users:    [████████████████] 20분
Products: [████████████████] 20분  ← 동시 실행!
Merge:    [██]               2분
                              ─────
                         총 22분 (48% 절감)
```

모듈이 3개, 4개로 늘어나면 절감 효과는 더 커집니다.

> **주의: Token 비용**
>
> Claude Code 세션을 병렬로 실행하면 token 사용량도 비례해서 증가합니다.
> 2개의 세션 = 약 2배의 token. 효율성 향상과 비용 증가를 함께 고려하세요.
> 독립적인 모듈이 3개 이상일 때 병렬 개발의 가치가 가장 큽니다.

---

## 연습 문제

### Challenge 1: 포트 충돌

두 worktree에서 동시에 `dotnet run`을 실행하면 어떻게 되나요?

<details>
<summary>정답</summary>

포트 충돌이 발생합니다! 둘 다 기본 포트 5000을 사용하려 하기 때문입니다.

해결 방법 — 각 worktree에서 다른 포트를 지정:

```bash
# Users worktree
$ ASPNETCORE_URLS=http://localhost:5001 dotnet run

# Products worktree
$ ASPNETCORE_URLS=http://localhost:5002 dotnet run
```

</details>

### Challenge 2: worktree에서 다른 worktree의 commit 보기

Users worktree에서 commit한 후, Products worktree에서 `git log --all`을 실행하면 Users의 commit이 보이나요?

<details>
<summary>정답</summary>

**보입니다.** 모든 worktree는 같은 `.git` repository를 공유하므로, 한 worktree에서 만든 commit은 다른 worktree에서 `git log --all --oneline`로 확인할 수 있습니다.

```bash
# Products worktree에서
$ git log --all --oneline
>>> def5678 (feature/users) feat: add Users module with CRUD endpoints
>>> abc1234 (HEAD -> feature/products, main) feat: initialize F# Giraffe project
```

</details>

### Challenge 3: claude --worktree 시도

이번 챕터에서는 수동으로 worktree를 생성했습니다. 같은 작업을 `claude --worktree`로 해보세요.

<details>
<summary>힌트</summary>

```bash
# 프로젝트 루트에서
$ claude --worktree feature-test

# Claude Code가 자동으로:
# 1. .claude/worktrees/feature-test/ 에 worktree 생성
# 2. 새 브랜치에서 세션 시작
#
# 세션 종료 시 worktree 유지/삭제 선택 가능
```

수동 worktree와의 차이점:
- 위치: `.claude/worktrees/` 내부 (repo 내부지만 Claude가 관리)
- 브랜치명: 자동 생성
- lifecycle: 세션 종료 시 자동 정리 옵션

</details>

---

## 다음 챕터

Clean merge는 성공했습니다. 하지만 실제 개발에서는 **충돌**이 자주 발생합니다.
다음 챕터에서는 **의도적으로 충돌을 만들고**, 해결하는 과정을 연습합니다. Orders 모듈도 추가합니다.

[Chapter 03: Merge Conflicts →](./03-merge-conflicts.md)
