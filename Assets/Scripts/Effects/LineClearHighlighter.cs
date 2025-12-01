using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 라인 클리어 시 그리드 테두리를 LineRenderer로 하이라이트 처리
/// </summary>
public class LineClearHighlighter : MonoBehaviour
{
    [Header("LineRenderer Settings")]
    [Tooltip("LineRenderer 프리팹 (선택사항, null이면 런타임 생성)")]
    public GameObject lineRendererPrefab;

    [Tooltip("하이라이트 색상")]
    public Color highlightColor = new Color(1f, 0.92f, 0.016f, 1f); // 황금색

    [Tooltip("라인 두께")]
    public float lineWidth = 0.1f;

    [Tooltip("하이라이트 지속 시간 (초)")]
    public float duration = 1.0f;

    [Header("Advanced Settings")]
    [Tooltip("LineRenderer가 사용할 Material (null이면 기본 Sprites/Default 사용)")]
    public Material lineMaterial;

    [Tooltip("LineRenderer의 Z 좌표 오프셋 (양수면 앞으로)")]
    public float zOffset = -1f;

    [Header("Debug")]
    [Tooltip("디버그 로그 표시 여부")]
    public bool enableDebugLogs = false;

    private GridManager _grid;

    /// <summary>
    /// GridManager 참조 초기화
    /// </summary>
    public void Initialize(GridManager grid)
    {
        _grid = grid;
        if (enableDebugLogs) Debug.Log($"[LineClearHighlighter] Initialized with GridManager: {grid != null}");
    }

    /// <summary>
    /// 라인 클리어 하이라이트 처리
    /// </summary>
    public IEnumerator HighlightLines(GridManager.LineClearResult result, Action<float> reportDuration)
    {
        float startTime = Time.time;

        Debug.Log($"[LineClearHighlighter] HighlightLines started - enableDebugLogs={enableDebugLogs}");

        if (_grid == null)
        {
            Debug.LogWarning("[LineClearHighlighter] GridManager is null! Did you call Initialize()?");
            reportDuration?.Invoke(0f);
            yield break;
        }

        if (result == null)
        {
            Debug.LogWarning("[LineClearHighlighter] LineClearResult is null!");
            reportDuration?.Invoke(0f);
            yield break;
        }

        Debug.Log($"[LineClearHighlighter] Result - ClearedRows: {result.ClearedRows?.Count ?? 0}, ClearedCols: {result.ClearedCols?.Count ?? 0}");

        List<GameObject> lineRenderers = new List<GameObject>();

        // 클리어된 행 하이라이트
        if (result.ClearedRows != null && result.ClearedRows.Count > 0)
        {
            Debug.Log($"[LineClearHighlighter] Creating highlights for {result.ClearedRows.Count} rows: [{string.Join(", ", result.ClearedRows)}]");
            foreach (int y in result.ClearedRows)
            {
                var lr = CreateRowHighlight(y);
                if (lr != null)
                {
                    lineRenderers.Add(lr);
                    Debug.Log($"[LineClearHighlighter] Row {y} LineRenderer created successfully");
                }
                else
                {
                    Debug.LogWarning($"[LineClearHighlighter] Row {y} LineRenderer creation FAILED");
                }
            }
        }

        // 클리어된 열 하이라이트
        if (result.ClearedCols != null && result.ClearedCols.Count > 0)
        {
            Debug.Log($"[LineClearHighlighter] Creating highlights for {result.ClearedCols.Count} columns: [{string.Join(", ", result.ClearedCols)}]");
            foreach (int x in result.ClearedCols)
            {
                var lr = CreateColHighlight(x);
                if (lr != null)
                {
                    lineRenderers.Add(lr);
                    Debug.Log($"[LineClearHighlighter] Col {x} LineRenderer created successfully");
                }
                else
                {
                    Debug.LogWarning($"[LineClearHighlighter] Col {x} LineRenderer creation FAILED");
                }
            }
        }

        Debug.Log($"[LineClearHighlighter] Created {lineRenderers.Count} LineRenderers total, waiting {duration}s");

        // 지속 시간만큼 대기
        yield return new WaitForSeconds(duration);

        // LineRenderer 정리
        if (enableDebugLogs) Debug.Log($"[LineClearHighlighter] Destroying {lineRenderers.Count} LineRenderers");
        foreach (var lr in lineRenderers)
        {
            if (lr != null) Destroy(lr);
        }

        // 실제 걸린 시간 보고
        float elapsed = Time.time - startTime;
        reportDuration?.Invoke(elapsed);
        if (enableDebugLogs) Debug.Log($"[LineClearHighlighter] Completed in {elapsed:F2}s");
    }

