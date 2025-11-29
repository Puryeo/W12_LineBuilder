using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Renders a simple ghost preview of a Block on the grid using SpriteRenderers.
/// - 기존 API 유지
/// - UpdatePreview(..., rotationSteps) 오버로드 추가
/// - SetRotation / RotateClockwise 제공
/// </summary>
public class GridGhostRenderer : MonoBehaviour
{
    [Header("Visual")]
    public Sprite sprite;
    public Color canColor = new Color(0.2f, 1f, 0.2f, 0.4f);
    public Color cannotColor = new Color(1f, 0.2f, 0.2f, 0.35f);

    private readonly List<SpriteRenderer> _cells = new List<SpriteRenderer>();
    private bool _visible;
    private int _rotationSteps = 0; // 0..3, clockwise 90deg steps

    public void Show(BlockSO block)
    {
        EnsureCells(block);
        _visible = true;
        SetActive(true);
    }

    public void Hide()
    {
        _visible = false;
        SetActive(false);
    }

    /// <summary>
    /// 기존 호출 유지 (rotationSteps는 내부 상태 사용)
    /// </summary>
    public void UpdatePreview(BlockSO block, Vector2Int originCell, bool canPlace)
    {
        UpdatePreview(block, originCell, canPlace, _rotationSteps);
    }

    /// <summary>
    /// rotationSteps: 0..3 (90deg clockwise steps)
    /// </summary>
    public void UpdatePreview(BlockSO block, Vector2Int originCell, bool canPlace, int rotationSteps)
    {
        if (block == null || block.ShapeOffsets == null) return;

        if (!_visible)
        {
            Show(block);
        }

        EnsureCells(block);

        // normalize rotation
        _rotationSteps = ((rotationSteps % 4) + 4) % 4;

        var color = canPlace ? canColor : cannotColor;

        for (int i = 0; i < block.ShapeOffsets.Length; i++)
        {
            var sr = _cells[i];
            if (sr == null) continue;

            sr.color = color;

            // apply rotation to offset
            var rotatedOff = RotateOffset(block.ShapeOffsets[i], _rotationSteps);
            var cell = originCell + rotatedOff;

            var pos = GridManager.Instance.GridToWorld(cell);
            sr.transform.position = new Vector3(pos.x, pos.y, -0.1f);

            // ensure sprite set (preserve existing sprite field if available)
            if (sr.sprite == null && sprite != null)
                sr.sprite = sprite;

            if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
        }

        // disable any extra cells
        for (int i = block.ShapeOffsets.Length; i < _cells.Count; i++)
        {
            if (_cells[i] != null)
                _cells[i].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Set rotation state and re-render preview using last known block/origin/canPlace (best-effort).
    /// InteractionManager usually calls UpdatePreview after rotating so this is optional.
    /// </summary>
    public void SetRotation(int rotationSteps)
    {
        _rotationSteps = ((rotationSteps % 4) + 4) % 4;
        // If currently visible, attempt to re-render using stored block/origin is not available here.
        // InteractionManager calls UpdatePreview(...) with rotation so explicit re-render normally not needed.
    }

    /// <summary>
    /// Convenience: rotate one step clockwise
    /// </summary>
    public void RotateClockwise()
    {
        SetRotation((_rotationSteps + 1) & 3);
    }

    private void EnsureCells(BlockSO block)
    {
        if (block == null || block.ShapeOffsets == null) return;
        while (_cells.Count < block.ShapeOffsets.Length)
        {
            var go = new GameObject("GhostCell");
            go.transform.SetParent(transform);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 1000;
            _cells.Add(sr);
        }
        for (int i = 0; i < _cells.Count; i++)
        {
            if (_cells[i] != null)
                _cells[i].gameObject.SetActive(i < block.ShapeOffsets.Length);
        }
    }

    private void SetActive(bool active)
    {
        foreach (var sr in _cells)
        {
            if (sr) sr.gameObject.SetActive(active);
        }
    }

    private static Vector2Int RotateOffset(Vector2Int off, int steps)
    {
        switch (steps & 3)
        {
            default:
            case 0:
                return new Vector2Int(off.x, off.y);
            case 1: // 90° CW
                return new Vector2Int(off.y, -off.x);
            case 2: // 180°
                return new Vector2Int(-off.x, -off.y);
            case 3: // 270° CW
                return new Vector2Int(-off.y, off.x);
        }
    }
}
