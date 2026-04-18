# ADR — `tools/run-smoke.ps1` 필요 여부 (B14)

- **Status**: Proposed (operator decision pending)
- **Date**: 2026-04-19 (iter52)
- **Context owner**: autopilot / idle-upkeep brainstorm B14
- **Supersedes**: BACKLOG B8 (원안 — "Headless smoke harness")

## 1. 배경 한 줄

MVP 7/8 달성 시점에 xunit 기반 e2e(G4/G5/G6/G7/G8) 가 이미 브로커 라운드트립·로테이션·dedup 까지
모두 커버하므로, 원안 B8("`tools/run-smoke.ps1` PowerShell smoke harness") 이 현재도 필요한지
재평가가 필요.

## 2. 검토 사실

| 질문 | 확인 결과 |
|------|----------|
| 브로커 routing 왕복이 자동 테스트로 검증되는가? | ✅ `BrokerRoutingRoundTripE2ETests` (iter44, 2 facts) |
| recovery_resume 프리앰블 주입이 검증되는가? | ✅ `BrokerRecoveryResumeE2ETests` (iter45, 2 facts) |
| 요약 파일 생성 + carry-forward 주입이 검증되는가? | ✅ `BrokerRotationSmokeE2ETests` (iter46, 1 fact) |
| 합의 수렴 이벤트·파일이 검증되는가? | ✅ `BrokerConvergenceE2ETests` (iter42, 2 facts) |
| replay-dedup + crash-survival 이 검증되는가? | ✅ `BrokerReplayDedupE2ETests` (iter49, 2 facts) |
| CWD 경합은 어떻게 해결? | ✅ `BrokerCwdMutatingCollection`(DisableParallelization=true) |
| CI 에서 전체 돌리는 법 | `dotnet test` 한 줄(81/81) |

## 3. 선택지

### Option A — 얇은 래퍼(thin `dotnet test` wrapper)

- 구현: `tools/run-smoke.ps1` 을 5 LOC 스크립트로 — `dotnet test --filter "FullyQualifiedName~E2E"` 실행.
- 장점: CI/로컬 공통 진입점 확보. 신규 e2e 추가해도 자동 포함(필터 매칭).
- 단점: `tools/` 는 `protected_paths` → 관리자 PR 리뷰 1회 필요.
- 비용: 1 iter, PR 1건(한국어, 단순).

### Option B — 원안 B8 재도입(full PowerShell harness)

- 구현: in-memory fixtures → 완전한 round-trip → PS1 내부에서 검증 assertion 구현(100+ LOC).
- 장점: .NET 없이도 smoke 가능(이론).
- 단점: xunit 과 중복, 유지보수 비용 2배, PS↔C# 불일치 위험.
- 비용: 2~3 iter + 관리자 PR 리뷰.

### Option C — 드랍(do nothing)

- 구현: B8/B14 취소. `dotnet test` 가 진실의 근원.
- 장점: 즉시, 중복 0.
- 단점: CLI 단축키 없음 — 운영자가 `dotnet test` 직접 입력.
- 비용: 0 iter.

## 4. 권고

**Option A (얇은 래퍼)** 를 기본 권고.

- 근거: xunit 이 이미 사실상 smoke 를 대체했고, 5 LOC 래퍼는 운영자 편의(한 줄 명령) + CI 진입점 통일
  효과만 얻으면서 중복 구현 리스크 없음. Option B 는 과잉, Option C 는 향후 CI 파이프라인 붙일 때
  재작업 유발 가능.
- 단, `tools/` 가 protected 이므로 **관리자 승인 후** iter53 에서 착수.

## 5. 응답 양식 (관리자님)

이 파일이나 PR 댓글에 한 줄:

```
B14: A / B / C
```

승인 도착 즉시 로봇이 해당 Option 으로 이동. 답 없으면 Option C 로 간주하고 iter53+ 에서 B14 영구 닫음.

---

*작성: iter52 (2026-04-19) · 근거: iter42·44·45·46·49 테스트 파일 · `.autopilot/BACKLOG.md` B14*
