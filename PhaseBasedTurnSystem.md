# Phase-Based Turn System Architecture

## 개요
이 문서는 코루틴 기반 페이즈 시스템으로 리팩토링된 턴 시스템의 구조와 실행 흐름을 설명합니다.

## 핵심 개념

### 1. IPhaseAction 인터페이스
모든 페이즈 액션이 구현해야 하는 통합 인터페이스입니다.

```csharp
public interface IPhaseAction
{
    /// <summary>
    /// 액션 실행 (코루틴)
    /// </summary>
    /// <param name="reportDuration">실제 걸린 시간을 보고하는 콜백</param>
    IEnumerator Play(Action<float> reportDuration);

    /// <summary>
    /// 이 액션이 강제로 직렬 실행을 요구하는지
    /// </summary>
    bool ForceSequential { get; }
}
```

**특징**:
- 콜백 패턴으로 실제 실행 시간을 보고
- 각 액션이 자신의 실행 시간을 결정
- ForceSequential로 병렬/직렬 실행 제어

### 2. ExecutionMode
페이즈 실행 방식을 제어합니다.

```csharp
public enum ExecutionMode
{
    Parallel,   // 모든 액션을 동시에 시작하고 가장 긴 시간만큼 대기
    Sequential  // 액션을 하나씩 순차적으로 실행
}
```

**자동 다운그레이드**:
- ExecutionMode가 Parallel이어도
- 액션 중 하나라도 `ForceSequential = true`이면
- 자동으로 Sequential 모드로 전환

### 3. 콜백 기반 시간 보고
액션이 완료되면 실제 걸린 시간을 콜백으로 보고합니다.

```csharp
// 예시: LineClearAction
public IEnumerator Play(Action<float> reportDuration)
{
    float totalTime = 0f;

    // ... 작업 수행
    yield return new WaitForSeconds(1.0f);
    totalTime += 1.0f;

    // 완료 시 시간 보고
    reportDuration?.Invoke(totalTime);
}
```

## TurnManager 페이즈 시스템

### TurnPhase Enum
```csharp
public enum TurnPhase
{
    TurnStart,          // 턴 시작
    TickPhase,          // 몬스터 턴 카운트 감소
    MonsterAttackPhase, // 몬스터 공격 실행
    BombPhase,          // 폭탄 카운트다운 & 터짐
    TurnEnd             // 턴 종료
}
```

### 턴 진행 흐름

```
RunTurn() 코루틴
├─ TurnStart Phase
│  └─ OnPhaseChanged 이벤트 발생
│
├─ TickPhase
│  ├─ MonsterAttackManager.TickAllMonsters()
│  │  └─ 각 몬스터의 RemainingTurns -= 1
│  └─ BombManager.TickAllBombs()
│     └─ 각 폭탄의 timer -= 1
│
├─ MonsterAttackPhase
│  ├─ MonsterAttackManager.GetReadyMonsters() (RemainingTurns <= 0)
│  ├─ ExecutionMode 결정 (Parallel/Sequential)
│  │  └─ 액션 중 ForceSequential이 있으면 Sequential로 자동 전환
│  │
│  ├─ Parallel 모드:
│  │  ├─ 모든 몬스터의 Play() 코루틴 동시 시작
│  │  ├─ 각 몬스터가 reportDuration 콜백으로 시간 보고
│  │  └─ 가장 긴 시간만큼 대기
│  │
│  └─ Sequential 모드:
│     └─ 각 몬스터의 Play()를 순차적으로 실행
│
├─ BombPhase
│  ├─ BombManager.GetExplodingBombs() (timer <= 0)
│  ├─ ExecutionMode 결정
│  │
│  ├─ Parallel 모드:
│  │  └─ 모든 폭탄 동시 폭발 + 가장 긴 시간 대기
│  │
│  └─ Sequential 모드:
│     └─ 각 폭탄 순차 폭발
│
└─ TurnEnd Phase
   ├─ OnPhaseChanged 이벤트 발생
   └─ _isTurnInProgress = false
```

### Pass 버튼 처리
```csharp
public void OnPassButtonPressed()
{
    if (_isTurnInProgress)
    {
        Debug.LogWarning("Turn already in progress");
        return;
    }

    StartCoroutine(RunTurn());
}
```

