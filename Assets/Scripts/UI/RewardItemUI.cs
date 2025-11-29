using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class RewardItemUI : MonoBehaviour
{
    [Header("UI")]
    public Button button;
    public TextMeshProUGUI titleText;
    public GameObject claimedOverlay; // 보상 획득 시 덮어씌울 이미지 (체크 표시 등)

    private Action _onClickAction;
    public bool IsClaimed { get; private set; } = false;

    public void Initialize(string title, Action onClick)
    {
        if (titleText != null) titleText.text = title;
        _onClickAction = onClick;

        IsClaimed = false;
        if (claimedOverlay != null) claimedOverlay.SetActive(false);
        if (button != null)
        {
            button.interactable = true;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClicked);
        }
    }

    private void OnClicked()
    {
        if (IsClaimed) return;
        _onClickAction?.Invoke();
    }

    public void MarkAsClaimed()
    {
        IsClaimed = true;
        if (button != null) button.interactable = false; // 클릭 방지
        if (claimedOverlay != null) claimedOverlay.SetActive(true); // 완료 표시
    }
}