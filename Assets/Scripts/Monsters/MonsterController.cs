using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 몬스???�리?�에 붙여 ?�턴??관리하??컴포?�트(?�리???�선).
/// - PatternElement[] ???�스?�터?�서 ?�정 (weight ?�드??반드???�리??컴포?�트??존재)
/// - MonsterAttackManager???�록?�어 TurnManager ?�름?�서 TickTurn() ?�출??
/// </summary>
[DisallowMultipleComponent]
public class MonsterController : MonoBehaviour, IMonsterController
{
    [Serializable]
    public class PatternElement
    {
        [Tooltip("?�제 ?�턴 SO")]
        public AttackPatternSO pattern;

        [Tooltip("?�택 비중 (>=0). ?�자?�너가 ?�리?�에???�정?�세??")]
        public int weight = 1;

        [Tooltip("?�행 ???�일 ?�턴???�동 ?�스케줄할지")]
        public bool repeat = false;
    }

    [Header("?�턴 ?� (?�리?�에???�정)")]
    public PatternElement[] patternElements;

    private readonly List<PatternElement> _patterns = new List<PatternElement>();

    private PatternElement _currentElement;
    public AttackPatternSO CurrentPattern => _currentElement != null ? _currentElement.pattern : null;
    public int RemainingTurns { get; private set; }

    private Monster _monster;
    private MonsterUI _ui;
    private static readonly System.Random _rng = new System.Random();
    private static readonly List<GridHeaderSlotUI> _slotSelectionBuffer = new List<GridHeaderSlotUI>(16);

    private struct DisabledSlotSnapshot
    {
        public GridHeaderSlotUI slot;
        public AttributeType previousAttribute;
    }
    private readonly List<DisabledSlotSnapshot> _disabledSlotSnapshots = new List<DisabledSlotSnapshot>(8);

    private bool _isDead = false;
    private int _hpAtPatternStart = -1;

    private void Awake()
    {
        _monster = GetComponent<Monster>();
        _ui = GetComponent<MonsterUI>();

        if (_monster != null)
        {
            _monster.OnDied += OnMonsterDied;
            _monster.OnHealthChanged += OnMonsterHealthChanged;
        }

        if (patternElements != null && patternElements.Length > 0)
            _patterns.AddRange(patternElements);

        // 초기 ?�동 ?�택: ?�리???�폰 직후 UI???�음 ?�턴???�시?�기 ?�함
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
                    UpdatePatternTracking(CurrentPattern);
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
        {
            _monster.OnDied -= OnMonsterDied;
            _monster.OnHealthChanged -= OnMonsterHealthChanged;
        }

        if (MonsterAttackManager.Instance != null)
            MonsterAttackManager.Instance.Unregister(this);
    }

    private void OnMonsterDied(Monster m)
    {
        // 즉시 ?�턴 ?�리 �?Tick 중�?
        _isDead = true;
        CancelScheduledPattern();
        _ui?.ClearPatternUI();

        if (MonsterAttackManager.Instance != null)
            MonsterAttackManager.Instance.Unregister(this);
    }

    /// <summary>?��??�서 ?��??�으�??�턴??주입(?�리??기본 ??��?��? ?�음 ??주입 ??기존 ?�??교체)</summary>
    public void SetPatternElements(PatternElement[] elements)
    {
        if (_isDead) return;

        _patterns.Clear();
        if (elements != null && elements.Length > 0)
            _patterns.AddRange(elements);
        Debug.Log($"[MonsterController] SetPatternElements -> {_patterns.Count} patterns on {name}", this);
        // UI 초기??
        _ui?.ClearPatternUI();
        _currentElement = null;
        RemainingTurns = 0;
        _hpAtPatternStart = -1;
        RestoreDisabledSlots();
    }

    public void SchedulePattern(AttackPatternSO pattern)
    {
        if (_isDead) return;
        if (pattern == null) return;

        RestoreDisabledSlots();

        PatternElement found = null;
        foreach (var p in _patterns)
            if (p != null && p.pattern == pattern) { found = p; break; }

        if (found == null)
            found = new PatternElement { pattern = pattern, weight = 1, repeat = false };

        _currentElement = found;
        RemainingTurns = Math.Max(0, pattern.delayTurns);
        _ui?.BindPattern(CurrentPattern, RemainingTurns);
        UpdatePatternTracking(CurrentPattern);
        Debug.Log($"[MonsterController] Scheduled '{pattern.displayName}' for {RemainingTurns} turns on {name}");
    }

