using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// InteractionManager:
/// 기존 기능 유지 + UI에서 호출 가능한 스크린좌표 기반 배치 API 추가.
/// Rotation support added (ghost + placement).
/// </summary>
[DefaultExecutionOrder(0)]
public class InteractionManager : MonoBehaviour
{
    public static InteractionManager Instance { get; private set; }

    [Header("References")]
    public GridManager grid;
    public GridGhostRenderer ghost;

    [Header("Debug/Test")]
    public BlockSO debugBlock; // Inspector에서 수동 테스트용으로 남겨둘 수 있음

    // 기존 멤버들...
    private BlockSO _selectedCard;
    private int _selectedHandIndex = -1;
    private bool _dragging;
    private Camera _cam;

    // 회전 상태: 0..3 (90deg steps clockwise)
    private int _ghostRotationSteps = 0;
    public int GhostRotationSteps => _ghostRotationSteps;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        if (grid == null) grid = GridManager.Instance;
    }

    private void Start()
    {
        _cam = Camera.main;
    }

    private void Update()
    {
        // 기존 Update 로직 유지 (숫자키 선택, 드래그 등)
        UpdateHandSelectionByKeys();

        var mouse = Input.mousePosition;
        var world = _cam != null ? _cam.ScreenToWorldPoint(mouse) : (Vector3)mouse;
        world.z = 0f;
        var cell = grid != null ? grid.WorldToGrid(world) : Vector2Int.zero;

        // 3) 드래그 시작/진행/종료
        if (!_dragging)
        {
            if (Input.GetMouseButtonDown(0))
            {
                // 시작: 선택 카드 또는 debugBlock이 있어야 함
                if (PrepareSelectedCard())
                {
                    _dragging = true;
                    _ghostRotationSteps = 0;
                    ghost?.Show(_selectedCard);
                }
                else
                {
                    // nothing selected — ignore (could open hand UI)
                }
            }
        }
        else
        {
            // rotation input: left held (dragging) + right clicked
            if (Input.GetMouseButton(0) && Input.GetMouseButtonDown(1))
            {
                _ghostRotationSteps = (_ghostRotationSteps + 1) & 3;
            }

            bool can = (_selectedCard != null && grid != null) ? grid.CanPlace(_selectedCard, cell, _ghostRotationSteps) : false;
            ghost?.UpdatePreview(_selectedCard, cell, can, _ghostRotationSteps);

            if (Input.GetMouseButtonUp(0))
            {
                if (can)
                {
                    // Place on grid with rotation
                    bool placed = grid.Place(_selectedCard, cell, _ghostRotationSteps);
                    if (placed)
                    {
                        // If card came from hand, notify CardManager to consume it using card reference (safer)
                        if (_selectedHandIndex >= 0 && CardManager.Instance != null)
                        {
                            bool used = CardManager.Instance.UseCardByReference(_selectedCard);
                            if (!used)
                            {
                                // fallback: find index manually since IReadOnlyList doesn't provide IndexOf
                                int fallbackIdx = -1;
                                var hand = CardManager.Instance.GetHand();
                                for (int k = 0; k < hand.Count; k++)
                                {
                                    if (hand[k] == _selectedCard)
                                    {
                                        fallbackIdx = k;
                                        break;
                                    }
                                }

                                if (fallbackIdx >= 0)
                                {
                                    CardManager.Instance.UseCardAt(fallbackIdx);
                                }
                                else
                                {
                                    Debug.LogWarning("[InteractionManager] Card placed but could not find card in hand to consume.");
                                }
                            }
                        }
                    }
                }

                ghost?.Hide();
                _dragging = false;

                // after drop clear selection if debugBlock was used; if from hand, selection resets to none
                _selectedHandIndex = -1;
                _selectedCard = null;
                _ghostRotationSteps = 0;
            }
        }
    }

    /// <summary>
    /// UI에서 스크린좌표로 배치를 시도합니다.
    /// rotationSteps: 0..3
    /// 반환: SUCCESSFULLY placed returns true (Grid.Place 성공), 아닌면 false.
    /// </summary>
    public bool TryPlaceAtScreen(BlockSO card, Vector2 screenPosition, int rotationSteps = 0)
    {
        if (card == null || grid == null) return false;
        // convert screen to world (use main camera)
        Camera cam = _cam != null ? _cam : Camera.main;
        var world = cam != null ? cam.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, cam.nearClipPlane)) : (Vector3)screenPosition;
        world.z = 0f;
        var cell = grid.WorldToGrid(world);
        if (!grid.CanPlace(card, cell, rotationSteps)) return false;
        bool placed = grid.Place(card, cell, rotationSteps);
        return placed;
    }

    // External/UI drag helpers (CardUI uses these)
    public void StartExternalDrag(BlockSO card)
    {
        _selectedCard = card;
        _selectedHandIndex = -1;
        _dragging = true;
        _ghostRotationSteps = 0;
        ghost?.Show(card);
    }

    public void RotateGhostClockwiseExternal()
    {
        if (!_dragging) return;
        _ghostRotationSteps = (_ghostRotationSteps + 1) & 3;
    }

    public void EndExternalDrag()
    {
        _dragging = false;
        _selectedCard = null;
        _selectedHandIndex = -1;
        _ghostRotationSteps = 0;
        ghost?.Hide();
    }

    private void UpdateHandSelectionByKeys()
    {
        // only if CardManager exists and has hand
        if (CardManager.Instance == null) return;

        var hand = CardManager.Instance.GetHand();
        // keys 1..n map to indices 0..n-1
        for (int i = 0; i < Mathf.Min(9, hand.Count); i++)
        {
            if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
            {
                _selectedHandIndex = i;
                _selectedCard = hand[i];
                Debug.Log($"[InteractionManager] Selected hand index {_selectedHandIndex} card {_selectedCard?.name}");
            }
        }
    }

    private bool PrepareSelectedCard()
    {
        // priority: selected from hand -> debugBlock
        if (_selectedHandIndex >= 0 && CardManager.Instance != null)
        {
            var hand = CardManager.Instance.GetHand();
            if (_selectedHandIndex < hand.Count)
            {
                _selectedCard = hand[_selectedHandIndex];
                return _selectedCard != null;
            }
            // invalid index fallthrough
            _selectedHandIndex = -1;
        }

        if (debugBlock != null)
        {
            _selectedCard = debugBlock;
            _selectedHandIndex = -1;
            return true;
        }

        // no card selected
        _selectedCard = null;
        return false;
    }
}
