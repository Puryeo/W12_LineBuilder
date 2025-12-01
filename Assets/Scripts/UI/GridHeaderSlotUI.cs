using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Handles drag/drop and visualization for the attribute slots displayed on the grid header.
/// </summary>
public class GridHeaderSlotUI : MonoBehaviour,
    IDropHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler
{
    public enum Axis { Row, Col }

    [Header("Settings")]
    public Axis axis;
    [Tooltip("Row/Column index (0-7).")]
    public int index;

    [Header("Visual")]
    public Image slotIcon;

    [Header("Attribute Sprites")]
    public Sprite woodSordSprite;
    public Sprite woodShieldSprite;
    public Sprite staff;
    public Sprite bratCandy;
    public Sprite cross;
    [Tooltip("Sprite used when the slot is locked by DisableSlot pattern, etc.")]
    public Sprite lockedSprite;

    private GridAttributeMap _attributeMap;
    private AttributeInventoryUI _inventoryUI;
    private Transform _canvasTransform;
    private GameObject _dragIcon;

    public static GridHeaderSlotUI draggedSlot;

    private bool _isLocked;
    private Sprite _lockOverrideSprite;
    public bool IsLocked => _isLocked;

    private void Start()
    {
        if (GridManager.Instance != null)
            _attributeMap = GridManager.Instance.GetComponent<GridAttributeMap>();

        // AttributeInventoryUI 찾기 (더 안전한 방법 사용)
#if UNITY_2023_1_OR_NEWER
        _inventoryUI = FindAnyObjectByType<AttributeInventoryUI>();
#else
        _inventoryUI = FindObjectOfType<AttributeInventoryUI>();
#endif

        if (_inventoryUI == null)
        {
            Debug.LogWarning($"[GridHeaderSlotUI] AttributeInventoryUI를 찾을 수 없습니다! (axis={axis}, index={index})");
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null) _canvasTransform = canvas.transform;

        UpdateVisual();
    }

    public void SetLocked(bool locked, Sprite overrideSprite = null)
    {
        _isLocked = locked;
        if (locked)
            _lockOverrideSprite = overrideSprite ?? _lockOverrideSprite;
        else
            _lockOverrideSprite = null;
        Debug.Log($"[GridHeaderSlotUI] SetLocked -> locked={locked} overrideSprite={(_lockOverrideSprite != null ? _lockOverrideSprite.name : "null")} baseSprite={(lockedSprite != null ? lockedSprite.name : "null")} (slot {axis} {index})", this);
        UpdateVisual();
    }

    public AttributeType GetCurrentAttribute()
    {
        if (_attributeMap == null) return AttributeType.None;
        return axis == Axis.Row ? _attributeMap.GetRow(index) : _attributeMap.GetCol(index);
    }

    public void SetAttribute(AttributeType type)
    {
        if (_attributeMap == null) return;
        if (_isLocked && type != AttributeType.None) return;

        if (axis == Axis.Row)
            _attributeMap.SetRow(index, type);
        else
            _attributeMap.SetCol(index, type);

        UpdateVisual();

        GridLineRenderer lineRenderer = FindAnyObjectByType<GridLineRenderer>();
        if (lineRenderer != null)
        {
            lineRenderer.RefreshGridLines();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_isLocked) return;
        if (eventData.button != PointerEventData.InputButton.Right) return;
        if (GameFlowManager.Instance != null &&
            GameFlowManager.Instance.currentState != GameFlowManager.GameState.Preparation) return;

        // 슬롯에 아이템이 있는지 확인
        var current = GetCurrentAttribute();
        if (current == AttributeType.None) return; // 빈 슬롯은 무시

        Debug.Log($"[GridHeaderSlotUI] 우클릭 - 장비 해제 시도: {current} (axis={axis}, index={index})");

        // 인벤토리 참조 재확인 (null이면 다시 찾기)
        if (_inventoryUI == null)
        {
            _inventoryUI = FindAnyObjectByType<AttributeInventoryUI>();
            Debug.LogWarning($"[GridHeaderSlotUI] _inventoryUI가 null이어서 재탐색 시도");
        }

        // 인벤토리로 반환 (장착 해제)
        if (_inventoryUI != null)
        {
            _inventoryUI.CreateItem(current);
            Debug.Log($"[GridHeaderSlotUI] 인벤토리에 아이템 추가 완료: {current}");
        }
        else
        {
            Debug.LogError("[GridHeaderSlotUI] _inventoryUI를 찾을 수 없어서 아이템을 인벤토리에 추가할 수 없습니다!");
            return; // 인벤토리에 추가 실패하면 슬롯도 비우지 않음
        }

        // 슬롯 비우기
        SetAttribute(AttributeType.None);

        // 설명 패널 끄기 (아이템이 사라졌으므로)
        ExplainPanelUI.Instance?.Hide();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_isLocked) return;
        if (GameFlowManager.Instance != null &&
            GameFlowManager.Instance.currentState != GameFlowManager.GameState.Preparation) return;

        if (GetCurrentAttribute() == AttributeType.None) return;

        draggedSlot = this;

        _dragIcon = new GameObject("SlotDragIcon");
        _dragIcon.transform.SetParent(_canvasTransform);
        _dragIcon.transform.SetAsLastSibling();

        var icon = _dragIcon.AddComponent<Image>();
        icon.sprite = slotIcon.sprite;
        icon.raycastTarget = false;

        var rt = _dragIcon.GetComponent<RectTransform>();
        rt.sizeDelta = GetComponent<RectTransform>().sizeDelta;
        rt.position = transform.position;

        slotIcon.color = new Color(1f, 1f, 1f, 0.5f);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragIcon != null)
            _dragIcon.transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        draggedSlot = null;
        if (_dragIcon != null) Destroy(_dragIcon);
        UpdateVisual();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (_isLocked) return;
        if (GameFlowManager.Instance != null &&
            GameFlowManager.Instance.currentState != GameFlowManager.GameState.Preparation)
            return;

        if (InventoryItemUI.draggedItem != null)
        {
            // 인벤토리 참조 재확인
            if (_inventoryUI == null)
            {
                _inventoryUI = FindAnyObjectByType<AttributeInventoryUI>();
            }

            // 기존에 있던 속성은 인벤토리로 반환 (교체 로직)
            var current = GetCurrentAttribute();
            if (current != AttributeType.None && _inventoryUI != null)
            {
                _inventoryUI.CreateItem(current);
                Debug.Log($"[GridHeaderSlotUI] 드롭 시 기존 아이템 반환: {current}");
            }

            SetAttribute(InventoryItemUI.draggedItem.attributeType);

            if (_inventoryUI != null)
                _inventoryUI.RemoveItem(InventoryItemUI.draggedItem);
            else
                Destroy(InventoryItemUI.draggedItem.gameObject);
        }
        else if (draggedSlot != null && draggedSlot != this)
        {
            var myAttribute = GetCurrentAttribute();
            var otherAttribute = draggedSlot.GetCurrentAttribute();
            SetAttribute(otherAttribute);
            draggedSlot.SetAttribute(myAttribute);
        }
    }

    public void UpdateVisual()
    {
        if (slotIcon == null) return;

        if (_isLocked)
        {
            var lockSprite = GetLockedSprite();
            if (lockSprite != null)
            {
                // 자물쇠 스프라이트가 있으면 표시
                slotIcon.sprite = lockSprite;
                slotIcon.color = Color.white;
            }
            else
            {
                // 자물쇠 스프라이트가 없으면 빨간색 X 표시 (fallback)
                slotIcon.sprite = null;
                slotIcon.color = new Color(1f, 0.2f, 0.2f, 0.8f); // 반투명 빨간색
                Debug.LogWarning($"[GridHeaderSlotUI] Locked slot ({axis} {index}) has no lock sprite! Assign 'lockedSprite' or pattern's 'lockedSlotSprite'", this);
            }
            return;
        }

        if (_attributeMap == null) return;

        var currentType = GetCurrentAttribute();
        if (currentType == AttributeType.None)
        {
            slotIcon.sprite = null;
            slotIcon.color = new Color(1f, 1f, 1f, 0f);
            return;
        }
        else
        {
            // 속성이 있으면 해당 이미지로 교체하고 불투명하게 만들기
            slotIcon.color = Color.white;

            switch (currentType)
            {
                case AttributeType.WoodSord:
                    slotIcon.sprite = woodSordSprite;
                    break;
                case AttributeType.WoodShield:
                    slotIcon.sprite = woodShieldSprite;
                    break;
                case AttributeType.Staff:
                    slotIcon.sprite = staff;
                    break;
                case AttributeType.BratCandy:
                    slotIcon.sprite = bratCandy;
                    break;
                case AttributeType.Cross:
                    slotIcon.sprite = cross;
                    break;
                default:
                    slotIcon.sprite = null;
                    slotIcon.color = new Color(1, 1, 1, 0);
                    break;
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_isLocked) return;

        var current = GetCurrentAttribute();
        if (current == AttributeType.None || slotIcon == null) return;

        var desc = AttributeInfo.GetDescription(current);
        var target = slotIcon.rectTransform;
        ExplainPanelUI.Instance?.Show(slotIcon.sprite, desc, target);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ExplainPanelUI.Instance?.Hide();
    }

    public void RemoveInventoryItem(InventoryItemUI targetItem)
    {
        if (_inventoryUI != null)
            _inventoryUI.RemoveItem(targetItem);
        else
            Destroy(targetItem);
    }

    private Sprite GetLockedSprite() => _lockOverrideSprite ?? lockedSprite;
}