### 라인 클리어 즉시 처리
```csharp
private void HandleLinesCleared(GridManager.LineClearResult result)
{
    // 라인 클리어는 턴 진행과 별개로 즉시 처리
    StartCoroutine(ProcessLineClearImmediate(result));
}

private IEnumerator ProcessLineClearImmediate(LineClearResult result)
{
    var action = new LineClearAction(result);
    yield return StartCoroutine(action.Play(duration => {
        Debug.Log($"Line clear completed in {duration:F2}s");
    }));
}
```

## 라인 클리어 시스템 (4단계 구조)

### LineClearAction.Play() 흐름

```
LineClearAction.Play()
│
├─ Phase 1: 라인 클리어 애니메이션
│  ├─ CombatManager.PlayLineClearAnimationHook(result, reportDuration)
│  ├─ 그리드 테두리 픽셀 이펙트 (TODO)
│  ├─ 슬롯 UI 하이라이트 (TODO)
│  └─ yield return new WaitForSeconds(lineClearDuration)
│
├─ Phase 2: 데미지 계산
│  ├─ CombatManager.CalculateLineClearDamage(result)
│  ├─ DamageCalculator.Calculate() 호출
│  │  ├─ baseDamage = (rows + cols) * baseWeaponDamage
│  │  ├─ attributeDamage (WoodSord 보너스)
│  │  ├─ hammerMultiplier (폭탄 존재 시)
│  │  ├─ aoeDamage (Staff 속성)
│  │  └─ finalDamage 계산
│  └─ DamageBreakdown 반환
│
├─ Phase 3: 실제 데미지 적용
│  ├─ CombatManager.ApplyCalculatedDamage(breakdown, result)
│  │  ├─ ApplyMonsterDamage(breakdown.finalDamage)
│  │  │  └─ MonsterManager.GetSelectedMonster().TakeDamage()
│  │  │     ├─ HP 감소
│  │  │     ├─ OnHealthChanged 이벤트 발생
│  │  │     └─ 사망 시 OnDied 이벤트 발생
│  │  │
│  │  ├─ MonsterManager.ApplyAoEDamage(breakdown.aoeDamage)
│  │  ├─ BombManager.TryDefuseBombs(result.RemovedBombPositions)
│  │  ├─ TrySpawnGridPopups() (데미지 팝업 표시)
│  │  └─ CheckWinLoseConditions()
│  │
│  └─ 즉시 완료 (대기 없음)
│
└─ Phase 4: 몬스터 피격 애니메이션
   ├─ CombatManager.PlayMonsterHitAnimationHook(damage, reportDuration)
   │  └─ MonsterManager.GetSelectedMonster().PlayHitFeedback(duration, callback)
   │     ├─ 1초간 Transform 애니메이션 실행
   │     │  ├─ 좌우 회전: Mathf.Sin(elapsed * rotationSpeed) * rotationAmount
   │     │  └─ 위아래 이동: Mathf.Sin(t * PI) * floatHeight
   │     └─ callback으로 실제 걸린 시간 보고
   │
   └─ yield return new WaitForSeconds(hitDuration)
```

### 타이밍 다이어그램

```
Time ─────────────────────────────────────────────────────>

블록 배치
│
GridManager.OnLinesCleared ───> LineClearAction 생성
│
├─ [Phase 1] ──────────> (1.0초)
│  그리드 이펙트 재생
│
├─ [Phase 2] (즉시)
│  데미지 계산
│
├─ [Phase 3] (즉시)
│  ├─ Monster.TakeDamage() ──> HP 감소, 이벤트 발생
│  ├─ AoE 데미지 적용
│  ├─ 폭탄 제거
│  └─ 팝업 표시
│
└─ [Phase 4] ──────────> (1.0초)
   몬스터 피격 애니메이션
   (아파하는 모션)
```

## 몬스터 공격 시스템

### MonsterController (IPhaseAction 구현)

