using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 그리드 라인 렌더링(일반/화염 효과) 및 4x4 영역 하이라이트 기능을 통합 관리하는 클래스
/// </summary>
public class GridLineRenderer : MonoBehaviour
{
    [Header("References")]
    public GridManager grid;
    public GridAttributeMap attributeMap;

    [Header("Detection Target")]
    public AttributeType targetAttribute = AttributeType.BratCandy; // 감지할 속성 (개초딩 사탕)

    // =========================================================
    // 1. Grid Line Settings (Normal & Fiery)
    // =========================================================
    [Header("Normal Line Settings (기본)")]
    public Material normalLineMaterial;
    public Color normalLineColor = new Color(1f, 1f, 1f, 0.1f);
    public float normalLineWidth = 0.02f;

    [Header("Fiery Line Settings (이글거림)")]
    public Material fieryLineMaterial;
    [ColorUsage(true, true)]
    public Color fieryLineColor = new Color(1f, 0.4f, 0f, 1.5f); // HDR 컬러

    [Header("Fiery Adjustments (인스펙터 조정)")]
    [Tooltip("라인 두께")]
    public float fireLineWidth = 0.15f;

    [Tooltip("텍스처 반복 횟수 (X: 길이 방향, Y: 두께 방향) - 뭉개짐 해결용")]
    public Vector2 fireTiling = new Vector2(5.0f, 1.0f);

    [Tooltip("흐르는 속도 (X: 가로 흐름, Y: 세로 흐름)")]
    public Vector2 fireScrollSpeed = new Vector2(2.0f, 0.0f);

    [Range(0f, 1f)]
    [Tooltip("깜빡임 강도 (0이면 안 깜빡임)")]
    public float flickerIntensity = 0.1f;

    [Tooltip("이글거리는 라인의 위치 미세 조정 (Z값을 -0.1 등으로 하여 앞으로 빼세요)")]
    public Vector3 fireOffset = new Vector3(0, 0, -0.05f);

    // =========================================================
    // 2. Quadrant Highlight Settings
    // =========================================================
    [Header("4x4 Quadrant Highlight")]
    public bool enableQuadrantHighlight = true;
    public Color quadrantHighlightColor = new Color(1f, 1f, 0f, 1f); // 노란색
    public float quadrantHighlightWidth = 0.1f;

    // =========================================================
    // Internal Variables
    // =========================================================

    // 그리드 라인 관리용
    private Dictionary<string, LineRenderer> _allLines = new Dictionary<string, LineRenderer>();
    private List<LineRenderer> _activeFieryLines = new List<LineRenderer>();

    // 4x4 하이라이트 관리용
    private LineRenderer _highlightLineRenderer;
    private int _currentHighlightedQuadrant = -1;
    private const int QUADRANT_SIZE = 4;
    private readonly Vector2Int[] _quadrantOrigins = new Vector2Int[]
    {
        new Vector2Int(0, 0), new Vector2Int(4, 0),
        new Vector2Int(0, 4), new Vector2Int(4, 4)
    };

    private void Start()
    {
        InitializeReferences();
        RefreshGridLines(); // 그리드 라인 생성
        CreateHighlightLineRenderer(); // 하이라이트 라인 생성
    }

    private void InitializeReferences()
    {
        if (grid == null) grid = GridManager.Instance;
        if (attributeMap == null) attributeMap = FindObjectOfType<GridAttributeMap>();

        // 머티리얼 안전장치
        if (normalLineMaterial == null) normalLineMaterial = new Material(Shader.Find("Sprites/Default"));
        if (fieryLineMaterial == null) fieryLineMaterial = new Material(Shader.Find("Sprites/Default"));
    }

    private void Update()
    {
        // 1. 화염 애니메이션 처리
        if (_activeFieryLines.Count > 0)
        {
            UpdateFieryAnimation();
        }

        // 2. 마우스 오버 4x4 하이라이트 처리
        if (enableQuadrantHighlight)
        {
            UpdateQuadrantHighlight();
        }
        else if (_highlightLineRenderer != null && _highlightLineRenderer.enabled)
        {
            _highlightLineRenderer.enabled = false;
        }
    }

