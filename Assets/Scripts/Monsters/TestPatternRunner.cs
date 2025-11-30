using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 테스트용: AttackPatternSO들을 스케줄하고 TurnManager.OnTurnAdvanced 이벤트로 카운트다운하여 실행함.
/// 간단 검증/디버깅용으로 Scene에 붙여 사용.
/// </summary>
public class TestPatternRunner : MonoBehaviour
{
    [Header("Optional: assign SO assets (if null, runtime-created samples will be used)")]
    public AttackPatternSO bombPatternAsset;
    public AttackPatternSO damagePatternAsset;
    public AttackPatternSO heavyPatternAsset;
    public AttackPatternSO groggyPatternAsset;

    [Header("Grid / spawn helpers (테스트용)")]
    [Tooltip("SpecificCell 또는 AroundMonster 모드 테스트 시 사용할 몬스터 그리드 위치")]
    public Vector2Int monsterGridPos = new Vector2Int(3, 3);

    [Tooltip("SpecificCell 모드에서 사용할 타깃 좌표")]
    public Vector2Int specificTarget = new Vector2Int(4, 4);

    private class Scheduled
    {
        public AttackPatternSO pattern;
        public int remainingTurns;
    }

    private readonly List<Scheduled> _scheduled = new List<Scheduled>();

    private void Awake()
    {
        // 런타임에 SO가 없으면 간단 샘플 생성
        if (bombPatternAsset == null)
        {
            var b = ScriptableObject.CreateInstance<BombAttackPatternSO>();
            b.displayName = "Test Bomb";
            b.attackType = AttackType.Bomb;
            b.delayTurns = 2;
            (b as BombAttackPatternSO).startTimer = 3;
            (b as BombAttackPatternSO).maxTimer = 6;
            (b as BombAttackPatternSO).spawnMode = BombAttackPatternSO.SpawnMode.RandomGrid;
            bombPatternAsset = b;
        }

        if (damagePatternAsset == null)
        {
            var d = ScriptableObject.CreateInstance<DamageAttackPatternSO>();
            d.displayName = "Test Damage";
            d.attackType = AttackType.Damage;
            d.delayTurns = 1;
            (d as DamageAttackPatternSO).damage = 5;
            (d as DamageAttackPatternSO).affectsShield = false;
            damagePatternAsset = d;
        }

        if (groggyPatternAsset == null)
        {
            var g = ScriptableObject.CreateInstance<GroggyAttackPatternSO>();
            g.displayName = "Test Groggy";
            g.attackType = AttackType.Groggy;
            g.delayTurns = 1;
            groggyPatternAsset = g;
        }

        if (heavyPatternAsset == null)
        {
            var h = ScriptableObject.CreateInstance<HeavyDamageAttackPatternSO>();
            h.displayName = "Test Heavy";
            h.attackType = AttackType.HeavyDamage;
            h.delayTurns = 2;
            (h as HeavyDamageAttackPatternSO).damage = 15;
            (h as HeavyDamageAttackPatternSO).cancelThreshold = 10;
            (h as HeavyDamageAttackPatternSO).groggyPattern = groggyPatternAsset;
            heavyPatternAsset = h;
        }
    }

