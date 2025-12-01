using System.Collections.Generic;
using UnityEngine;

public class GridLineRenderer : MonoBehaviour
{
    [Header("References")]
    public GridManager grid;
    public GridAttributeMap attributeMap;

    [Header("Detection Target")]
    public AttributeType targetAttribute = AttributeType.BratCandy; // 감지할 속성 (개초딩 사탕)

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


    // 내부 관리 변수
    private Dictionary<string, LineRenderer> _allLines = new Dictionary<string, LineRenderer>();
    private List<LineRenderer> _activeFieryLines = new List<LineRenderer>();

    private void Start()
    {
        if (grid == null) grid = GridManager.Instance;
        if (attributeMap == null) attributeMap = FindObjectOfType<GridAttributeMap>();

        // 머티리얼 안전장치
        if (normalLineMaterial == null) normalLineMaterial = new Material(Shader.Find("Sprites/Default"));
        if (fieryLineMaterial == null) fieryLineMaterial = new Material(Shader.Find("Sprites/Default"));

        RefreshGridLines();
    }

    private void Update()
    {
        // 런타임에 인스펙터 값을 바꾸면 즉시 반영되도록 갱신 (최적화 필요시 제거 가능)
        if (_activeFieryLines.Count > 0)
        {
            UpdateFieryAnimation();
        }
    }

    // 애니메이션 및 실시간 조정 처리
    private void UpdateFieryAnimation()
    {
        float time = Time.time;
        Vector2 offset = fireScrollSpeed * time;
        float flicker = Mathf.Sin(time * 15f) * flickerIntensity + 1f;
        float currentWidth = fireLineWidth * flicker;

        foreach (var lr in _activeFieryLines)
        {
            if (lr == null) continue;

            // 1. 텍스처 스크롤 (흐르는 효과)
            if (lr.material != null)
            {
                lr.material.mainTextureOffset = offset;
                // 인스펙터에서 조정한 Tiling 값을 실시간 적용
                lr.material.mainTextureScale = fireTiling;
            }

            // 2. 두께 및 색상 갱신 (인스펙터 값 반영)
            lr.startWidth = currentWidth;
            lr.endWidth = currentWidth;
            lr.startColor = fieryLineColor;
            lr.endColor = fieryLineColor;
        }
    }

    // 외부에서 호출 (아이템 장착 시)
    [ContextMenu("Force Refresh")]
    public void RefreshGridLines()
    {
        if (grid == null) return;
        if (attributeMap == null) attributeMap = FindObjectOfType<GridAttributeMap>();

        _activeFieryLines.Clear();

        // 1. 가로/세로 전체 불태우기 여부 판정 (Global Check)
        bool triggerAllHorizontalFire = CheckAnyRowHasAttribute();
        bool triggerAllVerticalFire = CheckAnyColHasAttribute();

        // ---------------------------------------------------------
        // 가로선 (Horizontal) 그리기
        // ---------------------------------------------------------
        for (int y = 0; y <= grid.height; y++)
        {
            string lineKey = $"H_{y}";
            LineRenderer lr = GetOrCreateLine(lineKey);

            // 해당 줄만 태우는게 아니라, 조건 만족시 '모든' 가로줄을 태움
            bool isFiery = triggerAllHorizontalFire;

            Vector3 startPos = grid.origin + new Vector3(0, y * grid.cellSize, 0);
            Vector3 endPos = grid.origin + new Vector3(grid.width * grid.cellSize, y * grid.cellSize, 0);

            if (isFiery)
            {
                // 불타는 라인 설정
                SetupLineStyle(lr, fieryLineMaterial, true);
                // 위치 보정 (약간 아래로, 앞으로 튀어나오게)
                Vector3 adjust = new Vector3(0, -fireLineWidth * 0.5f, 0) + fireOffset;
                lr.SetPosition(0, startPos + adjust);
                lr.SetPosition(1, endPos + adjust);

                _activeFieryLines.Add(lr);
            }
            else
            {
                // 일반 라인 설정
                SetupLineStyle(lr, normalLineMaterial, false);
                lr.SetPosition(0, startPos);
                lr.SetPosition(1, endPos);
            }
        }

        // ---------------------------------------------------------
        // 세로선 (Vertical) 그리기
        // ---------------------------------------------------------
        for (int x = 0; x <= grid.width; x++)
        {
            string lineKey = $"V_{x}";
            LineRenderer lr = GetOrCreateLine(lineKey);

            // 조건 만족시 '모든' 세로줄을 태움
            bool isFiery = triggerAllVerticalFire;

            Vector3 startPos = grid.origin + new Vector3(x * grid.cellSize, 0, 0);
            Vector3 endPos = grid.origin + new Vector3(x * grid.cellSize, grid.height * grid.cellSize, 0);

            if (isFiery)
            {
                SetupLineStyle(lr, fieryLineMaterial, true);
                // 위치 보정 (약간 오른쪽으로, 앞으로 튀어나오게)
                Vector3 adjust = new Vector3(fireLineWidth * 0.5f, 0, 0) + fireOffset;
                lr.SetPosition(0, startPos + adjust);
                lr.SetPosition(1, endPos + adjust);

                _activeFieryLines.Add(lr);
            }
            else
            {
                SetupLineStyle(lr, normalLineMaterial, false);
                lr.SetPosition(0, startPos);
                lr.SetPosition(1, endPos);
            }
        }
    }

    // 모든 가로 슬롯 중 하나라도 사탕이 있는지 검사
    private bool CheckAnyRowHasAttribute()
    {
        if (attributeMap == null) return false;
        for (int i = 0; i < grid.height; i++)
        {
            if (attributeMap.GetRow(i) == targetAttribute) return true;
        }
        return false;
    }

    // 모든 세로 슬롯 중 하나라도 사탕이 있는지 검사
    private bool CheckAnyColHasAttribute()
    {
        if (attributeMap == null) return false;
        for (int i = 0; i < grid.width; i++)
        {
            if (attributeMap.GetCol(i) == targetAttribute) return true;
        }
        return false;
    }

    private void SetupLineStyle(LineRenderer lr, Material mat, bool isFiery)
    {
        lr.material = mat;

        if (isFiery)
        {
            // 타일 모드 중요! (텍스처 반복)
            lr.textureMode = LineTextureMode.Tile;
            lr.sortingOrder = 20; // 맨 위에 그리기
        }
        else
        {
            lr.textureMode = LineTextureMode.Stretch;
            lr.startColor = normalLineColor;
            lr.endColor = normalLineColor;
            lr.startWidth = normalLineWidth;
            lr.endWidth = normalLineWidth;
            lr.sortingOrder = 0;
        }
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
}