    private void OnValidate()
    {
        // 에디터에서 값 변경 시 하이라이트 스타일 즉시 갱신
        if (_highlightLineRenderer != null)
        {
            ConfigureHighlightLine();
        }
    }

    private void OnDisable()
    {
        // 하이라이트 정리
        if (_highlightLineRenderer != null)
        {
            if (Application.isPlaying) Destroy(_highlightLineRenderer.gameObject);
            else DestroyImmediate(_highlightLineRenderer.gameObject);
        }
    }

    // =========================================================
    // Logic A: Fiery Grid Lines
    // =========================================================

    private void UpdateFieryAnimation()
    {
        float time = Time.time;
        Vector2 offset = fireScrollSpeed * time;
        float flicker = Mathf.Sin(time * 15f) * flickerIntensity + 1f;
        float currentWidth = fireLineWidth * flicker;

        foreach (var lr in _activeFieryLines)
        {
            if (lr == null) continue;

            // 텍스처 스크롤 & 타일링
            if (lr.material != null)
            {
                lr.material.mainTextureOffset = offset;
                lr.material.mainTextureScale = fireTiling;
            }

            // 두께 & 색상 애니메이션
            lr.startWidth = currentWidth;
            lr.endWidth = currentWidth;
            lr.startColor = fieryLineColor;
            lr.endColor = fieryLineColor;
        }
    }

    [ContextMenu("Force Refresh Grid Lines")]
    public void RefreshGridLines()
    {
        if (grid == null) return;
        if (attributeMap == null) attributeMap = FindObjectOfType<GridAttributeMap>();

        _activeFieryLines.Clear();

        // 전체 라인 불태우기 여부 판정
        bool triggerAllHorizontalFire = CheckAnyRowHasAttribute();
        bool triggerAllVerticalFire = CheckAnyColHasAttribute();

        // 1. 가로선 (Horizontal)
        for (int y = 0; y <= grid.height; y++)
        {
            string lineKey = $"H_{y}";
            LineRenderer lr = GetOrCreateLine(lineKey);

            bool isFiery = triggerAllHorizontalFire;
            Vector3 startPos = grid.origin + new Vector3(0, y * grid.cellSize, 0);
            Vector3 endPos = grid.origin + new Vector3(grid.width * grid.cellSize, y * grid.cellSize, 0);

            SetupGridLine(lr, startPos, endPos, isFiery, true);
        }

        // 2. 세로선 (Vertical)
        for (int x = 0; x <= grid.width; x++)
        {
            string lineKey = $"V_{x}";
            LineRenderer lr = GetOrCreateLine(lineKey);

            bool isFiery = triggerAllVerticalFire;
            Vector3 startPos = grid.origin + new Vector3(x * grid.cellSize, 0, 0);
            Vector3 endPos = grid.origin + new Vector3(x * grid.cellSize, grid.height * grid.cellSize, 0);

            SetupGridLine(lr, startPos, endPos, isFiery, false);
        }
    }

    private void SetupGridLine(LineRenderer lr, Vector3 start, Vector3 end, bool isFiery, bool isHorizontal)
    {
        if (isFiery)
        {
            lr.material = fieryLineMaterial;
            lr.textureMode = LineTextureMode.Tile;
            lr.sortingOrder = 20;

            // 위치 보정 (오프셋 적용)
            Vector3 offset = fireOffset;
            if (isHorizontal) offset += new Vector3(0, -fireLineWidth * 0.5f, 0);
            else offset += new Vector3(fireLineWidth * 0.5f, 0, 0);

            lr.SetPosition(0, start + offset);
            lr.SetPosition(1, end + offset);

            _activeFieryLines.Add(lr);
        }
        else
        {
            lr.material = normalLineMaterial;
            lr.textureMode = LineTextureMode.Stretch;
            lr.startColor = normalLineColor;
            lr.endColor = normalLineColor;
            lr.startWidth = normalLineWidth;
            lr.endWidth = normalLineWidth;
            lr.sortingOrder = 0;

            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
        }
    }

    private bool CheckAnyRowHasAttribute()
    {
        if (attributeMap == null) return false;
        for (int i = 0; i < grid.height; i++)
            if (attributeMap.GetRow(i) == targetAttribute) return true;
        return false;
    }

