# Chapter 05: CI/CD Integration (Scenario 4)

이 챕터에서는 worktree 기반 병렬 개발 워크플로우를 **GitHub Actions CI/CD**와 통합합니다. 각 도메인 모듈을 **matrix strategy로 병렬 빌드**하여 CI에서도 병렬성의 이점을 활용합니다.

## 목표

- 모듈별 독립 빌드/테스트를 CI에서 병렬 실행
- PR마다 변경된 모듈만 빌드 (선택적 실행)
- worktree branch push 시 자동 CI 트리거

---

## Step 1: 프로젝트 구조 확인

현재 `src/` 디렉토리 구조:

```
src/
├── Core.fs
├── Users/
│   ├── Domain.fs
│   └── Handlers.fs
├── Products/
│   ├── Domain.fs
│   └── Handlers.fs
├── Orders/
│   ├── Domain.fs
│   └── Handlers.fs
├── Program.fs
└── WorktreeApi.fsproj
```

모든 모듈이 하나의 `.fsproj`에 있으므로, 빌드는 프로젝트 단위로 수행됩니다. 하지만 **테스트**는 모듈별로 분리할 수 있습니다.

## Step 2: 테스트 프로젝트 설정

먼저 Expecto 테스트 프로젝트를 만듭니다.

```bash
# 테스트 프로젝트 생성
$ dotnet new console -o tests --name WorktreeApi.Tests -lang F#

# Expecto 패키지 추가
$ cd tests
$ dotnet add package Expecto --version 10.2.3

# 메인 프로젝트 참조 추가
$ dotnet add reference ../src/WorktreeApi.fsproj

$ cd ..
```

### `tests/WorktreeApi.Tests.fsproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <!-- === TEST FILES === -->
    <Compile Include="UsersTests.fs" />
    <Compile Include="ProductsTests.fs" />
    <Compile Include="OrdersTests.fs" />
    <Compile Include="Program.fs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Expecto" Version="10.2.3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../src/WorktreeApi.fsproj" />
  </ItemGroup>

</Project>
```

### `tests/UsersTests.fs`

```fsharp
module WorktreeApi.Tests.Users

open Expecto
open WorktreeApi.Users.Domain

let tests =
    testList
        "Users"
        [ testCase "create user with valid role"
          <| fun _ ->
              let req =
                  { Name = "Alice"
                    Email = "alice@example.com"
                    Role = "admin" }

              let result = create req
              Expect.isOk result "Should create user"

          testCase "create user with invalid role"
          <| fun _ ->
              let req =
                  { Name = "Bob"
                    Email = "bob@example.com"
                    Role = "superuser" }

              let result = create req
              Expect.isError result "Should reject invalid role"

          testCase "get all users"
          <| fun _ ->
              let users = getAll ()
              Expect.isNonEmpty users "Should have at least one user"

          testCase "delete non-existent user"
          <| fun _ ->
              let id = System.Guid.NewGuid()
              let result = delete id
              Expect.isFalse result "Should return false for non-existent user" ]
```

### `tests/ProductsTests.fs`

```fsharp
module WorktreeApi.Tests.Products

open Expecto
open WorktreeApi.Products.Domain

let tests =
    testList
        "Products"
        [ testCase "create product with valid data"
          <| fun _ ->
              let req =
                  { Name = "Keyboard"
                    Description = "Mechanical"
                    Price = 89.99m
                    Stock = 50 }

              let result = create req
              Expect.isOk result "Should create product"

          testCase "reject negative price"
          <| fun _ ->
              let req =
                  { Name = "Bad"
                    Description = "Negative"
                    Price = -10m
                    Stock = 5 }

              let result = create req
              Expect.isError result "Should reject negative price"

          testCase "reject negative stock"
          <| fun _ ->
              let req =
                  { Name = "Bad"
                    Description = "Negative stock"
                    Price = 10m
                    Stock = -1 }

              let result = create req
              Expect.isError result "Should reject negative stock" ]
```

### `tests/OrdersTests.fs`

```fsharp
module WorktreeApi.Tests.Orders

open Expecto
open WorktreeApi.Orders.Domain

let tests =
    testList
        "Orders"
        [ testCase "create order with valid items"
          <| fun _ ->
              let userId = System.Guid.NewGuid().ToString()
              let productId = System.Guid.NewGuid().ToString()

              let req =
                  { UserId = userId
                    Items =
                      [ { ProductId = productId
                          Quantity = 2
                          UnitPrice = 29.99m } ] }

              let result = create req
              Expect.isOk result "Should create order"

          testCase "reject order with invalid user ID"
          <| fun _ ->
              let req =
                  { UserId = "not-a-guid"
                    Items =
                      [ { ProductId = System.Guid.NewGuid().ToString()
                          Quantity = 1
                          UnitPrice = 10m } ] }

              let result = create req
              Expect.isError result "Should reject invalid user ID"

          testCase "parse valid order status"
          <| fun _ ->
              Expect.isSome (parseStatus "confirmed") "Should parse 'confirmed'"
              Expect.isSome (parseStatus "Shipped") "Should parse 'Shipped'"
              Expect.isNone (parseStatus "invalid") "Should reject invalid status" ]