    public void CancelScheduledPattern()
    {
        _currentElement = null;
        RemainingTurns = 0;
        _hpAtPatternStart = -1;
        _ui?.ClearPatternUI();
        RestoreDisabledSlots();
    }

    public void TickTurn()
    {
        if (_isDead || (_monster != null && _monster.IsDead)) return;

        // ?�재 ?�약???�턴???�으�??�?�서 ?�택(?�립?�행, weight 기반)
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
                UpdatePatternTracking(CurrentPattern);
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
            else if (pat.attackType == AttackType.HeavyDamage)
            {
                var hp = pat as HeavyDamageAttackPatternSO;
                if (hp != null)
                {
                    if (CombatManager.Instance != null)
                    {
                        CombatManager.Instance.ApplyPlayerDamage(hp.damage, $"{name}_HeavyAttack");
                        Debug.Log($"[MonsterController] Applied {hp.damage} heavy dmg to player via CombatManager.");
                    }
                    else Debug.Log($"[MonsterController] (No CombatManager) Simulated heavy damage: {hp.damage}");
                }
                else Debug.LogWarning("[MonsterController] Heavy damage pattern cast failed.");
            }
            else if (pat.attackType == AttackType.Groggy)
            {
                var gp = pat as GroggyAttackPatternSO;
                var desc = gp != null ? gp.description : string.Empty;
                Debug.Log($"[MonsterController] {name} is groggy. Skipping turn. {desc}");
            }
            else if (pat.attackType == AttackType.DisableSlot)
            {
                var sp = pat as DisableSlotAttackPatternSO;
                if (sp != null)
                {
                    ExecuteDisableSlotPattern(sp);
                }
                else Debug.LogWarning("[MonsterController] DisableSlot pattern cast failed.");
            }
            else
            {
                Debug.LogWarning($"[MonsterController] Unknown attack type '{pat.attackType}' on {name}.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MonsterController] Exception during ExecutePattern: {ex}");
        }

        // post-execute: repeat?�면 ?�일 ?�턴 ?�스케�? ?�니�?즉시 ?�음 ?�턴 ?�택?�여 UI 바인???�는 구간 ?�이)
        if (_currentElement != null && _currentElement.repeat && !_isDead)
        {
            RemainingTurns = Math.Max(0, _currentElement.pattern.delayTurns);
            _ui?.UpdatePatternTurns(RemainingTurns);
            UpdatePatternTracking(CurrentPattern);
            Debug.Log($"[MonsterController] Pattern '{_currentElement.pattern.displayName}' repeat scheduled on {name} for {RemainingTurns} turns.");
            // _currentElement remains the same
        }
        else
        {
            // 기존: CancelScheduledPattern() -> UI 공백 발생
            // 변�? 즉시 ?�음 ?�턴???�택?�여 UI ?�시
            var next = SelectNextPattern();
            if (next != null && next.pattern != null && !_isDead)
            {
                _currentElement = next;
                RemainingTurns = Math.Max(0, next.pattern.delayTurns);
                _ui?.BindPattern(CurrentPattern, RemainingTurns);
                UpdatePatternTracking(CurrentPattern);
                Debug.Log($"[MonsterController] After execute, immediately selected next '{next.pattern.displayName}' for {RemainingTurns} turns on {name}");
            }
            else
            {
                // ?�택???�턴???�으�?기존처럼 ?�리??
                CancelScheduledPattern();
            }
        }
    }

    private void OnMonsterHealthChanged(int current, int max)
    {
        if (_isDead) return;
        if (_currentElement == null || _currentElement.pattern == null) return;
        if (!(_currentElement.pattern is HeavyDamageAttackPatternSO heavyPattern)) return;
        if (RemainingTurns <= 0) return;

        if (_hpAtPatternStart < 0)
            _hpAtPatternStart = current;

        int loss = _hpAtPatternStart - current;
        if (loss >= heavyPattern.cancelThreshold)
            TriggerGroggyState(heavyPattern, loss);
    }