    private bool CheckAnyColHasAttribute()
    {
        if (attributeMap == null) return false;
        for (int i = 0; i < grid.width; i++)
            if (attributeMap.GetCol(i) == targetAttribute) return true;
        return false;
    }

    private LineRenderer GetOrCreateLine(string key)
    {
        if (_allLines.TryGetValue(key, out LineRenderer existingLr))
        {
            if (existingLr != null) return existingLr;
        }

        GameObject go = new GameObject($"GridLine_{key}");
        go.transform.SetParent(this.transform, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();

        lr.useWorldSpace = true;
        lr.receiveShadows = false;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.loop = false;
        lr.positionCount = 2;

        _allLines[key] = lr;
        return lr;
    }

    // =========================================================
    // Logic B: Quadrant Highlight
    // =========================================================

    private void CreateHighlightLineRenderer()
    {
        if (_highlightLineRenderer != null) return;

        var go = new GameObject("QuadrantHighlight");
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;

        _highlightLineRenderer = go.AddComponent<LineRenderer>();
        ConfigureHighlightLine();
        _highlightLineRenderer.enabled = false;
    }

    private void ConfigureHighlightLine()
    {
        if (_highlightLineRenderer == null) return;

        if (normalLineMaterial != null) // 하이라이트도 기본 머티리얼 공유 가능
            _highlightLineRenderer.material = normalLineMaterial;

        _highlightLineRenderer.startColor = quadrantHighlightColor;
        _highlightLineRenderer.endColor = quadrantHighlightColor;
        _highlightLineRenderer.useWorldSpace = true;
        _highlightLineRenderer.receiveShadows = false;
        _highlightLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _highlightLineRenderer.loop = true; // 사각형 루프
        _highlightLineRenderer.numCapVertices = 0;
        _highlightLineRenderer.numCornerVertices = 0;
        _highlightLineRenderer.startWidth = quadrantHighlightWidth;
        _highlightLineRenderer.endWidth = quadrantHighlightWidth;
        _highlightLineRenderer.positionCount = 4;
        _highlightLineRenderer.sortingOrder = 30; // 불꽃보다 위에 그림
    }

    private void UpdateQuadrantHighlight()
    {
        if (_highlightLineRenderer == null || grid == null) return;

        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2Int gridPos = grid.WorldToGrid(mouseWorldPos);

        if (!grid.InBounds(gridPos))
        {
            _highlightLineRenderer.enabled = false;
            _currentHighlightedQuadrant = -1;
            return;
        }

        int quadX = gridPos.x / QUADRANT_SIZE;
        int quadY = gridPos.y / QUADRANT_SIZE;
        int quadrantIndex = quadY * 2 + quadX;

        if (quadrantIndex == _currentHighlightedQuadrant && _highlightLineRenderer.enabled)
            return;

        _currentHighlightedQuadrant = quadrantIndex;
        DrawQuadrantOutline(quadrantIndex);
        _highlightLineRenderer.enabled = true;
    }

    private void DrawQuadrantOutline(int quadrantIndex)
    {
        if (quadrantIndex < 0 || quadrantIndex >= _quadrantOrigins.Length) return;

        Vector2Int origin = _quadrantOrigins[quadrantIndex];
        float halfCell = grid.cellSize * 0.5f;

        // 4x4 영역 모서리 계산
        Vector3 bottomLeft = grid.GridToWorld(origin) + new Vector3(-halfCell, -halfCell, -0.3f); // Z값을 더 앞으로
        Vector3 bottomRight = grid.GridToWorld(origin + new Vector2Int(QUADRANT_SIZE - 1, 0)) + new Vector3(halfCell, -halfCell, -0.3f);
        Vector3 topRight = grid.GridToWorld(origin + new Vector2Int(QUADRANT_SIZE - 1, QUADRANT_SIZE - 1)) + new Vector3(halfCell, halfCell, -0.3f);
        Vector3 topLeft = grid.GridToWorld(origin + new Vector2Int(0, QUADRANT_SIZE - 1)) + new Vector3(-halfCell, halfCell, -0.3f);

        _highlightLineRenderer.SetPosition(0, bottomLeft);
        _highlightLineRenderer.SetPosition(1, bottomRight);
        _highlightLineRenderer.SetPosition(2, topRight);
        _highlightLineRenderer.SetPosition(3, topLeft);
    }
}