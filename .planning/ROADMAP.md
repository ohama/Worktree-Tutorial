# Roadmap: Claude Code Worktree 병렬 개발 튜토리얼

## Overview

F# Giraffe REST API를 예제 코드베이스로 사용하여 Claude Code와 git worktree 병렬 개발 패턴을 가르치는 실전 튜토리얼. Foundation을 먼저 구축하고, 독립 도메인 모듈을 병렬 worktree로 fan-out 개발한 뒤, merge/conflict/hotfix/CI 시나리오를 단계적으로 쌓아 올린다. 각 phase는 독립적인 학습 단위로 완결된다.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: Foundation** — 공유 코드베이스 scaffold + worktree 기초 설명 챕터
- [ ] **Phase 2: Parallel Modules (Scenario 1)** — Users/Products 병렬 개발 + clean merge happy path
- [ ] **Phase 3: Merge + Conflict Resolution (Scenario 2)** — Orders 모듈 + 의도적 충돌 해결 시나리오
- [ ] **Phase 4: Hotfix Parallel (Scenario 3)** — feature 작업 중단 없는 hotfix 병행 시나리오
- [ ] **Phase 5: CI/CD Integration (Scenario 4)** — GitHub Actions per-module 병렬 빌드

## Phase Details

### Phase 1: Foundation
**Goal**: 독자가 worktree 병렬 개발을 시작할 수 있는 F# 프로젝트와 기초 지식을 갖춘다
**Depends on**: Nothing (first phase)
**Requirements**: FOUND-01, FOUND-02, FOUND-03, FOUND-04, FOUND-05, TUT1-01, TUT1-02, TUTC-01, TUTC-02, TUTC-03
**Success Criteria** (what must be TRUE):
  1. `dotnet run`으로 Giraffe 서버가 정상 기동되고 헬스체크 엔드포인트가 응답한다
  2. `Core.fs`의 공유 타입이 컴파일되고 `.fsproj` 컴파일 순서 주석이 보인다
  3. 독자가 `git worktree add`, `git worktree list`, `git worktree remove` 명령어를 tutorial 챕터를 따라 실행할 수 있다
  4. `claude --worktree` 플래그 사용법이 tutorial 챕터에 단계별로 문서화되어 있다
  5. tutorial/README.md index 파일과 Markdown 챕터 디렉토리 구조가 존재한다
**Plans**: TBD

Plans:
- [ ] 01-01: F# Giraffe project scaffold (FOUND-01 to FOUND-05)
- [ ] 01-02: Tutorial chapter 01 — Introduction + worktree lifecycle (TUT1-01, TUT1-02, TUTC-01, TUTC-02, TUTC-03)

### Phase 2: Parallel Modules (Scenario 1)
**Goal**: 독자가 Users와 Products 모듈을 실제로 병렬 worktree에서 개발하고 clean merge를 경험한다
**Depends on**: Phase 1
**Requirements**: USER-01, USER-02, USER-03, USER-04, PROD-01, PROD-02, PROD-03, PROD-04, TEST-01, TEST-02, TEST-03, TUT1-03, TUT1-04, TUT1-05, TUTC-05
**Success Criteria** (what must be TRUE):
  1. `/api/users` CRUD 엔드포인트가 200/201/204/400/404를 올바르게 반환한다
  2. `/api/products` CRUD 엔드포인트가 200/201/204/400/404를 올바르게 반환한다
  3. Expecto 테스트가 `dotnet test`로 실행되고 Users/Products 두 모듈 모두 통과한다
  4. 3개 터미널 병렬 세션 데모가 tutorial 챕터에 실행 가능한 형태로 문서화되어 있다
  5. 순차 개발 대비 병렬 효율성 비교가 tutorial 챕터에 포함되어 있다
**Plans**: TBD

Plans:
- [ ] 02-01: Users module (USER-01 to USER-04) + Expecto setup (TEST-01, TEST-02)
- [ ] 02-02: Products module (PROD-01 to PROD-04) + tests (TEST-03)
- [ ] 02-03: Tutorial chapter 02 — Parallel development scenario (TUT1-03, TUT1-04, TUT1-05, TUTC-05)

### Phase 3: Merge + Conflict Resolution (Scenario 2)
**Goal**: 독자가 의도적 충돌 시나리오를 직접 해결하고 Orders 모듈을 통합해 3-모듈 API를 완성한다
**Depends on**: Phase 2
**Requirements**: ORDR-01, ORDR-02, ORDR-03, ORDR-04, TEST-04, TUT2-01, TUT2-02, TUT2-03, TUT2-04
**Success Criteria** (what must be TRUE):
  1. `/api/orders` CRUD 엔드포인트가 Users/Products ID를 참조하며 200/201/204/400/404를 반환한다
  2. Orders 모듈 Expecto 테스트가 `dotnet test`로 통과한다
  3. Core.fs 의도적 충돌 시나리오가 tutorial 챕터에 재현 가능한 형태로 문서화되어 있다
  4. merge conflict 해결 (Core.fs + Program.fs route composition) 과정이 step-by-step으로 문서화되어 있다
**Plans**: TBD

Plans:
- [ ] 03-01: Orders module (ORDR-01 to ORDR-04) + tests (TEST-04)
- [ ] 03-02: Tutorial chapter 03 — Merge conflict scenario (TUT2-01 to TUT2-04)

### Phase 4: Hotfix Parallel (Scenario 3)
**Goal**: 독자가 feature worktree 작업을 중단하지 않고 hotfix를 main에 적용하고 rebase하는 전체 흐름을 완수한다
**Depends on**: Phase 3
**Requirements**: TUT3-01, TUT3-02, TUT3-03, TUTC-04
**Success Criteria** (what must be TRUE):
  1. hotfix worktree에서 패치를 적용하고 main에 merge하는 과정이 tutorial 챕터에 단계별로 문서화되어 있다
  2. feature worktree를 updated main에 rebase하는 과정이 실행 가능한 명령어와 함께 문서화되어 있다
  3. worktree cleanup 가이드 (lifecycle 전체 마무리)가 tutorial에 포함되어 있다
**Plans**: TBD

Plans:
- [ ] 04-01: Tutorial chapter 04 — Hotfix parallel + worktree cleanup (TUT3-01 to TUT3-03, TUTC-04)

### Phase 5: CI/CD Integration (Scenario 4)
**Goal**: 독자가 GitHub Actions matrix strategy로 per-module 병렬 빌드를 실제 CI 환경에서 동작시킬 수 있다
**Depends on**: Phase 4
**Requirements**: TUT4-01, TUT4-02, TUT4-03
**Success Criteria** (what must be TRUE):
  1. GitHub Actions workflow 파일이 repository에 존재하고 matrix strategy로 각 모듈을 독립적으로 빌드한다
  2. CI에서 worktree cleanup이 자동으로 수행되는 step이 workflow에 포함되어 있다
  3. tutorial 챕터가 GitHub Actions YAML을 line-by-line으로 설명한다
**Plans**: TBD

Plans:
- [ ] 05-01: GitHub Actions workflow + tutorial chapter 05 — CI/CD integration (TUT4-01 to TUT4-03)

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Foundation | 0/2 | Not started | - |
| 2. Parallel Modules (Scenario 1) | 0/3 | Not started | - |
| 3. Merge + Conflict Resolution (Scenario 2) | 0/2 | Not started | - |
| 4. Hotfix Parallel (Scenario 3) | 0/1 | Not started | - |
| 5. CI/CD Integration (Scenario 4) | 0/1 | Not started | - |
