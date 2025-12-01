using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// TurnManager: 페이즈 기반 턴 흐름 제어
/// - 코루틴 기반 턴 진행
/// - 라인 클리어는 즉시 처리 (기존 동작 유지)
/// - 몬스터 공격은 페이즈 시스템으로 처리 (병렬/직렬 실행 모드 지원)
/// - 애니메이션 완료 대기 시스템
/// </summary>
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    #region Turn Phase & Execution Mode Enums
    public enum TurnPhase
    {
        TurnStart,
        TickPhase,          // Shield, Bomb 타이머 감소, Monster 카운트다운
        // PlayerClearPhase는 제거 (라인 클리어는 즉시 처리됨)
        MonsterAttackPhase, // 몬스터 공격 실행
        BombPhase,          // 폭탄 자동 스폰
        TurnEnd
    }

    public enum ExecutionMode
    {
        Parallel,   // 모든 액션 동시 실행 (애니메이션 병렬)
        Sequential  // 액션 순차 실행 (하나씩 대기)
    }
    #endregion

    #region Inspector Settings
    [Header("Phase Execution Settings")]
    [Tooltip("몬스터 공격 페이즈 실행 모드 (ForceSequential이 true인 액션은 예외)")]
    public ExecutionMode monsterAttackMode = ExecutionMode.Parallel;

    // NOTE: 플레이어 라인 클리어는 즉시 처리되므로 실행 모드 설정 불필요
    // [Tooltip("플레이어 라인 클리어 페이즈 실행 모드")]
    // public ExecutionMode playerClearMode = ExecutionMode.Parallel;

    [Header("Debug")]
    public int TurnCount { get; private set; } = 0;
    #endregion

    #region Private State
    private bool _isTurnInProgress = false;
    // NOTE: 플레이어 액션 큐는 제거 (라인 클리어는 즉시 처리)
    // private Queue<IPhaseAction> _playerActionQueue = new Queue<IPhaseAction>();
    private System.Random _rng = new System.Random();
    #endregion

    #region Events
    public event Action<int> OnTurnAdvanced;
    public event Action<TurnPhase> OnPhaseChanged;

    /// <summary>
    /// 플레이어 턴 시작 이벤트 (블록 배치 가능)
    /// </summary>
    public event Action OnPlayerTurnStart;

    /// <summary>
    /// 적 턴 시작 이벤트 (몬스터 공격 및 폭탄 페이즈)
    /// </summary>
    public event Action OnEnemyTurnStart;

    public event Action<int> OnBombSpawnCountdownChanged
    {
        add { if (BombManager.Instance != null) BombManager.Instance.OnBombSpawnCountdownChanged += value; }
        remove { if (BombManager.Instance != null) BombManager.Instance.OnBombSpawnCountdownChanged -= value; }
    }

    public int TurnsUntilNextBomb => BombManager.Instance != null ? BombManager.Instance.TurnsUntilNextBomb : 0;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void Start()
    {
        // GridManager의 OnLinesCleared 이벤트 구독 → LineClearAction 큐잉
        if (GridManager.Instance != null)
        {
            GridManager.Instance.OnLinesCleared += HandleLinesCleared;
        }

        // CardManager의 OnHandEmpty 이벤트 구독
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnHandEmpty += HandleHandEmpty;
        }

        BombManager.Instance?.ScheduleNextBomb();

        // 게임 시작 시 플레이어 턴 시작
        StartPlayerTurn();
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
    #endregion

    #region Public API
    // NOTE: 플레이어 액션 큐는 제거 (라인 클리어는 즉시 처리)
    // /// <summary>
    // /// 플레이어 액션을 큐에 추가 (라인 클리어 등)
    // /// </summary>
    // public void EnqueuePlayerAction(IPhaseAction action)
    // {
    //     if (action == null) return;
    //     _playerActionQueue.Enqueue(action);
    //     Debug.Log($"[TurnManager] Player action enqueued. Queue size: {_playerActionQueue.Count}");
    // }

    /// <summary>
    /// 플레이어 턴 시작 (블록 배치 가능)
    /// </summary>
    private void StartPlayerTurn()
    {
        Debug.Log("[TurnManager] Player turn started");
        OnPlayerTurnStart?.Invoke();
        // 플레이어가 블록을 배치하고 라인을 클리어할 수 있음
    }

    /// <summary>
    /// Pass 버튼 클릭 시 호출: 수동 턴 종료
    /// </summary>
    public void DoPassAction()
    {
        if (_isTurnInProgress)
        {
            Debug.Log("[TurnManager] Turn already in progress, ignoring Pass action");
            return;
        }

        Debug.Log("[TurnManager] Pass action - manually ending turn");

        // 손패 전체 버림 및 새 손패 드로우
        if (CardManager.Instance != null)
        {
            CardManager.Instance.EndTurnDiscardAndDraw();
        }

        // 적 턴 시작 이벤트 발행 (몬스터 공격 및 폭탄 페이즈)
        OnEnemyTurnStart?.Invoke();
        Debug.Log("[TurnManager] Enemy turn started");

        StartCoroutine(RunTurn());
    }
    #endregion

    #region Event Handlers
    /// <summary>
    /// 라인 클리어 이벤트 핸들러 → 즉시 처리 (기존 로직 유지)
    /// </summary>
    private void HandleLinesCleared(GridManager.LineClearResult result)
    {
        if (result == null) return;

        int totalLines = result.ClearedRows.Count + result.ClearedCols.Count;
        if (totalLines == 0) return;

        Debug.Log($"[TurnManager] Line cleared: {totalLines} line(s) - Processing immediately");

        // 라인 클리어는 즉시 코루틴으로 처리 (기존 동작 유지)
        StartCoroutine(ProcessLineClearImmediate(result));
    }

    /// <summary>
    /// 라인 클리어 즉시 처리 코루틴
    /// </summary>
    private IEnumerator ProcessLineClearImmediate(GridManager.LineClearResult result)
    {
        var action = new LineClearAction(result);

        float duration = 0f;
        yield return StartCoroutine(action.Play(d => duration = d));

        Debug.Log($"[TurnManager] Line clear processed in {duration:F2}s");
    }

    /// <summary>
    /// 손패가 비었을 때 자동 턴 종료 (현재 주석 처리)
    /// </summary>
    private void HandleHandEmpty()
    {
        Debug.Log("[TurnManager] Hand is empty - auto turn advance disabled");
        // 필요 시 활성화
        // DoPassAction();
    }
    #endregion

    #region Main Turn Routine
    /// <summary>
    /// 메인 턴 진행 코루틴
    /// </summary>
    private IEnumerator RunTurn()
    {
        _isTurnInProgress = true;
        TurnCount++;

        Debug.Log($"[TurnManager] ========== Turn {TurnCount} Start ==========");

        // Phase 0: TurnStart
        OnPhaseChanged?.Invoke(TurnPhase.TurnStart);
        OnTurnAdvanced?.Invoke(TurnCount);

        // Phase 1: TickPhase (동기)
        yield return StartCoroutine(TickPhase());

        // Phase 2: PlayerClearPhase - 제거 (라인 클리어는 즉시 처리됨)
        // yield return StartCoroutine(PlayerClearPhase());

        // Phase 3: MonsterAttackPhase (코루틴)
        yield return StartCoroutine(MonsterAttackPhase());

        // Phase 4: BombPhase (동기)
        yield return StartCoroutine(BombPhase());

        // Phase 5: TurnEnd
        OnPhaseChanged?.Invoke(TurnPhase.TurnEnd);
        Debug.Log($"[TurnManager] ========== Turn {TurnCount} End ==========");

        // 적 턴 종료 후 잠시 대기 (플레이어 턴 UI 진입 전 버퍼)
        yield return new WaitForSeconds(1.5f);

        // 반드시 플래그 해제 (각 페이즈 내부에서 예외 처리)
        _isTurnInProgress = false;

        // 적 턴 종료 후 다시 플레이어 턴 시작
        StartPlayerTurn();
    }
    #endregion

    #region Phase Implementations
    /// <summary>
    /// TickPhase: 폭탄 타이머 감소/폭발, 몬스터 카운트다운, 쉴드 리셋
    /// </summary>
    private IEnumerator TickPhase()
    {
        OnPhaseChanged?.Invoke(TurnPhase.TickPhase);
        Debug.Log("[TurnManager] --- TickPhase Start ---");

        // 1. Shield per-turn 리셋
        if (PlayerShieldManager.Instance != null)
        {
            PlayerShieldManager.Instance.OnNewTurn($"TurnManager.TickPhase[{TurnCount}]");
        }

        // 2. 기존 폭탄 타이머 감소 및 폭발 처리
        var explodedBombs = BombManager.Instance != null ? BombManager.Instance.TickAllBombs() : new List<Bomb>();
        if (explodedBombs != null && explodedBombs.Count > 0)
        {
            int dmgPer = CombatManager.Instance != null ? CombatManager.Instance.bombExplosionPlayerDamage : 20;
            int totalDmg = explodedBombs.Count * dmgPer;
            if (totalDmg > 0)
            {
                CombatManager.Instance?.ApplyPlayerDamage(totalDmg);
                Debug.Log($"[TurnManager] Bombs exploded: {explodedBombs.Count} → Player takes {totalDmg} dmg");
            }
        }

        // 3. 몬스터 패턴 카운트다운 (실행은 하지 않음!)
        if (MonsterAttackManager.Instance != null)
        {
            MonsterAttackManager.Instance.TickAll();
            Debug.Log("[TurnManager] Monster patterns ticked (countdown only)");
        }
        else
        {
            Debug.LogWarning("[TurnManager] MonsterAttackManager.Instance is NULL");
        }

        Debug.Log("[TurnManager] --- TickPhase End ---");
        yield break;
    }

    // NOTE: PlayerClearPhase는 제거됨 (라인 클리어는 즉시 처리)
    // /// <summary>
    // /// PlayerClearPhase: 큐에 쌓인 플레이어 라인 클리어 액션 처리
    // /// </summary>
    // private IEnumerator PlayerClearPhase()
    // {
    //     OnPhaseChanged?.Invoke(TurnPhase.PlayerClearPhase);
    //     Debug.Log("[TurnManager] --- PlayerClearPhase Start ---");
    //
    //     // 큐 스냅샷 생성 (페이즈 도중 추가된 액션은 다음 턴으로)
    //     var actionsSnapshot = new List<IPhaseAction>();
    //     while (_playerActionQueue.Count > 0)
    //     {
    //         actionsSnapshot.Add(_playerActionQueue.Dequeue());
    //     }
    //
    //     if (actionsSnapshot.Count == 0)
    //     {
    //         Debug.Log("[TurnManager] No player actions to process");
    //         yield break;
    //     }
    //
    //     Debug.Log($"[TurnManager] Processing {actionsSnapshot.Count} player action(s)");
    //
    //     // 실행 모드 결정
    //     ExecutionMode mode = DetermineExecutionMode(playerClearMode, actionsSnapshot);
    //
    //     if (mode == ExecutionMode.Parallel)
    //     {
    //         yield return StartCoroutine(ExecuteParallel(actionsSnapshot));
    //     }
    //     else
    //     {
    //         yield return StartCoroutine(ExecuteSequential(actionsSnapshot));
    //     }
    //
    //     Debug.Log("[TurnManager] --- PlayerClearPhase End ---");
    // }

    /// <summary>
    /// MonsterAttackPhase: 준비된 몬스터들의 공격 실행
    /// </summary>
    private IEnumerator MonsterAttackPhase()
    {
        OnPhaseChanged?.Invoke(TurnPhase.MonsterAttackPhase);
        Debug.Log("[TurnManager] --- MonsterAttackPhase Start ---");

        if (MonsterAttackManager.Instance == null)
        {
            Debug.LogWarning("[TurnManager] MonsterAttackManager.Instance is NULL");
            yield break;
        }

        // 실행 준비된 몬스터 가져오기 (RemainingTurns <= 0 && !IsDead)
        var readyMonsters = MonsterAttackManager.Instance.GetReadyMonsters();

        if (readyMonsters == null || readyMonsters.Count == 0)
        {
            Debug.Log("[TurnManager] No ready monsters to attack");
            yield break;
        }

        Debug.Log($"[TurnManager] {readyMonsters.Count} monster(s) ready to attack");

        // MonsterController는 IPhaseAction을 구현하므로 캐스팅
        var actions = readyMonsters.OfType<IPhaseAction>().ToList();

        if (actions.Count == 0)
        {
            Debug.LogWarning("[TurnManager] No monsters implement IPhaseAction");
            yield break;
        }

        // 실행 모드 결정
        ExecutionMode mode = DetermineExecutionMode(monsterAttackMode, actions);

        if (mode == ExecutionMode.Parallel)
        {
            yield return StartCoroutine(ExecuteParallel(actions));
        }
        else
        {
            yield return StartCoroutine(ExecuteSequential(actions));
        }

        Debug.Log("[TurnManager] --- MonsterAttackPhase End ---");
    }

    /// <summary>
    /// BombPhase: 폭탄 자동 스폰 처리
    /// </summary>
    private IEnumerator BombPhase()
    {
        OnPhaseChanged?.Invoke(TurnPhase.BombPhase);
        Debug.Log("[TurnManager] --- BombPhase Start ---");

        if (BombManager.Instance != null)
        {
            BombManager.Instance.HandleTurnAdvance();
        }

        Debug.Log("[TurnManager] --- BombPhase End ---");
        yield break;
    }
    #endregion

    #region Execution Mode Logic
    /// <summary>
    /// 실행 모드 결정: 전역 모드 우선, ForceSequential이 하나라도 있으면 직렬로
    /// </summary>
    private ExecutionMode DetermineExecutionMode(ExecutionMode globalMode, List<IPhaseAction> actions)
    {
        // 하나라도 ForceSequential이면 전체를 직렬로
        bool hasForceSequential = actions.Any(a => a.ForceSequential);

        if (hasForceSequential)
        {
            Debug.Log($"[TurnManager] ExecutionMode: Sequential (ForceSequential detected)");
            return ExecutionMode.Sequential;
        }

        Debug.Log($"[TurnManager] ExecutionMode: {globalMode}");
        return globalMode;
    }

    /// <summary>
    /// 병렬 실행: 모든 액션을 동시에 시작하고, 가장 긴 것을 대기
    /// </summary>
    private IEnumerator ExecuteParallel(List<IPhaseAction> actions)
    {
        Debug.Log($"[TurnManager] ExecuteParallel: {actions.Count} action(s)");

        float maxDuration = 0f;
        int completedCount = 0;
        int totalActions = actions.Count;

        // 모든 액션을 동시에 시작
        foreach (var action in actions)
        {
            StartCoroutine(action.Play(duration =>
            {
                completedCount++;
                if (duration > maxDuration)
                {
                    maxDuration = duration;
                }
                Debug.Log($"[TurnManager] Action completed in {duration:F2}s ({completedCount}/{totalActions})");
            }));
        }

        // 모든 액션이 완료될 때까지 대기
        float timeout = 10f; // 최대 대기 시간
        float elapsed = 0f;

        while (completedCount < totalActions && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (elapsed >= timeout)
        {
            Debug.LogWarning($"[TurnManager] Parallel execution timeout! {completedCount}/{totalActions} completed");
        }
        else
        {
            Debug.Log($"[TurnManager] Parallel execution complete. Max duration: {maxDuration:F2}s");
        }
    }

    /// <summary>
    /// 직렬 실행: 액션을 하나씩 순차적으로 실행
    /// </summary>
    private IEnumerator ExecuteSequential(List<IPhaseAction> actions)
    {
        Debug.Log($"[TurnManager] ExecuteSequential: {actions.Count} action(s)");

        for (int i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            float duration = 0f;
            bool completed = false;

            yield return StartCoroutine(action.Play(d =>
            {
                duration = d;
                completed = true;
            }));

            // 완료 대기 (안전장치)
            float timeout = 10f;
            float elapsed = 0f;
            while (!completed && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Debug.Log($"[TurnManager] Action {i + 1}/{actions.Count} completed in {duration:F2}s");

            // 다음 액션 전 짧은 딜레이 (선택사항)
            if (i < actions.Count - 1)
            {
                yield return new WaitForSeconds(0.2f);
            }
        }

        Debug.Log("[TurnManager] Sequential execution complete");
    }
    #endregion
}
