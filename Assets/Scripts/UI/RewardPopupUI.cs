using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

public class RewardPopupUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panel;        // 팝업 전체
    public Transform listContainer; // Vertical Layout Group이 있는 곳
    public Button confirmButton;    // 확인(닫기) 버튼

    [Header("Prefabs & SubPanels")]
    public GameObject rewardItemPrefab; // RewardItemUI가 붙은 프리팹
    public SlotRewardSelectionPanel slotSelectionPanel; // 슬롯 선택 하위 패널
    public BlockRewardSelectionPanel blockSelectionPanel; // 블록 선택 하위 패널

    private Action _onConfirmCallback;
    private List<RewardItemUI> _activeRewardItems = new List<RewardItemUI>();

    private void Start()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirmClicked);
        }
    }

    public void SetCallBack(Action onConfirm)
    {
        _onConfirmCallback = onConfirm;

        GenerateRewards();
        UpdateConfirmButtonState();
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void GenerateRewards()
    {
        // 기존 목록 청소
        foreach (Transform child in listContainer) Destroy(child.gameObject);
        _activeRewardItems.Clear();

        // --- 보상 1: 슬롯 4개 선택 ---
        CreateRewardItem("슬롯 x 4 선택", () =>
        {
            // 클릭 시 실행할 동작: 슬롯 선택 패널 열기
            if (slotSelectionPanel != null)
            {
                // 패널을 열면서, 선택이 완료되면 실행할 함수(람다)를 전달
                slotSelectionPanel.Open(() =>
                {
                    // 선택이 완료되면 이 보상 아이템을 '수령함' 처리
                    MarkRewardAsClaimed(0);
                });
            }
        });

        // --- 보상 2: 블록 선택 (아직 구현 안 함 - 껍데기만) ---
        CreateRewardItem("블록 선택하기", () =>
        {
            if(blockSelectionPanel != null)
            {
                blockSelectionPanel.Open(() =>
                {
                    // 선택이 완료되면 이 보상 아이템을 '수령함' 처리
                    MarkRewardAsClaimed(1);
                });
            }
        });
    }

    private void CreateRewardItem(string title, Action onClick)
    {
        if (rewardItemPrefab == null || listContainer == null) return;

        GameObject go = Instantiate(rewardItemPrefab, listContainer);
        RewardItemUI ui = go.GetComponent<RewardItemUI>();

        // 아이템 초기화
        // 클릭 시 onClick 함수를 실행하고, 그 뒤에 버튼 상태 체크를 하도록 감쌈
        ui.Initialize(title, onClick);

        _activeRewardItems.Add(ui);
    }

    private void MarkRewardAsClaimed(int index)
    {
        if (index >= 0 && index < _activeRewardItems.Count)
        {
            _activeRewardItems[index].MarkAsClaimed();
            UpdateConfirmButtonState(); // 하나 받을 때마다 확인 버튼 켜도 되는지 검사
        }
    }

    private void UpdateConfirmButtonState()
    {
        if (confirmButton == null) return;

        // 모든 보상 아이템이 Claimed 상태인지 확인
        bool allClaimed = true;
        foreach (var item in _activeRewardItems)
        {
            if (!item.IsClaimed)
            {
                allClaimed = false;
                break;
            }
        }

        confirmButton.interactable = allClaimed;
    }

    private void OnConfirmClicked()
    {
        // 비활성화 상태면 클릭 안되겠지만 안전장치
        if (confirmButton != null && !confirmButton.interactable) return;

        Hide();
        _onConfirmCallback?.Invoke();
    }
}