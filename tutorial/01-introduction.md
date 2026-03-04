# Chapter 01: Introduction

## Git Worktree란?

일반적인 git workflow에서는 브랜치를 전환할 때 `git checkout` 또는 `git switch`를 사용합니다. 이 방식은 **하나의 디렉토리에서 한 번에 하나의 브랜치만** 작업할 수 있습니다.

```
# 일반적인 브랜치 전환
$ git switch feature/users    # Users 작업 시작
# ... 작업 중 ...
$ git switch feature/products  # Products로 전환 (Users 작업 중단!)
# ... 작업 중 ...
$ git switch feature/users     # 다시 Users로 돌아옴
```

**git worktree**는 하나의 repository에서 **여러 브랜치를 동시에 체크아웃**할 수 있게 해줍니다. 각 worktree는 독립된 작업 디렉토리를 가지므로, 브랜치 전환 없이 여러 기능을 동시에 개발할 수 있습니다.

```
project/                    # main 브랜치 (원본)
project-users/              # feature/users 브랜치 (worktree)
project-products/           # feature/products 브랜치 (worktree)
```

세 디렉토리 모두 **같은 git repository를 공유**합니다. 한쪽에서 commit하면 다른 쪽에서도 보입니다.

## 순차 개발 vs 병렬 개발

### 순차 개발 (기존 방식)

```
시간 ──────────────────────────────────────────────────►

[main]
  │
  ├── feature/users ──── 개발 ──── 테스트 ──── merge ──┐
  │                                                     │
  │◄────────────────────────────────────────────────────┘
  │
  ├── feature/products ── 개발 ── 테스트 ── merge ──┐
  │                                                  │
  │◄─────────────────────────────────────────────────┘
  │
  ├── feature/orders ──── 개발 ── 테스트 ── merge ──┐
  │                                                  │
  │◄─────────────────────────────────────────────────┘

총 소요: ████████████████████████████████████████ ~60분
```

### 병렬 개발 (worktree 방식)

```
시간 ──────────────────────────────────────────────────►

[main]
  │
  ├── feature/users ────── 개발 ── 테스트 ──┐
  │                                          │── merge
  ├── feature/products ─── 개발 ── 테스트 ──┘
  │
  ├── feature/orders ──── 개발 ── 테스트 ── merge ──┐
  │                                                  │
  │◄─────────────────────────────────────────────────┘

총 소요: ████████████████████ ~25분
```

독립적인 모듈을 **동시에** 개발하면 전체 소요 시간이 크게 줄어듭니다.

## Claude Code의 Worktree 지원

Claude Code는 `--worktree` 플래그로 worktree 생성과 관리를 지원합니다.

### 방법 1: `claude --worktree` 플래그

```bash
# 새 worktree를 생성하고 Claude Code 세션을 시작
$ claude --worktree

# worktree에 이름을 지정
$ claude --worktree feature-users
```

이 명령은 다음을 자동으로 수행합니다:
1. `.claude/worktrees/` 디렉토리에 새 worktree 생성
2. HEAD 기반으로 새 브랜치 생성
3. 새 worktree에서 Claude Code 세션 시작
4. 세션 종료 시 worktree 유지/삭제 선택

### 방법 2: 수동 worktree + 별도 Claude 세션

```bash
# 1. worktree를 직접 생성 (sibling 디렉토리로)
$ git worktree add ../project-users feature/users

# 2. 새 터미널에서 해당 디렉토리로 이동
$ cd ../project-users

# 3. 그 디렉토리에서 Claude Code 시작
$ claude
```

이 튜토리얼에서는 **방법 2 (수동 worktree)**를 주로 사용합니다. worktree의 동작 원리를 정확히 이해하기 위해서입니다. 원리를 이해한 후에는 `claude --worktree`로 빠르게 작업할 수 있습니다.

## git worktree 기본 명령어

### worktree 생성

```bash
# 새 브랜치를 만들면서 worktree 생성
$ git worktree add ../project-users -b feature/users
>>> Preparing worktree (new branch 'feature/users')
>>> HEAD is now at abc1234 latest commit message

# 기존 브랜치로 worktree 생성
$ git worktree add ../project-users feature/users
>>> Preparing worktree (checking out 'feature/users')
>>> HEAD is now at abc1234 latest commit message
```

> **주의**: worktree는 반드시 **repository 외부** (sibling 디렉토리)에 생성하세요.
> repository 내부에 생성하면 git 도구들이 혼란을 일으킵니다.
>
> ```
> # BAD — repo 내부에 생성
> $ git worktree add ./worktrees/users feature/users
>
> # GOOD — sibling 디렉토리에 생성
> $ git worktree add ../project-users feature/users
> ```

### worktree 목록 확인

```bash
$ git worktree list
>>> /path/to/project          abc1234 [main]
>>> /path/to/project-users    def5678 [feature/users]
>>> /path/to/project-products 9ab0123 [feature/products]
```

### worktree 제거

