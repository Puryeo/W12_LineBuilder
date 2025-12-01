using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 그리드의 가로/세로 헤더(칼, 방패 모양). 
/// 속성 아이템을 드롭받아 GridAttributeMap을 갱신함.
/// </summary>
public class GridHeaderSlotUI : MonoBehaviour, IDropHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public enum Axis { Row, Col }

    [Header("Settings")]
    public Axis axis;
    [Tooltip("몇 번째 줄인지 (0~7)")]
    public int index;

    [Header("Visual")]
    public Image slotIcon; // 현재 속성을 보여줄 아이콘 (비어있으면 기본 이미지)

    [Header("Attribute Sprites")]
    public Sprite woodSordSprite;
    public Sprite woodShieldSprite;
    public Sprite staff;
    public Sprite hammer;
    public Sprite cross;

    private GridAttributeMap _attributeMap;
    private AttributeInventoryUI _inventoryUI;
    private Transform _canvasTransform;
    private GameObject _dragIcon;

    // 현재 드래그 중인 슬롯을 전역으로 공유
    public static GridHeaderSlotUI draggedSlot;

    private void Start()
    {
        // GridAttributeMap 참조 찾기 (GridManager에 붙어있음)
        if (GridManager.Instance != null)
            _attributeMap = GridManager.Instance.GetComponent<GridAttributeMap>();

        // AttributeInventoryUI 찾기 (더 안전한 방법 사용)
        _inventoryUI = FindAnyObjectByType<AttributeInventoryUI>();

        if (_inventoryUI == null)
        {
            Debug.LogWarning($"[GridHeaderSlotUI] AttributeInventoryUI를 찾을 수 없습니다! (axis={axis}, index={index})");
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null) _canvasTransform = canvas.transform;

        UpdateVisual();
    }

    public AttributeType GetCurrentAttribute()
    {
        if (_attributeMap == null) return AttributeType.None;
        return (axis == Axis.Row) ? _attributeMap.GetRow(index) : _attributeMap.GetCol(index);
    }

    // 속성 설정 헬퍼
    public void SetAttribute(AttributeType type)
    {
        if (_attributeMap == null) return;

        if (axis == Axis.Row)
        {
            _attributeMap.SetRow(index, type);
            Debug.Log($"[GridHeaderSlotUI] Row {index} 속성 설정: {type}");
        }
        else
        {
            _attributeMap.SetCol(index, type);
            Debug.Log($"[GridHeaderSlotUI] Col {index} 속성 설정: {type}");
        }

        UpdateVisual();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 1. 우클릭인지 확인
        if (eventData.button != PointerEventData.InputButton.Right) return;

        // 2. 정비 단계인지 확인
        if (GameFlowManager.Instance != null && GameFlowManager.Instance.currentState != GameFlowManager.GameState.Preparation) return;

        // 3. 슬롯에 아이템이 있는지 확인
        AttributeType current = GetCurrentAttribute();
        if (current == AttributeType.None) return; // 빈 슬롯은 무시

        Debug.Log($"[GridHeaderSlotUI] 우클릭 - 장비 해제 시도: {current} (axis={axis}, index={index})");

        // 4. 인벤토리 참조 재확인 (null이면 다시 찾기)
        if (_inventoryUI == null)
        {
            _inventoryUI = FindAnyObjectByType<AttributeInventoryUI>();
            Debug.LogWarning($"[GridHeaderSlotUI] _inventoryUI가 null이어서 재탐색 시도");
        }

        // 5. 인벤토리로 반환 (장착 해제)
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

        // 6. 슬롯 비우기
        SetAttribute(AttributeType.None);

        // 7. 설명 패널 끄기 (아이템이 사라졌으므로)
        ExplainPanelUI.Instance?.Hide();
    }

    // 드래그 시작 (슬롯 -> 어딘가로)
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (GameFlowManager.Instance != null && GameFlowManager.Instance.currentState != GameFlowManager.GameState.Preparation) return;

        // 빈 슬롯은 드래그 불가
        if (GetCurrentAttribute() == AttributeType.None) return;

        draggedSlot = this;

        // 드래그 아이콘 생성 (Visual Feedback)
        _dragIcon = new GameObject("SlotDragIcon");
        _dragIcon.transform.SetParent(_canvasTransform);
        _dragIcon.transform.SetAsLastSibling(); // 맨 위에 그리기

        Image iconImg = _dragIcon.AddComponent<Image>();
        iconImg.sprite = slotIcon.sprite;
        iconImg.raycastTarget = false; // 드롭 감지를 방해하지 않도록

        RectTransform rt = _dragIcon.GetComponent<RectTransform>();
        rt.sizeDelta = GetComponent<RectTransform>().sizeDelta;
        rt.position = transform.position;

        // 드래그 중인 본체 슬롯은 잠시 흐리게
        slotIcon.color = new Color(1, 1, 1, 0.5f);
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
        UpdateVisual(); // 색상 복구
    }

    public void OnDrop(PointerEventData eventData)
    {
        // 게임이 정비 상태일 때만 변경 가능
        if (GameFlowManager.Instance != null && GameFlowManager.Instance.currentState != GameFlowManager.GameState.Preparation)
            return;

        if (InventoryItemUI.draggedItem != null)
        {
            // 인벤토리 참조 재확인
            if (_inventoryUI == null)
            {
                _inventoryUI = FindAnyObjectByType<AttributeInventoryUI>();
            }

            // 1. 기존에 있던 속성은 인벤토리로 반환 (교체 로직)
            AttributeType current = GetCurrentAttribute();

            if (current != AttributeType.None && _inventoryUI != null)
            {
                _inventoryUI.CreateItem(current);
                Debug.Log($"[GridHeaderSlotUI] 드롭 시 기존 아이템 반환: {current}");
            }

            SetAttribute(InventoryItemUI.draggedItem.attributeType);

            if (_inventoryUI != null)
            {
                _inventoryUI.RemoveItem(InventoryItemUI.draggedItem);
            }
            else
            {
                Destroy(InventoryItemUI.draggedItem.gameObject);
            }
        }
        else if (draggedSlot != null && draggedSlot != this)
        {
            AttributeType myAttribute = GetCurrentAttribute();
            AttributeType otherAttribute = draggedSlot.GetCurrentAttribute();
            SetAttribute(otherAttribute);
            draggedSlot.SetAttribute(myAttribute);
        }
    }

    // 현재 GridAttributeMap의 상태에 따라 아이콘 색상 등을 변경
    public void UpdateVisual()
    {
        if (_attributeMap == null || slotIcon == null) return;

        // 현재 이 슬롯의 속성값 가져오기
        AttributeType currentType = GetCurrentAttribute();

        // 속성이 None이면 아이콘 숨기기
        if (currentType == AttributeType.None)
        {
            slotIcon.sprite = null;
            slotIcon.color = new Color(1, 1, 1, 0); // 투명하게 만들기
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
                case AttributeType.Hammer:
                    slotIcon.sprite = hammer;
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

    // Hover: 슬롯 위에 마우스가 올라가면 설명 패널 표시
    public void OnPointerEnter(PointerEventData eventData)
    {
        AttributeType current = GetCurrentAttribute();
        if (current == AttributeType.None || slotIcon == null) return;

        string desc = AttributeInfo.GetDescription(current);
        Sprite sp = slotIcon.sprite;
        RectTransform target = slotIcon.rectTransform;
        if (ExplainPanelUI.Instance != null)
            ExplainPanelUI.Instance.Show(sp, desc, target);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ExplainPanelUI.Instance?.Hide();
    }

    public void RemoveInventoryItem(InventoryItemUI targetItem)
    {
        if (_inventoryUI != null)
        {
            _inventoryUI.RemoveItem(targetItem);
        }
        else
        {
            Destroy(targetItem);
        }
    }
}