    private void TriggerGroggyState(HeavyDamageAttackPatternSO heavyPattern, int hpLoss)
    {
        Debug.Log($"[MonsterController] Heavy attack '{heavyPattern.displayName}' cancelled on {name} (HP loss {hpLoss}/{heavyPattern.cancelThreshold}).");
        _hpAtPatternStart = -1;

        var groggy = heavyPattern.groggyPattern;
        if (groggy == null)
        {
            Debug.LogWarning($"[MonsterController] Groggy pattern not assigned for '{heavyPattern.displayName}'. Cancelling pattern only.", this);
            CancelScheduledPattern();
            return;
        }

        SchedulePattern(groggy);
    }

    private void UpdatePatternTracking(AttackPatternSO pattern)
    {
        if (_monster == null || pattern == null)
        {
            _hpAtPatternStart = -1;
            return;
        }

        if (pattern is HeavyDamageAttackPatternSO)
            _hpAtPatternStart = _monster.currentHP;
        else
            _hpAtPatternStart = -1;
    }

    private void ExecuteDisableSlotPattern(DisableSlotAttackPatternSO pattern)
    {
        var candidates = GatherSlotCandidates(pattern);
        if (candidates.Count == 0)
        {
            Debug.LogWarning($"[MonsterController] DisableSlot pattern '{pattern.displayName}' had no valid targets.", this);
            return;
        }

        int iterations = Mathf.Clamp(pattern.slotsToDisable, 1, candidates.Count);
        for (int i = 0; i < iterations; i++)
        {
            if (candidates.Count == 0) break;
            int idx = _rng.Next(0, candidates.Count);
            var slot = candidates[idx];
            candidates.RemoveAt(idx);
            if (slot == null) continue;

            var before = slot.GetCurrentAttribute();

            slot.SetAttribute(AttributeType.None);
            RecordDisabledSlot(slot, before);
            slot.SetLocked(true, pattern.lockedSlotSprite);
            Debug.Log($"[MonsterController] DisableSlot removed {before} from {slot.axis} {slot.index}.");
        }
    }

    private List<GridHeaderSlotUI> GatherSlotCandidates(DisableSlotAttackPatternSO pattern)
    {
        _slotSelectionBuffer.Clear();

        if (pattern.includeRows)
            AppendSlots(_slotSelectionBuffer, AttributeInventoryUI.AllRowSlots, pattern.requireOccupiedSlot);
        if (pattern.includeColumns)
            AppendSlots(_slotSelectionBuffer, AttributeInventoryUI.AllColumnSlots, pattern.requireOccupiedSlot);

        if (_slotSelectionBuffer.Count == 0)
        {
            var fallback = UnityEngine.Object.FindObjectsByType<GridHeaderSlotUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var slot in fallback)
            {
                if (slot == null) continue;
                if (slot.axis == GridHeaderSlotUI.Axis.Row && !pattern.includeRows) continue;
                if (slot.axis == GridHeaderSlotUI.Axis.Col && !pattern.includeColumns) continue;
                if (pattern.requireOccupiedSlot && slot.GetCurrentAttribute() == AttributeType.None) continue;
                _slotSelectionBuffer.Add(slot);
            }
        }

        return _slotSelectionBuffer;
    }

    private void AppendSlots(List<GridHeaderSlotUI> dest, List<GridHeaderSlotUI> source, bool requireOccupied)
    {
        if (source == null) return;
        foreach (var slot in source)
        {
            if (slot == null) continue;
            if (requireOccupied && slot.GetCurrentAttribute() == AttributeType.None) continue;
            dest.Add(slot);
        }
    }

    private void RecordDisabledSlot(GridHeaderSlotUI slot, AttributeType previous)
    {
        if (slot == null) return;
        if (_disabledSlotSnapshots.Exists(s => s.slot == slot)) return;
        _disabledSlotSnapshots.Add(new DisabledSlotSnapshot { slot = slot, previousAttribute = previous });
    }

    private void RestoreDisabledSlots()
    {
        if (_disabledSlotSnapshots.Count == 0) return;

        foreach (var snapshot in _disabledSlotSnapshots)
        {
            if (snapshot.slot == null) continue;
            var slot = snapshot.slot;

            slot.SetLocked(false);

            if (snapshot.previousAttribute != AttributeType.None && slot.GetCurrentAttribute() == AttributeType.None)
                slot.SetAttribute(snapshot.previousAttribute);
        }
        _disabledSlotSnapshots.Clear();
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

        int r = _rng.Next(0, sum); // ?�수 기반 ?�플�?
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
}