```

### `tests/Program.fs`

```fsharp
module WorktreeApi.Tests.Program

open Expecto

[<EntryPoint>]
let main args =
    testList
        "All"
        [ WorktreeApi.Tests.Users.tests
          WorktreeApi.Tests.Products.tests
          WorktreeApi.Tests.Orders.tests ]
    |> runTestsWithCLIArgs [] args
```

### 테스트 실행 확인

```bash
$ cd tests && dotnet run
>>> [13:00:00 INF] EXPECTO? Running tests...
>>> [13:00:00 INF] EXPECTO! 10 tests run in 00:00:00.xxx
>>>               10 passed, 0 ignored, 0 failed, 0 errored.
```

---

## Step 3: GitHub Actions Workflow

### `.github/workflows/ci.yml`

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  # ─────────────────────────────────────────────
  # Stage 1: Build (전체 프로젝트)
  # ─────────────────────────────────────────────
  build:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 9.0
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 9.0.x

      - name: Restore dependencies
        run: dotnet restore src/WorktreeApi.fsproj

      - name: Build
        run: dotnet build src/WorktreeApi.fsproj --no-restore --configuration Release

  # ─────────────────────────────────────────────
  # Stage 2: Test (모듈별 병렬)
  # ─────────────────────────────────────────────
  test:
    runs-on: ubuntu-latest
    needs: build   # build 성공 후 실행

    strategy:
      matrix:
        module: [Users, Products, Orders]
      fail-fast: false   # 하나가 실패해도 나머지 계속 실행

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 9.0
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 9.0.x

      - name: Restore
        run: dotnet restore tests/WorktreeApi.Tests.fsproj

      - name: Run ${{ matrix.module }} tests
        run: |
          dotnet run --project tests/WorktreeApi.Tests.fsproj -- \
            --filter "${{ matrix.module }}"

  # ─────────────────────────────────────────────
  # Stage 3: Format check
  # ─────────────────────────────────────────────
  format:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 9.0
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 9.0.x

      - name: Restore tools
        run: dotnet tool restore

      - name: Check formatting
        run: dotnet fantomas --check src/ tests/
```

### Workflow 구조 설명

```
                    push / PR
                       │
                       ▼
              ┌──── [build] ────┐
              │   전체 빌드 확인  │
              └────────┬────────┘
                       │ needs: build
          ┌────────────┼────────────┐
          ▼            ▼            ▼
    [test/Users] [test/Products] [test/Orders]    ← matrix 병렬 실행!
          │            │            │
          └────────────┼────────────┘
                       │
              ┌────────▼────────┐
              │    [format]     │                  ← build와 병렬 실행
              │  코드 포맷 검사  │
              └─────────────────┘
```

**핵심**: `matrix.module`로 3개 테스트 job이 **동시에** 실행됩니다. 모듈이 늘어나도 CI 시간은 늘지 않습니다.

---

## Step 4: Worktree Branch에서 CI 활용

worktree에서 작업한 브랜치를 push하면 PR CI가 자동으로 트리거됩니다.

### 일반적인 worktree + CI 워크플로우

```bash
# Terminal 1 (main): worktree 생성
$ git worktree add ../project-feature -b feature/new-module

# Terminal 2 (worktree): 개발
$ cd ../project-feature
$ claude
# ... 개발 ...
$ git add -A && git commit -m "feat: add new module"

# worktree에서 직접 push
$ git push -u origin feature/new-module
>>> remote: Create a pull request for 'feature/new-module' on GitHub by visiting:
>>> remote:   https://github.com/user/repo/pull/new/feature/new-module

# GitHub에서 PR 생성 → CI 자동 실행
# - build job 실행
# - Users/Products/Orders 테스트 병렬 실행
# - format check 실행
```

### PR 상태 확인 (gh CLI)

```bash
# PR의 CI 상태 확인
$ gh pr checks
>>> build       pass   5s   Build
>>> test/Users  pass   8s   Run Users tests
>>> test/Products pass 7s   Run Products tests
>>> test/Orders  pass  9s   Run Orders tests
>>> format      pass   3s   Check formatting
```

---

## Step 5: Worktree 정리 in CI (선택사항)

CI 환경에서 worktree를 사용하는 경우 (예: E2E 테스트에서 여러 버전 비교), 정리가 필요합니다.

### CI에서 worktree 정리 step

```yaml
  cleanup:
    runs-on: ubuntu-latest
    needs: [test]
    if: always()   # 테스트 실패해도 정리 실행

    steps:
      - uses: actions/checkout@v4

      - name: Cleanup worktrees
        run: |
          echo "Active worktrees:"
          git worktree list

          # stale worktree 정리
          git worktree prune -v

          echo "After cleanup:"
          git worktree list
```

---

## Step 6: 커밋

