# Task-To-Do — Grid Attribute 시스템 (속성: 불/물/풀/번개)

요약
- 목적: 8x8 그리드의 행/열에 속성을 부여하여 줄 클리어 시 추가 효과(데미지·실드·회복·곱연산)를 적용한다.
- 우선순위: 기능적 안정성 및 가시성 확보 → 로그/텔레메트리로 관측 → 밸런스 조정(패치).

설계 전제(결정사항 요약)
- 블록당 추가 대미지 = 1 (cap 없음).  
- 물: 그리드에 존재하는 모든 폭탄의 타이머 += 1 (단, 각 폭탄의 maxTimer = 6).  
  실드 = waterBlocks * 2, 지속 5턴, 실드 총합 cap = 20, 획득 시 remainingTurns = 5로 갱신.  
- 풀: 회복 = grassBlocks * 2 (밸런스 추후 조정).  
- 번개: 전역 조건, 모든 합산 대미지에 대해 최종적으로 곱연산 적용(x2). (한 턴에 한 번만 적용)  
- 데미지 계산 시 정해진 처리 순서 준수(문서화 후 코드로 강제).

구성(큰 카테고리)
1. 핵심 시스템 구현
2. 데미지 계산 모듈
3. 폭탄 타이머 처리
4. 실드 시스템
5. 번개 곱연산 처리
6. UI / UX
7. 이벤트/API 계약
8. 테스트·시뮬레이션·텔레메트리
9. 문서화·릴리즈 계획
10. 리스크 및 대응

세부 작업 목록

1) 핵심 시스템 구현 (Priority: High)
- [ ] Attribute enum 정의: { None, Fire, Water, Grass, Lightning }  
- [ ] GridAttributeMap: 행(0..7) / 열(0..7)에 각 속성 할당 저장 구조 구현.
- [ ] Grid 초기화 / 에디터 지원(속성 편집용 데이터 소스).

2) 데미지 계산 모듈 (Priority: High)
- [ ] DamageCalculator 클래스 생성(순수 로직, 유닛테스트 대상).
  - 입력: clearedLines(리스트), 각 줄의 블록별 속성 카운트, 장비/유물 보정, 현재 폭탄 존재 여부/수.
  - 출력: DamageBreakdown { base, additivePerLine, equipmentAdd, preLightningSum, lightningApplied(bool), finalDamage }.
- [ ] 처리 순서 구현:
  1) 줄 클리어 판정 수집(동시 삭제 모두)  
  2) 각 줄별 base(10) + additive(불 등) 계산 → 합산  
  3) 장비/슬롯 효과 적용(additive)  
  4) 번개 곱(x2) 최종 적용(한 턴 한 번)  
  5) 실드 흡수 → 플레이어 HP 적용  
  6) 폭탄 해제 보상 처리
- [ ] 모든 스텝에서 디버그 로깅(원인별 수치 분해).

3) 폭탄 타이머 처리 (Priority: High)
- [ ] Bomb 클래스에 MaxTimer(예: 6) 필드 도입.
- [ ] 물 효과 적용 로직: 모든 Bomb.timer = Min(Bomb.timer + 1, Bomb.maxTimer).
- [ ] 적용 시점: 줄 클리어 이펙트 처리 중 즉시 적용(환경 tick 이전). 문서화.

4) 실드 시스템 (Priority: High)
- [ ] PlayerShieldManager 구현:
  - 필드: currentShield, remainingTurns, perTurnGainLimit(예: 8), totalCap = 20.
  - 메서드: AddShield(amount) → cap/perTurnLimit 적용, remainingTurns 갱신(=5).
  - 매턴 감소 및 UI 갱신 로직.
- [ ] 실드는 데미지 흡수형: DamageCalculator와 연동해 흡수량 계산.

5) 번개 곱연산 처리 (Priority: High)
- [ ] LightningRule: 한 턴에 복수 줄 번개가 있어도 곱은 단일(x2) 적용.
- [ ] DamageCalculator에서 lightningApplied 여부 판단(전역 탐색, 폭탄 존재 조건 등은 기획대로).
- [ ] 번개 발동 시 손패 처리(설계: 손패 전부 버리고 같은 매수만큼 드로우) — CardManager 연동.

6) UI / UX (Priority: High)
- [ ] UIManager.cs 변경:
  - 이벤트 구독 추가: OnShieldChanged(int amount, int remainingTurns), OnDamageBreakdown(DamageBreakdown), OnLineAttributeTriggered(LineAttributeInfo).
  - 필드 추가: TextMeshProUGUI shieldText, Image shieldBar, Animation 번개Effect, Tooltip 라인속성.
- [ ] Grid UI: 각 행/열에 속성 아이콘 표시(툴팁 포함).
- [ ] Line clear 플로팅 텍스트: +X DMG / +Y Shield / +Z Heal 등.
- [ ] 개발용 디버그 로그 패널(토글 가능): 계산 스텝별 수치 노출.