    /// <summary>
    /// 행 하이라이트 생성 (테두리만)
    /// </summary>
    private GameObject CreateRowHighlight(int y)
    {
        if (y < 0 || y >= _grid.height)
        {
            Debug.LogWarning($"[LineClearHighlighter] Invalid row index: {y}");
            return null;
        }

        // 행 테두리: 왼쪽 상단 → 오른쪽 상단 → 오른쪽 하단 → 왼쪽 하단 → 닫기
        Vector3[] points = new Vector3[5];

        float halfCell = _grid.cellSize / 2f;

        // 왼쪽 상단
        Vector3 topLeft = _grid.GridToWorld(new Vector2Int(0, y)) + new Vector3(-halfCell, halfCell, zOffset);
        // 오른쪽 상단
        Vector3 topRight = _grid.GridToWorld(new Vector2Int(_grid.width - 1, y)) + new Vector3(halfCell, halfCell, zOffset);
        // 오른쪽 하단
        Vector3 bottomRight = _grid.GridToWorld(new Vector2Int(_grid.width - 1, y)) + new Vector3(halfCell, -halfCell, zOffset);
        // 왼쪽 하단
        Vector3 bottomLeft = _grid.GridToWorld(new Vector2Int(0, y)) + new Vector3(-halfCell, -halfCell, zOffset);

        points[0] = topLeft;
        points[1] = topRight;
        points[2] = bottomRight;
        points[3] = bottomLeft;
        points[4] = topLeft; // 닫기

        if (enableDebugLogs) Debug.Log($"[LineClearHighlighter] Row {y} highlight: {topLeft} -> {topRight} -> {bottomRight} -> {bottomLeft}");

        return CreateLineRenderer(points, $"RowHighlight_{y}");
    }

    /// <summary>
    /// 열 하이라이트 생성 (테두리만)
    /// </summary>
    private GameObject CreateColHighlight(int x)
    {
        if (x < 0 || x >= _grid.width)
        {
            Debug.LogWarning($"[LineClearHighlighter] Invalid column index: {x}");
            return null;
        }

        // 열 테두리: 왼쪽 상단 → 오른쪽 상단 → 오른쪽 하단 → 왼쪽 하단 → 닫기
        Vector3[] points = new Vector3[5];

        float halfCell = _grid.cellSize / 2f;

        // 왼쪽 상단
        Vector3 topLeft = _grid.GridToWorld(new Vector2Int(x, _grid.height - 1)) + new Vector3(-halfCell, halfCell, zOffset);
        // 오른쪽 상단
        Vector3 topRight = _grid.GridToWorld(new Vector2Int(x, _grid.height - 1)) + new Vector3(halfCell, halfCell, zOffset);
        // 오른쪽 하단
        Vector3 bottomRight = _grid.GridToWorld(new Vector2Int(x, 0)) + new Vector3(halfCell, -halfCell, zOffset);
        // 왼쪽 하단
        Vector3 bottomLeft = _grid.GridToWorld(new Vector2Int(x, 0)) + new Vector3(-halfCell, -halfCell, zOffset);

        points[0] = topLeft;
        points[1] = topRight;
        points[2] = bottomRight;
        points[3] = bottomLeft;
        points[4] = topLeft; // 닫기

        if (enableDebugLogs) Debug.Log($"[LineClearHighlighter] Col {x} highlight: {topLeft} -> {topRight} -> {bottomRight} -> {bottomLeft}");

        return CreateLineRenderer(points, $"ColHighlight_{x}");
    }

    /// <summary>
    /// LineRenderer 게임오브젝트 생성
    /// </summary>
    private GameObject CreateLineRenderer(Vector3[] points, string objectName)
    {
        GameObject obj;

        if (lineRendererPrefab != null)
        {
            obj = Instantiate(lineRendererPrefab, transform);
            obj.name = objectName;
        }
        else
        {
            // 프리팹이 없으면 런타임 생성
            obj = new GameObject(objectName);
            obj.transform.SetParent(transform);
            obj.AddComponent<LineRenderer>();
        }

        var lr = obj.GetComponent<LineRenderer>();
        if (lr == null)
        {
            Debug.LogError($"[LineClearHighlighter] LineRenderer component not found on {objectName}");
            Destroy(obj);
            return null;
        }

        // LineRenderer 설정
        lr.positionCount = points.Length;
        lr.SetPositions(points);
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.startColor = highlightColor;
        lr.endColor = highlightColor;
        lr.useWorldSpace = true;
        lr.loop = false; // loop는 false로 설정 (마지막 점이 첫 점과 같으므로)

        // Material 설정 (확실하게 작동하도록)
        if (lineMaterial != null)
        {
            lr.material = lineMaterial;
            if (enableDebugLogs) Debug.Log($"[LineClearHighlighter] Using custom material: {lineMaterial.name}");
        }
        else
        {
            // Unity 내장 셰이더 사용 (항상 존재)
            Material defaultMat = new Material(Shader.Find("Hidden/Internal-Colored"));
            defaultMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            defaultMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            defaultMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            defaultMat.SetInt("_ZWrite", 0);
            lr.material = defaultMat;

            if (enableDebugLogs) Debug.Log($"[LineClearHighlighter] Using default material (Hidden/Internal-Colored)");
        }

        // 정렬 레이어 설정 (선택사항)
        lr.sortingLayerName = "Default";
        lr.sortingOrder = 100; // 다른 오브젝트 위에 표시

        // 디버그 로그
        if (enableDebugLogs)
        {
            Debug.Log($"[LineClearHighlighter] Created {objectName}: width={lineWidth}, color={highlightColor}, points={points.Length}, material={(lr.material != null ? lr.material.name : "NULL")}");
            Debug.Log($"[LineClearHighlighter] Point positions: {string.Join(", ", points)}");
        }

        return obj;
    }
}
