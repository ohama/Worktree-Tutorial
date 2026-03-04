# Claude Code Worktree 병렬 개발 튜토리얼

## What This Is

Claude Code와 git worktree를 활용한 병렬 개발 패턴을 가르치는 실전 튜토리얼. F# REST API 프로젝트를 예제로 사용하여, 독립적인 도메인 모듈을 여러 worktree에서 동시에 개발하고 merge하는 전체 워크플로우를 다룬다. 대상 독자는 Claude Code를 이미 사용하고 있지만 worktree 병렬 패턴을 모르는 개발자.

## Core Value

"worktree 병렬 개발이 순차 개발보다 얼마나 효율적인지"를 실제 코드와 함께 체감하게 만드는 것.

## Requirements

### Validated

(None yet — ship to validate)

### Active

- [ ] 튜토리얼 문서를 `tutorial/`에 한영 혼용 Markdown으로 작성
- [ ] F# REST API 예제 프로젝트를 `src/`에 작성 (Giraffe 프레임워크)
- [ ] 독립 도메인 모듈 (Users/Products/Orders)을 병렬 worktree로 개발하는 패턴 시연
- [ ] `claude --worktree` 내장 기능 사용법 설명
- [ ] 여러 터미널에서 Claude Code 병렬 세션 관리 방법 설명
- [ ] worktree 간 merge 및 충돌 해결 과정 가이드
- [ ] main에서 hotfix하면서 feature worktree도 병행하는 시나리오
- [ ] worktree 별 테스트/빌드를 동시에 실행하는 CI/CD 연동 패턴
- [ ] 공통 기반(Core types, DB 설정, API 프레임워크) → fan-out(도메인 모듈 병렬) → merge-back 흐름 구현

### Out of Scope

- 프로덕션 배포 — 튜토리얼이므로 실제 배포는 불필요
- F# 언어 자체 입문 — 독자는 기본적인 F# 이해가 있다고 가정
- Claude Code 설치/초기 설정 — 이미 사용 중인 독자 대상
- GSD 워크플로우 통합 — `/gsd` 명령어 활용은 별도 주제

## Context

- Git worktree는 하나의 repo에서 여러 브랜치를 동시에 체크아웃할 수 있게 해주는 기능. 각 worktree는 독립된 작업 디렉토리를 가짐
- Claude Code는 `claude --worktree` 플래그로 worktree 생성/관리를 지원
- F# Giraffe 프레임워크는 ASP.NET Core 위에서 동작하는 함수형 웹 프레임워크
- REST API의 도메인 모듈(Users/Products/Orders)은 서로 독립적이어서 병렬 개발에 이상적
- 튜토리얼 문서는 한국어 설명 + 영어 코드/명령어의 한영 혼용 스타일

## Constraints

- **언어**: F# — 프로젝트 코드는 반드시 F#으로 작성
- **프레임워크**: Giraffe — F# 함수형 웹 프레임워크 (ASP.NET Core 기반)
- **디렉토리 구조**: 튜토리얼은 `tutorial/`, 예제 코드는 `src/`
- **문서 형식**: Markdown 파일, 한영 혼용
- **독자 수준**: Claude Code 사용 경험 있음, worktree는 처음

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| F# + Giraffe 선택 | 사용자 요구사항 (F# 필수), Giraffe는 F# 생태계에서 가장 성숙한 웹 프레임워크 | — Pending |
| REST API 도메인 분리 (Users/Products/Orders) | 각 도메인이 독립적이어서 worktree 병렬 개발의 이점을 명확히 보여줌 | — Pending |
| 4가지 시나리오 구성 (병렬개발/merge/hotfix병행/CI) | worktree 활용의 전체 스펙트럼을 커버하면서도 실무에서 가장 자주 쓰이는 패턴 | — Pending |

---
*Last updated: 2026-03-04 after initialization*