```bash
# 반드시 git worktree remove를 사용!
$ git worktree remove ../project-users
>>> Removing worktree '/path/to/project-users'

# rm -rf로 삭제하면 stale metadata가 남음!
# 만약 실수로 rm -rf했다면:
$ git worktree prune
```

> **절대 `rm -rf`로 worktree 디렉토리를 삭제하지 마세요.**
> git 내부 metadata가 남아서 같은 브랜치로 worktree를 다시 생성할 수 없게 됩니다.
> 실수로 삭제했다면 `git worktree prune`으로 stale metadata를 정리하세요.

### worktree 규칙

1. **하나의 브랜치는 하나의 worktree에서만** 체크아웃 가능
2. worktree는 같은 `.git` repository를 공유
3. 한 worktree에서 commit하면 다른 worktree에서 `git log`로 볼 수 있음
4. worktree를 제거해도 브랜치는 남아있음

---

## 프로젝트 셋업

이제 이 튜토리얼의 예제 프로젝트를 만듭니다. F# Giraffe REST API 프로젝트를 처음부터 생성합니다.

### Step 1: 프로젝트 디렉토리 생성

```bash
# 작업할 디렉토리 생성
$ mkdir worktree-tutorial && cd worktree-tutorial

# git 초기화
$ git init
>>> Initialized empty Git repository in /path/to/worktree-tutorial/.git/
```

### Step 2: .NET 프로젝트 생성

```bash
# Giraffe 템플릿 설치
$ dotnet new install "giraffe-template::*"
>>> The following template packages will be installed:
>>>    giraffe-template
>>> Success: giraffe-template installed the following templates:
>>>    giraffe    Giraffe

# src/ 디렉토리에 Giraffe 프로젝트 생성
$ dotnet new giraffe -o src --name WorktreeApi
>>> The template "Giraffe" was created successfully.
```

### Step 3: .fsproj 확인 및 수정

생성된 `src/WorktreeApi.fsproj`를 확인합니다:

```bash
$ cat src/WorktreeApi.fsproj
```

다음과 같이 수정합니다. F#에서는 **파일 컴파일 순서가 중요**합니다. `.fsproj`에 나열된 순서대로 컴파일되며, 위의 파일은 아래의 파일을 참조할 수 없습니다.

**`src/WorktreeApi.fsproj`:**

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <!-- === CORE (shared types — compile first) === -->
    <Compile Include="Core.fs" />

    <!-- === DOMAIN MODULES (independent — add in any order) === -->
    <!-- Users module -->
    <!-- Products module -->
    <!-- Orders module -->

    <!-- === ENTRY POINT (compile last) === -->
    <Compile Include="Program.fs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Giraffe" Version="8.2.0" />
  </ItemGroup>

</Project>
```

> **왜 zone 주석이 중요한가?**
>
> 나중에 여러 worktree에서 동시에 파일을 추가할 때, 각자 `.fsproj`의 **다른 zone**에 파일을 추가하면 merge conflict를 최소화할 수 있습니다.
> 주석이 없으면 같은 위치에 파일을 추가하게 되어 불필요한 충돌이 발생합니다.

### Step 4: Core.fs 작성

모든 도메인 모듈이 공유하는 타입을 정의합니다. 이 파일은 **dependency ceiling** — 모든 모듈이 참조하지만 어떤 모듈도 import하지 않는 최상위 공유 파일입니다.

**`src/Core.fs`:**

```fsharp
namespace WorktreeApi

open System

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
```

### Step 5: Program.fs 작성

Giraffe 서버의 진입점입니다. 지금은 health check endpoint만 있습니다.

**`src/Program.fs`:**

```fsharp
module WorktreeApi.App

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Giraffe

// === Health Check ===
let healthCheck: HttpHandler =
    fun next ctx ->
        json
            {| status = "healthy"
               timestamp = System.DateTime.UtcNow |}
            next
            ctx

// === Route Composition ===
let webApp: HttpHandler =
    choose
        [ GET >=> route "/health" >=> healthCheck

          // === DOMAIN ROUTES ===
          // (각 worktree에서 여기에 route를 추가합니다)

          RequestErrors.NOT_FOUND "Not Found" ]

// === Server Configuration ===
let configureApp (app: IApplicationBuilder) = app.UseGiraffe webApp

let configureServices (services: IServiceCollection) =
    services.AddGiraffe() |> ignore

[<EntryPoint>]
let main args =
    Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(fun webHost ->
            webHost
                .Configure(configureApp)
                .ConfigureServices(configureServices)
            |> ignore)
        .Build()
        .Run()

    0
```

### Step 6: 빌드 확인

```bash
$ cd src
$ dotnet build
>>> Build succeeded.
>>>     0 Warning(s)
>>>     0 Error(s)

# 서버 실행 테스트
$ dotnet run &
>>> info: Microsoft.Hosting.Lifetime[14]
>>>       Now listening on: http://localhost:5000

# health check 테스트
$ curl http://localhost:5000/health
>>> {"status":"healthy","timestamp":"2026-03-04T12:00:00Z"}

