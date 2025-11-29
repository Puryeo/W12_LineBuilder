using System;
using UnityEngine;
using UnityEngine.UI;

public class BlockRewardSelectionPanel : MonoBehaviour
{
    [Header("Option Buttons")]
    public Button option1Btn;
    public Button option2Btn;
    public Button option3Btn;

    [Header("Option Block Data (Assign in Inspector)")]
    public BlockSO option1Block;
    public BlockSO option2Block;
    public BlockSO option3Block;

    [Header("Highlight Images (Assign outlines)")]
    [Tooltip("선택 시 켜질 테두리 이미지들 (버튼 순서대로 할당)")]
    public GameObject highlight1;
    public GameObject highlight2;
    public GameObject highlight3;

    [Header("Action Buttons")]
    public Button confirmBtn; 
    public Button cancelBtn;

    // 내부 상태
    private Action _onSelectionComplete; // 부모에게 알릴 콜백
    private BlockSO _selectedBlock = null; // 현재 선택된 블록

    private void Start()
    {
        // 1. 옵션 버튼 리스너 연결
        if (option1Btn != null)
            option1Btn.onClick.AddListener(() => OnOptionClicked(option1Block, 0));
        if (option2Btn != null)
            option2Btn.onClick.AddListener(() => OnOptionClicked(option2Block, 1));
        if (option3Btn != null)
            option3Btn.onClick.AddListener(() => OnOptionClicked(option3Block, 2));
        // 2. 하단 액션 버튼 리스너 연결
        if (confirmBtn != null)
            confirmBtn.onClick.AddListener(OnConfirmClicked);
        if (cancelBtn != null)
            cancelBtn.onClick.AddListener(OnCancelClicked);
    }

    public void Open(Action onSelectionComplete)
    {
        _onSelectionComplete = onSelectionComplete;

        // 열릴 때마다 상태 초기화
        ResetSelection();
        gameObject.SetActive(true);
    }

    private void ResetSelection()
    {
        _selectedBlock = null;

        // 모든 하이라이트 끄기
        if (highlight1 != null) highlight1.SetActive(false);
        if (highlight2 != null) highlight2.SetActive(false);
        if (highlight3 != null) highlight3.SetActive(false);

        // 확인 버튼 비활성화 (선택된 게 없으므로)
        if (confirmBtn != null) confirmBtn.interactable = false;
    }

    private void OnOptionClicked(BlockSO blockData, int index)
    {
        _selectedBlock = blockData;

        if (highlight1 != null) highlight1.SetActive(index == 0);
        if (highlight2 != null) highlight2.SetActive(index == 1);
        if (highlight3 != null) highlight3.SetActive(index == 2);

        if (confirmBtn != null) confirmBtn.interactable = true;
    }

    private void OnConfirmClicked()
    {
        // 안전장치: 선택된 게 없으면 무시
        if (_selectedBlock == null) return;

        if (CardManager.Instance != null)
        {
            CardManager.Instance.AddCardToDiscard(_selectedBlock);
            Debug.Log($"[Reward] {_selectedBlock} 지급 완료");
        }

        CloseAndNotify();
    }

    private void OnCancelClicked()
    {
        Debug.Log("[Reward] 보상을 받지 않고 건너뜁니다.");
        CloseAndNotify();
    }

    private void CloseAndNotify()
    {
        // 패널 닫기
        gameObject.SetActive(false);

        _onSelectionComplete?.Invoke();
    }
}
