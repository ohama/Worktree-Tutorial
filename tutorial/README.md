# Claude Code + Git Worktree 병렬 개발 튜토리얼

## 개요

이 튜토리얼은 Claude Code와 git worktree를 활용하여 **하나의 프로젝트를 여러 브랜치에서 동시에 개발**하는 방법을 가르칩니다.

F# REST API 프로젝트를 예제로 사용하며, 독립적인 도메인 모듈(Users, Products, Orders)을 각각의 worktree에서 병렬로 개발합니다.

## 사전 준비

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Claude Code](https://docs.anthropic.com/en/docs/claude-code) (설치 완료 + 사용 경험)
- Git 2.20 이상
- 터미널 3개 이상 (iTerm2 split, tmux, 또는 별도 터미널 창)

## 챕터 목록

| # | 챕터 | 내용 | 소요 시간 |
|---|-------|------|-----------|
| 01 | [Introduction](./01-introduction.md) | git worktree 개념, F# 프로젝트 셋업, 첫 worktree 생성 | 30분 |
| 02 | [Parallel Development](./02-parallel-development.md) | Users + Products를 병렬 worktree에서 동시 개발, clean merge | 45분 |
| 03 | [Merge Conflicts](./03-merge-conflicts.md) | 의도적 충돌 시나리오, 충돌 해결, Orders 모듈 통합 | 40분 |
| 04 | [Hotfix Parallel](./04-hotfix-parallel.md) | feature 작업 중단 없이 hotfix 적용, rebase | 30분 |
| 05 | [CI/CD Integration](./05-cicd-integration.md) | GitHub Actions per-module 병렬 빌드 | 20분 |

## 워크플로우 전체 흐름

```
Chapter 01: Foundation
    main ──── Core.fs + Program.fs + scaffold
                │
Chapter 02: Parallel Development
                ├─── worktree: feature/users ────── Users 모듈
                │                                      │
                ├─── worktree: feature/products ──── Products 모듈
                │                                      │
                ▼                                      ▼
            merge back ◄────────────────────── clean merge
                │
Chapter 03: Merge + Conflict Resolution
                ├─── worktree: feature/orders ──── Orders 모듈
                │         (Core.fs 수정 → 충돌!)        │
                ▼                                      ▼
            conflict resolution ◄──────────── intentional conflict
                │
Chapter 04: Hotfix Parallel
                ├─── worktree: feature/search ──── 새 feature 작업 중...
                │
                ├─── worktree: hotfix/bug-fix ──── 긴급 버그 수정
                │         │
                │         ▼
                │     merge to main
                │         │
                ▼         ▼
            rebase feature onto updated main
                │
Chapter 05: CI/CD
                └─── GitHub Actions matrix build
```

## 컨벤션

이 튜토리얼에서 사용하는 표기법:

- `$` — 쉘 명령어 (복사해서 터미널에 붙여넣기)
- `# 주석` — 명령어 설명
- `>>>` — 예상 출력
- **Terminal 1/2/3** — 각각 다른 터미널 창 (병렬 작업 시)
- `[main]`, `[feature/users]` 등 — 현재 브랜치/worktree 표시
