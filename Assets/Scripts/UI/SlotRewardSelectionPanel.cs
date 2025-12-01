using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class SlotRewardSelectionPanel : MonoBehaviour
{
    [Header("Option Buttons")]
    public Button option1Btn;
    public GameObject highlight1;
    public Image option1Icon;
    public TMP_Text option1Text;

    public Sprite staffIcon;
    public Sprite crossIcon;
    public Sprite bratCandyIcon;

    public string staffText;
    public string crossText;
    public string bratCandyText;

    [Header("Action Buttons")]
    public Button confirmBtn;
    public Button cancelBtn; 

    [Header("Settings")]
    public int rewardAmount = 4;

    [Header("References")]
    public AttributeInventoryUI attributeInventoryUI;

    // 내부 상태
    private Action _onSelectionComplete; // 부모에게 알릴 콜백
    private AttributeType _selectedAttribute = AttributeType.None; // 현재 선택된 속성

    private void OnEnable()
    {
        AttributeType attributeType = AttributeType.None;

        switch (GameFlowManager.Instance.currentRoundIndex)
        {
            case 0:
                attributeType = AttributeType.BratCandy;
                option1Icon.sprite = bratCandyIcon;
                option1Text.text = bratCandyText;
                break;
            case 1:
                attributeType = AttributeType.Cross;
                option1Icon.sprite = crossIcon;
                option1Text.text = crossText;
                break;
            case 2:
                attributeType = AttributeType.Staff;
                option1Icon.sprite = staffIcon;
                option1Text.text = staffText;
                break;
            default:
                break;
        }

        if (option1Btn != null)
            option1Btn.onClick.AddListener(() => OnOptionClicked(attributeType, 0));

        AddHoverToButton(option1Btn, attributeType);

        // 2. 하단 액션 버튼 리스너 연결
        if (confirmBtn != null)
            confirmBtn.onClick.AddListener(OnConfirmClicked);

        if (cancelBtn != null)
            cancelBtn.onClick.AddListener(OnCancelClicked);
    }

    /// <summary>
    /// 패널을 열 때 호출 (외부에서)
    /// </summary>
    public void Open(Action onSelectionComplete)
    {
        _onSelectionComplete = onSelectionComplete;

        // 열릴 때마다 상태 초기화
        ResetSelection();
        gameObject.SetActive(true);
    }

    private void ResetSelection()
    {
        _selectedAttribute = AttributeType.None;

        // 모든 하이라이트 끄기
        if (highlight1 != null) highlight1.SetActive(false);

        // 확인 버튼 비활성화 (선택된 게 없으므로)
        if (confirmBtn != null) confirmBtn.interactable = false;
    }

    /// <summary>
    /// 3개의 옵션 중 하나를 클릭했을 때
    /// </summary>
    private void OnOptionClicked(AttributeType type, int index)
    {
        _selectedAttribute = type;

        // 1. 하이라이트 갱신 (선택된 놈만 켜고 나머지 끄기)
        if (highlight1 != null) highlight1.SetActive(index == 0);

        // 2. 확인 버튼 활성화
        if (confirmBtn != null) confirmBtn.interactable = true;
    }

    /// <summary>
    /// [받는다] 버튼 클릭
    /// </summary>
    private void OnConfirmClicked()
    {
        if (_selectedAttribute == AttributeType.None) return;

        rewardAmount = 4;

        if (attributeInventoryUI != null)
        {
            if(_selectedAttribute == AttributeType.BratCandy)
                rewardAmount = 1; // 개초딩 캔디는 1개 지급

            attributeInventoryUI.AddRoundRewards(_selectedAttribute, rewardAmount);
            Debug.Log($"[Reward] {_selectedAttribute} x {rewardAmount} 지급 완료");
        }

        CloseAndNotify();
    }

    /// <summary>
    /// [받지 않는다] 버튼 클릭
    /// </summary>
    private void OnCancelClicked()
    {
        Debug.Log("[Reward] 보상을 받지 않고 건너뜁니다.");
        CloseAndNotify();
    }

    private void CloseAndNotify()
    {
        // 패널 닫기
        gameObject.SetActive(false);

        // 부모(팝업)에게 "이 보상 처리 끝났어"라고 알림
        // (받았든 안 받았든 이 단계는 끝난 것이므로 완료 처리)
        _onSelectionComplete?.Invoke();
    }

    // --- 추가 메서드: 버튼에 호버 이벤트를 동적으로 등록 ---
    private void AddHoverToButton(Button btn, AttributeType type)
    {
        if (btn == null) return;

        GameObject go = btn.gameObject;
        EventTrigger trigger = go.GetComponent<EventTrigger>();
        if (trigger == null) trigger = go.AddComponent<EventTrigger>();

        // Enter
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        // 캡쳐 문제 방지: 로컬에 복사
        Button localBtn = btn;
        AttributeType localType = type;
        enter.callback.AddListener((data) =>
        {
            if (ExplainPanelUI.Instance == null) return;
            // 드래그 중이면 호버 무시
            if (InventoryItemUI.draggedItem != null) return;

            // 1) 우선 SlotIcon 이름을 가진 자식 Image를 찾음 (대소문자 무시)
            Image img = FindChildIconImage(localBtn);

            // 2) Sprite 결정: AttributeInfo 기반 설명
            Sprite sp = img != null ? img.sprite : null;
            string desc = AttributeInfo.GetDescription(localType);
            RectTransform target = (img != null) ? img.rectTransform : localBtn.transform as RectTransform;

            ExplainPanelUI.Instance.Show(sp, desc, target);
        });

        // Exit
        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener((data) =>
        {
            ExplainPanelUI.Instance?.Hide();
        });

        trigger.triggers.Add(enter);
        trigger.triggers.Add(exit);
    }

    // 우선순위: 이름이 "SlotIcon"인 자식(Image) -> 자식 중 sprite가 있는 첫 Image(버튼 이미지 제외) -> 자식 중 sprite가 있는 첫 Image -> 버튼의 Image
    private Image FindChildIconImage(Button btn)
    {
        Image buttonImage = btn.GetComponent<Image>();

        // 1. name match (자손 전체 탐색)
        var allImages = btn.GetComponentsInChildren<Image>(true);
        foreach (var image in allImages)
        {
            if (string.Equals(image.gameObject.name, "SlotIcon", StringComparison.OrdinalIgnoreCase))
                return image;
        }

        // 2. child image with sprite and not equal to button's image
        foreach (var image in allImages)
        {
            if (image == buttonImage) continue;
            if (image.sprite != null) return image;
        }

        // 3. any child image with sprite
        foreach (var image in allImages)
        {
            if (image.sprite != null) return image;
        }

        // 4. fallback to button image
        return buttonImage;
    }
}