```bash
# 프로젝트 루트에서
$ mkdir -p .github/workflows
# (위의 ci.yml 파일을 .github/workflows/ci.yml에 저장)

$ git add -A
$ git commit -m "ci: add GitHub Actions workflow with parallel module tests"
>>> [main ggg7777] ci: add GitHub Actions workflow with parallel module tests
```

---

## 전체 아키텍처 정리

```
┌─────────────────────────────────────────────────────────┐
│                    개발 워크플로우                         │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  Local Development (worktree 병렬)                       │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐                │
│  │ worktree │ │ worktree │ │ worktree │                │
│  │ Users    │ │ Products │ │ Orders   │                │
│  │          │ │          │ │          │                │
│  │ claude   │ │ claude   │ │ claude   │  ← 병렬 세션   │
│  └────┬─────┘ └────┬─────┘ └────┬─────┘                │
│       │             │            │                      │
│       └──────┬──────┘            │                      │
│              ▼                   │                      │
│         merge to main            │                      │
│              │                   │                      │
│              └──────┬────────────┘                      │
│                     ▼                                   │
│                git push                                 │
│                     │                                   │
├─────────────────────┼───────────────────────────────────┤
│                     ▼                                   │
│  CI/CD (GitHub Actions 병렬)                             │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐                │
│  │ test     │ │ test     │ │ test     │                │
│  │ Users    │ │ Products │ │ Orders   │  ← matrix 병렬 │
│  └────┬─────┘ └────┬─────┘ └────┬─────┘                │
│       │             │            │                      │
│       └──────┬──────┘────────────┘                      │
│              ▼                                          │
│         All passed ✓                                    │
│              │                                          │
│              ▼                                          │
│         merge PR                                        │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

로컬에서도 병렬, CI에서도 병렬. 모듈이 독립적이라서 가능한 구조입니다.

---

## 연습 문제

### Challenge 1: 변경된 모듈만 테스트

현재 workflow는 모든 PR에서 3개 모듈 테스트를 모두 실행합니다. Users만 변경한 PR에서 Users 테스트만 실행하려면 어떻게 해야 할까요?

<details>
<summary>힌트</summary>

`paths` filter와 `dorny/paths-filter` 액션을 사용합니다:

```yaml
  detect-changes:
    runs-on: ubuntu-latest
    outputs:
      users: ${{ steps.filter.outputs.users }}
      products: ${{ steps.filter.outputs.products }}
      orders: ${{ steps.filter.outputs.orders }}
    steps:
      - uses: actions/checkout@v4
      - uses: dorny/paths-filter@v3
        id: filter
        with:
          filters: |
            users:
              - 'src/Users/**'
            products:
              - 'src/Products/**'
            orders:
              - 'src/Orders/**'

  test-users:
    needs: [build, detect-changes]
    if: needs.detect-changes.outputs.users == 'true'
    # ...
```

</details>

### Challenge 2: worktree에서 CI 결과 확인

worktree에서 push한 후, 같은 worktree에서 CI 결과를 확인하는 방법은?

<details>
<summary>정답</summary>

```bash
# worktree에서 직접 gh 명령 사용 가능
$ gh pr create --fill
$ gh pr checks --watch

# 또는 main worktree에서도 확인 가능 (같은 repo 공유)
```

모든 worktree는 같은 git repository를 공유하므로, 어떤 worktree에서든 `gh` 명령을 사용할 수 있습니다.

</details>

---

## 튜토리얼 완료!

5개 챕터를 통해 다음을 학습했습니다:

| 챕터 | 학습 내용 |
|------|----------|
| 01 Introduction | git worktree 개념, F# 프로젝트 셋업, worktree lifecycle |
| 02 Parallel Development | 병렬 worktree 개발, clean merge, Claude Code 병렬 세션 |
| 03 Merge Conflicts | 의도적 충돌, conflict resolution, 공유 파일 관리 |
| 04 Hotfix Parallel | 작업 중단 없는 hotfix, rebase 워크플로우 |
| 05 CI/CD Integration | GitHub Actions matrix 빌드, 모듈별 병렬 테스트 |

## Quick Reference

```bash
# === Worktree 생성 ===
git worktree add ../project-feature -b feature/name
claude --worktree feature-name

# === Worktree 확인 ===
git worktree list

# === Worktree 정리 ===
git worktree remove ../project-feature
git branch -d feature/name
git worktree prune   # stale metadata 정리

# === Merge ===
git merge feature/name          # main에서
git rebase main                 # feature에서 (main 업데이트 반영)

# === 포트 분리 (동시 실행 시) ===
ASPNETCORE_URLS=http://localhost:5001 dotnet run   # worktree 1
ASPNETCORE_URLS=http://localhost:5002 dotnet run   # worktree 2

# === Claude Code 병렬 세션 ===
# Terminal 1: cd ../project-main && claude
# Terminal 2: cd ../project-users && claude
# Terminal 3: cd ../project-products && claude
```

[← 처음으로 돌아가기](./README.md)
