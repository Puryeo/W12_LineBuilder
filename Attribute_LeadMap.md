# Attribute_LeadMap — 그리드 속성 시스템 가이드 (등대 역할)

요약
- 목적: 8x8 그리드의 행/열 속성(불/물/풀/번개)을 기능·UI·로그·테스트 관점에서 일관되게 구현하여 기획 의도(폭발적 빌드 피크 포함)를 안전하게 검증·확장할 수 있게 한다.
- 핵심 원칙: 반복적(Iteration) 개발 → 사용자 검증 → 로그 관찰 → 수정 → 재검증. 각 Iteration 끝에 "구현 요약"과 "사용자가 해야 할 일"을 명확히 기록.

핵심 연락처(코드 오너)
- 시스템 설계 / 기획 검증: 기획자
- 코어 로직: Developer A
- UI: Developer B
- CardManager 연동: Developer C
- QA / 시뮬레이션: QA 팀

구성요소(개요)
- Attribute enum, GridAttributeMap (에디터 편집 가능)
- DamageCalculator (순수 로직 + 로그)
- Bomb (maxTimer)
- PlayerShieldManager
- LightningRule 및 CardManager 연동
- UIManager 확장 (실드, 라인속성, 데미지 분해)
- Telemetry / Simulator / Unit Tests

Iteration별 상세 작업 (각 Iteration은 구현 → 사용자가 검증 → 디버깅 → 재검증 → 승인)
- 각 Iteration 끝에 "간단 요약(1줄)"과 "사용자가 해야 할 일"을 제공.

Iteration 0 — 준비 (공통 인프라)
- 목표
  - 이벤트/DTO 스켈레톤 준비 및 공통 타입 선언.
- 작업
  - Create DTO: DamageBreakdown { int baseDamage, Dictionary<string,int> additivePerSource, int equipmentAdd, int preLightningSum, bool lightningApplied, int finalDamage }
  - 이벤트 선언(빈): OnLineCleared, OnDamageCalculationResolved, OnShieldChanged.
- 산출물
  - 파일: Assets/Scripts/Combat/DamageBreakdown.cs (DTO)
  - 파일: Assets/Scripts/Events/GameEvents.cs (빈 이벤트 delegates)
- 사용자가 해야 할 일
  - TestScene 생성, 이벤트 스텁 호출으로 로그 출력 확인.
- Acceptance
  - 빌드 통과, 빈 이벤트 구독/발행 시 오류 없음.

Iteration 1 — Attribute 데이터 및 에디터
- 목표
  - 행/열 속성 편집 가능(에디터)하도록 GridAttributeMap 완성.
- 작업
  - enum AttributeType { None, Fire, Water, Grass, Lightning }
  - GridAttributeMap: public AttributeType[] rows = new AttributeType[8]; public AttributeType[] cols = new AttributeType[8];
  - 에디터에서 저장/로드 가능, 직렬화.
- 산출물
  - 파일: Assets/Scripts/Grid/GridAttributeMap.cs
- 사용자가 해야 할 일
  - TestScene에서 Grid 오브젝트에 컴포넌트 부착 후 인스펙터에서 속성 할당 확인.
- Acceptance
  - 인스펙터 변경 → 플레이모드 진입 시 값 반영.

Iteration 2 — Bomb.maxTimer 및 Water 효과
- 목표
  - Bomb에 maxTimer 도입, Water 효과로 모든 폭탄 timer +=1 적용(캡).
- 작업
  - Bomb.cs: int timer; int maxTimer = 6;
  - WaterEffect 적용 메서드: ApplyWaterToAllBombs() -> for each bomb.timer = Min(bomb.timer+1, bomb.maxTimer)
  - 디버그 로그: "WaterEffect: Bomb[id] 2->3"
- 산출물
  - 파일: Assets/Scripts/Game/Bomb.cs (수정)
- 사용자가 해야 할 일
  - TestScene에 Bomb 배치, 물 라인 트리거하여 콘솔 로그 확인.
- Acceptance
  - 타이머가 cap 초과하지 않음, 로그 정상 출력.

Iteration 3 — PlayerShieldManager + UI 기본
- 목표
  - 실드 누적/갱신 로직, UI 표시(숫자 및 바).
