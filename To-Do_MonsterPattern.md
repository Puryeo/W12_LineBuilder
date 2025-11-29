# Monster Attack Pattern — 설계 및 작업 계획 (업데이트)

목표 요약
- 몬스터가 소유한 공격 패턴(Delay 기반)을 ScriptableObject(SO)로 정의.
- 라운드 데이터(RoundDataSO)를 통해 몬스터 프리팹에 패턴 풀을 주입(프리팹 인스펙터 대신 라운드 레벨에서 관리).
- 폭탄 스폰은 몬스터가 호출하는 온디맨드 API(BombManager.SpawnRandomGridBomb)만 사용. SpecificCell / AroundMonster 모드는 초기 구현에서 제외.
- 패턴 선택은 독립 시행(매 선택마다 풀에서 샘플), 정수 weight로 빈도 조정, repeat 플래그로 자동 재스케줄 제어.

핵심 변경 요약 (핵심 결정사항)
- RoundDataSO 확장:
  - EnemySpawnEntry: GameObject prefab + PatternElement[] patterns
  - PatternElement: AttackPatternSO pattern; int weight; bool repeat
  - 우선순위: RoundDataSO에 엔트리가 있으면 해당 프리팹의 패턴을 덮어씀(명확성).
  - 초기에는 pattern-level override(예: delayOverride, bombTimerOverride)는 지원하지 않음(간단성).
- 폭탄 SpawnMode: RandomGrid로 고정. BombAttackPatternSO.spawnMode 사용하지 않음(향후 확장 가능).
- 턴 처리 순서(정확한 위치):
  0) PlayerShieldManager.OnNewTurn() — 실드 per-turn 처리  
  1) BombManager.TickAllBombs() — 기존 폭탄 타이머 감소 및 즉시 폭발 처리(폭발 피해 즉시 적용)  
  1.5) MonsterAttackManager.Instance?.TickAll() — 모든 몬스터 Tick(remainingTurns--, remaining<=0 -> ExecutePattern())  
  2) BombManager.HandleTurnAdvance() — 자동 스폰 카운트 감소 및 (필요 시) 자동 스폰. (새로 생성된 폭탄은 생성 턴에는 tick되지 않음)
- 패턴 선택 semantics:
  - "독립 시행": 패턴은 풀에서 제거되지 않음. 매 선택 시 weight 기반 샘플링(랜덤).
  - weight: int >= 0. 모든 weight == 0이면 균등 샘플링 및 경고 로그.
  - repeat: true이면 Execute 후 동일 패턴을 delayTurns 만큼 자동 재스케줄. false이면 Execute 후 CurrentPattern 해제(다음 턴 샘플링 가능).
- 책임 분리:
  - TurnManager: 턴 흐름의 단일 진입점(위 순서 유지).
  - MonsterAttackManager: 씬 단위 중앙 Tick 관리자(등록/해제, TickAll 제공).
  - MonsterController: 개별 몬스터의 패턴 풀(주입 가능), 스케줄/카운트다운/실행 담당.
  - BombManager: 온디맨드 폭탄 생성 API 제공(SpawnRandomGridBomb/SpawnGridBombAt 유지). 자동 스폰 로직은 단계적 마이그레이션 후 제거 가능.

데이터 구조(권장 시그니처 예시)
- PatternElement (serializable)
  - AttackPatternSO pattern
  - int weight
  - bool repeat
- EnemySpawnEntry (serializable)
  - GameObject prefab
  - PatternElement[] patterns
- RoundDataSO 에는 EnemySpawnEntry[] enemySpawnEntries 필드 추가. (기존 enemiesToSpawn 호환 유지 — 비어있을 때 레거시 동작 유지 가능)

구현 파일(추가/수정)
- 신규
  - Assets/Scripts/Monsters/MonsterController.cs (IMonsterController 구현; weighted sampling, repeat 처리, Bomb/Combat 호출)
  - Assets/Scripts/Monsters/MonsterAttackManager.cs (싱글턴, Register/Unregister, TickAll)
