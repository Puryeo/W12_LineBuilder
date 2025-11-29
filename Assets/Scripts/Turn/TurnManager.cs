using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TurnManager: 턴 흐름 제어.
/// 새로운 플레이 방식:
/// - 손패 전체를 한 턴에 사용
/// - 라인 완성 시 보너스 드로우 및 턴 연장
/// - 손패가 비면 자동으로 턴 종료
/// - Pass 버튼으로 수동 턴 종료도 가능
/// </summary>
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    [Header("Debug")]
    public int TurnCount { get; private set; } = 0;

    public int TurnsUntilNextBomb => BombManager.Instance != null ? BombManager.Instance.TurnsUntilNextBomb : 0;

    private System.Random _rng = new System.Random();

    public event Action<int> OnTurnAdvanced;
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
        // GridManager의 OnLinesCleared 이벤트 구독 (LineClearResult 타입)
        if (GridManager.Instance != null)
        {
            GridManager.Instance.OnLinesCleared += HandleLinesCleared;
        }

        // CardManager의 OnHandEmpty 이벤트 구독 (손패가 비면 자동 턴 종료)
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnHandEmpty += HandleHandEmpty;
        }

        BombManager.Instance?.ScheduleNextBomb();
    }

    private void OnDestroy()
    {
        if (GridManager.Instance != null)
        {
            GridManager.Instance.OnLinesCleared -= HandleLinesCleared;
        }

        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnHandEmpty -= HandleHandEmpty;
        }
    }

    /// <summary>
    /// 라인이 클리어될 때 호출됩니다.
    /// LineClearResult를 받아서 폭탄 포함 여부에 따라 보너스 드로우 처리
    /// </summary>
    private void HandleLinesCleared(GridManager.LineClearResult result)
    {
        if (CardManager.Instance == null || result == null) return;

        // 총 클리어된 라인 수 계산
        int totalLines = result.ClearedRows.Count + result.ClearedCols.Count;

        if (result.ContainedBomb)
        {
            // 폭탄 포함 라인 완성: 2장 드로우
            Debug.Log($"[TurnManager] Bomb line cleared! Drawing 2 bonus cards. (Lines: {totalLines})");
            CardManager.Instance.DrawBonusForBombLine();
        }
        else if (totalLines > 0)
        {
            // 일반 라인 완성: 1장 드로우
            Debug.Log($"[TurnManager] Normal line cleared! Drawing 1 bonus card. (Lines: {totalLines})");
            CardManager.Instance.DrawBonusForNormalLine();
        }
    }

    /// <summary>
    /// 손패가 비었을 때 자동으로 호출: 자동 턴 종료
    /// </summary>
    private void HandleHandEmpty()
    {
        Debug.Log("[TurnManager] Hand is empty - automatically ending turn");

        // 손패 전체 버림 및 새 손패 드로우
        /*if (CardManager.Instance != null)
        {
            CardManager.Instance.EndTurnDiscardAndDraw();
        }*/
    }

    /// <summary>
    /// Pass 버튼을 눌렀을 때 호출: 수동 턴 종료
    /// </summary>
    public void DoPassAction()
    {
        Debug.Log("[TurnManager] Pass action - manually ending turn");

        // 손패 전체 버림 및 새 손패 드로우
        if (CardManager.Instance != null)
        {
            CardManager.Instance.EndTurnDiscardAndDraw();
        }

        AdvanceTurn();
    }

    /// <summary>
    /// 턴을 진행합니다 (손패가 비거나 Pass 시 호출됨)
    /// </summary>
    private void AdvanceTurn()
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

        // 1.5) 몬스터 패턴 Tick
        if (MonsterAttackManager.Instance == null)
        {
            Debug.LogWarning("[TurnManager] MonsterAttackManager.Instance is NULL at AdvanceTurn time.");
        }
        else
        {
            MonsterAttackManager.Instance.TickAll();
        }

        // 2) 스폰 카운트 감소 및 필요 시 스폰 처리
        if (BombManager.Instance != null)
            BombManager.Instance.HandleTurnAdvance();
    }
}