7) 이벤트 / API 계약 (Priority: High)
- [ ] Define events (패턴: 기존 TurnManager 이벤트와 동일):
  - TurnManager.OnTurnAdvanced(int turn)
  - PlayerStats.OnShieldChanged(int totalShield, int remainingTurns)
  - CombatManager.OnDamageCalculationResolved(DamageBreakdown)
  - Grid.OnLineCleared(List<LineClearInfo>)
- [ ] 계약 문서(README 섹션)에 각 이벤트의 사용법·타이밍 명시.

8) 테스트·시뮬레이션·텔레메트리 (Priority: High)
- [ ] Unit tests: DamageCalculator 각 단계(단일 줄, 다중 줄, 실드 흡수, 번개 적용 등).
- [ ] Integration test: 폭탄 타이머 연기 시나리오, 실드 갱신 시나리오, 손패 드로우 시나리오.
- [ ] 시뮬레이션: 랜덤 시드 기반 100k 턴 스트레스 시뮬레이터(극단값 탐지). 초기에는 로컬 스크립트로 실행.
- [ ] Telemetry events 설계: AttributeTrigger, DamageEvent(분해형), ShieldEvent, LightningEvent. (로그 포맷 정의)

9) 플레이테스트, 밸런스, 롤아웃 (Priority: Medium)
- [ ] 내부 플레이테스트 체크리스트 작성(시나리오별 기대값).
- [ ] Soft-cap(운영용) 기능 구현: 한 턴 데미지 soft cap, per-turn 실드 획득 limit — 기본은 로그 모드로 시작.
- [ ] 단계적 롤아웃 계획: 내부 → 소규모 외부 테스트 → 전체.

10) 문서화·코드 주석 (Priority: High)
- [ ] Overview 문서에 처리 순서, 중첩 규칙, UI 표기법, 샘플 계산 예시 추가(기획서 보완).
- [ ] 각 핵심 클래스에 요약 주석 및 경계 조건 명시.

11) 리스크 및 완화 (Priority: High)
- [ ] 로그/텔레메트리: 초대형 수치 발생 시 자동 경고(로그 수준 상승).  
- [ ] 운영 파라미터: 번개 multiplier, per-turn limits 등은 런타임에서 조정 가능하도록 설계(데이터테이블/ScriptableObject).  
- [ ] UI 부족 시 플레이어 혼란 발생 — 최소한 실드/번개/라인속성 시각화는 반드시 동시 제공.

책임자(예: 소유자) & 추정
- 설계 문서 작성: 기획자 (1d)  
- Core logic(DamageCalculator, Bomb handling, ShieldManager): 개발자 A (3d)  
- UI 변경(UIManager, Grid icons, Floating texts): 개발자 B (2d)  
- CardManager 연동(번개 손패 처리): 개발자 C (1d)  
- 테스트·시뮬레이션 스크립트: QA / 개발자 D (2d)  
- 플레이테스트 + 데이터수집: 기획자 + QA (3d)  
(추정은 소규모 팀·프로토타입 기준)

Acceptance Criteria (간단)
- 모든 줄 클리어에 대해 DamageBreakdown 로그가 생성되고 UI에 요약 노출될 것.  
- 물 속성 발동 시 모든 폭탄의 timer가 +1(단 maxTimer까지) 적용되고 UI에 변화가 표시될 것.  
- 실드가 cap 이하로 정확히 누적·갱신되며 남은 턴이 UI에 표시될 것.  
- 번개 발동 시 최종 대미지 x2 적용 및 손패 처리(버림→드로우) 연동이 정상 동작할 것.  
- 단위 테스트 통과 및 시뮬레이션에서 극단값 경고 로그가 정상 동작할 것.

우선순위(간단)
- 당장 구현: DamageCalculator, Bomb timer update, ShieldManager, UI 기초(실드 표시, 라인속성 아이콘), 이벤트 정의.  
- 다음: 번개 손패 연동, 시뮬레이션·텔레메트리, soft-cap 토글.  
- 이후: 밸런스 튜닝, 고급 UX(애니메이션, 튜토리얼 등).

파일 변경 제안(참고)
- Assets/Scripts/Combat/DamageCalculator.cs (신규)  
- Assets/Scripts/Game/Bomb.cs (수정: maxTimer)  
- Assets/Scripts/Player/PlayerShieldManager.cs (신규)  
- Assets/Scripts/UI/UIManager.cs (수정: 이벤트/필드 추가)  
- Assets/Scripts/Card/CardManager.cs (수정: 번개 손패 처리)  
- Tests/Unit/DamageCalculatorTests.cs (신규)  
- Tools/Simulators/CombatSimulator.cs (신규)

마무리
- 첫 단계로 DamageCalculator와 PlayerShieldManager의 인터페이스(메서드 시그니처)와 이벤트 계약을 확정하면 구현·테스트 작업을 병렬로 시작할 수 있습니다.