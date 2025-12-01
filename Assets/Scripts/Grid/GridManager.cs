using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro; // TextMeshPro for world text

/// <summary>
/// Manages an 8x8 grid, placement validation and applying block placements.
/// Extended: bomb spawn/tick APIs and simple bomb view (timer overlay).
/// </summary>
[DefaultExecutionOrder(-50)]
public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Serializable]
    public struct Cell
    {
        public bool occupied;
        public bool isBomb;
        public int bombTimer;
        public BlockSO block; // reference of the block occupying this cell (for now simple ref)
        public int rotation;  // 0..3 rotation steps stored per cell (for future use)
    }

    [Header("Grid Settings")]
    public int width = 8;
    public int height = 8;
    public Vector3 origin = Vector3.zero;
    public float cellSize = 1f;

    [Header("View")]
    public GameObject blockViewPrefab; // 1x1 sprite prefab (SpriteRenderer expected)

    private Cell[,] _cells;
    private Dictionary<Vector2Int, GameObject> _cellViews;

    // 기존 이벤트: 블록 배치
    public event Action<BlockSO, Vector2Int, List<Vector2Int>> OnBlockPlaced;

    // 신규 이벤트: 라인 클리어 결과 전달
    public event Action<LineClearResult> OnLinesCleared;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        Initialize(width, height);
    }

    public void Initialize(int w, int h)
    {
        width = Mathf.Max(1, w);
        height = Mathf.Max(1, h);
        _cells = new Cell[width, height];
        _cellViews = new Dictionary<Vector2Int, GameObject>(new Vector2IntComparer());
    }

    private void OnDestroy()
    {
        // cleanup instantiated views
        if (_cellViews != null)
        {
            foreach (var kv in _cellViews)
            {
                if (kv.Value != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying) DestroyImmediate(kv.Value);
                    else Destroy(kv.Value);
#else
                    Destroy(kv.Value);
#endif
                }
            }
            _cellViews.Clear();
        }
    }

    public bool InBounds(Vector2Int p)
    {
        return p.x >= 0 && p.x < width && p.y >= 0 && p.y < height;
    }

    public Vector2Int WorldToGrid(Vector3 world)
    {
        var local = world - origin;
        int x = Mathf.FloorToInt(local.x / cellSize);
        int y = Mathf.FloorToInt(local.y / cellSize);
        return new Vector2Int(x, y);
    }

    public Vector3 GridToWorld(Vector2Int grid)
    {
        return origin + new Vector3((grid.x + 0.5f) * cellSize, (grid.y + 0.5f) * cellSize, 0f);
    }

    public bool IsEmpty(Vector2Int p)
    {
        if (_cells == null) return false;
        if (!InBounds(p)) return false;
        return !_cells[p.x, p.y].occupied && !_cells[p.x, p.y].isBomb;
    }

    // Old API preserved
    public List<Vector2Int> GetAbsoluteCells(BlockSO block, Vector2Int originCell)
    {
        return GetAbsoluteCells(block, originCell, 0);
    }

    // New: rotation-aware absolute cells
    public List<Vector2Int> GetAbsoluteCells(BlockSO block, Vector2Int originCell, int rotationSteps)
    {
        var result = new List<Vector2Int>(block != null && block.ShapeOffsets != null ? block.ShapeOffsets.Length : 0);
        if (block == null || block.ShapeOffsets == null) return result;
        rotationSteps = ((rotationSteps % 4) + 4) % 4;
        foreach (var off in block.ShapeOffsets)
        {
            var rot = RotateOffset(off, rotationSteps);
            result.Add(originCell + rot);
        }
        return result;
    }

    public bool CanPlace(BlockSO block, Vector2Int originCell)
    {
        return CanPlace(block, originCell, 0);
    }

    // rotation-aware CanPlace
    public bool CanPlace(BlockSO block, Vector2Int originCell, int rotationSteps)
    {
        if (_cells == null) return false;
        if (block == null || block.ShapeOffsets == null || block.ShapeOffsets.Length == 0) return false;
        var abs = GetAbsoluteCells(block, originCell, rotationSteps);
        foreach (var p in abs)
        {
            if (!InBounds(p)) return false;
            if (_cells[p.x, p.y].occupied || _cells[p.x, p.y].isBomb) return false;
        }
        return true;
    }

    public bool Place(BlockSO block, Vector2Int originCell)
    {
        return Place(block, originCell, 0);
    }

    // rotation-aware Place
    public bool Place(BlockSO block, Vector2Int originCell, int rotationSteps)
    {
        if (!CanPlace(block, originCell, rotationSteps)) return false;
        var abs = GetAbsoluteCells(block, originCell, rotationSteps);
        foreach (var p in abs)
        {
            var c = _cells[p.x, p.y];
            c.occupied = true;
            c.block = block;
            c.rotation = rotationSteps & 3;
            _cells[p.x, p.y] = c;

            // instantiate view for this cell
            CreateCellView(p, block);
        }

        // 즉시 라인 검사 및 제거 처리 (PRD: 배치 직후 라인 검사)
        var result = CheckLineClear();
        if (result.HasClear)
        {
            // RemoveLines 내부에서 OnLinesCleared 이벤트를 발생시킴
            RemoveLines(result);
        }

        // 알림: 배치가 "완료된 시점"에 전파 (라인 클리어/해체 처리가 끝난 후)
        OnBlockPlaced?.Invoke(block, originCell, abs);

        return true;
    }

    /// <summary>
    /// Clear all cells (used by GridCollapse). Returns removed count.
    /// </summary>
    public void ClearAll(out int removedCount)
    {
        removedCount = 0;
        if (_cells == null) return;
        var keysToRemove = new List<Vector2Int>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var c = _cells[x, y];
                if (c.occupied || c.isBomb)
                {
                    _cells[x, y] = default;
                    removedCount++;

                    var key = new Vector2Int(x, y);
                    keysToRemove.Add(key);
                }
            }
        }

        // destroy views
        foreach (var k in keysToRemove)
        {
            DestroyCellView(k);
        }
    }

    /// <summary>
    /// Clears a square area centered on the given cell (radius defaults to 1 for a 3×3 block).
    /// Each cell in the area is reset and its runtime view is destroyed so the region becomes empty.
    /// </summary>
    public void ClearSquareCentered(Vector2Int center, int radius = 1)
    {
        if (_cells == null) return;
        if (radius < 0) radius = 0;

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                var target = new Vector2Int(center.x + dx, center.y + dy);
                if (!InBounds(target)) continue;

                var cell = _cells[target.x, target.y];
                if (cell.isBomb) continue;

                _cells[target.x, target.y] = default;
                DestroyCellView(target);
            }
        }
    }

    /// <summary>
    /// 검사: 꽉 찬 행 및 열을 찾아 반환합니다. HasClear 플래그로 라인 존재 여부 확인 가능.
    /// </summary>
    public LineClearResult CheckLineClear()
    {
        var rows = new List<int>();
        var cols = new List<int>();

        if (_cells == null) return new LineClearResult(rows, cols);

        // 행 검사 (y 고정)
        for (int y = 0; y < height; y++)
        {
            bool full = true;
            for (int x = 0; x < width; x++)
            {
                // 수정: 폭탄도 '차있음'으로 취급하여 라인 가득 참 판정에 포함
                if (!(_cells[x, y].occupied || _cells[x, y].isBomb))
                {
                    full = false;
                    break;
                }
            }
            if (full) rows.Add(y);
        }

        // 열 검사 (x 고정)
        for (int x = 0; x < width; x++)
        {
            bool full = true;
            for (int y = 0; y < height; y++)
            {
                // 수정: 폭탄도 '차있음'으로 취급
                if (!(_cells[x, y].occupied || _cells[x, y].isBomb))
                {
                    full = false;
                    break;
                }
            }
            if (full) cols.Add(x);
        }

        return new LineClearResult(rows, cols);
    }

    /// <summary>
    /// 지정된 행/열을 제거하고 결과(제거된 셀 수, 폭탄 위치)를 LineClearResult에 채웁니다.
    /// 중복 셀은 한 번만 제거됩니다.
    /// </summary>
    public void RemoveLines(LineClearResult result)
    {
        if (result == null || _cells == null) return;

        var toClear = new HashSet<Vector2Int>(new Vector2IntComparer());

        // 행들
        foreach (int y in result.ClearedRows)
        {
            for (int x = 0; x < width; x++)
                toClear.Add(new Vector2Int(x, y));
        }

        // 열들
        foreach (int x in result.ClearedCols)
        {
            for (int y = 0; y < height; y++)
                toClear.Add(new Vector2Int(x, y));
        }

        int removed = 0;
        var removedBombs = new List<Vector2Int>();

        foreach (var p in toClear)
        {
            if (!InBounds(p)) continue;
            var c = _cells[p.x, p.y];
            if (c.occupied || c.isBomb)
            {
                if (c.isBomb)
                    removedBombs.Add(p);

                _cells[p.x, p.y] = default;
                removed++;

                // destroy runtime view if exists
                DestroyCellView(p);
            }
        }

        // result.RemovedCount / result.RemovedBombPositions 채운 뒤 추가 처리
        result.RemovedCount = removed;
        result.RemovedBombPositions = removedBombs;
        result.ContainedBomb = removedBombs.Count > 0;

        var rowsStr = result.ClearedRows != null ? string.Join(", ", result.ClearedRows) : string.Empty;
        var colsStr = result.ClearedCols != null ? string.Join(", ", result.ClearedCols) : string.Empty;
        Debug.Log($"[GridManager] RemoveLines -> rows:{rowsStr} cols:{colsStr} removed:{removed} bombs:{removedBombs.Count}");

        // 라인 클리어 이벤트 발생
        OnLinesCleared?.Invoke(result);

        // 추가: Grid에서 제거된 폭탄에 대해 BombManager의 인스턴스도 제거하도록 통지
        if (removedBombs.Count > 0 && BombManager.Instance != null)
        {
            foreach (var bp in removedBombs)
            {
                bool removedFromManager = BombManager.Instance.RemoveGridBombAt(bp);
                if (removedFromManager)
                    Debug.Log($"[GridManager] Notified BombManager to remove grid bomb at {bp}");
                else
                    Debug.LogWarning($"[GridManager] Bomb at {bp} removed from grid but BombManager had no matching Bomb instance.");
            }
        }
    }

    /// <summary>
    /// 라운드 종료 시 호출: 모든 셀의 뷰와 데이터를 초기화합니다.
    /// </summary>
    public void ClearAllGridEntities()
    {
        // 1. 시각적 요소(View) 제거
        if (_cellViews != null)
        {
            foreach (var kv in _cellViews)
            {
                if (kv.Value != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying) DestroyImmediate(kv.Value);
                    else Destroy(kv.Value);
#else
                    Destroy(kv.Value);
#endif
                }
            }
            _cellViews.Clear();
        }

        // 2. 논리적 데이터(Cell Data) 초기화
        if (_cells != null)
        {
            // 배열의 모든 요소를 기본값(비어있음)으로 초기화
            Array.Clear(_cells, 0, _cells.Length);
        }

        Debug.Log("[GridManager] Grid Cleared (View & Data)");
    }

    #region Cell Data Access (for GridRotationManager)

    /// <summary>
    /// 특정 위치의 셀 데이터를 가져옵니다.
    /// 범위를 벗어나면 기본값(빈 셀)을 반환합니다.
    /// </summary>
    public Cell GetCell(Vector2Int pos)
    {
        if (_cells == null || !InBounds(pos))
            return default;
        return _cells[pos.x, pos.y];
    }

    /// <summary>
    /// 특정 위치의 셀을 초기화(비움)하고 해당 뷰를 제거합니다.
    /// </summary>
    public void ClearCell(Vector2Int pos)
    {
        if (_cells == null || !InBounds(pos))
            return;

        _cells[pos.x, pos.y] = default;
        DestroyCellView(pos);
    }

    /// <summary>
    /// 특정 위치에 셀 데이터를 설정합니다.
    /// 뷰는 별도로 RecreateView로 생성해야 합니다.
    /// </summary>
    public void SetCell(Vector2Int pos, Cell cell)
    {
        if (_cells == null || !InBounds(pos))
            return;

        _cells[pos.x, pos.y] = cell;
    }

    /// <summary>
    /// 주어진 셀 데이터를 기반으로 블록 또는 폭탄 뷰를 재생성합니다.
    /// </summary>
    public void RecreateView(Vector2Int pos, Cell cell)
    {
        if (cell.isBomb)
        {
            CreateBombView(pos, cell.bombTimer);
        }
        else if (cell.occupied && cell.block != null)
        {
            CreateCellView(pos, cell.block);
        }
    }

    #endregion

    #region Bomb APIs

    /// <summary>
    /// Spawn a bomb at an explicit position with given timer.
    /// Returns true if spawn succeeded.
    /// </summary>
    public bool SpawnBombAt(Vector2Int pos, int timer)
    {
        if (!InBounds(pos)) return false;
        var c = _cells[pos.x, pos.y];
        if (c.occupied || c.isBomb) return false;

        c.isBomb = true;
        c.bombTimer = Mathf.Max(0, timer);
        _cells[pos.x, pos.y] = c;

        CreateBombView(pos, c.bombTimer);
        Debug.Log($"[GridManager] SpawnBombAt {pos} timer={c.bombTimer}");
        return true;
    }

    /// <summary>
    /// Spawn a bomb in a random empty cell. Returns true if spawn succeeded.
    /// </summary>
    public bool SpawnRandomBomb(int timer)
    {
        if (_cells == null) return false;
        var empties = new List<Vector2Int>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var p = new Vector2Int(x, y);
                if (!_cells[x, y].occupied && !_cells[x, y].isBomb)
                    empties.Add(p);
            }
        }
        if (empties.Count == 0) return false;
        var idx = UnityEngine.Random.Range(0, empties.Count);
        return SpawnBombAt(empties[idx], timer);
    }

    /// <summary>
    /// Try to spawn a bomb in a random empty cell. Returns true and out pos when succeeded.
    /// This variant exposes the chosen position so BombManager can create/register a Bomb component.
    /// </summary>
    public bool TrySpawnRandomBomb(int timer, out Vector2Int pos)
    {
        pos = default;
        if (_cells == null) return false;
        var empties = new List<Vector2Int>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!_cells[x, y].occupied && !_cells[x, y].isBomb)
                    empties.Add(new Vector2Int(x, y));
            }
        }
        if (empties.Count == 0) return false;
        var idx = UnityEngine.Random.Range(0, empties.Count);
        pos = empties[idx];
        bool ok = SpawnBombAt(pos, timer);
        return ok;
    }

    /// <summary>
    /// Decrement bomb timers by 1. Returns list of positions that exploded this tick.
    /// Caller should handle player damage for explosions.
    /// </summary>
    public List<Vector2Int> TickBombTimers()
    {
        var exploded = new List<Vector2Int>();
        if (_cells == null) return exploded;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var c = _cells[x, y];
                if (!c.isBomb) continue;
                c.bombTimer = Mathf.Max(0, c.bombTimer - 1);
                _cells[x, y] = c;

                // update overlay via BombView if exists
                var pos = new Vector2Int(x, y);
                if (_cellViews != null && _cellViews.TryGetValue(pos, out var go) && go != null)
                {
                    var bv = go.GetComponent<BombView>();
                    if (bv != null)
                        bv.SetTimer(c.bombTimer);
                }

                if (c.bombTimer <= 0)
                {
                    exploded.Add(pos);
                    // remove cell and view immediately
                    _cells[x, y] = default;
                    DestroyCellView(pos);
                }
            }
        }

        if (exploded.Count > 0)
            Debug.Log($"[GridManager] Bombs exploded at: {string.Join(", ", exploded)}");

        return exploded;
    }

    /// <summary>
    /// Update the BombView overlay timer for a bomb at the given grid position.
    /// BombManager 호출 시, GridView 텍스트/뷰를 동기화하는 목적으로 사용.
    /// </summary>
    public void UpdateBombViewTimer(Vector2Int pos, int timer)
    {
        if (_cellViews == null) return;
        if (_cellViews.TryGetValue(pos, out var go) && go != null)
        {
            var bv = go.GetComponent<BombView>();
            if (bv != null)
                bv.SetTimer(timer);
            // also update TextMeshPro if present
            var tm = go.GetComponentInChildren<TMPro.TextMeshPro>();
            if (tm != null)
            {
                tm.text = timer > 0 ? $"{timer}" : "폭발!";
                // keep editor-configured font size/color in sync
                if (BombManager.Instance != null)
                {
                    if (BombManager.Instance.bombTimerFont != null)
                        tm.font = BombManager.Instance.bombTimerFont;
                    tm.fontSize = BombManager.Instance.bombTimerFontSize;
                    tm.color = BombManager.Instance.bombTimerColor;
                }
            }
        }
    }

    /// <summary>
    /// Clear bomb at grid position: clears cell state and destroys view.
    /// BombManager should call this when a grid bomb explodes or is removed.
    /// </summary>
    public void ClearBombAt(Vector2Int pos)
    {
        if (!InBounds(pos)) return;
        var c = _cells[pos.x, pos.y];
        if (!c.isBomb) return;
        _cells[pos.x, pos.y] = default;
        DestroyCellView(pos);
        Debug.Log($"[GridManager] ClearBombAt {pos}");
    }

    #endregion

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Grid wireframe
        Gizmos.color = new Color(1f, 1f, 1f, 0.15f);
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var center = origin + new Vector3((x + 0.5f) * cellSize, (y + 0.5f) * cellSize, 0f);
                var size = new Vector3(cellSize, cellSize, 0.01f);
                Gizmos.DrawWireCube(center, size);
            }
        }

        // Occupied visualization in editor
        if (_cells != null && _cells.Length == width * height)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var c = _cells[x, y];
                    if (c.occupied)
                    {
                        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.25f);
                        var center = origin + new Vector3((x + 0.5f) * cellSize, (y + 0.5f) * cellSize, 0f);
                        var size = new Vector3(cellSize * 0.9f, cellSize * 0.9f, 0.01f);
                        Gizmos.DrawCube(center, size);
                    }
                    if (c.isBomb)
                    {
                        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);
                        var center = origin + new Vector3((x + 0.5f) * cellSize, (y + 0.5f) * cellSize, 0f);
                        var size = new Vector3(cellSize * 0.8f, cellSize * 0.8f, 0.01f);
                        Gizmos.DrawCube(center, size);
                    }
                }
            }
        }
    }
