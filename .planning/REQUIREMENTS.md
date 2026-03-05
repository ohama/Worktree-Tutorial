# Requirements: Claude Code Worktree 병렬 개발 튜토리얼

**Defined:** 2026-03-04
**Core Value:** worktree 병렬 개발이 순차 개발보다 얼마나 효율적인지 실제 코드와 함께 체감하게 만드는 것

## v1 Requirements

### Foundation

- [ ] **FOUND-01**: F# Giraffe 프로젝트 scaffold 생성 (.NET 9.0, Giraffe 8.2.0)
- [ ] **FOUND-02**: Core.fs에 공유 타입 정의 (UserId, ProductId, OrderId, ApiResponse)
- [ ] **FOUND-03**: `.fsproj` 컴파일 순서 zone 주석으로 구분
- [ ] **FOUND-04**: Fantomas 7.0.5 dotnet local tool 설정
- [ ] **FOUND-05**: Skeleton Program.fs with route composition point

### Users Module

- [ ] **USER-01**: Users CRUD endpoints (`/api/users`)
- [ ] **USER-02**: Role discriminated union 필드 포함
- [ ] **USER-03**: In-memory store (모듈 내부)
- [ ] **USER-04**: HTTP status codes (200, 201, 204, 400, 404)

### Products Module

- [ ] **PROD-01**: Products CRUD endpoints (`/api/products`)
- [ ] **PROD-02**: Stock field 포함
- [ ] **PROD-03**: In-memory store (모듈 내부)
- [ ] **PROD-04**: HTTP status codes (200, 201, 204, 400, 404)

### Orders Module

- [ ] **ORDR-01**: Orders CRUD endpoints (`/api/orders`)
- [ ] **ORDR-02**: Users/Products ID 참조 (값이 아닌 ID만)
- [ ] **ORDR-03**: In-memory store (모듈 내부)
- [ ] **ORDR-04**: HTTP status codes (200, 201, 204, 400, 404)

### Testing

- [ ] **TEST-01**: Expecto 테스트 프레임워크 설정
- [ ] **TEST-02**: Users 모듈 단위 테스트
- [ ] **TEST-03**: Products 모듈 단위 테스트
- [ ] **TEST-04**: Orders 모듈 단위 테스트

### Tutorial — Scenario 1: Parallel Development

- [ ] **TUT1-01**: git worktree lifecycle 설명 (add/list/remove/prune)
- [ ] **TUT1-02**: `claude --worktree` 플래그 사용법 walkthrough
- [ ] **TUT1-03**: 3개 터미널 병렬 세션 데모 (main + users-worktree + products-worktree)
- [ ] **TUT1-04**: Clean merge 워크플로우 (fast-forward)
- [ ] **TUT1-05**: 순차 vs 병렬 효율성 비교

### Tutorial — Scenario 2: Merge + Conflict Resolution

- [ ] **TUT2-01**: 의도적 Core.fs 충돌 시나리오 설계
- [ ] **TUT2-02**: merge conflict 해결 step-by-step walkthrough
- [ ] **TUT2-03**: Program.fs route composition 충돌 해결
- [ ] **TUT2-04**: Orders 모듈 통합 과정

### Tutorial — Scenario 3: Hotfix Parallel

- [ ] **TUT3-01**: main에서 hotfix worktree 생성 패턴
- [ ] **TUT3-02**: feature worktree 작업 중단 없이 hotfix 적용
- [ ] **TUT3-03**: feature branch를 updated main에 rebase

### Tutorial — Scenario 4: CI/CD Integration

- [ ] **TUT4-01**: GitHub Actions workflow 파일 작성
- [ ] **TUT4-02**: per-module matrix strategy로 병렬 빌드
- [ ] **TUT4-03**: worktree cleanup in CI

### Tutorial — Common

