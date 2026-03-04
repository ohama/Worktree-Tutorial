---
phase: 02-parallel-modules
plan: "02-01"
subsystem: api
tags: [fsharp, giraffe, expecto, rest-api, users-crud, fsharp-systemtextjson]

# Dependency graph
requires:
  - phase: 01-foundation
    provides: Core.fs with UserId/ApiResponse types, Program.fs scaffold, WorktreeApi.fsproj base
provides:
  - Users CRUD module (Domain.fs + Handlers.fs) with full HTTP handlers
  - FsharpFriendlySerializer registration with PropertyNameCaseInsensitive=true
  - Expecto test infrastructure (test project + 6 passing domain tests)
  - /api/users endpoints: GET list, GET by id, POST create, PUT update, DELETE delete
affects:
  - 02-02 (Products module — same Domain.fs + Handlers.fs pattern)
  - 02-03 (tutorial chapter — JSON output examples now accurate)
  - 03-merge-conflicts (merge pattern established for zone-comment approach)

# Tech tracking
tech-stack:
  added:
    - Expecto 10.2.3 (F# test framework)
    - YoloDev.Expecto.TestSdk 0.15.5 (dotnet test bridge)
    - Microsoft.NET.Test.Sdk 17.12.0 (test runner infrastructure)
    - FsharpFriendlySerializer with PropertyNameCaseInsensitive (already in Giraffe, now configured)
  patterns:
    - Domain.fs pattern: namespace + module Domain + ConcurrentDictionary store + pure functions returning Result/option
    - Handlers.fs pattern: HTTP adapters with ctx.SetStatusCode BEFORE json body + exported routes value
    - FsharpFriendlySerializer with PropertyNameCaseInsensitive=true for case-insensitive JSON deserialization
    - Zone-comment fsproj structure: Domain.fs before Handlers.fs within module
    - Expecto test structure: [<Tests>] in test files, single [<EntryPoint>] in TestMain.fs

key-files:
  created:
    - src/Users/Domain.fs
    - src/Users/Handlers.fs
    - tests/WorktreeApi.Tests.fsproj
    - tests/UsersTests.fs
    - tests/TestMain.fs
  modified:
    - src/WorktreeApi.fsproj
    - src/Program.fs

key-decisions:
  - "FsharpFriendlySerializer requires PropertyNameCaseInsensitive=true to accept lowercase JSON fields (name/email/role) mapped to F# record fields (Name/Email/Role)"
  - "FsharpFriendlySerializer constructor takes JsonFSharpOptions option AND JsonSerializerOptions option as separate parameters"
  - "Test project duplicates parseRole logic rather than ProjectReference src/ — simpler build, acceptable for tutorial"
  - "Role DU serializes as {Case: Admin} with default JsonFSharpOptions — tutorial output examples need updating"

patterns-established:
  - "Pattern: Domain.fs (types + private ConcurrentDictionary + pure functions) + Handlers.fs (HTTP adapters + routes)"
  - "Pattern: ctx.SetStatusCode N then return! json body (status before body — mandatory HTTP ordering)"
  - "Pattern: subRoute /api/X + choose [GET >=> choose [routef /%O getById; route getAll]; POST; PUT; DELETE]"
  - "Pattern: Expecto tests — [<Tests>] value per module, TestMain.fs with single [<EntryPoint>] combining all"

# Metrics
duration: 3min
completed: 2026-03-05
---

# Phase 2 Plan 01: Users Module + Expecto Tests Summary

**Users CRUD module (Domain.fs + Handlers.fs) with FsharpFriendlySerializer and 6-test Expecto project on net10.0**

## Performance

- **Duration:** 3 min
- **Started:** 2026-03-04T22:38:18Z
- **Completed:** 2026-03-04T22:41:33Z
- **Tasks:** 3
- **Files modified:** 7

## Accomplishments

- Full Users CRUD module: 5 HTTP endpoints (GET list, GET by id, POST create, PUT update, DELETE) with correct status codes (200/201/204/400/404)
- FsharpFriendlySerializer registered with PropertyNameCaseInsensitive=true — enables lowercase JSON requests to map to F# record fields
- Expecto test infrastructure with 6 passing unit tests covering parseRole function

## Task Commits

Each task was committed atomically:

1. **Task 1: Create Users module (Domain.fs + Handlers.fs) and update .fsproj** - `9feeb2b` (feat)
2. **Task 2: Update Program.fs — FsharpFriendlySerializer + Users routes** - `2bb419e` (feat)
3. **Task 3: Create Expecto test project with UsersTests** - `4f23216` (feat)

## Files Created/Modified

- `src/Users/Domain.fs` - Role DU, User record, ConcurrentDictionary store, parseRole + 4 CRUD functions
- `src/Users/Handlers.fs` - 5 HTTP handlers with correct status codes, exported routes value
- `src/WorktreeApi.fsproj` - Users/Domain.fs and Users/Handlers.fs added in Users zone
- `src/Program.fs` - FsharpFriendlySerializer with PropertyNameCaseInsensitive=true + Users.Handlers.routes wired
- `tests/WorktreeApi.Tests.fsproj` - Expecto 10.2.3 + YoloDev 0.15.5 + Microsoft.NET.Test.Sdk 17.12.0
- `tests/UsersTests.fs` - 6 parseRole tests (admin/Admin/member/guest/superuser/empty), no [<EntryPoint>]
- `tests/TestMain.fs` - single [<EntryPoint>] combining all test lists

## Decisions Made

1. **PropertyNameCaseInsensitive=true required:** The plan specified `FsharpFriendlySerializer(JsonFSharpOptions.Default())` but this fails at runtime because `ctx.BindJsonAsync` uses the configured serializer which by default is case-sensitive. F# record fields are PascalCase (Name, Email, Role) but JSON requests use lowercase. Passing a `JsonSerializerOptions(PropertyNameCaseInsensitive = true)` as second constructor argument fixes this. The `FsharpFriendlySerializer` constructor signature is `fsharpOptions: JsonFSharpOptions option * jsonOptions: JsonSerializerOptions option`.

2. **Test project duplicates parseRole:** The plan specified tests that duplicate domain logic rather than referencing src/. This avoids cross-project build complexity and keeps the tutorial simple. Acceptable for unit testing pure functions.

3. **Role serializes as `{"Case":"Admin"}`:** With `JsonFSharpOptions.Default()`, fieldless DUs serialize as `{"Case":"Admin"}` not `"Admin"`. The tutorial chapter JSON output examples show `{"case":"UserId","fields":["..."]}` which is also incorrect for single-case DUs (those unwrap to the raw Guid string). Tutorial output examples need updating in plan 02-03.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Added PropertyNameCaseInsensitive=true to FsharpFriendlySerializer**

- **Found during:** Task 2 (smoke test of POST /api/users)
- **Issue:** Plan specified `FsharpFriendlySerializer(JsonFSharpOptions.Default())` but this caused runtime exception: `JsonException: Missing field for record type WorktreeApi.Users.Domain+CreateUserRequest: Name` because JSON body uses lowercase fields and F# record fields are PascalCase
- **Fix:** Changed registration to pass `JsonSerializerOptions(PropertyNameCaseInsensitive = true)` as second argument: `Giraffe.Json.FsharpFriendlySerializer(JsonFSharpOptions.Default(), jsonOpts)`; also added `open System.Text.Json` to opens
- **Files modified:** src/Program.fs
- **Verification:** POST /api/users with `{"name":"Alice","email":"alice@example.com","role":"admin"}` returns 201 with user JSON
- **Committed in:** `2bb419e` (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 — bug fix)
**Impact on plan:** Required fix for API to function. The plan's exact `FsharpFriendlySerializer(JsonFSharpOptions.Default())` pattern doesn't handle case mismatch between JSON and F# naming conventions.

## Issues Encountered

Port 5000 was in use during smoke testing. Used port 5099 for verification. No impact on implementation.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Users module complete and verified: all 5 endpoints tested live (201/200/400/404)
- Expecto infrastructure ready: can add ProductsTests.fs to test project in plan 02-02
- Pattern established for Products module: same Domain.fs + Handlers.fs structure
- Concern: Tutorial chapter 02's JSON output examples show incorrect format (`{"case":"UserId","fields":[...]}`) — actual output is `{"Id":"<guid-string>","Role":{"Case":"Admin"},...}`. Should be corrected when tutorial chapter is updated (plan 02-03 or 02-04).

---
*Phase: 02-parallel-modules*
*Completed: 2026-03-05*
