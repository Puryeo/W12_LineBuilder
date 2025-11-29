using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TurnManager: 턴 흐름 제어.
/// 코루틴 기반 단계별 턴 시스템으로 각 단계마다 애니메이션 대기 가능
/// </summary>
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    [Header("Turn Animation Settings")]
    [Tooltip("폭탄 폭발 애니메이션 최소 대기 시간 (초)")]
    public float bombExplosionMinDuration = 0.5f;

    [Tooltip("폭탄 폭발 애니메이션 최대 대기 시간 (초)")]
    public float bombExplosionMaxDuration = 2.0f;

    [Tooltip("몬스터 공격 간 딜레이 (초)")]
    public float monsterAttackDelay = 0.5f;

    [Header("Debug")]
    public int TurnCount { get; private set; } = 0;

    public int TurnsUntilNextBomb => BombManager.Instance != null ? BombManager.Instance.TurnsUntilNextBomb : 0;

    public bool IsTurnInProgress { get; private set; } = false;

    private bool _isGameEnded = false;

    private System.Random _rng = new System.Random();

    // 이벤트
    public event Action<int> OnTurnAdvanced; // 하위 호환성 유지
    public event Action<TurnPhase> OnTurnPhaseChanged;
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
        if (result == null) return;

        // 총 클리어된 라인 수 계산
        int totalLines = result.ClearedRows.Count + result.ClearedCols.Count;

        if (totalLines == 0) return;

        //// 보너스 드로우
        //if (CardManager.Instance != null)
        //{
        //    if (result.ContainedBomb)
        //    {
        //        // 폭탄 포함 라인 완성: 2장 드로우
        //        Debug.Log($"[TurnManager] Bomb line cleared! Drawing 2 bonus cards. (Lines: {totalLines})");
        //        CardManager.Instance.DrawBonusForBombLine();
        //    }
        //    else
        //    {
        //        // 일반 라인 완성: 1장 드로우
        //        Debug.Log($"[TurnManager] Normal line cleared! Drawing 1 bonus card. (Lines: {totalLines})");
        //        CardManager.Instance.DrawBonusForNormalLine();
        //    }
        //}

        // eraser 충전 부분 제거 (Pass 버튼으로만 충전)
        // if (EraserManager.Instance != null)
        // {
        //     EraserManager.Instance.AddEraserCharge(totalLines);
        //     Debug.Log($"[TurnManager] Added {totalLines} eraser charge(s) for line clear");
        // }
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
        // 턴 진행 중이면 무시
        if (IsTurnInProgress)
        {
            Debug.LogWarning("[TurnManager] Turn already in progress, ignoring DoPassAction");
            return;
        }

        Debug.Log("[TurnManager] Pass action - manually ending turn");

        // 손패 전체 버림 및 새 손패 드로우 (동기)
        if (CardManager.Instance != null)
        {
            CardManager.Instance.EndTurnDiscardAndDraw();
        }

        // 턴 진행 코루틴 시작
        StartCoroutine(AdvanceTurnRoutine());
    }

    /// <summary>
    /// 게임 종료 시 호출 (승리/패배)
    /// </summary>
    public void OnGameEnded()
    {
        _isGameEnded = true;
        StopAllCoroutines();

        // 입력 차단 해제
        if (InputBlocker.Instance != null)
        {
            InputBlocker.Instance.UnblockInput("TurnInProgress");
        }

        IsTurnInProgress = false;
    }

    /// <summary>
    /// 턴을 코루틴으로 진행 (단계별 애니메이션 대기 가능)
    /// </summary>
    private IEnumerator AdvanceTurnRoutine()
    {
        // 진입 체크
        if (IsTurnInProgress)
        {
            Debug.LogWarning("[TurnManager] Turn already in progress, aborting");
            yield break;
        }

        IsTurnInProgress = true;

        // 입력 차단
        if (InputBlocker.Instance != null)
        {
            InputBlocker.Instance.BlockInput("TurnInProgress");
        }

        try
        {
            TurnCount++;
            Debug.Log($"[TurnManager] AdvanceTurnRoutine -> TurnCount={TurnCount}");

            // 게임 종료 체크
            if (_isGameEnded)
            {
                Debug.Log("[TurnManager] Game ended, aborting turn");
                yield break;
            }

            // 하위 호환 이벤트 발행
            try { OnTurnAdvanced?.Invoke(TurnCount); } catch (Exception ex) { Debug.LogError($"[TurnManager] OnTurnAdvanced error: {ex}"); }

            // === Phase 1: Turn Start ===
            try { OnTurnPhaseChanged?.Invoke(TurnPhase.TurnStart); } catch (Exception ex) { Debug.LogError($"[TurnManager] OnTurnPhaseChanged error: {ex}"); }

            // === Phase 2: Shield Reset ===
            try { OnTurnPhaseChanged?.Invoke(TurnPhase.ShieldReset); } catch (Exception ex) { Debug.LogError($"[TurnManager] OnTurnPhaseChanged error: {ex}"); }

            if (PlayerShieldManager.Instance != null)
            {
                PlayerShieldManager.Instance.OnNewTurn($"TurnManager.Phase2[{TurnCount}]");
            }

            // === Phase 3: Bomb Explosion ===
            try { OnTurnPhaseChanged?.Invoke(TurnPhase.BombExplosion); } catch (Exception ex) { Debug.LogError($"[TurnManager] OnTurnPhaseChanged error: {ex}"); }

            if (BombManager.Instance != null)
            {
                var explodedBombs = BombManager.Instance.TickAllBombs();

                if (explodedBombs != null && explodedBombs.Count > 0)
                {
                    // TODO: 폭탄 폭발 이펙트 스폰
                    // foreach (var bomb in explodedBombs)
                    // {
                    //     Instantiate(bombExplosionEffectPrefab, bomb.position, Quaternion.identity);
                    // }

                    // 애니메이션 대기
                    float startTime = Time.time;
                    while (Time.time - startTime < bombExplosionMaxDuration)
                    {
                        if (_isGameEnded) yield break;

                        if (Time.time - startTime >= bombExplosionMinDuration)
                        {
                            break;
                        }

                        yield return null;
                    }

                    // 게임 종료 체크
                    if (_isGameEnded) yield break;

                    // 실제 데미지 적용
                    int dmgPer = CombatManager.Instance != null ? CombatManager.Instance.bombExplosionPlayerDamage : 20;
                    int totalDmg = explodedBombs.Count * dmgPer;
                    if (totalDmg > 0 && CombatManager.Instance != null)
                    {
                        CombatManager.Instance.ApplyPlayerDamage(totalDmg, $"BombExplosion[x{explodedBombs.Count}]");
                        Debug.Log($"[TurnManager] Bombs exploded: {explodedBombs.Count} -> Player takes {totalDmg} dmg.");
                    }
                }
            }

            // === Phase 4: Monster Attack ===
            try { OnTurnPhaseChanged?.Invoke(TurnPhase.MonsterAttack); } catch (Exception ex) { Debug.LogError($"[TurnManager] OnTurnPhaseChanged error: {ex}"); }

            if (MonsterAttackManager.Instance != null)
            {
                // 모든 몬스터 카운트다운 (동기)
                MonsterAttackManager.Instance.TickAll();

                // 실행 준비된 몬스터 스냅샷 복사
                var allMonsters = MonsterAttackManager.Instance.GetRegisteredMonsters();
                var readyMonsters = new List<IMonsterController>();

                foreach (var monster in allMonsters)
                {
                    if (monster != null && monster.IsReadyToExecute())
                    {
                        readyMonsters.Add(monster);
                    }
                }

                // 각 몬스터 순차 공격
                foreach (var monster in readyMonsters)
                {
                    // 게임 종료 체크
                    if (_isGameEnded) yield break;

                    // 몬스터 사망 재확인
                    if (monster.IsDead()) continue;

                    // 몬스터 공격 코루틴 실행 (대기)
                    yield return StartCoroutine(monster.ExecutePatternRoutine());

                    // 다음 몬스터 전 짧은 딜레이
                    if (monsterAttackDelay > 0)
                    {
                        yield return new WaitForSeconds(monsterAttackDelay);
                    }
                }
            }
            else
            {
                Debug.LogWarning("[TurnManager] MonsterAttackManager.Instance is NULL");
            }

            // === Phase 5: Bomb Auto-Spawn ===
            try { OnTurnPhaseChanged?.Invoke(TurnPhase.BombAutoSpawn); } catch (Exception ex) { Debug.LogError($"[TurnManager] OnTurnPhaseChanged error: {ex}"); }

            if (BombManager.Instance != null)
            {
                BombManager.Instance.HandleTurnAdvance();
            }

            // === Phase 6: Turn End ===
            try { OnTurnPhaseChanged?.Invoke(TurnPhase.TurnEnd); } catch (Exception ex) { Debug.LogError($"[TurnManager] OnTurnPhaseChanged error: {ex}"); }

            Debug.Log($"[TurnManager] Turn {TurnCount} completed");
        }
        finally
        {
            // 반드시 플래그 해제
            IsTurnInProgress = false;

            // 입력 차단 해제
            if (InputBlocker.Instance != null)
            {
                InputBlocker.Instance.UnblockInput("TurnInProgress");
            }
        }
    }
}