- 작업
  - PlayerShieldManager:
    - fields: int currentShield, int remainingTurns, int perTurnGainLimit = 8, int totalCap = 20
    - methods: AddShield(int amount) // cap/perTurnLimit 적용, remainingTurns = 5
    - TickTurn() // remainingTurns--, if 0 -> currentShield=0
  - UIManager 확장: public TextMeshProUGUI shieldText; public Image shieldBar; subscribe OnShieldChanged.
  - Event: PlayerStats.OnShieldChanged(int totalShield, int remainingTurns)
- 사용자가 해야 할 일
  - Canvas에 shieldText/shieldBar 배치 후 UIManager에 연결.
  - 물 라인 트리거 시 UI가 업데이트되는지 확인.
- Acceptance
  - 실드 획득 시 UI 업데이트, per-turn limit 및 cap 동작 확인.

Iteration 4 — DamageCalculator (핵심 로직 + 로그)
- 목표
  - 모든 라인 클리어 → 분해된 데미지 계산(재현 가능 로그).
- 작업
  - DamageCalculator.Calculate(List<LineClearInfo> clearedLines, PlayerState, EquipmentState) : DamageBreakdown
  - 처리 순서(강제):
    1) collect clearedLines
    2) per-line base = 10 + (fireBlocks*1) // 또 다른 additive는 별도
    3) equipmentAdd (slot effects)
    4) preLightningSum = sum(...)
    5) if lightningApplied -> final = preLightningSum * 2
    6) apply shield absorption -> remainingDamage
    7) return DamageBreakdown, emit OnDamageCalculationResolved(DamageBreakdown)
  - 모든 단계에서 로그(원인별 값) 출력.
- 메서드 시그니처(권장)
  - public DamageBreakdown Calculate(LineClearInfo[] clears, PlayerState p, EquipmentState e)
- 사용자가 해야 할 일
  - TestScene에서 다양한 라인 조합을 설정하고 DamageBreakdown 로그와 기대값 비교.
  - 스크린샷/로그 제출.
- Acceptance
  - 로그가 각 스텝을 분해하여 출력, 값이 기획 수치와 일치.

Iteration 5 — LightningRule + CardManager 연동
- 목표
  - 번개 규칙 적용(한 턴 한 번 x2) 및 손패 처리(버림→동일 수치 드로우).
- 작업
  - LightningRule.DetermineApplied(clears, bombsPresent) -> bool lightningApplied
  - DamageCalculator honors lightningApplied flag and applies multiplier at final step.
  - CardManager: public void ReplaceHandWithDrawn(int count) -> discard currentHand, draw count
  - 번개 발동 시 CardManager.ReplaceHandWithDrawn(handSize);
- 사용자가 해야 할 일
  - 번개 케이스 시 데미지 로그 및 손패 변화 확인.
- Acceptance
  - lightningApplied=true 시 finalDamage is doubled; hand replaced accordingly.

Iteration 6 — 통합 시뮬레이터·테스트·텔레메트리
- 목표
  - 대량 시뮬레이션으로 극단값·빈도 파악, 단위/통합 테스트 통과.
- 작업
  - CombatSimulator: 랜덤 시드 기반 턴 시뮬레이터(설정: N 시뮬레이션, 로그 요약)
  - Unit tests: Tests/Unit/DamageCalculatorTests.cs
  - Telemetry Events: emit AttributeTrigger, DamageEvent(detail), ShieldEvent, LightningEvent
  - 경고 조건: finalDamage > softCap -> telemetry warn
- 사용자가 해야 할 일
  - 로컬에서 시뮬레이터 실행(초기 10k 턴), 로그 결과 업로드.
  - 발견된 이상치(예: percentiles 초과) 보고.
- Acceptance
  - 시뮬레이터 실행 가능, 주요 메트릭 수집 및 이상값 로깅.

Iteration 7 — 플레이테스트·밸런스·릴리즈 준비
- 목표
  - 소규모 외부 플레이테스트 → 파라미터 조정 → 릴리즈
- 작업
  - soft-cap runtime parameter 노출(ScriptableObject)
  - 플레이테스트 체크리스트 배포(시나리오 포함)
  - 문서화(Processing order, stacking rules, UI tooltip text)