```csharp
public class MonsterController : MonoBehaviour, IMonsterController, IPhaseAction
{
    // ForceSequential 속성
    public bool ForceSequential => _currentElement?.pattern?.forceSequential ?? false;

    // Play 구현
    public IEnumerator Play(Action<float> reportDuration)
    {
        float startTime = Time.time;

        // 사망/패턴 체크
        if (_isDead || _currentElement == null) {
            reportDuration?.Invoke(0f);
            yield break;
        }

        // 애니메이션 훅 호출 (나중에 구현)
        PlayAttackAnimationHook(pattern);

        // 애니메이션 대기
        float duration = pattern.attackType == AttackType.Bomb
            ? defaultBombSpawnDuration
            : defaultAttackDuration;
        yield return new WaitForSeconds(duration);

        // 실제 패턴 실행 (데미지/폭탄 적용)
        ExecutePatternLogic(pattern);

        // 다음 패턴 스케줄링
        ScheduleNextPattern();

        // 실제 걸린 시간 보고
        reportDuration?.Invoke(Time.time - startTime);
    }
}
```

### MonsterAttackPhase 실행 흐름

```
MonsterAttackPhase()
│
├─ MonsterAttackManager.GetReadyMonsters()
│  ├─ RemainingTurns <= 0인 몬스터만 필터링
│  ├─ null 체크, 사망 체크, active 체크
│  └─ List<IMonsterController> 반환
│
├─ ExecutionMode 결정
│  ├─ 기본: TurnManager.monsterAttackExecutionMode
│  └─ 액션 중 ForceSequential이 있으면 Sequential로 자동 전환
│
├─ Parallel 모드 실행
│  ├─ 모든 몬스터의 Play() 동시 시작
│  │  MonsterController.Play(duration => maxDuration = Max(maxDuration, duration))
│  │
│  ├─ 각 몬스터가 독립적으로:
│  │  ├─ 공격 애니메이션 재생
│  │  ├─ yield return new WaitForSeconds(duration)
│  │  ├─ 폭탄 스폰 또는 플레이어 데미지
│  │  └─ 다음 패턴 선택
│  │
│  └─ yield return new WaitForSeconds(maxDuration)
│     가장 긴 애니메이션이 끝날 때까지 대기
│
└─ Sequential 모드 실행
   └─ foreach (monster in readyMonsters)
      ├─ yield return StartCoroutine(monster.Play(reportDuration))
      └─ 한 몬스터씩 순차 실행
```

## Monster 피격 애니메이션

### Monster.PlayHitFeedback()

```csharp
public void PlayHitFeedback(float duration, Action onComplete)
{
    // 중복 방지
    if (_hitFeedbackCoroutine != null)
        StopCoroutine(_hitFeedbackCoroutine);

    _hitFeedbackCoroutine = StartCoroutine(HitFeedbackCoroutine(duration, onComplete));
}

private IEnumerator HitFeedbackCoroutine(float duration, Action onComplete)
{
    Vector3 originalPosition = transform.localPosition;
    Quaternion originalRotation = transform.localRotation;

    float elapsed = 0f;
    while (elapsed < duration)
    {
        // 사망 체크
        if (_isDead) break;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        // 좌우 회전 (Sin 기반 진동)
        float rotationAngle = Mathf.Sin(elapsed * rotationSpeed) * rotationAmount;
        transform.localRotation = originalRotation * Quaternion.Euler(0f, 0f, rotationAngle);

        // 위로 올라갔다가 내려오는 포물선 운동
        float verticalOffset = Mathf.Sin(t * Mathf.PI) * floatHeight;
        transform.localPosition = originalPosition + new Vector3(0f, verticalOffset, 0f);

        yield return null;
    }

    // 원래 상태로 복원
    transform.localPosition = originalPosition;
    transform.localRotation = originalRotation;

    _hitFeedbackCoroutine = null;
    onComplete?.Invoke();
}
```

### 안전성 메커니즘
1. **중복 방지**: 기존 코루틴이 있으면 정지 후 새로 시작
2. **사망 체크**: 코루틴 내부에서 매 프레임 `_isDead` 확인
3. **OnDisable 정리**: 컴포넌트 비활성화 시 코루틴 정지
4. **Transform null 체크**: 시작 전 & 코루틴 내부에서 확인
5. **참조 정리**: 완료 시 `_hitFeedbackCoroutine = null`

## 주요 클래스 및 역할

### Core Classes

