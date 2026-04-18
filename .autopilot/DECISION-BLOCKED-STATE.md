# DECISION: Active task "blocked on operator" 처리 규칙 (B10 대체 경로)

- **Status**: Proposed (non-protected landing · 본 규칙을 `.autopilot/PROMPT.md`
  mutable 섹션에 배선하려면 protected PR 필요 → 운영자 승인 후 후속 iter 에서 수행)
- **Date**: 2026-04-19 (iter58)
- **Owner**: autopilot brainstorm (iter50 B10)

## 1. 왜 필요한가

현재 `.autopilot/PROMPT.md` IMMUTABLE 섹션은 HALT 규칙(`.autopilot/HALT` 파일
존재 시 중단)은 있지만, "active task 가 운영자 결정을 기다리는 상태로 N iter 지속"
케이스에 대한 자동 전환 규칙이 없습니다. 지금까지 iter25~iter49 동안 `.autopilot/G1-*`
작업이 3 iter 이상 대기하면 로봇이 암묵적으로 Brainstorm/Idle-upkeep 모드로 빠졌는데,
이는 규칙이 아니라 ad-hoc 판단에 의존 — 재현성/투명성 둘 다 낮음.

## 2. 제안 규칙 (PROMPT.md mutable 섹션 추가 대상)

> **R-BLOCKED**: `active_task` 가 `status: active` 이지만 STATE.md
> `operator_requests` 또는 active_task 메모에 "blocked on operator" 표기가
> 있는 경우:
>
> 1. **블록 발견 iter+1 ~ iter+2**: Active 모드 유지, 폴링/부속 조사만 수행.
> 2. **블록 지속 iter+3 ~ iter+4**: **Brainstorm 모드 자동 전환** — BACKLOG 재스코어링,
>    대체 경로 착수(operator 결정 없이 진행 가능한 후속 작업).
> 3. **블록 지속 iter+5 이상**: **Idle-upkeep 모드** — `idle_upkeep_streak` 카운트 증가,
>    코드 무변경 정책. 매 iter 폴링 1회 + 문서 정리 1건 이상.
> 4. **블록 지속 iter+10 이상**: `status: halted` 로 전환 + `.autopilot/LAST_HALT_NOTE`
>    기록, ScheduleWakeup 중지 대기(운영자 수동 재개).

## 3. 실제 적용 예 (회고 검증)

- **iter25~iter32**: G3 → G5 사이 operator_requests 3건 대기. 현 규칙이면
  iter27 부터 Brainstorm, iter29 부터 Idle-upkeep. 실제로는 G5 계획 수립 후 자동
  진행 — **규칙과 실제 행동 일치**.
- **iter50~iter53**: G1 operator-blocked + MVP 7/8 완주 직후. 현 규칙이면
  iter50 부터 Idle-upkeep(블록 +1), 실제 BACKLOG 재스코어링 — **일치**.
- **iter54~iter57**: PR #51 review-wait + B13.1 병행 개선. 리뷰 대기는
  blocked-on-operator 이지만 차단 사유가 명확(정책) → 이 규칙 적용 대상 아님.
  별도 R-REVIEW 규칙 필요 (본 문서 §5 참조).

## 4. 기대 효과

- 판단 ad-hoc 제거, 로그만으로 재현 가능.
- 운영자가 언제부터 로봇이 "대기 → 우회 → 정지" 모드로 전환할지 예측 가능.
- `idle_upkeep_streak` 이 이미 METRICS 에 기록 중 → 규칙의 객관적 측정 완료.

## 5. 후속 (별도 제안)

- **R-REVIEW**: PR 리뷰 대기는 blocked-on-operator 이지만 CI 그린 + 자동 머지 규칙
  미적용(protected_paths) 인 경우 별도 분류. 폴링 상한 10회(약 10분) 후 idle-upkeep
  병행, 24회(약 24분) 후 다음 작업으로 이동. 현재 iter54~ 에서 이 모드로 운영 중.
- **R-DRIFT**: active_task 가 status=active 인데 3 iter 연속 STATE/대시보드/METRICS
  외 파일 변경이 전혀 없는 경우 drift 경고. (B10 원안에는 없던 추가 룰.)

## 6. 운영자 응답

이 문서는 규칙안만 담고 있으며 `.autopilot/PROMPT.md` 실제 수정은 별도 PR 필요.
응답 양식(PR 댓글 또는 본 파일 직접 수정):

```
R-BLOCKED: (승인 / 수정요청 / 보류)
R-REVIEW: (승인 / 수정요청 / 보류)
R-DRIFT: (승인 / 수정요청 / 보류)
```

승인 도착 시 iter59+ 에서 `.autopilot/PROMPT.md` PR 로 배선.

---

*작성: iter58 (2026-04-19) · 근거: `.autopilot/BACKLOG.md` B10 · iter25/32/50~57 회고*
