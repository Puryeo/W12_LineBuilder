using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 그리드 라인을 렌더링하고, 마우스 오버 시 4x4 영역을 하이라이트 표시합니다.
/// </summary>
[RequireComponent(typeof(Transform))]
public class GridLineRenderer : MonoBehaviour
{
    public GridManager grid;
    public Material lineMaterial;
    public Color lineColor = Color.white;

    [Tooltip("World units")]
    public float lineWidth = 0.03f;

    [Header("4x4 Quadrant Highlight")]
    [Tooltip("4x4 영역 하이라이트 활성화 여부")]
    public bool enableQuadrantHighlight = true;

    [Tooltip("4x4 영역 외곽선 색상")]
    public Color quadrantHighlightColor = new Color(1f, 1f, 0f, 1f); // 노란색

    [Tooltip("4x4 영역 외곽선 두께")]
    public float quadrantHighlightWidth = 0.1f;

    private readonly List<LineRenderer> _lines = new List<LineRenderer>();

    [Tooltip("에디터에서 기존 라인들을 즉시 다시 구성할지 여부")]
    public bool allowEditorRebuild = false;

    // 4x4 하이라이트용 LineRenderer (동적 생성)
    private LineRenderer _highlightLineRenderer;
    private int _currentHighlightedQuadrant = -1;

    // 4개의 4x4 영역 정의 (좌하단 기준) - GridRotationManager와 동일
    private readonly Vector2Int[] _quadrantOrigins = new Vector2Int[]
    {
        new Vector2Int(0, 0),   // 좌하단
        new Vector2Int(4, 0),   // 우하단
        new Vector2Int(0, 4),   // 좌상단
        new Vector2Int(4, 4)    // 우상단
    };
    private const int QUADRANT_SIZE = 4;

    private void OnEnable()
    {
        if (grid == null)
            grid = GridManager.Instance;
        EnsureMaterial();
        ConfigureExistingLines();
        CreateHighlightLineRenderer();
    }

    private void OnDisable()
    {
        // 하이라이트 LineRenderer 정리
        if (_highlightLineRenderer != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(_highlightLineRenderer.gameObject);
            else Destroy(_highlightLineRenderer.gameObject);
#else
            Destroy(_highlightLineRenderer.gameObject);
#endif
            _highlightLineRenderer = null;
        }
    }

    private void Update()
    {
        if (!enableQuadrantHighlight || grid == null)
        {
            // 하이라이트 비활성화 시 숨김
            if (_highlightLineRenderer != null)
                _highlightLineRenderer.enabled = false;
            return;
        }

        UpdateQuadrantHighlight();
    }

    private void OnValidate()
    {
        // 인스펙터 변경 시 기존 자식 LineRenderer 스타일을 갱신
        if (!Application.isPlaying && !allowEditorRebuild) return;
        EnsureMaterial();
        ConfigureExistingLines();

        // 하이라이트 LineRenderer 스타일도 갱신
        if (_highlightLineRenderer != null)
        {
            ConfigureHighlightLine();
        }
    }