# 서버 종료
$ kill %1
```

### Step 7: Fantomas 설정

코드 포맷터를 설정합니다. 여러 worktree에서 작업할 때 일관된 코드 스타일을 유지하기 위해 중요합니다.

```bash
# 프로젝트 루트로 이동
$ cd ..

# dotnet tool manifest 생성
$ dotnet new tool-manifest
>>> The template "Dotnet local tool manifest file" was created successfully.

# Fantomas 설치
$ dotnet tool install fantomas --version 7.0.5
>>> You can invoke the tool from this directory using the following commands:
>>>     'dotnet tool run fantomas' or 'dotnet fantomas'.
>>> Tool 'fantomas' (version '7.0.5') was successfully installed.
```

### Step 8: .gitignore 작성

**`.gitignore`:**

```
# .NET build output
bin/
obj/

# IDE
.vs/
.vscode/
.idea/
*.user
*.suo

# Fantomas
.fantomas-ignore

# OS
.DS_Store
Thumbs.db
```

### Step 9: 첫 번째 커밋

```bash
$ git add -A
$ git status
>>> On branch main
>>>
>>> No commits yet
>>>
>>> Changes to be committed:
>>>   new file:   .config/dotnet-tools.json
>>>   new file:   .gitignore
>>>   new file:   src/Core.fs
>>>   new file:   src/Program.fs
>>>   new file:   src/WorktreeApi.fsproj
>>>   new file:   tutorial/...

$ git commit -m "feat: initialize F# Giraffe project with Core types and health check"
>>> [main (root-commit) abc1234] feat: initialize F# Giraffe project with Core types and health check
>>>  5 files changed, xxx insertions(+)
```

### Step 10: 첫 worktree 만들어보기 (연습)

실제 개발 전에 worktree 생성/삭제를 연습합니다.

```bash
# worktree 생성 (sibling 디렉토리)
$ git worktree add ../worktree-tutorial-test -b test/practice
>>> Preparing worktree (new branch 'test/practice')
>>> HEAD is now at abc1234 feat: initialize F# Giraffe project

# worktree 목록 확인
$ git worktree list
>>> /path/to/worktree-tutorial       abc1234 [main]
>>> /path/to/worktree-tutorial-test  abc1234 [test/practice]

# 새 worktree 디렉토리로 이동해서 확인
$ ls ../worktree-tutorial-test/
>>> src/  .gitignore  .config/

# 같은 파일들이 있는 것을 확인!
$ cat ../worktree-tutorial-test/src/Core.fs | head -5
>>> namespace WorktreeApi
>>>
>>> open System
>>>
>>> module Core =

# worktree 제거 (연습 완료)
$ git worktree remove ../worktree-tutorial-test
>>> Removing worktree '/path/to/worktree-tutorial-test'

# 브랜치도 정리
$ git branch -d test/practice
>>> Deleted branch test/practice (was abc1234).

# worktree 목록 확인 — main만 남아있음
$ git worktree list
>>> /path/to/worktree-tutorial  abc1234 [main]
```

축하합니다! git worktree의 전체 lifecycle (생성 → 확인 → 제거)을 연습했습니다.

---

## 연습 문제

### Challenge 1: worktree 방향 확인

다음 중 올바른 worktree 생성 명령은?

```bash
# A
$ git worktree add ./worktrees/test -b test/branch

# B
$ git worktree add ../project-test -b test/branch
```

<details>
<summary>정답</summary>

**B**가 올바릅니다. worktree는 repository **외부** (sibling 디렉토리)에 생성해야 합니다.
A처럼 repository 내부에 생성하면 `.gitignore` 설정이 필요하고, IDE 도구들이 혼란을 일으킬 수 있습니다.

</details>

### Challenge 2: stale worktree 복구

worktree 디렉토리를 `rm -rf`로 실수로 삭제했습니다. `git worktree add`로 같은 브랜치의 worktree를 다시 만들려고 하면 에러가 발생합니다. 어떻게 해결하나요?

<details>
<summary>정답</summary>

```bash
$ git worktree prune
```

`prune` 명령은 더 이상 존재하지 않는 worktree의 metadata를 정리합니다.
그 후 같은 브랜치로 worktree를 다시 생성할 수 있습니다.

</details>

### Challenge 3: Claude Code worktree

Claude Code에서 worktree를 자동 생성하고 세션을 시작하는 명령은?

<details>
<summary>정답</summary>

```bash
$ claude --worktree
# 또는 이름을 지정:
$ claude --worktree my-feature
```

이 명령은 `.claude/worktrees/`에 worktree를 생성하고, 새 브랜치에서 Claude Code 세션을 시작합니다.

</details>

---

## 다음 챕터

Foundation이 준비되었습니다. 다음 챕터에서는 실제로 **2개의 worktree를 동시에** 만들어서 Users와 Products 모듈을 병렬로 개발합니다.

[Chapter 02: Parallel Development →](./02-parallel-development.md)
