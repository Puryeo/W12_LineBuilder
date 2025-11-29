using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 인벤토리에 있는 속성 아이콘 하나. 드래그 가능.
/// </summary>
public class InventoryItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public AttributeType attributeType;
    public Image iconImage;

    [Header("설명 (호버 시 나타납니다)")]
    [TextArea]
    public string effectDescription;

    [HideInInspector] public Transform parentBeforeDrag;
    public static InventoryItemUI draggedItem; // 현재 드래그 중인 아이템 정적 참조

    private CanvasGroup _canvasGroup;
    private Transform _canvasTransform;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // 캔버스를 찾아야 함 (UI 최상위)
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null) _canvasTransform = canvas.transform;
    }

    // 초기화: 이제 설명도 함께 받음
    public void Initialize(AttributeType type, Sprite sprite, string description)
    {
        attributeType = type;
        if (iconImage != null) iconImage.sprite = sprite;
        effectDescription = description;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 1. 우클릭인지 확인
        if (eventData.button != PointerEventData.InputButton.Right) return;

        // 2. 정비 단계인지 확인
        if (GameFlowManager.Instance != null && GameFlowManager.Instance.currentState != GameFlowManager.GameState.Preparation) return;

        // 3. 자동 장착 시도
        TryAutoEquip();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        draggedItem = this;
        parentBeforeDrag = transform.parent;

        // 드래그 시 UI가 다른 요소 위에 그려지도록 부모 변경
        if (_canvasTransform != null) transform.SetParent(_canvasTransform);

        _canvasGroup.alpha = 0.6f;
        _canvasGroup.blocksRaycasts = false; // 드롭 감지를 위해 레이캐스트 통과
    }

    public void OnDrag(PointerEventData eventData)
    {
        transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        draggedItem = null;
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;

        // 드롭에 실패했다면(부모가 캔버스 그대로라면) 원래 위치로 복귀
        if (transform.parent == _canvasTransform)
        {
            transform.SetParent(parentBeforeDrag);
            // Layout 갱신을 위해 위치 초기화는 LayoutGroup이 처리하겠지만 명시적으로
            transform.localPosition = Vector3.zero;
        }
    }

    // Hover: 아이콘 기준으로 설명 패널 표시
    public void OnPointerEnter(PointerEventData eventData)
    {
        // 드래그 중에는 호버 패널 표시하지 않음
        if (draggedItem != null) return;

        if (ExplainPanelUI.Instance == null) return;
        if (iconImage == null || iconImage.rectTransform == null) return;

        Sprite s = iconImage.sprite;
        RectTransform target = iconImage.rectTransform;

        ExplainPanelUI.Instance.Show(s, effectDescription, target);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ExplainPanelUI.Instance?.Hide();
    }

    private void TryAutoEquip()
    {
        var rowSlots = AttributeInventoryUI.AllRowSlots;
        var colSlots = AttributeInventoryUI.AllColumnSlots;

        GridHeaderSlotUI targetSlot = null;

        foreach(var slot in rowSlots)
        {
            if (slot.GetCurrentAttribute() == AttributeType.None)
            {
                targetSlot = slot;
                break;
            }
        }

        if (targetSlot == null)
        {
            foreach(var slot in colSlots)
            {
                if (slot.GetCurrentAttribute() == AttributeType.None)
                {
                    targetSlot = slot;
                    break;
                }
            }
        }

        if (targetSlot != null)
        {
            // 슬롯에 속성 적용 (데이터 + 비주얼 갱신)
            targetSlot.SetAttribute(this.attributeType);

            // 인벤토리에서 제거 (리스트 갱신 + 오브젝트 파괴)
            targetSlot.RemoveInventoryItem(this);

            Debug.Log($"[AutoEquip] {attributeType} 장착됨 -> {targetSlot.axis} {targetSlot.index}");
        }
        else
        {
            Debug.Log("[AutoEquip] 빈 슬롯이 없습니다.");
        }

        ExplainPanelUI.Instance?.Hide();
    }
}