# Chapter 04: Hotfix Parallel (Scenario 3)

이 챕터에서는 **feature 개발 중에 긴급 버그 수정**이 필요한 실전 시나리오를 다룹니다. worktree 덕분에 feature 작업을 중단하지 않고 hotfix를 병행할 수 있습니다.

## 시나리오

당신은 새로운 검색 기능을 개발하고 있습니다. 그런데 프로덕션에서 **긴급 버그**가 발견되었습니다 — Users API의 delete endpoint가 존재하지 않는 user를 삭제할 때 204 대신 500을 반환합니다.

기존 workflow (worktree 없이):

```
feature 작업 중...
  │
  ├── git stash (작업 중단!)
  ├── git switch main
  ├── hotfix 작성
  ├── commit + push
  ├── git switch feature/search
  ├── git stash pop (작업 복원)
  └── 작업 재개... (context 잃어버림)
```

worktree workflow:

```
feature 작업 계속 진행 중...
  │                              별도 worktree에서:
  │                              ├── hotfix 작성
  │                              ├── commit
  │                              └── main에 merge
  │                                    │
  ▼                                    ▼
feature를 updated main에 rebase
```

**핵심 차이**: feature 작업이 **한 번도 중단되지 않습니다.**

---

## Step 1: Feature 개발 시작

먼저 새 feature를 위한 worktree를 만들고 작업을 시작합니다.

**Terminal 1** (main):

```bash
# 검색 기능 worktree 생성
$ git worktree add ../worktree-tutorial-search -b feature/search
>>> Preparing worktree (new branch 'feature/search')
>>> HEAD is now at ccc3333 merge: resolve Core.fs conflict
```

**Terminal 2** (feature/search):

```bash
$ cd ../worktree-tutorial-search
$ claude   # 또는 직접 작업

# 검색 기능 개발 시작 — Core.fs에 SearchQuery 타입 추가
```

`src/Core.fs`에 추가:

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

**이 시점에서 작업이 진행 중입니다. commit하지 않았습니다.**

---

## Step 2: 긴급 버그 발견!

프로덕션에서 버그 리포트가 들어옵니다:

> "DELETE /api/users/{id} — 존재하지 않는 ID로 요청하면 500 Internal Server Error가 발생합니다."

**긴급 수정이 필요합니다.** 하지만 feature/search 작업을 중단하고 싶지 않습니다.

### Hotfix worktree 생성

**Terminal 1** (main)에서 hotfix worktree를 만듭니다:

```bash
# main에서 hotfix 브랜치 생성
$ git worktree add ../worktree-tutorial-hotfix -b hotfix/users-delete-404
>>> Preparing worktree (new branch 'hotfix/users-delete-404')
>>> HEAD is now at ccc3333 merge: resolve Core.fs conflict
```

현재 상태:

```
worktree-tutorial/            [main]                  ← Terminal 1
worktree-tutorial-search/     [feature/search]        ← Terminal 2 (작업 중!)
worktree-tutorial-hotfix/     [hotfix/users-delete-404] ← Terminal 3 (새로 생성)
```

---

## Step 3: Hotfix 작성

**Terminal 3** (hotfix worktree)에서 버그를 수정합니다. feature/search 작업은 **그대로 계속 진행**됩니다.

```bash
$ cd ../worktree-tutorial-hotfix
```

### 버그 확인

`src/Users/Handlers.fs`의 delete 핸들러를 확인합니다:

```fsharp
    let delete (id: Guid) : HttpHandler =
        fun next ctx ->
            if Domain.delete id then
                ctx.SetStatusCode 204
                next ctx
            else
                ctx.SetStatusCode 404
                json (ApiResponse.error "User not found") next ctx
```

코드 자체는 맞아 보입니다. `Domain.delete`를 확인합니다:

`src/Users/Domain.fs`:

```fsharp
    let delete (id: Guid) = store.TryRemove(id) |> fst
```

`ConcurrentDictionary.TryRemove`는 key가 없으면 `false`를 반환하므로 500이 아닌 404를 반환해야 합니다. 실제 버그는 다른 곳에 있을 수 있습니다.

> **튜토리얼 목적으로** 버그를 시뮬레이션합니다. 실제로 버그를 하나 만들어봅시다.

### 버그 시뮬레이션 (튜토리얼용)

이 시나리오를 연습하기 위해, 에러 응답에 적절한 status code를 추가하는 개선을 hotfix로 처리합니다.

