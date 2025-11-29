using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TurnManager: 턴 흐름 제어.
/// Shield 관련 per-turn 리셋/타이머 감소를 Bomb/Combat 처리 이전에 처리하도록 연동.
/// 변경: 카드 사용 시 자동 턴 종료 제거, 명시적 EndPlayerTurn 추가, 턴 시작 시 카드 보충 처리 추가.
/// 진단: Draw/Monster 매니저 존재 여부 로그 추가.
/// </summary>
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    [Header("Rest / Collapse")]
    public int maxRestStreak = 3;
    public int collapseDamagePerRemoved = 5;

    [Header("Card Draw")]
    [Tooltip("새 턴이 시작될 때 자동으로 드로우할 카드 장수")]
    public int drawCardsOnTurnStart = 1;

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

        // 변경: 배치 직후 자동으로 턴을 종료하지 않습니다.
    }

    /// <summary>
    /// 플레이어가 End Turn 버튼을 눌렀을 때 호출합니다.
    /// </summary>
    public void EndPlayerTurn()
    {
        Debug.Log("[TurnManager] EndPlayerTurn called (UI End Turn)");
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

        // Rest는 즉시 턴 종료(기존 동작 유지)
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

        // 진단: CardManager 및 hand 상태
        if (CardManager.Instance == null)
        {
            Debug.LogWarning("[TurnManager] CardManager.Instance is NULL at AdvanceTurn time.");
        }
        else
        {
            int beforeHand = CardManager.Instance.GetHand()?.Count ?? -1;
            Debug.Log($"[TurnManager] drawCardsOnTurnStart={drawCardsOnTurnStart}, handBeforeDraw={beforeHand}");
            if (drawCardsOnTurnStart > 0)
            {
                CardManager.Instance.Draw(drawCardsOnTurnStart);
                int afterHand = CardManager.Instance.GetHand()?.Count ?? -1;
                Debug.Log($"[TurnManager] Drew {drawCardsOnTurnStart} card(s) at turn start. handAfterDraw={afterHand}");
            }
        }

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