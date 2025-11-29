using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 몬스터 프리팹에 붙여 패턴을 관리하는 컴포넌트(프리팹 우선).
/// - PatternElement[] 을 인스펙터에서 설정 (weight 필드는 반드시 프리팹 컴포넌트에 존재)
/// - MonsterAttackManager에 등록되어 TurnManager 흐름에서 TickTurn() 호출됨
/// </summary>
[DisallowMultipleComponent]
public class MonsterController : MonoBehaviour, IMonsterController
{
    [Header("Attack Animation Settings")]
    [Tooltip("공격 이펙트 프리팹 (AutoPlayEffect 컴포넌트 필요)")]
    public GameObject attackEffectPrefab;

    [Tooltip("공격 애니메이션 최소 대기 시간 (초)")]
    public float attackAnimationMinDuration = 0.8f;

    [Tooltip("공격 애니메이션 최대 대기 시간 (초)")]
    public float attackAnimationMaxDuration = 2.0f;
    [Serializable]
    public class PatternElement
    {
        [Tooltip("실제 패턴 SO")]
        public AttackPatternSO pattern;

        [Tooltip("선택 비중 (>=0). 디자이너가 프리팹에서 설정하세요.")]
        public int weight = 1;

        [Tooltip("실행 후 동일 패턴을 자동 재스케줄할지")]
        public bool repeat = false;
    }

    [Header("패턴 풀 (프리팹에서 설정)")]
    public PatternElement[] patternElements;

    private readonly List<PatternElement> _patterns = new List<PatternElement>();

    private PatternElement _currentElement;
    public AttackPatternSO CurrentPattern => _currentElement != null ? _currentElement.pattern : null;
    public int RemainingTurns { get; private set; }

    private Monster _monster;
    private MonsterUI _ui;
    private static readonly System.Random _rng = new System.Random();

    private bool _isDead = false;
    private bool _isExecuting = false; // 공격 실행 중 플래그 (중복 방지)

    private void Awake()
    {
        _monster = GetComponent<Monster>();
        _ui = GetComponent<MonsterUI>();

        if (_monster != null)
            _monster.OnDied += OnMonsterDied;

        if (patternElements != null && patternElements.Length > 0)
            _patterns.AddRange(patternElements);

        // 초기 자동 선택: 프리팹 스폰 직후 UI에 다음 패턴을 표시하기 위함
        if (_patterns.Count > 0)
        {
            var initial = SelectNextPattern();
            if (initial != null)
            {
                if (initial.pattern == null)
                {
                    Debug.LogWarning($"[MonsterController] Initial selected PatternElement.pattern is NULL on {name}. Check prefab patternElements.", this);
                }
                else
                {
                    _currentElement = initial;
                    RemainingTurns = Math.Max(0, initial.pattern.delayTurns);
                    _ui?.BindPattern(CurrentPattern, RemainingTurns);
                    Debug.Log($"[MonsterController] Initial auto-selected '{initial.pattern.displayName}' for {RemainingTurns} turns on {name}");
                }
            }
        }
    }

    private void OnEnable()
    {
        if (MonsterAttackManager.Instance != null)
            MonsterAttackManager.Instance.Register(this);
        else
            Debug.LogWarning("[MonsterController] MonsterAttackManager.Instance is NULL on OnEnable. Ensure MonsterAttackManager exists in the scene.", this);
    }

    private void OnDisable()
    {
        if (_monster != null)
            _monster.OnDied -= OnMonsterDied;

        if (MonsterAttackManager.Instance != null)
            MonsterAttackManager.Instance.Unregister(this);
    }

    private void OnMonsterDied(Monster m)
    {
        // 즉시 패턴 정리 및 Tick 중지
        _isDead = true;
        CancelScheduledPattern();
        _ui?.ClearPatternUI();

        if (MonsterAttackManager.Instance != null)
            MonsterAttackManager.Instance.Unregister(this);
    }

