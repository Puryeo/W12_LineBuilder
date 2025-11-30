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
    public Sprite hammer;
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

#if UNITY_2023_1_OR_NEWER
        _inventoryUI = FindAnyObjectByType<AttributeInventoryUI>();
#else
        _inventoryUI = FindObjectOfType<AttributeInventoryUI>();
#endif

        var canvas = GetComponentInParent<Canvas>();
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
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_isLocked) return;
        if (eventData.button != PointerEventData.InputButton.Right) return;
        if (GameFlowManager.Instance != null &&
            GameFlowManager.Instance.currentState != GameFlowManager.GameState.Preparation) return;

        var current = GetCurrentAttribute();
        if (current == AttributeType.None) return;

        if (_inventoryUI != null)
            _inventoryUI.CreateItem(current);

        SetAttribute(AttributeType.None);
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
            var current = GetCurrentAttribute();
            if (current != AttributeType.None && _inventoryUI != null)
                _inventoryUI.CreateItem(current);

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
            slotIcon.sprite = lockSprite;
            slotIcon.color = lockSprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
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
            case AttributeType.Hammer:
                slotIcon.sprite = hammer;
                break;
            case AttributeType.Cross:
                slotIcon.sprite = cross;
                break;
            default:
                slotIcon.sprite = null;
                slotIcon.color = new Color(1f, 1f, 1f, 0f);
                break;
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