`src/Users/Handlers.fs`의 delete 핸들러를 개선합니다:

```fsharp
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

변경점: 삭제 전에 존재 여부를 먼저 확인하고, 에러 메시지에 ID를 포함시킵니다.

### 빌드 확인 및 커밋

```bash
$ cd src && dotnet build
>>> Build succeeded.

$ cd ..
$ git add -A
$ git commit -m "fix: improve Users delete handler — check existence before delete, include ID in error"
>>> [hotfix/users-delete-404 ddd4444] fix: improve Users delete handler
```

---

## Step 4: Hotfix를 Main에 Merge

**Terminal 1** (main)에서:

```bash
# hotfix를 main에 merge
$ git merge hotfix/users-delete-404
>>> Updating ccc3333..ddd4444
>>> Fast-forward
>>>  src/Users/Handlers.fs | 8 +++++---
>>>  1 file changed, 5 insertions(+), 3 deletions(-)
```

Clean fast-forward merge. 프로덕션에 hotfix가 적용되었습니다.

### Hotfix worktree 정리

```bash
$ git worktree remove ../worktree-tutorial-hotfix
>>> Removing worktree '/path/to/worktree-tutorial-hotfix'

$ git branch -d hotfix/users-delete-404
>>> Deleted branch hotfix/users-delete-404 (was ddd4444).
```

---

## Step 5: Feature Branch를 Updated Main에 Rebase

feature/search는 hotfix **이전의** main에서 분기되었습니다. main이 업데이트되었으므로, feature branch를 최신 main 위에 rebase합니다.

**Terminal 2** (feature/search)에서:

```bash
# 먼저 진행 중인 작업을 commit (또는 stash)
$ git add -A
$ git commit -m "wip: search query types"
>>> [feature/search eee5555] wip: search query types

# main의 최신 상태를 가져오기
# (worktree는 같은 repo를 공유하므로 fetch 불필요 — 이미 보임)
$ git log --oneline main
>>> ddd4444 (main) fix: improve Users delete handler
>>> ccc3333 merge: resolve Core.fs conflict
>>> ...

# main 위에 rebase
$ git rebase main
>>> Successfully rebased and updated refs/heads/feature/search.
```

> **왜 merge 대신 rebase인가?**
>
> Feature branch에서는 `rebase`가 `merge`보다 깔끔합니다:
> - **rebase**: feature commit들이 main 최신 위에 놓임 → 선형적 history
> - **merge**: merge commit이 생기고 history가 복잡해짐
>
> ```
> rebase:
> main ── A ── B (hotfix) ── C (feature commit, rebased)
>
> merge:
> main ── A ── B (hotfix) ──── M (merge commit)
>                          ╱
> feature ─── C ──────────╱
> ```

### Rebase 충돌이 발생한 경우

만약 hotfix가 수정한 파일을 feature에서도 수정했다면 rebase 중 충돌이 발생할 수 있습니다:

```bash
$ git rebase main
>>> CONFLICT (content): Merge conflict in src/Users/Handlers.fs
>>> error: could not apply eee5555... wip: search query types
>>>
>>> Resolve all conflicts manually, mark them as resolved with
>>> "git add <pathspec>" then run "git rebase --continue".

# 충돌 해결
$ vim src/Users/Handlers.fs   # 충돌 마커 해결
$ cd src && dotnet build       # 빌드 확인
$ cd ..
$ git add src/Users/Handlers.fs
$ git rebase --continue
>>> Successfully rebased and updated refs/heads/feature/search.
```

### Rebase 후 확인

```bash
$ git log --oneline
>>> fff6666 (HEAD -> feature/search) wip: search query types
>>> ddd4444 (main) fix: improve Users delete handler
>>> ccc3333 merge: resolve Core.fs conflict
>>> ...
```

feature/search commit이 hotfix commit **위에** 깔끔하게 놓여있습니다.

---

## Step 6: Feature 작업 계속

Rebase가 완료되었으므로 feature 개발을 계속합니다. hotfix의 변경사항이 이미 포함되어 있으므로 충돌 걱정 없이 작업할 수 있습니다.

```bash
# feature 개발 계속...
# (실제로는 여기서 검색 기능을 완성하고 commit)

# 작업 완료 후 main에 merge
# Terminal 1 (main)에서:
$ git merge feature/search
>>> Fast-forward (또는 merge commit)
```

---

## 전체 타임라인 정리

```
시간 ──────────────────────────────────────────────────────────►

