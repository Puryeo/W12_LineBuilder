using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DeckViewUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("덱 보기 팝업 패널 (처음엔 꺼져있어야 함)")]
    public GameObject popupPanel;

    [Tooltip("카드가 나열될 부모 Transform (Scroll View의 Content)")]
    public Transform cardContainer;

    [Tooltip("패널을 닫는 버튼")]
    public Button closeButton;

    [Tooltip("BattlePanel에 있는 덱 보기 열기 버튼")]
    public Button openButton;

    [Header("Prefab Settings")]
    [Tooltip("리스트에 생성할 카드 프리팹 (Image 컴포넌트 필수)")]
    public GameObject cardDisplayPrefab;
    public Sprite defaultCardSprite;


    private void Start()
    {
        // 시작 시 팝업 숨김
        if (popupPanel != null) popupPanel.SetActive(false);

        // 버튼 리스너 연결
        if (openButton != null)
            openButton.onClick.AddListener(OpenDeckView);

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseDeckView);
    }

    /// <summary>
    /// 덱 보기 팝업을 엽니다.
    /// </summary>
    public void OpenDeckView()
    {
        if (popupPanel != null) popupPanel.SetActive(true);
        RefreshDeckList();
    }

    /// <summary>
    /// 덱 보기 팝업을 닫습니다.
    /// </summary>
    public void CloseDeckView()
    {
        if (popupPanel != null) popupPanel.SetActive(false);
    }

    /// <summary>
    /// CardManager에서 현재 덱 정보를 가져와 UI를 갱신합니다.
    /// </summary>
    private void RefreshDeckList()
    {
        // 1. 기존 목록 청소
        foreach (Transform child in cardContainer)
        {
            Destroy(child.gameObject);
        }

        if (CardManager.Instance == null) return;

        // 2. 현재 덱 리스트 가져오기
        // (참고: CardManager는 리스트의 '끝'에서부터 드로우하므로, 순서를 뒤집어서 보여주거나 그대로 보여줄 수 있습니다.
        // 여기서는 리스트 순서 그대로 보여줍니다.)
        IReadOnlyList<BlockSO> currentDeck = CardManager.Instance.GetDeck();

        // 3. 카드 생성
        foreach (var cardData in currentDeck)
        {
            if (cardData == null) continue;

            GameObject go = Instantiate(cardDisplayPrefab, cardContainer);

            // 간단 구현: 프리팹에 CardUI 컴포넌트가 있다면 활용하되 인터랙션은 끔
            var cardUI = go.GetComponent<CardUI>();
            if (cardUI != null)
            {
                cardUI.Initialize(cardData, defaultCardSprite);
                // 덱 확인용이므로 드래그 방지
                Destroy(cardUI); // 컴포넌트만 제거해서 기능 정지
            }
            else
            {
                // CardUI가 없는 단순 프리팹인 경우: 자식에서 아이콘 찾기 시도
                if (go.transform.childCount > 0)
                {
                    var iconImg = go.transform.GetChild(0).GetComponent<Image>();
                    if (iconImg != null && cardData.previewSprite != null)
                    {
                        iconImg.sprite = cardData.previewSprite;
                        iconImg.preserveAspect = true;
                    }
                }
            }
        }
    }
}