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

    [Header("Turn-based Usage")]
    [SerializeField, Tooltip("턴당 사용 가능한 지우개 횟수")]
    private int eraserUsesPerTurn = 3;

    [Header("UI (optional)")]
    public TextMeshProUGUI eraserCountText;
    public GameObject eraserModeIndicator;

    private readonly List<SpriteRenderer> _previewCells = new List<SpriteRenderer>(9);
    private bool _isActive;
    private bool _hasHover;
    private int _remainingUsesThisTurn;

    public int RemainingUses => _remainingUsesThisTurn;
    public bool IsEraserModeActive => _isActive;
    public event System.Action<int> OnEraserUsesChanged;

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

        // TurnManager 이벤트 구독
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnAdvanced += OnTurnAdvanced;
        }

        // 초기 충전
        _remainingUsesThisTurn = eraserUsesPerTurn;
        UpdateEraserUI();

        try
        {
            if (eraserModeIndicator != null)
                eraserModeIndicator.SetActive(false);
        }
        catch (UnassignedReferenceException)
        {
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
    /// 턴이 진행될 때 호출됩니다. 지우개 사용 횟수를 리셋합니다.
    /// </summary>
    private void OnTurnAdvanced(int turnCount)
    {
        _remainingUsesThisTurn = eraserUsesPerTurn;
        UpdateEraserUI();
        OnEraserUsesChanged?.Invoke(_remainingUsesThisTurn);
        Debug.Log($"[EraserManager] Turn {turnCount}: Eraser uses recharged to {eraserUsesPerTurn}");
    }

    /// <summary>
    /// Called from the pass button OnClick event (in addition to whatever pass handling already exists).
    /// 지우개 모드를 시작합니다 (충전 없이 대기 모드만 활성화).
    /// </summary>
    public void OnPassButtonClicked()
    {
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
        if (_remainingUsesThisTurn <= 0)
        {
            Debug.Log("[EraserManager] No eraser uses remaining this turn.");
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
        if (_remainingUsesThisTurn <= 0 || grid == null) return false;
        if (!_isActive && !bypassActiveCheck) return false;

        // 실제 그리드를 지운 후에 사용 횟수 차감
        grid.ClearSquareCentered(center, 1);
        _remainingUsesThisTurn--;
        UpdateEraserUI();
        OnEraserUsesChanged?.Invoke(_remainingUsesThisTurn);

        Debug.Log($"[EraserManager] Eraser used at {center}. Remaining uses this turn: {_remainingUsesThisTurn}");

        if (_isActive && _remainingUsesThisTurn <= 0)
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
            eraserCountText.text = _remainingUsesThisTurn.ToString();
    }

    private void OnDisable()
    {
        CancelEraserMode();

        // TurnManager 이벤트 구독 해제
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnAdvanced -= OnTurnAdvanced;
        }
    }
}