Terminal 2 (feature/search):
    [search 작업 시작]──────────[계속 작업]──────[commit]──[rebase]──[계속]
                                    │                        ▲
                                    │                        │
Terminal 3 (hotfix):                │                        │
                        [hotfix 생성]─[수정]─[commit]        │
                                            │                │
Terminal 1 (main):                          │                │
    ────────────────────────────[merge hotfix]────────────────

핵심: feature 작업이 단 한 번도 중단되지 않았습니다!
```

---

## Worktree Lifecycle 전체 정리

이 튜토리얼을 통해 경험한 worktree lifecycle:

### 1. 생성 (Create)

```bash
# 새 브랜치와 함께 생성
$ git worktree add ../project-feature -b feature/name

# 기존 브랜치로 생성
$ git worktree add ../project-feature feature/name

# Claude Code 통합
$ claude --worktree feature-name
```

### 2. 확인 (List)

```bash
$ git worktree list
>>> /path/to/main     abc1234 [main]
>>> /path/to/feature  def5678 [feature/name]
```

### 3. 작업 (Work)

```bash
$ cd ../project-feature
$ claude   # 또는 직접 편집
# ... 개발 ...
$ git add -A && git commit -m "feat: ..."
```

### 4. 동기화 (Sync)

```bash
# worktree에서 main의 최신 변경 가져오기
$ git rebase main

# 또는 main에서 worktree의 브랜치 merge
$ cd ../main-project
$ git merge feature/name
```

### 5. 정리 (Cleanup)

```bash
# worktree 제거
$ git worktree remove ../project-feature

# 브랜치 정리 (merge 완료 후)
$ git branch -d feature/name

# stale metadata 정리 (실수로 rm -rf한 경우)
$ git worktree prune
```

### 주의사항 요약

| 규칙 | 이유 |
|------|------|
| sibling 디렉토리에 생성 | repo 내부에 생성하면 IDE 혼란 |
| `git worktree remove` 사용 | `rm -rf`는 stale metadata 남김 |
| 하나의 브랜치 = 하나의 worktree | 같은 브랜치를 2개 체크아웃 불가 |
| 포트 번호 분리 | 동시 서버 실행 시 충돌 방지 |
| commit 후 rebase | stash보다 안전하고 history 깔끔 |

---

## 연습 문제

### Challenge 1: 동시 hotfix

2개의 hotfix가 동시에 필요한 상황을 시뮬레이션하세요:
- hotfix/users-bug: Users 모듈 수정
- hotfix/products-bug: Products 모듈 수정

두 hotfix worktree를 동시에 만들고, 각각 수정하고, 순서대로 main에 merge하세요.

<details>
<summary>힌트</summary>

```bash
$ git worktree add ../project-hotfix-users -b hotfix/users-bug
$ git worktree add ../project-hotfix-products -b hotfix/products-bug

# 각 worktree에서 수정 + commit

# main에서 순서대로 merge
$ git merge hotfix/users-bug
$ git merge hotfix/products-bug    # 다른 파일이면 충돌 없음
```

</details>

### Challenge 2: Rebase 중 충돌

feature branch에서 `Core.fs`를 수정한 상태에서, hotfix도 `Core.fs`를 수정한 경우의 rebase 충돌을 직접 만들고 해결해보세요.

<details>
<summary>힌트</summary>

1. feature worktree에서 Core.fs의 ApiResponse에 필드 추가
2. hotfix worktree에서 Core.fs의 같은 ApiResponse에 다른 필드 추가
3. hotfix를 main에 merge
4. feature에서 `git rebase main` → 충돌 발생
5. 충돌 해결 후 `git rebase --continue`

</details>

### Challenge 3: claude --worktree로 hotfix

수동 worktree 대신 `claude --worktree`를 사용해서 hotfix 시나리오를 재현해보세요.

<details>
<summary>힌트</summary>

```bash
# main에서
$ claude --worktree hotfix-test

# Claude Code 세션 안에서:
# "src/Products/Handlers.fs의 delete 핸들러를 개선해줘"

# 세션 종료 시 worktree 유지 선택
# main에서 merge
```

</details>

---

## 다음 챕터

모든 개발 시나리오를 마스터했습니다. 마지막 챕터에서는 이 워크플로우를 **CI/CD와 통합**하여 팀 전체가 활용할 수 있게 만듭니다.

[Chapter 05: CI/CD Integration →](./05-cicd-integration.md)
