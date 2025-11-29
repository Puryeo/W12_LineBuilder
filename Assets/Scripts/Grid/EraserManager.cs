using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Tracks "eraser" charges awarded exclusively via pass actions and presents a 3×3 preview
/// when the eraser mode is active. Clicking while hovering consumes one charge and clears
/// that area on the grid.
/// </summary>
public class EraserManager : MonoBehaviour
{
    public static EraserManager Instance { get; private set; }

    [Header("Dependencies")]
    public GridManager grid;
    public Camera worldCamera;
    public GridGhostRenderer ghostRenderer;
    [Tooltip("Optional prefab that provides a SpriteRenderer for the preview cells. Falls back to GridManager.blockViewPrefab if empty.")]
    public GameObject previewCellTemplate;

    [Header("Preview")]
    public Color previewColor = new Color(0.2f, 1f, 0.2f, 0.4f);
    [Tooltip("설정한 previewColor 대신 GridGhostRenderer의 색을 사용하려면 체크하세요.")]
    public bool useGhostColorForPreview;

    [Header("UI (optional)")]
    public TextMeshProUGUI eraserCountText;
    public GameObject eraserModeIndicator;

    private readonly List<SpriteRenderer> _previewCells = new List<SpriteRenderer>(9);
    private bool _isActive;
    private bool _hasHover;
    private int _eraserCount;

    public int EraserCount => _eraserCount;
    public bool IsEraserModeActive => _isActive;
    public event System.Action<int> OnEraserCountChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        grid ??= GridManager.Instance;
        worldCamera ??= Camera.main;
        PreparePreviewCells();
        UpdateEraserUI();

        // 기존: eraserModeIndicator?.SetActive(false);
        try
        {
            if (eraserModeIndicator != null)
                eraserModeIndicator.SetActive(false);
        }
        catch (UnassignedReferenceException)
        {
            // Inspector에 할당된 오브젝트가 삭제되어 "unassigned" 상태일 때 예외 발생함.
            // 안전하게 null로 정리하고 경고 로그를 남깁니다.
            eraserModeIndicator = null;
            Debug.LogWarning("[EraserManager] eraserModeIndicator reference is missing in Inspector. Cleared reference to avoid exception.");
        }
    }

    private void Update()
    {
        if (!_isActive || grid == null) return;

        worldCamera ??= Camera.main;
        if (worldCamera == null) return;

        var mouse = Input.mousePosition;
        var world = worldCamera.ScreenToWorldPoint(mouse);
        world.z = 0f;
        var cell = grid.WorldToGrid(world);

        if (grid.InBounds(cell))
        {
            _hasHover = true;
            UpdatePreview(cell);
        }
        else
        {
            _hasHover = false;
            HidePreview();
        }

        if (_hasHover && Input.GetMouseButtonDown(0))
        {
            TryUseEraserAt(cell);
        }

        if (Input.GetMouseButtonDown(1))
        {
            CancelEraserMode();
        }
    }

    /// <summary>
    /// Called from the pass button OnClick event (in addition to whatever pass handling already exists).
    /// This method strictly increments the eraser charge so the count only grows when pass is pressed.
    /// </summary>
    public void OnPassButtonClicked()
    {
        _eraserCount++;
        UpdateEraserUI();
        OnEraserCountChanged?.Invoke(_eraserCount);
        StartEraserMode();
    }

    /// <summary>
    /// Called from the eraser button OnClick event to enter eraser targeting mode if charges are available.
    /// </summary>
    public void OnEraserButtonClicked()
    {
        StartEraserMode();
    }

    public void StartEraserMode()
    {
        if (_eraserCount <= 0)
        {
            Debug.Log("[EraserManager] No eraser charges available.");
            return;
        }

        if (_isActive) return;

        _isActive = true;
        _hasHover = false;
        HidePreview();
        eraserModeIndicator?.SetActive(true);
    }

    public void CancelEraserMode()
    {
        if (!_isActive) return;

        _isActive = false;
        _hasHover = false;
        HidePreview();
        eraserModeIndicator?.SetActive(false);
    }

    private bool TryUseEraserAt(Vector2Int center, bool bypassActiveCheck = false)
    {
        if (_eraserCount <= 0 || grid == null) return false;
        if (!_isActive && !bypassActiveCheck) return false;

        grid.ClearSquareCentered(center, 1);
        _eraserCount--;
        UpdateEraserUI();
        OnEraserCountChanged?.Invoke(_eraserCount);

        if (_isActive && _eraserCount <= 0)
        {
            CancelEraserMode();
        }
        return true;
    }

    private bool TryGetCursorCell(out Vector2Int cell)
    {
        cell = Vector2Int.zero;
        var cam = worldCamera ?? Camera.main;
        if (cam == null || grid == null) return false;

        var mouse = Input.mousePosition;
        var world = cam.ScreenToWorldPoint(mouse);
        world.z = 0f;
        var candidate = grid.WorldToGrid(world);
        if (!grid.InBounds(candidate)) return false;
        cell = candidate;
        return true;
    }

    private void UpdatePreview(Vector2Int center)
    {
        EnsurePreviewCells();

        if (_previewCells.Count != 9) return;

        int idx = 0;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                var target = new Vector2Int(center.x + dx, center.y + dy);
                var sr = _previewCells[idx++];
                if (!grid.InBounds(target))
                {
                    sr.gameObject.SetActive(false);
                    continue;
                }

                var world = grid.GridToWorld(target);
                sr.transform.position = new Vector3(world.x, world.y, -0.1f);
                sr.gameObject.SetActive(true);
            }
        }
    }

    private void HidePreview()
    {
        foreach (var sr in _previewCells)
        {
            if (sr != null)
                sr.gameObject.SetActive(false);
        }
    }

    private void PreparePreviewCells()
    {
        if (_previewCells.Count > 0) return;

        Sprite sprite = null;
        Material material = null;
        int sortingOrder = 1000;
        Color cellColor = previewColor;

        if (ghostRenderer != null)
        {
            sprite = ghostRenderer.sprite;
            if (useGhostColorForPreview)
                cellColor = ghostRenderer.canColor;
        }

        if (previewCellTemplate != null)
        {
            var sample = previewCellTemplate.GetComponent<SpriteRenderer>();
            if (sample != null)
            {
                sprite = sample.sprite;
                material = sample.sharedMaterial;
                sortingOrder = sample.sortingOrder;
                cellColor = sample.color;
            }
        }

        if (sprite == null && grid != null && grid.blockViewPrefab != null)
        {
            var sample = grid.blockViewPrefab.GetComponent<SpriteRenderer>();
            if (sample != null)
            {
                sprite = sample.sprite;
                material = sample.sharedMaterial;
                sortingOrder = sample.sortingOrder;
            }
        }

        for (int i = 0; i < 9; i++)
        {
            var go = new GameObject($"EraserPreview_{i}");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            if (material != null)
                sr.sharedMaterial = material;
            sr.color = cellColor;
            sr.sortingOrder = sortingOrder + 5;
            go.SetActive(false);
            _previewCells.Add(sr);
        }
    }

    private void EnsurePreviewCells()
    {
        if (_previewCells.Count > 0) return;
        PreparePreviewCells();
    }

    private void UpdateEraserUI()
    {
        if (eraserCountText != null)
            eraserCountText.text = _eraserCount.ToString();
    }

    private void OnDisable()
    {
        CancelEraserMode();
    }
}
