using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HandUIManager:
/// - CardManager의 손패를 UI로 표현
/// - CardUI prefab 인스턴스화 및 갱신
/// </summary>
public class HandUIManager : MonoBehaviour
{
    [Header("References")]
    public RectTransform handContainer; // 하단에 정렬할 부모 RectTransform
    public GameObject cardUIPrefab;     // 카드 UI 프리팹 (Image + CardUI)
    public Sprite defaultCardSprite;    // 카드 이미지 기본 스프라이트

    private readonly List<GameObject> _instanced = new List<GameObject>();

    private void Awake()
    {
        if (handContainer == null)
            Debug.LogWarning("[HandUIManager] handContainer is not assigned.");
        if (cardUIPrefab == null)
            Debug.LogWarning("[HandUIManager] cardUIPrefab is not assigned.");
    }

    private void Start()
    {
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnHandChanged += OnHandChanged;
            // initial populate
            OnHandChanged(new List<BlockSO>(CardManager.Instance.GetHand()));
        }
    }

    private void OnDestroy()
    {
        if (CardManager.Instance != null)
            CardManager.Instance.OnHandChanged -= OnHandChanged;
    }

    private void OnHandChanged(List<BlockSO> hand)
    {
        // clear existing
        foreach (var go in _instanced) { if (go != null) Destroy(go); }
        _instanced.Clear();

        if (hand == null || cardUIPrefab == null || handContainer == null) return;

        for (int i = 0; i < hand.Count; i++)
        {
            var card = hand[i];
            var go = Instantiate(cardUIPrefab, handContainer);

            // 안전한 방식: 문자열 연결로 이름 생성 (이스케이프 문제 회피)
            string cardName = (card != null && !string.IsNullOrEmpty(card.name)) ? card.name : "null";
            go.name = "CardUI_" + i + "_" + cardName;

            var cardUI = go.GetComponent<CardUI>();
            if (cardUI != null)
            {
                cardUI.Initialize(card, defaultCardSprite);
            }
            _instanced.Add(go);
        }
    }
}