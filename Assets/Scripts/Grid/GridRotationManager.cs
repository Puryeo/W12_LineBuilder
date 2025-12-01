using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 8x8 그리드를 4개의 4x4 영역으로 나누어 각 영역을 회전시키는 기능을 관리합니다.
/// 마우스 클릭 + 우클릭으로 해당 영역을 시계방향 90도 회전시킵니다.
/// </summary>
public class GridRotationManager : MonoBehaviour
{
    public static GridRotationManager Instance { get; private set; }

    [Header("Rotation Settings")]
    [SerializeField]
    [Tooltip("회전 기능 활성화 여부")]
    private bool enableRotation = true;

    // 4개의 4x4 영역 정의 (좌하단 기준)
    private readonly Vector2Int[] _quadrantOrigins = new Vector2Int[]
    {
        new Vector2Int(0, 0),   // 좌하단
        new Vector2Int(4, 0),   // 우하단
        new Vector2Int(0, 4),   // 좌상단
        new Vector2Int(4, 4)    // 우상단
    };

    private const int QUADRANT_SIZE = 4;

    // 현재 클릭 중인 영역
    private int _currentQuadrantIndex = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        if (!enableRotation) return;
        if (GridManager.Instance == null) return;

        HandleInput();
    }

    /// <summary>
    /// 마우스 입력을 처리합니다.
    /// 영역 내에서 우클릭하면 해당 영역을 시계방향으로 회전시킵니다.
    /// 하이라이트는 GridLineRenderer가 자동으로 처리합니다.
    /// </summary>
    private void HandleInput()
    {
        // 블록을 들고 있으면 그리드 회전 비활성화 (블록 회전과 충돌 방지)
        if (InteractionManager.Instance != null && InteractionManager.Instance.IsDragging)
        {
            return;
        }

        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2Int gridPos = GridManager.Instance.WorldToGrid(mouseWorldPos);

        // 우클릭: 현재 마우스가 올라간 영역 회전
        if (Input.GetMouseButtonDown(1))
        {
            if (GridManager.Instance.InBounds(gridPos))
            {
                int quadrantToRotate = GetQuadrantIndex(gridPos);
                Debug.Log($"[GridRotationManager] Right-click detected in quadrant {quadrantToRotate}");
                RotateQuadrant(quadrantToRotate);
            }
            else
            {
                Debug.LogWarning("[GridRotationManager] Right-click outside grid bounds");
            }
        }
    }

    /// <summary>
    /// 주어진 그리드 좌표가 속한 4x4 영역의 인덱스를 반환합니다.
    /// </summary>
    /// <param name="gridPos">그리드 좌표</param>
    /// <returns>영역 인덱스 (0~3), 범위 밖이면 -1</returns>
    private int GetQuadrantIndex(Vector2Int gridPos)
    {
        if (!GridManager.Instance.InBounds(gridPos)) return -1;

        // 어느 4x4 영역에 속하는지 계산
        int quadX = gridPos.x / QUADRANT_SIZE; // 0 or 1
        int quadY = gridPos.y / QUADRANT_SIZE; // 0 or 1

        return quadY * 2 + quadX;
    }

    /// <summary>
    /// 지정된 4x4 영역을 시계방향으로 90도 회전시킵니다.
    /// 회전 후 라인 클리어를 체크합니다.
    /// </summary>
    /// <param name="quadrantIndex">회전할 영역 인덱스 (0~3)</param>
    private void RotateQuadrant(int quadrantIndex)
    {
        if (quadrantIndex < 0 || quadrantIndex >= _quadrantOrigins.Length)
        {
            Debug.LogError($"[GridRotationManager] Invalid quadrant index: {quadrantIndex}");
            return;
        }
        if (GridManager.Instance == null)
        {
            Debug.LogError("[GridRotationManager] GridManager.Instance is null");
            return;
        }

        Vector2Int origin = _quadrantOrigins[quadrantIndex];
        Debug.Log($"[GridRotationManager] Starting rotation for quadrant {quadrantIndex} at origin {origin}");

        // 1단계: 현재 영역의 모든 셀 데이터를 임시 배열에 복사
        GridManager.Cell[,] tempCells = new GridManager.Cell[QUADRANT_SIZE, QUADRANT_SIZE];

        int cellCount = 0;
        for (int x = 0; x < QUADRANT_SIZE; x++)
        {
            for (int y = 0; y < QUADRANT_SIZE; y++)
            {
                Vector2Int globalPos = origin + new Vector2Int(x, y);
                tempCells[x, y] = GetCellAt(globalPos);

                if (tempCells[x, y].occupied || tempCells[x, y].isBomb)
                    cellCount++;
            }
        }
        Debug.Log($"[GridRotationManager] Found {cellCount} occupied cells in quadrant");

        // 2단계: 기존 영역의 뷰(View) 모두 제거
        for (int x = 0; x < QUADRANT_SIZE; x++)
        {
            for (int y = 0; y < QUADRANT_SIZE; y++)
            {
                Vector2Int globalPos = origin + new Vector2Int(x, y);
                ClearCellAt(globalPos);
            }
        }

        // 3단계: 시계방향 90도 회전된 위치에 셀 데이터 다시 배치
        // 회전 공식: (x, y) -> (QUADRANT_SIZE-1-y, x)
        int rotatedCount = 0;
        for (int x = 0; x < QUADRANT_SIZE; x++)
        {
            for (int y = 0; y < QUADRANT_SIZE; y++)
            {
                GridManager.Cell cell = tempCells[x, y];

                // 빈 셀은 스킵
                if (!cell.occupied && !cell.isBomb) continue;

                // 회전된 로컬 좌표 계산
                int newX = QUADRANT_SIZE - 1 - y;
                int newY = x;

                Vector2Int oldGlobalPos = origin + new Vector2Int(x, y);
                Vector2Int newGlobalPos = origin + new Vector2Int(newX, newY);

                Debug.Log($"[GridRotationManager] Rotating cell from {oldGlobalPos} to {newGlobalPos}");

                // 회전된 위치에 셀 데이터 설정
                SetCellAt(newGlobalPos, cell);

                // 뷰(View) 재생성
                if (cell.isBomb)
                {
                    // 폭탄인 경우 BombManager에게 위치 업데이트 알림
                    if (BombManager.Instance != null)
                    {
                        bool updated = BombManager.Instance.UpdateBombPosition(oldGlobalPos, newGlobalPos);
                        if (updated)
                            Debug.Log($"[GridRotationManager] Updated bomb position: {oldGlobalPos} -> {newGlobalPos}");
                    }
                    RecreateBlockView(newGlobalPos, cell);
                }
                else if (cell.occupied)
                {
                    // 일반 블록인 경우 뷰 재생성
                    RecreateBlockView(newGlobalPos, cell);
                }

                rotatedCount++;
            }
        }

        Debug.Log($"[GridRotationManager] Rotated {rotatedCount} cells in quadrant {quadrantIndex} at origin {origin}");

        // 4단계: 회전 후 라인 클리어 체크
        var result = GridManager.Instance.CheckLineClear();
        if (result.HasClear)
        {
            // RemoveLines 내부에서 OnLinesCleared 이벤트를 발생시키므로 여기서는 호출하지 않음
            GridManager.Instance.RemoveLines(result);
            Debug.Log($"[GridRotationManager] Line clear after rotation: {result.ClearedRows.Count} rows, {result.ClearedCols.Count} cols");
        }
    }

    #region GridManager Helper Methods

    /// <summary>
    /// GridManager의 특정 셀 데이터를 가져옵니다.
    /// </summary>
    private GridManager.Cell GetCellAt(Vector2Int pos)
    {
        if (GridManager.Instance == null) return default;
        return GridManager.Instance.GetCell(pos);
    }

    /// <summary>
    /// GridManager의 특정 셀을 초기화합니다.
    /// </summary>
    private void ClearCellAt(Vector2Int pos)
    {
        if (GridManager.Instance == null) return;
        GridManager.Instance.ClearCell(pos);
    }

    /// <summary>
    /// GridManager의 특정 셀에 데이터를 설정합니다.
    /// </summary>
    private void SetCellAt(Vector2Int pos, GridManager.Cell cell)
    {
        if (GridManager.Instance == null) return;
        GridManager.Instance.SetCell(pos, cell);
    }

    /// <summary>
    /// 블록 또는 폭탄 뷰를 재생성합니다.
    /// </summary>
    private void RecreateBlockView(Vector2Int pos, GridManager.Cell cell)
    {
        if (GridManager.Instance == null) return;
        GridManager.Instance.RecreateView(pos, cell);
    }

    #endregion

    private void OnDestroy()
    {
        // GridLineRenderer가 하이라이트를 관리하므로 별도 정리 불필요
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (GridManager.Instance == null) return;

        // 4x4 영역 경계선 표시
        Gizmos.color = new Color(1f, 1f, 0f, 0.5f);

        foreach (var origin in _quadrantOrigins)
        {
            Vector3 worldOrigin = GridManager.Instance.GridToWorld(origin);
            Vector3 size = new Vector3(
                GridManager.Instance.cellSize * QUADRANT_SIZE,
                GridManager.Instance.cellSize * QUADRANT_SIZE,
                0.01f
            );
            Vector3 center = worldOrigin + new Vector3(
                GridManager.Instance.cellSize * (QUADRANT_SIZE / 2f) - GridManager.Instance.cellSize * 0.5f,
                GridManager.Instance.cellSize * (QUADRANT_SIZE / 2f) - GridManager.Instance.cellSize * 0.5f,
                0f
            );

            Gizmos.DrawWireCube(center, size);
        }
    }
#endif
}