#endif

    #region Helper Types

    public class LineClearResult
    {
        public List<int> ClearedRows { get; }
        public List<int> ClearedCols { get; }
        public int RemovedCount { get; set; }
        public List<Vector2Int> RemovedBombPositions { get; set; }
        public bool ContainedBomb { get; set; }
        public bool HasClear => (ClearedRows != null && ClearedRows.Count > 0) || (ClearedCols != null && ClearedCols.Count > 0);

        public LineClearResult(List<int> rows, List<int> cols)
        {
            ClearedRows = rows ?? new List<int>();
            ClearedCols = cols ?? new List<int>();
            RemovedCount = 0;
            RemovedBombPositions = new List<Vector2Int>();
            ContainedBomb = false;
        }
    }

    // HashSet needs comparer for Vector2Int
    private class Vector2IntComparer : IEqualityComparer<Vector2Int>
    {
        public bool Equals(Vector2Int a, Vector2Int b) => a.x == b.x && a.y == b.y;
        public int GetHashCode(Vector2Int v) => v.x * 397 ^ v.y;
    }

    #endregion

    #region View Helpers

    private void CreateCellView(Vector2Int cellPos, BlockSO block)
    {
        if (blockViewPrefab == null) return;
        if (_cellViews == null) _cellViews = new Dictionary<Vector2Int, GameObject>(new Vector2IntComparer());
        if (_cellViews.ContainsKey(cellPos)) return;

        var go = Instantiate(blockViewPrefab, transform);
        var world = GridToWorld(cellPos);
        go.transform.position = new Vector3(world.x, world.y, -0.05f);
        go.transform.localScale = Vector3.one * (cellSize);

        // tint sprite if possible
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = block != null ? block.Color : Color.white;
        }

        _cellViews[cellPos] = go;
    }

    private void CreateBombView(Vector2Int cellPos, int timer)
    {
        if (blockViewPrefab == null) return;
        if (_cellViews == null) _cellViews = new Dictionary<Vector2Int, GameObject>(new Vector2IntComparer());
        if (_cellViews.ContainsKey(cellPos)) return;

        var go = Instantiate(blockViewPrefab, transform);
        var world = GridToWorld(cellPos);
        go.transform.position = new Vector3(world.x, world.y, -0.05f);
        go.transform.localScale = Vector3.one * (cellSize);

        // tint red to indicate bomb
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = new Color(1f, 0.2f, 0.2f, 1f);
        }

        // add TextMeshPro (world) overlay for timer
        var txtGO = new GameObject("BombTimerText");
        txtGO.transform.SetParent(go.transform, false);
        txtGO.transform.localPosition = new Vector3(0f, 0f, -BombManager.Instance?.bombTimerZOffset ?? -0.1f);

        var tm = txtGO.AddComponent<TMPro.TextMeshPro>();
        if (BombManager.Instance != null && BombManager.Instance.bombTimerFont != null)
            tm.font = BombManager.Instance.bombTimerFont;
        tm.enableWordWrapping = false;
        tm.alignment = TMPro.TextAlignmentOptions.Center;

        // apply configured font size and color (fallback to previous defaults)
        tm.fontSize = BombManager.Instance != null ? BombManager.Instance.bombTimerFontSize : 3f;
        tm.color = BombManager.Instance != null ? BombManager.Instance.bombTimerColor : Color.white;

        if (timer > 0)
            tm.text = $"{timer}턴 남았다";
        else
            tm.text = "폭발!";

        tm.enableWordWrapping = false;

        // Add BombView component to handle blinking and updates
        var bv = go.AddComponent<BombView>();
        bv.Initialize(timer);

        _cellViews[cellPos] = go;
    }

    private void DestroyCellView(Vector2Int cellPos)
    {
        if (_cellViews == null) return;
        if (!_cellViews.TryGetValue(cellPos, out var go) || go == null)
        {
            _cellViews.Remove(cellPos);
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying) DestroyImmediate(go);
        else Destroy(go);
#else
        Destroy(go);
#endif
        _cellViews.Remove(cellPos);
    }

    #endregion

    #region Utils

    private static Vector2Int RotateOffset(Vector2Int off, int steps)
    {
        switch (steps & 3)
        {
            default:
            case 0: return new Vector2Int(off.x, off.y);
            case 1: return new Vector2Int(off.y, -off.x);
            case 2: return new Vector2Int(-off.x, -off.y);
            case 3: return new Vector2Int(-off.y, off.x);
        }
    }

    #endregion
}