    private void EnsureMaterial()
    {
        if (lineMaterial == null)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) lineMaterial = new Material(shader);
        }
    }

    /// <summary>
    /// 씬에 이미 존재하는 자식 LineRenderer들을 찾아 스타일을 적용합니다.
    /// (동적 생성 로직은 제거되어 기존 라인을 재사용함)
    /// </summary>
    public void ConfigureExistingLines()
    {
        _lines.Clear();
        var found = GetComponentsInChildren<LineRenderer>(true);
        foreach (var lr in found)
        {
            if (lr == null) continue;

            // 하이라이트용 LineRenderer는 제외
            if (lr == _highlightLineRenderer) continue;

            _lines.Add(lr);
            if (lineMaterial != null)
                lr.material = lineMaterial;
            lr.startColor = lineColor;
            lr.endColor = lineColor;
            lr.useWorldSpace = true;
            lr.receiveShadows = false;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.loop = false;
            lr.numCapVertices = 0;
            lr.numCornerVertices = 0;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
        }
    }

    /// <summary>
    /// 4x4 영역 하이라이트용 LineRenderer를 생성합니다.
    /// </summary>
    private void CreateHighlightLineRenderer()
    {
        if (_highlightLineRenderer != null) return;

        var go = new GameObject("QuadrantHighlight");
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;

        _highlightLineRenderer = go.AddComponent<LineRenderer>();
        ConfigureHighlightLine();

        _highlightLineRenderer.enabled = false; // 초기에는 숨김
    }

    /// <summary>
    /// 하이라이트 LineRenderer의 스타일을 설정합니다.
    /// </summary>
    private void ConfigureHighlightLine()
    {
        if (_highlightLineRenderer == null) return;

        if (lineMaterial != null)
            _highlightLineRenderer.material = lineMaterial;

        _highlightLineRenderer.startColor = quadrantHighlightColor;
        _highlightLineRenderer.endColor = quadrantHighlightColor;
        _highlightLineRenderer.useWorldSpace = true;
        _highlightLineRenderer.receiveShadows = false;
        _highlightLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _highlightLineRenderer.loop = true; // 사각형이므로 루프
        _highlightLineRenderer.numCapVertices = 0;
        _highlightLineRenderer.numCornerVertices = 0;
        _highlightLineRenderer.startWidth = quadrantHighlightWidth;
        _highlightLineRenderer.endWidth = quadrantHighlightWidth;
        _highlightLineRenderer.positionCount = 4; // 사각형 4개 점
    }

    /// <summary>
    /// 마우스 위치에 따라 4x4 영역 하이라이트를 업데이트합니다.
    /// </summary>
    private void UpdateQuadrantHighlight()
    {
        if (_highlightLineRenderer == null) return;

        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2Int gridPos = grid.WorldToGrid(mouseWorldPos);

        if (!grid.InBounds(gridPos))
        {
            // 그리드 밖이면 하이라이트 숨김
            _highlightLineRenderer.enabled = false;
            _currentHighlightedQuadrant = -1;
            return;
        }

        // 어느 4x4 영역에 속하는지 계산
        int quadX = gridPos.x / QUADRANT_SIZE; // 0 or 1
        int quadY = gridPos.y / QUADRANT_SIZE; // 0 or 1
        int quadrantIndex = quadY * 2 + quadX;

        // 같은 영역이면 업데이트 불필요
        if (quadrantIndex == _currentHighlightedQuadrant)
            return;

        _currentHighlightedQuadrant = quadrantIndex;

        // 해당 영역의 외곽선 그리기
        DrawQuadrantOutline(quadrantIndex);
        _highlightLineRenderer.enabled = true;
    }

    /// <summary>
    /// 지정된 4x4 영역의 외곽선을 LineRenderer로 그립니다.
    /// </summary>
    /// <param name="quadrantIndex">영역 인덱스 (0~3)</param>
    private void DrawQuadrantOutline(int quadrantIndex)
    {
        if (quadrantIndex < 0 || quadrantIndex >= _quadrantOrigins.Length) return;
        if (_highlightLineRenderer == null) return;

        Vector2Int origin = _quadrantOrigins[quadrantIndex];

        // 4x4 영역의 실제 경계 계산
        // origin은 좌하단 셀, 영역은 origin부터 origin + (3, 3)까지
        float halfCell = grid.cellSize * 0.5f;

        // 좌하단 셀의 좌하단 모서리
        Vector3 bottomLeftCell = grid.GridToWorld(origin);
        Vector3 bottomLeft = bottomLeftCell + new Vector3(-halfCell, -halfCell, -0.2f);

        // 우하단 셀의 우하단 모서리 (x가 origin.x + 3)
        Vector3 bottomRightCell = grid.GridToWorld(origin + new Vector2Int(QUADRANT_SIZE - 1, 0));
        Vector3 bottomRight = bottomRightCell + new Vector3(halfCell, -halfCell, -0.2f);

        // 우상단 셀의 우상단 모서리 (x가 origin.x + 3, y가 origin.y + 3)
        Vector3 topRightCell = grid.GridToWorld(origin + new Vector2Int(QUADRANT_SIZE - 1, QUADRANT_SIZE - 1));
        Vector3 topRight = topRightCell + new Vector3(halfCell, halfCell, -0.2f);

        // 좌상단 셀의 좌상단 모서리 (y가 origin.y + 3)
        Vector3 topLeftCell = grid.GridToWorld(origin + new Vector2Int(0, QUADRANT_SIZE - 1));
        Vector3 topLeft = topLeftCell + new Vector3(-halfCell, halfCell, -0.2f);

        // LineRenderer에 네 점 설정 (시계방향)
        _highlightLineRenderer.SetPosition(0, bottomLeft);
        _highlightLineRenderer.SetPosition(1, bottomRight);
        _highlightLineRenderer.SetPosition(2, topRight);
        _highlightLineRenderer.SetPosition(3, topLeft);
    }

    /// <summary>
    /// 외부에서 특정 영역을 하이라이트할 때 사용 (GridRotationManager에서 호출 가능)
    /// </summary>
    public void HighlightQuadrant(int quadrantIndex)
    {
        if (!enableQuadrantHighlight) return;
        if (_highlightLineRenderer == null) CreateHighlightLineRenderer();

        _currentHighlightedQuadrant = quadrantIndex;
        DrawQuadrantOutline(quadrantIndex);
        _highlightLineRenderer.enabled = true;
    }

    /// <summary>
    /// 하이라이트를 숨깁니다.
    /// </summary>
    public void HideHighlight()
    {
        if (_highlightLineRenderer != null)
            _highlightLineRenderer.enabled = false;
        _currentHighlightedQuadrant = -1;
    }
}