- [ ] **TUTC-01**: tutorial/ 디렉토리에 번호별 Markdown 챕터 구성
- [ ] **TUTC-02**: README.md index 파일
- [ ] **TUTC-03**: 한영 혼용 스타일 (설명: 한국어, 코드/명령어: 영어)
- [ ] **TUTC-04**: worktree cleanup 가이드 (전체 lifecycle 마무리)
- [ ] **TUTC-05**: 각 챕터에 break-and-fix 연습 포함

## v2 Requirements

### Advanced Tutorial Content

- **ADV-01**: `isolation: worktree` subagent frontmatter 예제
- **ADV-02**: bare repository 패턴 vs regular clone 비교
- **ADV-03**: 한국어 localization 품질 개선

### Extended API Features

- **EXT-01**: Integration 테스트 (TestHost 기반)
- **EXT-02**: 에러 핸들링 미들웨어

## Out of Scope

| Feature | Reason |
|---------|--------|
| Real database (PostgreSQL/SQLite) | worktree 패턴에 집중, DB 설정 복잡도 제거 |
| Authentication/JWT | Users를 모든 모듈에 coupling, 독립성 파괴 |
| Docker/컨테이너화 | 배포는 튜토리얼 범위 밖 |
| Shopping Cart 모듈 | Users+Products에 동시 의존, 독립 개발 불가 |
| 프로덕션 배포 | 튜토리얼이므로 실제 배포 불필요 |
| F# 언어 입문 | 독자는 기본 F# 이해 전제 |
| GSD 워크플로우 통합 | 별도 주제, 이 튜토리얼의 범위 밖 |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| FOUND-01 | Phase 1 | Complete |
| FOUND-02 | Phase 1 | Complete |
| FOUND-03 | Phase 1 | Complete |
| FOUND-04 | Phase 1 | Complete |
| FOUND-05 | Phase 1 | Complete |
| USER-01 | Phase 2 | Complete |
| USER-02 | Phase 2 | Complete |
| USER-03 | Phase 2 | Complete |
| USER-04 | Phase 2 | Complete |
| PROD-01 | Phase 2 | Complete |
| PROD-02 | Phase 2 | Complete |
| PROD-03 | Phase 2 | Complete |
| PROD-04 | Phase 2 | Complete |
| ORDR-01 | Phase 3 | Complete |
| ORDR-02 | Phase 3 | Complete |
| ORDR-03 | Phase 3 | Complete |
| ORDR-04 | Phase 3 | Complete |
| TEST-01 | Phase 2 | Complete |
| TEST-02 | Phase 2 | Complete |
| TEST-03 | Phase 2 | Complete |
| TEST-04 | Phase 3 | Complete |
| TUT1-01 | Phase 1 | Complete |
| TUT1-02 | Phase 1 | Complete |
| TUT1-03 | Phase 2 | Complete |
| TUT1-04 | Phase 2 | Complete |
| TUT1-05 | Phase 2 | Complete |
| TUT2-01 | Phase 3 | Complete |
| TUT2-02 | Phase 3 | Complete |
| TUT2-03 | Phase 3 | Complete |
| TUT2-04 | Phase 3 | Complete |
| TUT3-01 | Phase 4 | Pending |
| TUT3-02 | Phase 4 | Pending |
| TUT3-03 | Phase 4 | Pending |
| TUT4-01 | Phase 5 | Pending |
| TUT4-02 | Phase 5 | Pending |
| TUT4-03 | Phase 5 | Pending |
| TUTC-01 | Phase 1 | Complete |
| TUTC-02 | Phase 1 | Complete |
| TUTC-03 | Phase 1 | Complete |
| TUTC-04 | Phase 4 | Pending |
| TUTC-05 | Phase 2 | Complete |

**Coverage:**
- v1 requirements: 40 total
- Mapped to phases: 40
- Unmapped: 0 ✓

---
*Requirements defined: 2026-03-04*
*Last updated: 2026-03-04 after roadmap creation — traceability confirmed*
