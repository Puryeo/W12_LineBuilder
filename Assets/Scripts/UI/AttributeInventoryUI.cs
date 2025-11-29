using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

public class AttributeInventoryUI : MonoBehaviour, IDropHandler
{
    public GameObject itemPrefab; // InventoryItemUI가 붙은 프리팹
    public Transform container;   // 아이템이 생성될 부모 (GridLayoutGroup 권장)

    [Header("Sprites")]
    public Sprite woodSord;
    public Sprite woodShield;
    public Sprite staff;
    public Sprite hammer;
    public Sprite cross;

    public List<InventoryItemUI> AllInventoryItem = new();
    public static List<GridHeaderSlotUI> AllHeaderSlots = new();
    public static List<GridHeaderSlotUI> AllRowSlots = new();
    public static List<GridHeaderSlotUI> AllColumnSlots = new();

    public void Initialize()
    {
        AllHeaderSlots.Clear();
        AllRowSlots.Clear();
        AllColumnSlots.Clear();

        var allSlots = FindObjectsByType<GridHeaderSlotUI>(FindObjectsSortMode.None);
        AllHeaderSlots = new List<GridHeaderSlotUI>(allSlots);

        AllRowSlots =  allSlots.Where(s => s.axis == GridHeaderSlotUI.Axis.Row)
            .OrderBy(s => s.index).ToList();

        AllColumnSlots = allSlots.Where(s => s.axis == GridHeaderSlotUI.Axis.Col)
            .OrderBy(s => s.index).ToList();
    }

    // 테스트용: 정비 단계 시작 시 기본 아이템 생성
    public void SetupTestItems()
    {
        // 기존 아이템 제거
        foreach (Transform child in container) Destroy(child.gameObject);

        AllInventoryItem.Clear();

        for(int i = 0;i < 8; i++)
        {
            CreateItem(AttributeType.WoodSord);
        }

        for(int i = 0;i < 8; i++)
        {
            CreateItem(AttributeType.WoodShield);
        }
    }

    public void CreateItem(AttributeType type)
    {
        if (itemPrefab == null || container == null) return;

        var go = Instantiate(itemPrefab, container);
        var ui = go.GetComponent<InventoryItemUI>();

        Sprite s = null;
        switch (type)
        {
            case AttributeType.WoodSord: s = woodSord; break;
            case AttributeType.WoodShield: s = woodShield; break;
            case AttributeType.Staff: s = staff; break;
            case AttributeType.Hammer: s = hammer; break;
            case AttributeType.Cross: s = cross; break;
        }

        // 설명을 함께 전달
        string desc = AttributeInfo.GetDescription(type);
        ui.Initialize(type, s, desc);

        AllInventoryItem.Add(ui);
    }

    public void RemoveItem(InventoryItemUI item)
    {
        if (item != null && AllInventoryItem.Contains(item))
        {
            AllInventoryItem.Remove(item);
            Destroy(item.gameObject);
        }
    }

    public void AddRoundRewards(AttributeType attributeType, int num)
    {
        for(int i = 0; i < num; i++)
        {
            CreateItem(attributeType);
        }
    }

    /// <summary>
    /// 슬롯에 있던 아이템을 인벤토리로 반환 받았을 때
    /// </summary>
    /// <param name="eventData"></param>
    public void OnDrop(PointerEventData eventData)
    {
        if (GameFlowManager.Instance != null && GameFlowManager.Instance.currentState != GameFlowManager.GameState.Preparation)
            return;

        GridHeaderSlotUI draggedSlot = GridHeaderSlotUI.draggedSlot;
        if (draggedSlot != null)
        {
            // 현재 슬롯에 있는 속성 가져오기
            AttributeType typeToReturn = draggedSlot.GetCurrentAttribute();

            if (typeToReturn != AttributeType.None)
            {
                CreateItem(typeToReturn);
                draggedSlot.SetAttribute(AttributeType.None);
            }
        }
    }

    public void ClearInventory()
    {
        foreach (Transform child in container) Destroy(child.gameObject);
        AllInventoryItem.Clear();
    }
}