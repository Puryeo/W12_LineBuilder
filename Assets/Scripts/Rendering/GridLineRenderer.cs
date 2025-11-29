using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Transform))]
public class GridLineRenderer : MonoBehaviour
{
    public GridManager grid;
    public Material lineMaterial;
    public Color lineColor = Color.white;
    [Tooltip("World units")]
    public float lineWidth = 0.03f;

    private readonly List<LineRenderer> _lines = new List<LineRenderer>();

    [Tooltip("에디터에서 기존 라인들을 즉시 다시 구성할지 여부")]
    public bool allowEditorRebuild = false;

    private void OnEnable()
    {
        if (grid == null)
            grid = GridManager.Instance;

        EnsureMaterial();
        ConfigureExistingLines();
    }

    private void OnDisable()
    {
        // 더 이상 동적으로 생성/삭제하지 않으므로 별도 정리 없음.
    }

    private void OnValidate()
    {
        // 인스펙터 변경 시 기존 자식 LineRenderer 스타일을 갱신
        if (!Application.isPlaying && !allowEditorRebuild) return;
        EnsureMaterial();
        ConfigureExistingLines();
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
}