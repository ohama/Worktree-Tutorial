# Chapter 05: CI/CD Integration (Scenario 4)

이 챕터에서는 worktree 기반 병렬 개발 워크플로우를 **GitHub Actions CI/CD**와 통합합니다. 각 도메인 모듈을 **matrix strategy로 병렬 테스트**하여 CI에서도 병렬성의 이점을 활용합니다.

## 목표

- 모듈별 독립 테스트를 CI에서 병렬 실행
- PR마다 전체 테스트를 세 개의 job으로 분산
- worktree branch push 시 자동 CI 트리거
- CI 완료 후 worktree 메타데이터 자동 정리

---

## Step 1: 프로젝트 구조 확인

현재 프로젝트 구조:

```
.
├── src/
│   ├── Core.fs
│   ├── Users/
│   │   ├── Domain.fs
│   │   └── Handlers.fs
│   ├── Products/
│   │   ├── Domain.fs
│   │   └── Handlers.fs
│   ├── Orders/
│   │   ├── Domain.fs
│   │   └── Handlers.fs
│   ├── Program.fs
│   └── WorktreeApi.fsproj
└── tests/
    ├── UsersTests.fs
    ├── ProductsTests.fs
    ├── OrdersTests.fs
    ├── TestMain.fs
    └── WorktreeApi.Tests.fsproj
```

모든 모듈이 하나의 `.fsproj`에 있으므로, 빌드는 프로젝트 단위로 수행됩니다. 하지만 **테스트**는 Expecto의 `--filter-test-list` 플래그를 사용해 모듈별로 분리할 수 있습니다.

---

## Step 2: 테스트 실행 확인

테스트 프로젝트는 Phase 2에서 이미 만들었습니다. 모듈별 테스트 실행을 확인해봅니다.

```bash
# Users 모듈 테스트만 실행
$ dotnet run --project tests/WorktreeApi.Tests.fsproj -- --filter-test-list Users
>>> [13:00:00 INF] EXPECTO? Running tests...
>>> [13:00:00 INF] EXPECTO! 6 tests run in 00:00:00.xxx
>>>               6 passed, 0 ignored, 0 failed, 0 errored.

# Products 모듈 테스트만 실행
$ dotnet run --project tests/WorktreeApi.Tests.fsproj -- --filter-test-list Products
>>> [13:00:00 INF] EXPECTO? Running tests...
>>> [13:00:00 INF] EXPECTO! 5 tests run in 00:00:00.xxx
>>>               5 passed, 0 ignored, 0 failed, 0 errored.

# Orders 모듈 테스트만 실행
$ dotnet run --project tests/WorktreeApi.Tests.fsproj -- --filter-test-list Orders
>>> [13:00:00 INF] EXPECTO? Running tests...
>>> [13:00:00 INF] EXPECTO! 10 tests run in 00:00:00.xxx
>>>               10 passed, 0 ignored, 0 failed, 0 errored.

# 전체 테스트 (합계: 21개)
$ dotnet run --project tests/WorktreeApi.Tests.fsproj
>>> [13:00:00 INF] EXPECTO! 21 tests run — 21 passed, 0 ignored, 0 failed, 0 errored.
```

`--filter-test-list Users`는 test list 이름에 "Users" 문자열이 포함된 테스트만 실행합니다. `tests/TestMain.fs`에서 `testList "Users" [...]`로 정의된 테스트가 여기에 해당합니다.

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
  # Stage 1: 전체 프로젝트 빌드
  build:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 10.0
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

  # Stage 2: 모듈별 병렬 테스트 (matrix strategy)
  test:
    runs-on: ubuntu-latest
    needs: build

    strategy:
      matrix:
        module: [Users, Products, Orders]
      fail-fast: false   # 하나가 실패해도 나머지 계속 실행

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 10.0
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore
        run: dotnet restore tests/WorktreeApi.Tests.fsproj

      - name: Run ${{ matrix.module }} tests
        run: |
          dotnet run --project tests/WorktreeApi.Tests.fsproj \
            -- --filter-test-list "${{ matrix.module }}"

  # Stage 3: worktree 정리 (항상 실행)
  cleanup:
    runs-on: ubuntu-latest
    needs: [test]
    if: always()   # 테스트 실패해도 정리 실행

    steps:
      - uses: actions/checkout@v4

      - name: Prune stale worktree metadata
        run: |
          echo "Active worktrees before prune:"
          git worktree list
          git worktree prune -v
          echo "Active worktrees after prune:"
          git worktree list