#### TurnManager (Assets/Scripts/Turn/TurnManager.cs)
- 턴 시스템의 중앙 관리자
- 페이즈 진행 오케스트레이션
- ExecutionMode 제어
- 이벤트: `OnPhaseChanged`, `OnTurnAdvanced`

#### IPhaseAction (Assets/Scripts/Turn/IPhaseAction.cs)
- 모든 페이즈 액션의 통합 인터페이스
- 구현 클래스:
  - `LineClearAction`
  - `MonsterController`
  - `BombExplosionAction` (향후 추가 가능)

#### LineClearAction (Assets/Scripts/Combat/Actions/LineClearAction.cs)
- 라인 클리어 4단계 처리
- GridManager.OnLinesCleared 이벤트 발생 시 생성
- ForceSequential = false (병렬 실행 가능)

#### MonsterController (Assets/Scripts/Monsters/MonsterController.cs)
- IPhaseAction 구현
- 패턴 선택 및 실행
- 애니메이션 훅 제공
- ForceSequential은 패턴 SO에서 설정

#### Monster (Assets/Scripts/Combat/Monster.cs)
- HP 관리
- TakeDamage() 처리
- PlayHitFeedback() 애니메이션
- 이벤트: `OnHealthChanged`, `OnDied`

#### CombatManager (Assets/Scripts/Combat/CombatManager.cs)
- 전투 로직 관리
- 애니메이션 훅 제공:
  - `PlayLineClearAnimationHook()`
  - `PlayMonsterHitAnimationHook()`
- 데미지 계산 및 적용:
  - `CalculateLineClearDamage()`
  - `ApplyCalculatedDamage()`

#### MonsterAttackManager (Assets/Scripts/Monsters/MonsterAttackManager.cs)
- 몬스터 등록/관리
- `GetReadyMonsters()`: RemainingTurns <= 0인 몬스터 필터링
- `TickAllMonsters()`: 모든 몬스터 턴 카운트 감소

#### DamageCalculator (Assets/Scripts/Combat/DamageCalculator.cs)
- 정적 클래스
- `Calculate()`: 라인 클리어 데미지 계산
- 속성별 보너스 계산 (WoodSord, Hammer, Staff 등)

#### DamageBreakdown (Assets/Scripts/Combat/DamageBreakdown.cs)
- 데미지 계산 결과 저장
- 필드: `baseDamage`, `attributeDamage`, `aoeDamage`, `finalDamage` 등

## 전체 실행 흐름 예시

### 시나리오: 플레이어가 라인을 클리어하고 Pass 버튼을 누름

```
[1] 플레이어가 블록 배치
    └─> GridManager.TryPlace()
        └─> GridManager.CheckAndClearLines()
            └─> OnLinesCleared 이벤트 발생

[2] TurnManager.HandleLinesCleared() 즉시 실행
    └─> ProcessLineClearImmediate() 코루틴 시작
        │
        ├─ LineClearAction.Play() 실행
        │  ├─ Phase 1: 라인 클리어 애니메이션 (1.0초)
        │  ├─ Phase 2: 데미지 계산
        │  ├─ Phase 3: 데미지 적용
        │  │  └─> Monster.TakeDamage() → HP 감소
        │  └─ Phase 4: 몬스터 피격 애니메이션 (1.0초)
        │     └─> Monster.PlayHitFeedback()
        │
        └─ 총 약 2.0초 소요

[3] 플레이어가 Pass 버튼 누름
    └─> TurnManager.OnPassButtonPressed()
        └─> RunTurn() 코루틴 시작

[4] RunTurn() 실행
    │
    ├─ TurnStart Phase
    │  └─> OnPhaseChanged(TurnStart) 이벤트
    │
    ├─ TickPhase
    │  ├─> MonsterAttackManager.TickAllMonsters()
    │  │   └─> 각 MonsterController.TickTurn()
    │  │       └─> RemainingTurns -= 1
    │  │
    │  └─> BombManager.TickAllBombs()
    │      └─> 각 폭탄 timer -= 1
    │
    ├─ MonsterAttackPhase
    │  ├─> MonsterAttackManager.GetReadyMonsters()
    │  │   └─> [Monster A (턴 0), Monster C (턴 0)] 반환
    │  │
    │  ├─> ExecutionMode 결정
    │  │   └─> Parallel (두 몬스터 모두 ForceSequential = false)
    │  │
    │  ├─> Parallel 실행
    │  │   ├─> Monster A.Play() 시작 (폭탄 스폰, 0.8초)
    │  │   ├─> Monster C.Play() 시작 (데미지 공격, 1.2초)
    │  │   └─> yield return new WaitForSeconds(1.2초)
    │  │       가장 긴 시간만큼 대기
    │  │
    │  └─> Monster A, C 모두 다음 패턴 선택 완료
    │
    ├─ BombPhase
    │  ├─> BombManager.GetExplodingBombs()
    │  │   └─> [Bomb at (3,5)] 반환
    │  │
    │  └─> Sequential 실행 (폭탄은 보통 ForceSequential = true)
    │      └─> Bomb.Explode()
    │          ├─> 폭발 애니메이션 (1.0초)
    │          └─> 플레이어 데미지 적용
    │
    └─ TurnEnd Phase
       ├─> OnPhaseChanged(TurnEnd) 이벤트
       └─> _isTurnInProgress = false
```