    /// <summary>외부에서 런타임으로 패턴을 주입(프리팹 기본 덮어쓰지 않음 — 주입 시 기존 풀을 교체)</summary>
    public void SetPatternElements(PatternElement[] elements)
    {
        if (_isDead) return;

        _patterns.Clear();
        if (elements != null && elements.Length > 0)
            _patterns.AddRange(elements);
        Debug.Log($"[MonsterController] SetPatternElements -> {_patterns.Count} patterns on {name}", this);
        // UI 초기화
        _ui?.ClearPatternUI();
        _currentElement = null;
        RemainingTurns = 0;
    }

    public void SchedulePattern(AttackPatternSO pattern)
    {
        if (_isDead) return;
        if (pattern == null) return;

        PatternElement found = null;
        foreach (var p in _patterns)
            if (p != null && p.pattern == pattern) { found = p; break; }

        if (found == null)
            found = new PatternElement { pattern = pattern, weight = 1, repeat = false };

        _currentElement = found;
        RemainingTurns = Math.Max(0, pattern.delayTurns);
        _ui?.BindPattern(CurrentPattern, RemainingTurns);
        Debug.Log($"[MonsterController] Scheduled '{pattern.displayName}' for {RemainingTurns} turns on {name}");
    }

    public void CancelScheduledPattern()
    {
        _currentElement = null;
        RemainingTurns = 0;
        _ui?.ClearPatternUI();
    }

    public void TickTurn()
    {
        if (_isDead || (_monster != null && _monster.IsDead)) return;

        // 현재 예약된 패턴이 없으면 풀에서 선택(독립시행, weight 기반)
        if (_currentElement == null)
        {
            var sel = SelectNextPattern();
            if (sel != null)
            {
                if (sel.pattern == null)
                {
                    Debug.LogWarning($"[MonsterController] Selected PatternElement.pattern is NULL on {name}. Skipping.", this);
                    return;
                }

                _currentElement = sel;
                RemainingTurns = Math.Max(0, sel.pattern.delayTurns);
                _ui?.BindPattern(CurrentPattern, RemainingTurns);
                Debug.Log($"[MonsterController] Auto-selected '{sel.pattern.displayName}' for {RemainingTurns} turns on {name}");
            }
            return;
        }

        RemainingTurns = Math.Max(0, RemainingTurns - 1);
        _ui?.UpdatePatternTurns(RemainingTurns);

        if (RemainingTurns <= 0)
            ExecutePattern();
    }

