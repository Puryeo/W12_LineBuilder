using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TurnManager: 턴 흐름 제어.
/// Shield 관련 per-turn 리셋/타이머 감소를 Bomb/Combat 처리 이전에 처리하도록 연동.
/// </summary>
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    [Header("Rest / Collapse")]
    public int maxRestStreak = 3;
    public int collapseDamagePerRemoved = 5;

    [Header("Debug")]
    public int TurnCount { get; private set; } = 0;
    public int RestStreak { get; private set; } = 0;

    public int TurnsUntilNextBomb => BombManager.Instance != null ? BombManager.Instance.TurnsUntilNextBomb : 0;

    private System.Random _rng = new System.Random();

    public event Action<int> OnTurnAdvanced;
    public event Action<int> OnRestStreakChanged;
    public event Action<int> OnBombSpawnCountdownChanged
    {
        add { if (BombManager.Instance != null) BombManager.Instance.OnBombSpawnCountdownChanged += value; }
        remove { if (BombManager.Instance != null) BombManager.Instance.OnBombSpawnCountdownChanged -= value; }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void Start()
    {
        if (GridManager.Instance != null)
            GridManager.Instance.OnBlockPlaced += HandleBlockPlaced;

        BombManager.Instance?.ScheduleNextBomb();
    }

    private void OnDestroy()
    {
        if (GridManager.Instance != null)
            GridManager.Instance.OnBlockPlaced -= HandleBlockPlaced;
    }

    private void HandleBlockPlaced(BlockSO block, Vector2Int origin, List<Vector2Int> abs)
    {
        if (RestStreak != 0)
        {
            RestStreak = 0;
            try { OnRestStreakChanged?.Invoke(RestStreak); } catch (Exception) { }
        }

        AdvanceTurn();
    }

    public void DoRestAction()
    {
        bool discarded = false;
        if (CardManager.Instance != null)
            discarded = CardManager.Instance.Rest();

        RestStreak++;
        Debug.Log($"[TurnManager] Rest performed. RestStreak={RestStreak}");
        try { OnRestStreakChanged?.Invoke(RestStreak); } catch (Exception) { }

        if (RestStreak >= maxRestStreak)
        {
            int removed = 0;
            GridManager.Instance?.ClearAll(out removed);
            int dmg = removed * collapseDamagePerRemoved;
            if (dmg > 0)
            {
                CombatManager.Instance?.ApplyPlayerDamage(dmg);
                Debug.Log($"[TurnManager] GridCollapse: removed {removed} cells -> Player takes {dmg} dmg.");
            }
            RestStreak = 0;
            try { OnRestStreakChanged?.Invoke(RestStreak); } catch (Exception) { }
        }

        AdvanceTurn();
    }

    public void AdvanceTurn()
    {
        TurnCount++;
        Debug.Log($"[TurnManager] AdvanceTurn -> TurnCount={TurnCount}");

        try { OnTurnAdvanced?.Invoke(TurnCount); } catch (Exception) { }

        // 0) Shield per-turn 리셋 및 remainingTurns 감소 처리
        if (PlayerShieldManager.Instance != null)
            PlayerShieldManager.Instance.OnNewTurn($"TurnManager.AdvanceTurn[{TurnCount}]");

        // 1) 기존 폭탄들에 대해 틱 처리(폭발 검사 및 Grid 동기화)
        var explodedBombs = BombManager.Instance != null ? BombManager.Instance.TickAllBombs() : new List<Bomb>();
        if (explodedBombs != null && explodedBombs.Count > 0)
        {
            int dmgPer = CombatManager.Instance != null ? CombatManager.Instance.bombExplosionPlayerDamage : 20;
            int totalDmg = explodedBombs.Count * dmgPer;
            if (totalDmg > 0)
            {
                CombatManager.Instance?.ApplyPlayerDamage(totalDmg);
                Debug.Log($"[TurnManager] Bombs exploded: {explodedBombs.Count} -> Player takes {totalDmg} dmg.");
            }
        }

        // 1.5) 몬스터 패턴 Tick: 폭발 처리 후, 자동 스폰 전 호출 (To-Do 문서 권장 위치)
        if (MonsterAttackManager.Instance == null)
        {
            Debug.LogWarning("[TurnManager] MonsterAttackManager.Instance is NULL at AdvanceTurn time.");
        }
        else
        {
            MonsterAttackManager.Instance.TickAll();
        }

        // 2) 스폰 카운트 감소 및 필요 시 스폰 처리 (신규로 생성된 폭탄은 다음 턴부터 틱됨)
        if (BombManager.Instance != null)
            BombManager.Instance.HandleTurnAdvance();
    }
}