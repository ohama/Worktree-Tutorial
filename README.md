# Claude Code + Git Worktree 병렬 개발 튜토리얼

## Documentation

[Git Worktree Tutorial](https://ohama.github.io/Worktree-Tutorial/)

Claude Code와 git worktree를 활용한 병렬 개발 패턴을 가르치는 실전 튜토리얼.
F# REST API 프로젝트를 예제로 사용하여, 독립적인 도메인 모듈을 여러 worktree에서 동시에 개발하고 merge하는 전체 워크플로우를 다룹니다.

## 대상 독자

- Claude Code를 이미 사용하고 있지만 worktree 병렬 패턴을 모르는 개발자
- 기본적인 F# 이해가 있는 개발자

## 다루는 시나리오

| Scenario | 내용 | 핵심 학습 |
|----------|------|-----------|
| 1. Parallel Development | Users + Products 모듈을 병렬 worktree에서 동시 개발 | `claude --worktree`, 병렬 세션, clean merge |
| 2. Merge + Conflict Resolution | Core.fs 의도적 충돌 + Orders 모듈 통합 | merge conflict 해결, route composition |
| 3. Hotfix Parallel | feature 작업 중단 없이 main에 hotfix 적용 | hotfix worktree, rebase |
| 4. CI/CD Integration | GitHub Actions per-module 병렬 빌드 | matrix strategy, worktree cleanup |

## Tech Stack

- **Language:** F# 9.0 / .NET 9.0
- **Framework:** [Giraffe](https://github.com/giraffe-fsharp/Giraffe) 8.2.0
- **Testing:** [Expecto](https://github.com/haf/expecto) 10.2.3
- **Formatter:** [Fantomas](https://github.com/fsprojects/fantomas) 7.0.5

## 디렉토리 구조

```
.
├── tutorial/          # 튜토리얼 문서 (Markdown, 한영 혼용)
│   ├── README.md      # 챕터 인덱스
│   ├── 01-introduction.md
│   ├── 02-parallel-development.md
│   ├── 03-merge-conflicts.md
│   ├── 04-hotfix-parallel.md
│   └── 05-cicd-integration.md
├── src/               # F# REST API 예제 프로젝트
│   ├── Core.fs        # 공유 타입 (UserId, ProductId, OrderId)
│   ├── Users/         # Users 도메인 모듈
│   ├── Products/      # Products 도메인 모듈
│   ├── Orders/        # Orders 도메인 모듈
│   └── Program.fs     # Route composition (의도적 merge conflict zone)
└── .planning/         # 프로젝트 계획 문서
```

## 시작하기

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download)
- [Claude Code](https://claude.ai/code)
- Git 2.20+

### Quick Start

```bash
# 프로젝트 빌드
cd src
dotnet build

# 서버 실행
dotnet run

# 테스트
dotnet test
```

## 튜토리얼 핵심 흐름

```
Foundation (Core.fs + scaffold)
        │
        ├──── worktree: feature/users ──── Users 모듈 개발
        │                                        │
        ├──── worktree: feature/products ── Products 모듈 개발
        │                                        │
        ▼                                        ▼
    merge back ◄──────────────────────────── clean merge
        │
        ├──── worktree: feature/orders ─── Orders 모듈 개발
        │                                        │
        ▼                                        ▼
    conflict resolution ◄───────────────── intentional conflict
        │
        ├──── worktree: hotfix/bug-fix ─── Hotfix 적용
        │         │
        │         ▼
        │     merge to main
        │         │
        ▼         ▼
    rebase feature onto updated main
```

## License

MIT