- 수정
  - Assets/Scripts/Turn/TurnManager.cs : AdvanceTurn()에 MonsterAttackManager.TickAll() 호출 삽입(폭발 처리 후, 자동 스폰 전)
  - Assets/Scripts/Game/BombManager.cs : SpawnRandomGridBomb API 재사용(이미 존재함). 자동 스폰 로직은 초기 유지하되 옵션화 권장.
  - Assets/Scripts/Data/RoundDataSO.cs (또는 해당 파일) : EnemySpawnEntry 및 PatternElement 추가, SpawnMonstersForCurrentRound() 에서 주입 처리
  - Prefabs: 몬스터 프리팹은 RoundDataSO에서 할당한 patterns로 덮어쓰기 되도록 설계(프리팹 인스펙터와 중복 방지 문서화)
- 옵션/후속
  - Assets/Scripts/Events/GameEvents.cs : OnMonsterPatternExecuted 이벤트 추가 가능(로그/UI용)
  - Bomb.cs: originatingMonsterId 메타데이터 추가 가능(디버깅/밸런스 추적용)

구현 단계(권장 순서)
1. RoundDataSO 확장 — PatternElement, EnemySpawnEntry 추가 / 에셋 호환성 고려
2. GameFlowManager.SpawnMonstersForCurrentRound() 보강 — Instantiate 후 RoundDataSO 엔트리가 있으면 MonsterController에 패턴 주입
3. MonsterController 구현 — patterns 보관, weighted 샘플링, Schedule/Cancel/Tick/Execute 구현, repeat semantics
4. MonsterAttackManager 구현 — MonsterController 등록/해제 및 TickAll
5. TurnManager.AdvanceTurn()에 TickAll 호출 삽입(폭발 처리 후)
6. 테스트 씬 구성 및 검증(아래 테스트 체크리스트 따름)
7. 레거시 자동 스폰 단계적 비활성화(옵션 플래그) 및 리팩터링

테스트 체크리스트 (필수)
- RoundDataSO의 EnemySpawnEntry를 통해 스폰된 몬스터에 패턴들이 정상 주입되는가?
- TurnManager.AdvanceTurn() 실행 흐름에서 몬스터의 RemainingTurns가 감소하고 RemainingTurns<=0일 때 ExecutePattern 호출되는가?
- Bomb 패턴 실행 시 BombManager.SpawnRandomGridBomb 호출로 Grid와 Bomb 오브젝트가 일관되게 생성되는가?
- 새로 생성된 폭탄은 생성 턴에는 tick되지 않고 다음 턴부터 tick되는가?
- Damage 패턴 실행은 PlayerShieldManager의 규칙에 따라 실드로 흡수되는가?
- Weighted 샘플링: weight 값에 따라 통계적으로 빈도 차이가 발생하는가(간단한 시뮬레이션으로 검증).
- repeat=true 패턴은 실행 후 자동으로 다시 스케줄 되는가; repeat=false는 실행 후 해제되는가?
- 다수 몬스터 동시 스케줄에서 우선순위 문제가 있지는 않은가(등록순으로 시작, 필요 시 priority 도입).

마이그레이션 및 운영 고려사항
- 기존 RoundDataSO 에셋은 enemySpawnEntries 필드가 비어 있을 경우 기존 enemiesToSpawn 동작을 유지하도록 코드를 작성하여 호환성 확보.
- 디자이너가 weight를 0으로 잘못 설정할 경우 경고 로그를 남기고 fallback 균등샘플링 적용.
- BombManager의 기존 자동 스폰은 즉시 제거하지 말고 옵션화(flag로 비활성화)한 뒤 단계적으로 전환 권장.
- 필요 시 PatternElement에 delayOverride / bombTimerOverride 등 필드 추가는 추후 확장으로 진행.

안내(다음 액션)
- 아래 세부 기본값으로 진행을 권장합니다. 진행 승인 부탁드립니다:
  1. RoundDataSO 우선순위: RoundDataSO가 패턴을 덮어씀(명확성).
  2. PatternElement override: 초기에는 비허용(단순성).
  3. MonsterAttackManager 호출 정렬: 초기엔 등록순(우선순위 필드 미도입).
- 승인하시면 위 설계에 맞춘 md 업데이트는 확정으로 간주하고, 실제 코드 파일(RoundDataSO 확장, MonsterController, MonsterAttackManager, TurnManager 변경)을 생성/수정하겠습니다.
- 추가로 원하면 GameEvents.OnMonsterPatternExecuted 이벤트와 Bomb.originatingMonsterId 같은 디버깅 필드도 함께 추가합니다.

	