- 사용자가 해야 할 일
  - 플레이테스트 수행, 피드백 제출(로그 포함)
- Acceptance
  - 데이터 기반 파라미터 결정, 릴리즈 승인.

검증용 Scene 구성 가이드 (항목별)
- TestScene 기본 요소
  - Grid 오브젝트 (GridAttributeMap 연결)
  - BombSpawner (여러 위치, 다양한 timer)
  - Player 오브젝트 (PlayerShieldManager 연결)
  - UI Canvas (shieldText, shieldBar, damageLogPanel)
  - CardManager (hand UI)
- 권장 초기 세팅
  - Row0 = Fire, Row1 = Water, Col2 = Lightning 등 (인스펙터로 설정)
  - Bombs: place 3 bombs with timers 1,2,3
  - Hand: 3 cards, Deck size 14

핵심 테스트 케이스(우선순위)
- TC-01: 단일 Fire 줄(불 블록 3) → 기대: base10 + +3 = 13 → 로그 확인
- TC-02: Water 줄(물 블록 2) + 폭탄 존재 → 폭탄 모든 timer +=1(캡 적용), 실드 +4, UI 갱신
- TC-03: Grass 줄(풀 블록 4) → 회복 +8 적용(HP 변화)
- TC-04: 번개 발동 + 다중 라인 삭제 → additive 합산 후 ×2 적용(한 번만) → 손패 전부 버리고 동일 수 드로우
- TC-05: 반복 물 루프 시 폭탄 영구 연기 가능성(시뮬레이션으로 검사)
- TC-06: 실드 cap/perTurnLimit 체크(동시 다중 물 획득 시)

디버깅 체크리스트(발생 시점별)
- 데미지 계산 불일치
  - 확인: DamageBreakdown 로그(모든 단계) 확인
  - 확인: Order of operations 위반 여부(DamageCalculator 호출 위치)
- 폭탄 타이머 이상
  - 확인: Bomb.maxTimer 값, WaterEffect 적용 로그
- 실드 UI 불일치
  - 확인: PlayerShieldManager.OnShieldChanged 이벤트 발생 타임스탬프와 UI 업데이트 일치 여부
- 번개 손패 처리 실패
  - 확인: CardManager.ReplaceHandWithDrawn 호출 로그, 덱/버린더미 상태

텔레메트리 및 핵심 메트릭 (수집 권장)
- 속성별 발동 카운트(불/물/풀/번개)
- Damage distribution (mean, median, p90, p99, max)
- LightningEvent 발생 빈도 & finalDamage 분포
- Shield cap 도달 빈도 및 perTurnGain 초과 빈도
- 반복적 폭탄 연기 패턴(루프 탐지)

운영·롤백·핫픽스 전략
- 운영 파라미터를 ScriptableObject/JSON으로 분리하여 런타임 변경 가능하게 설계.
- 릴리즈 시 '관측 모드' 활성화(soft caps는 로그 모드 → 문제 시 즉시 활성화).
- Hotfix: softCap/LightningMultiplier/perTurnShieldLimit 값만 변경하면 즉시 패치 가능하도록 우선순위 유지.

문서·코드 표준 (간단)
- 모든 계산 로직은 DamageCalculator에 집중 — UI/애니메이션은 사이드 이펙트만 수행.
- 이벤트 발행 시에는 항상 Origin(what triggered)과 Timestamp 포함.
- 변경 시 반드시 Acceptance checklist 업데이트.

마무리(등대 역할)
- 이 파일은 개발 중 길을 잃지 않게 하는 기준점입니다.  
- 각 Iteration 완료 시 반드시 여기 "간단 요약"과 "사용자가 해야 할 일"을 채워 PR 설명에 포함하세요.  
- 문제가 발견되면 먼저 로그/시뮬레이션 데이터를 수집한 뒤 기획자와 함께 해결 우선순위를 정하십시오.

빠른 시작(권장)
1. Iteration 0 완료 후 저에게 알려주세요 — 이벤트·DTO 시그니처와 DamageCalculator 메서드 시그니처를 코드 형식으로 생성해 드리겠습니다.  
2. 또는 바로 Iteration 1로 진행하시려면 GridAttributeMap 템플릿을 생성해 드립니다.