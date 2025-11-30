using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class BombManager : MonoBehaviour
{
    public static BombManager Instance { get; private set; }

    [Header("Bomb Spawn")]
    [Tooltip("자동 스폰 활성화 여부 (false면 몬스터/외부에서만 폭탄 생성)")]
    public bool enableAutoSpawn = true;

    [Tooltip("최소 스폰 간격(턴)")]
    public int minBombSpawnTurns = 6;

    [Tooltip("최대 스폰 간격(턴)")]
    public int maxBombSpawnTurns = 7;

    [Tooltip("스폰된 폭탄의 초기 timer")]
    public int bombTimerOnSpawn = 3;

    [Tooltip("스폰된 폭탄의 maxTimer (cap). 기본값 6")]
    public int bombMaxTimer = 6;

    [Header("View (Bomb Timer Text)")]
    [Tooltip("폭탄 타이머에 사용할 TMP Font Asset")]
    public TMP_FontAsset bombTimerFont;
    [Tooltip("폭탄 타이머 폰트 크기 (world text scale)")]
    public float bombTimerFontSize = 3f;
    [Tooltip("폭탄 타이머 텍스트 색상")]
    public Color bombTimerColor = Color.white;
    [Tooltip("타이머 텍스트의 Z 오프셋 (뷰에 대한 높낮이 조정)")]
    public float bombTimerZOffset = 0.1f;

    [Header("Debug")]
    [Tooltip("다음 폭탄까지 남은 턴(읽기 전용)")]
    public int TurnsUntilNextBomb { get; private set; }

    private readonly List<Bomb> _bombs = new List<Bomb>();
    private int _turnsUntilNextBomb;
    private System.Random _rng = new System.Random();

    public event Action<int> OnBombSpawnCountdownChanged; // 인자: remaining turns

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void Start()
    {
        if (enableAutoSpawn)
            ScheduleNextBomb();
        else
            Debug.Log("[BombManager] Auto-spawn disabled (enableAutoSpawn=false).");
    }

    #region Registration
    public void Register(Bomb b)
    {
        if (b == null) return;
        if (!_bombs.Contains(b)) _bombs.Add(b);
    }

    public void Unregister(Bomb b)
    {
        if (b == null) return;
        _bombs.Remove(b);
    }

    public IReadOnlyList<Bomb> GetAllBombs() => _bombs.AsReadOnly();
    #endregion

    #region Spawn scheduling
    public void ScheduleNextBomb()
    {
        if (!enableAutoSpawn)
        {
            // 자동 스폰 비활성화이면 카운트/이벤트를 건너뜀
            TurnsUntilNextBomb = -1;
            return;
        }

        _turnsUntilNextBomb = UnityEngine.Random.Range(minBombSpawnTurns, maxBombSpawnTurns + 1);
        TurnsUntilNextBomb = _turnsUntilNextBomb;
        Debug.Log($"[BombManager] Next bomb in {_turnsUntilNextBomb} turns.");
        try { OnBombSpawnCountdownChanged?.Invoke(TurnsUntilNextBomb); } catch (Exception) { }
    }

    /// <summary>
    /// 자동 스폰 카운트 감소 및 필요 시 자동 스폰 수행.
    /// enableAutoSpawn == false면 아무 동작도 하지 않음.
    /// </summary>
    public bool HandleTurnAdvance()
    {
        if (!enableAutoSpawn) return false;

        _turnsUntilNextBomb--;
        TurnsUntilNextBomb = _turnsUntilNextBomb;
        try { OnBombSpawnCountdownChanged?.Invoke(TurnsUntilNextBomb); } catch (Exception) { }

        if (_turnsUntilNextBomb <= 0)
        {
            Vector2Int pos = default;
            bool spawned = false;
            if (GridManager.Instance != null)
                spawned = GridManager.Instance.TrySpawnRandomBomb(bombTimerOnSpawn, out pos);

            Debug.Log($"[BombManager] Bomb spawn attempted: success={spawned} pos={pos}");
            if (spawned)
            {
                var go = new GameObject($"Bomb_{pos.x}_{pos.y}");
                go.transform.SetParent(GridManager.Instance.transform, false);
                go.transform.position = GridManager.Instance.GridToWorld(pos);
                var b = go.AddComponent<Bomb>();
                b.InitializeGridBomb(pos, bombTimerOnSpawn, bombMaxTimer);
                Register(b);

                GridManager.Instance.UpdateBombViewTimer(pos, b.timer);
            }
            ScheduleNextBomb();
            return spawned;
        }
        return false;
    }
    #endregion

    #region Global effects / Tick
    public void ApplyShieldToAllBombs()
    {
        Debug.Log($"[BombManager] ApplyShieldToAllBombs -> {_bombs.Count} bombs");
        foreach (var b in _bombs)
        {
            if (b == null) continue;
            int before = b.timer;
            int after = Math.Min(before + 1, b.maxTimer);
            if (before != after)
            {
                b.SetTimer(after);
                Debug.Log($"WaterEffect: Bomb[{b.bombId}] {before}->{after}", b);
            }
            else
            {
                Debug.Log($"WaterEffect: Bomb[{b.bombId}] capped {before} (max {b.maxTimer})", b);
            }

            if (b.isGridBomb)
                GridManager.Instance?.UpdateBombViewTimer(b.gridPos, b.timer);
        }
    }

    public List<Bomb> TickAllBombs()
    {
        var exploded = new List<Bomb>();
        for (int i = _bombs.Count - 1; i >= 0; i--)
        {
            var b = _bombs[i];
            if (b == null) { _bombs.RemoveAt(i); continue; }

            int before = b.timer;
            b.timer = Math.Max(0, b.timer - 1);
            if (before != b.timer)
                Debug.Log($"[BombManager] Tick Bomb[{b.bombId}] {before}->{b.timer}", b);

            if (b.isGridBomb)
            {
                GridManager.Instance?.UpdateBombViewTimer(b.gridPos, b.timer);
            }

            if (b.timer <= 0)
            {
                exploded.Add(b);
                _bombs.RemoveAt(i);
                Debug.Log($"[BombManager] Bomb exploded: {b.bombId}", b);

                if (b.isGridBomb)
                {
                    GridManager.Instance?.ClearBombAt(b.gridPos);
                }

                if (b.gameObject != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying) DestroyImmediate(b.gameObject);
                    else
#endif
                        Destroy(b.gameObject);
                }
            }
        }
        return exploded;
    }
    #endregion

    // 새 메서드: Grid에서 제거된 폭탄에 대해 BombManager 내부 인스턴스도 즉시 제거
    public bool RemoveGridBombAt(Vector2Int gridPos)
    {
        for (int i = _bombs.Count - 1; i >= 0; i--)
        {
            var b = _bombs[i];
            if (b == null) { _bombs.RemoveAt(i); continue; }
            if (b.isGridBomb && b.gridPos == gridPos)
            {
                _bombs.RemoveAt(i);
                Debug.Log($"[BombManager] RemoveGridBombAt -> Removing bomb {b.bombId} at {gridPos}", b);
                if (b.gameObject != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying) DestroyImmediate(b.gameObject);
                    else
#endif
                        Destroy(b.gameObject);
                }
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 그리드 회전 시 폭탄의 위치를 업데이트합니다.
    /// oldPos에 있던 폭탄을 찾아서 newPos로 gridPos만 변경합니다.
    /// GameObject의 transform은 GridManager가 관리하므로 여기서는 논리적 위치만 갱신.
    /// </summary>
    public bool UpdateBombPosition(Vector2Int oldPos, Vector2Int newPos)
    {
        for (int i = 0; i < _bombs.Count; i++)
        {
            var b = _bombs[i];
            if (b == null) continue;

            // oldPos에 있는 그리드 폭탄을 찾음
            if (b.isGridBomb && b.gridPos == oldPos)
            {
                // 논리적 위치만 업데이트 (GameObject 이름도 변경)
                b.gridPos = newPos;
                if (b.gameObject != null)
                {
                    b.gameObject.name = $"Bomb_{newPos.x}_{newPos.y}";
                }

                Debug.Log($"[BombManager] UpdateBombPosition: {oldPos} -> {newPos} for Bomb[{b.bombId}]");
                return true;
            }
        }

        Debug.LogWarning($"[BombManager] UpdateBombPosition: No bomb found at {oldPos}");
        return false;
    }

    #region On-demand spawn API (몬스터/기타에서 호출)
    /// <summary>
    /// 빈 그리드 셀 중 랜덤 위치에 폭탄을 생성하고 생성 위치를 반환.
    /// 몬스터가 호출해서 폭탄을 생성할 때 사용.
    /// </summary>
    public bool SpawnRandomGridBomb(int startTimer, int maxTimer, out Vector2Int pos)
    {
        pos = default;
        if (GridManager.Instance == null) return false;
        bool ok = GridManager.Instance.TrySpawnRandomBomb(startTimer, out pos);
        if (!ok) return false;

        var go = new GameObject($"Bomb_{pos.x}_{pos.y}");
        go.transform.SetParent(GridManager.Instance.transform, false);
        go.transform.position = GridManager.Instance.GridToWorld(pos);
        var b = go.AddComponent<Bomb>();
        b.InitializeGridBomb(pos, startTimer, maxTimer);
        Register(b);

        GridManager.Instance.UpdateBombViewTimer(pos, b.timer);
        Debug.Log($"[BombManager] SpawnRandomGridBomb -> spawned at {pos} (timer {startTimer}, max {maxTimer})");
        return true;
    }

    /// <summary>
    /// 특정 gridPos에 폭탄을 생성하려 시도. GridManager의 SpawnBombAt(…) 반환값에 따름.
    /// </summary>
    public bool SpawnGridBombAt(Vector2Int pos, int startTimer, int maxTimer)
    {
        if (GridManager.Instance == null) return false;
        bool ok = GridManager.Instance.SpawnBombAt(pos, startTimer);
        if (!ok) return false;

        var go = new GameObject($"Bomb_{pos.x}_{pos.y}");
        go.transform.SetParent(GridManager.Instance.transform, false);
        go.transform.position = GridManager.Instance.GridToWorld(pos);
        var b = go.AddComponent<Bomb>();
        b.InitializeGridBomb(pos, startTimer, maxTimer);
        Register(b);

        GridManager.Instance.UpdateBombViewTimer(pos, b.timer);
        Debug.Log($"[BombManager] SpawnGridBombAt -> spawned at {pos} (timer {startTimer}, max {maxTimer})");
        return true;
    }
    #endregion

    /// <summary>
    /// 라운드 종료 시 호출: 모든 폭탄을 제거하고 리스트를 비웁니다.
    /// </summary>
    public void ClearAllBombs()
    {
        // 리스트를 역순으로 돌면서 안전하게 삭제
        for (int i = _bombs.Count - 1; i >= 0; i--)
        {
            var b = _bombs[i];
            if (b != null)
            {
                // 1. GridManager에게 통보하여 View 제거 (GridBomb인 경우)
                if (b.isGridBomb && GridManager.Instance != null)
                {
                    GridManager.Instance.ClearBombAt(b.gridPos);
                }

                // 2. GameObject 파괴
                if (b.gameObject != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying) DestroyImmediate(b.gameObject);
                    else
#endif
                        Destroy(b.gameObject);
                }
            }
        }

        // 3. 리스트 비우기
        _bombs.Clear();

        // 4. 스폰 카운트다운 초기화 (다음 라운드 위해 리셋)
        ScheduleNextBomb();

        Debug.Log("[BombManager] All bombs cleared.");
    }
}