    private void OnEnable()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnAdvanced += HandleTurnAdvanced;
        else
            StartCoroutine(AttachWhenReady());
    }

    private System.Collections.IEnumerator AttachWhenReady()
    {
        while (TurnManager.Instance == null)
        {
            yield return null;
        }
        TurnManager.Instance.OnTurnAdvanced += HandleTurnAdvanced;
    }

    private void OnDisable()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnAdvanced -= HandleTurnAdvanced;
    }

    private void HandleTurnAdvanced(int turn)
    {
        // 각 스케줄 카운트다운
        for (int i = _scheduled.Count - 1; i >= 0; i--)
        {
            var s = _scheduled[i];
            s.remainingTurns--;
            Debug.Log($"[TestPatternRunner] Tick pattern '{s.pattern.displayName}' -> remaining {s.remainingTurns}");
            if (s.remainingTurns <= 0)
            {
                ExecutePattern(s.pattern);
                _scheduled.RemoveAt(i);
            }
        }
    }

    private void ExecutePattern(AttackPatternSO pattern)
    {
        Debug.Log($"[TestPatternRunner] ExecutePattern: {pattern.displayName} ({pattern.attackType})");

        if (pattern.attackType == AttackType.Bomb)
        {
            var bp = pattern as BombAttackPatternSO;
            if (bp == null)
            {
                Debug.LogWarning("[TestPatternRunner] Pattern is Bomb type but cast failed.");
                return;
            }

            if (BombManager.Instance == null)
            {
                Debug.LogWarning("[TestPatternRunner] No BombManager instance in scene.");
                return;
            }

            bool spawned = false;
            Vector2Int outPos = default;

            switch (bp.spawnMode)
            {
                case BombAttackPatternSO.SpawnMode.RandomGrid:
                    spawned = BombManager.Instance.SpawnRandomGridBomb(bp.startTimer, bp.maxTimer, out outPos);
                    break;
                case BombAttackPatternSO.SpawnMode.SpecificCell:
                    spawned = BombManager.Instance.SpawnGridBombAt(specificTarget, bp.startTimer, bp.maxTimer);
                    outPos = specificTarget;
                    break;
                case BombAttackPatternSO.SpawnMode.AroundMonster:
                    // offsets 순서로 시도하여 첫 성공 위치에 스폰
                    if (bp.offsets != null && bp.offsets.Length > 0)
                    {
                        foreach (var off in bp.offsets)
                        {
                            var tryPos = new Vector2Int(monsterGridPos.x + off.x, monsterGridPos.y + off.y);
                            if (BombManager.Instance.SpawnGridBombAt(tryPos, bp.startTimer, bp.maxTimer))
                            {
                                spawned = true;
                                outPos = tryPos;
                                break;
                            }
                        }
                    }
                    break;
            }

            Debug.Log($"[TestPatternRunner] Bomb spawn attempted: success={spawned} pos={outPos}");
            return;
        }

        if (pattern.attackType == AttackType.Damage)
        {
            var dp = pattern as DamageAttackPatternSO;
            if (dp == null)
            {
                Debug.LogWarning("[TestPatternRunner] Pattern is Damage type but cast failed.");
                return;
            }

            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.ApplyPlayerDamage(dp.damage);
                Debug.Log($"[TestPatternRunner] Applied {dp.damage} dmg to player via CombatManager.");
            }
            else
            {
                Debug.Log($"[TestPatternRunner] (No CombatManager) Simulated damage: {dp.damage}");
            }
            return;
        }

        if (pattern.attackType == AttackType.HeavyDamage)
        {
            var hp = pattern as HeavyDamageAttackPatternSO;
            if (hp == null)
            {
                Debug.LogWarning("[TestPatternRunner] Pattern is HeavyDamage type but cast failed.");
                return;
            }

            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.ApplyPlayerDamage(hp.damage);
                Debug.Log($"[TestPatternRunner] Applied {hp.damage} heavy dmg to player via CombatManager.");
            }
            else
            {
                Debug.Log($"[TestPatternRunner] (No CombatManager) Simulated heavy damage: {hp.damage}");
            }
            return;
        }

        if (pattern.attackType == AttackType.Groggy)
        {
            var gp = pattern as GroggyAttackPatternSO;
            var desc = gp != null ? gp.description : string.Empty;
            Debug.Log($"[TestPatternRunner] Groggy pattern executed. (desc: {desc})");
            return;
        }

        Debug.LogWarning("[TestPatternRunner] Unknown pattern type.");
    }

    // Inspector 호출용: 즉시 스케줄 (delay = pattern.delayTurns)
    public void SchedulePattern(AttackPatternSO pattern)
    {
        if (pattern == null) { Debug.LogWarning("pattern null"); return; }
        _scheduled.Add(new Scheduled { pattern = pattern, remainingTurns = Math.Max(0, pattern.delayTurns) });
        Debug.Log($"[TestPatternRunner] Scheduled '{pattern.displayName}' for {pattern.delayTurns} turns.");
    }

    // Context menu / 인스펙터에서 바로 실행(디버깅)
    [ContextMenu("Schedule sample Bomb pattern")]
    private void ContextScheduleBomb() => SchedulePattern(bombPatternAsset);

    [ContextMenu("Schedule sample Damage pattern")]
    private void ContextScheduleDamage() => SchedulePattern(damagePatternAsset);

    [ContextMenu("Schedule sample Heavy pattern")]
    private void ContextScheduleHeavy() => SchedulePattern(heavyPatternAsset);

    [ContextMenu("Schedule sample Groggy pattern")]
    private void ContextScheduleGroggy() => SchedulePattern(groggyPatternAsset);
}