```

### YAML 라인별 설명

| 항목 | 설명 |
|------|------|
| `on: push/pull_request` | CI 트리거 조건. `main` 브랜치에 push하거나 PR을 열면 워크플로우 실행 |
| `needs: build` | test 잡은 build 잡이 성공해야 시작. 빌드 실패 시 테스트 불필요 |
| `matrix.module: [Users, Products, Orders]` | 세 가지 값으로 test 잡을 세 개 병렬 생성. 코드 중복 없이 병렬화 달성 |
| `fail-fast: false` | 한 모듈 테스트가 실패해도 나머지 모듈 테스트 계속 실행. 전체 실패 현황 파악 가능 |
| `dotnet-version: '10.0.x'` | .NET 10 최신 패치 버전 설치. 프로젝트의 `<TargetFramework>net10.0</TargetFramework>`와 일치 |
| `dotnet restore tests/...fsproj` | 각 job은 새 VM에서 실행됨. build job의 패키지가 공유되지 않으므로 restore 재실행 필요 |
| `--filter-test-list "${{ matrix.module }}"` | Expecto CLI 플래그. test list 이름의 부분 문자열로 필터링. `Users` → `testList "Users" [...]` 실행 |
| `needs: [test]` | cleanup은 test 잡이 끝난 후 실행 |
| `if: always()` | test 실패해도 cleanup 실행 보장. 기본값은 의존 잡 실패 시 skip |
| `git worktree prune -v` | stale 상태의 worktree 메타데이터 정리. CI 신규 클론에서는 no-op이나, CI가 직접 worktree를 생성하는 시나리오에서 필수 |

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
                       │ needs: [test]
              ┌────────▼────────┐
              │    [cleanup]    │    ← if: always() — 항상 실행
              │  worktree 정리  │
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
# - cleanup job 실행 (if: always())
```

### PR 상태 확인 (gh CLI)

```bash
# PR의 CI 상태 확인
$ gh pr checks
>>> build            pass   5s   Build
>>> test (Users)     pass   8s   Run Users tests
>>> test (Products)  pass   7s   Run Products tests
>>> test (Orders)    pass   9s   Run Orders tests
>>> cleanup          pass   2s   Prune stale worktree metadata
```

---

## Step 5: CI에서 Worktree 정리가 필요한 이유

`cleanup` 잡은 워크플로우의 3단계에 포함되어 있으며 `if: always()`로 항상 실행됩니다.

**이 잡이 필요한 이유:**

CI 파이프라인이 단순한 빌드/테스트를 넘어 직접 worktree를 생성하는 시나리오 (예: E2E 테스트에서 여러 버전 동시 실행, 카나리 배포 준비)에서는 이전 실행의 worktree 메타데이터가 남아 충돌을 유발할 수 있습니다. `git worktree prune -v`는 실제 디렉토리가 사라진 stale worktree 항목만 정리합니다.

**신규 CI 클론에서는 no-op입니다:**

```bash
$ git worktree list
/home/runner/work/project/project  abc1234 [main]   ← 메인 워크트리만 존재

$ git worktree prune -v
# (출력 없음 — 정리할 항목 없음)

$ git worktree list
/home/runner/work/project/project  abc1234 [main]   ← 변화 없음
```

`if: always()`의 의미: 테스트 중 하나가 실패해도 cleanup은 반드시 실행됩니다. 기본 동작은 의존 잡이 실패하면 해당 잡을 skip하는 것이므로 명시적으로 지정해야 합니다.

---

## Step 6: 커밋

```bash
# 프로젝트 루트에서
$ git add .github/workflows/ci.yml
$ git commit -m "ci: add GitHub Actions workflow with parallel module tests"
>>> [main abc1234] ci: add GitHub Actions workflow with parallel module tests
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
| 05 CI/CD Integration | GitHub Actions matrix 테스트, 모듈별 병렬 실행, cleanup |

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

# === 모듈별 테스트 실행 ===
dotnet run --project tests/WorktreeApi.Tests.fsproj -- --filter-test-list Users
dotnet run --project tests/WorktreeApi.Tests.fsproj -- --filter-test-list Products
dotnet run --project tests/WorktreeApi.Tests.fsproj -- --filter-test-list Orders
```

[← 처음으로 돌아가기](./README.md)