    public void ExecutePattern()
    {
        if (_isDead || (_monster != null && _monster.IsDead)) 
        {
            CancelScheduledPattern();
            return;
        }

        if (_currentElement == null || _currentElement.pattern == null)
        {
            CancelScheduledPattern();
            return;
        }

        var pat = _currentElement.pattern;
        Debug.Log($"[MonsterController] ExecutePattern -> {pat.displayName} ({pat.attackType}) on {name}");

        try
        {
            if (pat.attackType == AttackType.Bomb)
            {
                var bp = pat as BombAttackPatternSO;
                if (bp != null)
                {
                    if (BombManager.Instance != null)
                    {
                        Vector2Int pos;
                        bool spawned = BombManager.Instance.SpawnRandomGridBomb(bp.startTimer, bp.maxTimer, out pos);
                        Debug.Log($"[MonsterController] Bomb spawn attempted by {name}: success={spawned} pos={pos}");
                    }
                    else Debug.LogWarning("[MonsterController] BombManager instance not found.");
                }
                else Debug.LogWarning("[MonsterController] Bomb pattern cast failed.");
            }
            else if (pat.attackType == AttackType.Damage)
            {
                var dp = pat as DamageAttackPatternSO;
                if (dp != null)
                {
                    if (CombatManager.Instance != null)
                    {
                        CombatManager.Instance.ApplyPlayerDamage(dp.damage);
                        Debug.Log($"[MonsterController] Applied {dp.damage} dmg to player via CombatManager.");
                    }
                    else Debug.Log($"[MonsterController] (No CombatManager) Simulated damage: {dp.damage}");
                }
                else Debug.LogWarning("[MonsterController] Damage pattern cast failed.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MonsterController] Exception during ExecutePattern: {ex}");
        }

        // post-execute: repeat이면 동일 패턴 재스케줄, 아니면 즉시 다음 패턴 선택하여 UI 바인딩(쉬는 구간 없이)
        if (_currentElement != null && _currentElement.repeat && !_isDead)
        {
            RemainingTurns = Math.Max(0, _currentElement.pattern.delayTurns);
            _ui?.UpdatePatternTurns(RemainingTurns);
            Debug.Log($"[MonsterController] Pattern '{_currentElement.pattern.displayName}' repeat scheduled on {name} for {RemainingTurns} turns.");
            // _currentElement remains the same
        }
        else
        {
            // 기존: CancelScheduledPattern() -> UI 공백 발생
            // 변경: 즉시 다음 패턴을 선택하여 UI 표시
            var next = SelectNextPattern();
            if (next != null && next.pattern != null && !_isDead)
            {
                _currentElement = next;
                RemainingTurns = Math.Max(0, next.pattern.delayTurns);
                _ui?.BindPattern(CurrentPattern, RemainingTurns);
                Debug.Log($"[MonsterController] After execute, immediately selected next '{next.pattern.displayName}' for {RemainingTurns} turns on {name}");
            }
            else
            {
                // 선택할 패턴이 없으면 기존처럼 클리어
                CancelScheduledPattern();
            }
        }
    }

    private PatternElement SelectNextPattern()
    {
        if (_patterns == null || _patterns.Count == 0) return null;

        int sum = 0;
        for (int i = 0; i < _patterns.Count; i++)
            sum += (_patterns[i] != null ? Math.Max(0, _patterns[i].weight) : 0);

        if (sum <= 0)
        {
            // fallback uniform
            int idx = _rng.Next(0, _patterns.Count);
            return _patterns[idx];
        }

        int r = _rng.Next(0, sum); // 정수 기반 샘플링
        int acc = 0;
        for (int i = 0; i < _patterns.Count; i++)
        {
            var p = _patterns[i];
            if (p == null) continue;
            acc += Math.Max(0, p.weight);
            if (r < acc) return p;
        }

        return _patterns[_rng.Next(0, _patterns.Count)];
    }

    // ========================================
    // 코루틴 기반 턴 시스템 메서드
    // ========================================

    /// <summary>
    /// 패턴 실행 준비가 되었는지 확인
    /// </summary>
    public bool IsReadyToExecute()
    {
        return RemainingTurns <= 0 &&
               _currentElement != null &&
               !_isDead &&
               !_isExecuting;
    }

    /// <summary>
    /// 몬스터가 죽었는지 확인
    /// </summary>
    public bool IsDead()
    {
        return _isDead || (_monster != null && _monster.IsDead);
    }

    /// <summary>
    /// 패턴을 코루틴으로 실행 (애니메이션 대기 포함)
    /// </summary>
    public IEnumerator ExecutePatternRoutine()
    {
        // 진입 체크
        if (_isExecuting)
        {
            Debug.LogWarning($"[MonsterController] Already executing pattern on {name}, skip");
            yield break;
        }

        _isExecuting = true;

        try
        {
            // 사망/패턴 체크
            if (_isDead || (_monster != null && _monster.IsDead) || _currentElement == null)
            {
                CancelScheduledPattern();
                yield break;
            }

            var pat = _currentElement.pattern;
            if (pat == null)
            {
                CancelScheduledPattern();
                yield break;
            }

            Debug.Log($"[MonsterController] ExecutePatternRoutine -> {pat.displayName} ({pat.attackType}) on {name}");

            // 타입별 공격 처리
            if (pat.attackType == AttackType.Damage)
            {
                var dp = pat as DamageAttackPatternSO;
                if (dp != null)
                {
                    // 1. 공격 이펙트 스폰
                    GameObject attackEffect = null;
                    if (attackEffectPrefab != null)
                    {
                        attackEffect = Instantiate(attackEffectPrefab, transform.position, Quaternion.identity);
                        Debug.Log($"[MonsterController] Attack effect spawned for {name}");
                    }

                    // 2. 애니메이션 완료 대기 (하이브리드)
                    float startTime = Time.time;
                    bool animationComplete = false;

                    // 이펙트가 AutoPlayEffect를 가지고 있으면 완료 이벤트 구독
                    if (attackEffect != null)
                    {
                        var autoPlay = attackEffect.GetComponent<AutoPlayEffect>();
                        if (autoPlay != null)
                        {
                            autoPlay.OnEffectComplete += () => animationComplete = true;
                        }
                    }

                    while (Time.time - startTime < attackAnimationMaxDuration)
                    {
                        // 중간 사망 체크
                        if (_isDead || (_monster != null && _monster.IsDead))
                        {
                            if (attackEffect != null) Destroy(attackEffect);
                            yield break;
                        }

                        // 최소 시간 경과 AND 애니메이션 완료
                        if (Time.time - startTime >= attackAnimationMinDuration)
                        {
                            if (animationComplete || attackEffect == null)
                            {
                                break;
                            }
                        }

                        yield return null;
                    }

                    // 3. 다시 사망 체크
                    if (_isDead || (_monster != null && _monster.IsDead))
                    {
                        yield break;
                    }

                    // 4. 실제 데미지 적용
                    if (CombatManager.Instance != null)
                    {
                        CombatManager.Instance.ApplyPlayerDamage(dp.damage, $"Monster:{name}");
                        Debug.Log($"[MonsterController] Applied {dp.damage} damage to player from {name}");
                    }
                }
            }
            else if (pat.attackType == AttackType.Bomb)
            {
                var bp = pat as BombAttackPatternSO;
                if (bp != null)
                {
                    // 1. 폭탄 스폰 이펙트
                    GameObject spawnEffect = null;
                    if (attackEffectPrefab != null)
                    {
                        spawnEffect = Instantiate(attackEffectPrefab, transform.position, Quaternion.identity);
                        Debug.Log($"[MonsterController] Bomb spawn effect spawned for {name}");
                    }

                    // 2. 대기
                    float startTime = Time.time;
                    bool effectComplete = false;

                    if (spawnEffect != null)
                    {
                        var autoPlay = spawnEffect.GetComponent<AutoPlayEffect>();
                        if (autoPlay != null)
                        {
                            autoPlay.OnEffectComplete += () => effectComplete = true;
                        }
                    }

                    while (Time.time - startTime < attackAnimationMaxDuration)
                    {
                        if (_isDead || (_monster != null && _monster.IsDead))
                        {
                            if (spawnEffect != null) Destroy(spawnEffect);
                            yield break;
                        }

                        if (Time.time - startTime >= attackAnimationMinDuration)
                        {
                            if (effectComplete || spawnEffect == null)
                            {
                                break;
                            }
                        }

                        yield return null;
                    }

                    // 3. 사망 체크
                    if (_isDead || (_monster != null && _monster.IsDead))
                    {
                        yield break;
                    }

                    // 4. 실제 폭탄 스폰
                    if (BombManager.Instance != null)
                    {
                        Vector2Int spawnPos;
                        bool spawned = BombManager.Instance.SpawnRandomGridBomb(bp.startTimer, bp.maxTimer, out spawnPos);
                        Debug.Log($"[MonsterController] Bomb spawn attempted by {name}: success={spawned} pos={spawnPos}");
                    }
                }
            }

            // 다음 패턴 스케줄링 (기존 로직 유지)
            if (!_isDead)
            {
                if (_currentElement != null && _currentElement.repeat)
                {
                    RemainingTurns = Math.Max(0, _currentElement.pattern.delayTurns);
                    _ui?.UpdatePatternTurns(RemainingTurns);
                    Debug.Log($"[MonsterController] Pattern '{_currentElement.pattern.displayName}' repeat scheduled on {name} for {RemainingTurns} turns.");
                }
                else
                {
                    var next = SelectNextPattern();
                    if (next != null && next.pattern != null)
                    {
                        _currentElement = next;
                        RemainingTurns = Math.Max(0, next.pattern.delayTurns);
                        _ui?.BindPattern(CurrentPattern, RemainingTurns);
                        Debug.Log($"[MonsterController] After execute, immediately selected next '{next.pattern.displayName}' for {RemainingTurns} turns on {name}");
                    }
                    else
                    {
                        CancelScheduledPattern();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MonsterController] Exception during ExecutePatternRoutine on {name}: {ex}");
            CancelScheduledPattern();
        }
        finally
        {
            _isExecuting = false;
        }
    }
}