## 확장 가능성

### 새로운 액션 추가
IPhaseAction을 구현하면 턴 시스템에 자동 통합됩니다.

```csharp
public class PlayerSkillAction : IPhaseAction
{
    public bool ForceSequential => true; // 스킬은 순차 실행

    public IEnumerator Play(Action<float> reportDuration)
    {
        float startTime = Time.time;

        // 스킬 애니메이션
        yield return new WaitForSeconds(2.0f);

        // 스킬 효과 적용
        ApplySkillEffect();

        reportDuration?.Invoke(Time.time - startTime);
    }
}
```

### 애니메이션 확장
가상 메서드로 제공되므로 상속으로 확장 가능합니다.

```csharp
// CombatManager를 상속한 커스텀 매니저
public class CustomCombatManager : CombatManager
{
    public override void PlayLineClearAnimationHook(LineClearResult result, Action<float> reportDuration)
    {
        // 커스텀 그리드 이펙트 재생
        StartCoroutine(CustomGridEffect(result, reportDuration));
    }
}

// MonsterController를 상속한 커스텀 컨트롤러
public class BossMonsterController : MonsterController
{
    protected override void PlayAttackAnimationHook(AttackPatternSO pattern)
    {
        // 보스 전용 공격 애니메이션
        animator.SetTrigger("BossAttack");
        Instantiate(bossAttackEffectPrefab, transform.position, Quaternion.identity);
    }
}
```

## 주요 설계 원칙

1. **단일 책임 원칙**
   - TurnManager: 페이즈 오케스트레이션
   - CombatManager: 전투 로직
   - Monster: HP 및 애니메이션
   - MonsterController: 패턴 관리

2. **인터페이스 분리**
   - IPhaseAction: 페이즈 액션 통합
   - IMonsterController: 몬스터 제어 인터페이스

3. **콜백 패턴**
   - 액션이 자신의 실행 시간을 결정
   - reportDuration 콜백으로 시간 보고
   - 타이밍 제어의 유연성 확보

4. **확장성**
   - virtual 메서드로 애니메이션 훅 제공
   - 새로운 IPhaseAction 구현 추가 용이
   - ExecutionMode로 병렬/직렬 제어

5. **안전성**
   - 코루틴 중복 방지
   - null 체크
   - 사망 상태 확인
   - OnDisable 정리

## 향후 개선 사항

1. **PlayLineClearAnimationHook 구현**
   - 그리드 테두리 픽셀 이펙트
   - 슬롯 UI 하이라이트

2. **MonsterController 애니메이션 훅 구현**
   - PlayAttackAnimationHook()에 실제 애니메이션 추가

3. **BombExplosionAction 분리**
   - 현재 BombManager에서 직접 처리
   - IPhaseAction으로 분리하여 일관성 확보

4. **ExecutionMode 동적 설정**
   - 게임 상황에 따라 런타임 변경
   - 예: 보스전에서는 강제 Sequential

5. **애니메이션 시간 설정 UI**
   - 인스펙터에서 각 액션별 duration 설정
   - ScriptableObject로 애니메이션 프리셋 관리
