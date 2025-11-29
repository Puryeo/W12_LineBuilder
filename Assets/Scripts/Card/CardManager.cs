using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CardManager:
/// - 덱(deck), 버린덱(discard), 손패(hand) 관리
/// - 덱 초기화(기본 블록 SO 배열을 Inspector에서 지정)
/// - UseCardByReference로 카드 사용
/// - 라인 완성 시 보너스 드로우 처리
/// - 손패가 비면 자동으로 턴 종료
/// </summary>
public class CardManager : MonoBehaviour
{
    public static CardManager Instance { get; private set; }

    [Header("Deck Source (assign 7 unique BlockSO ideally)")]
    public BlockSO[] baseBlockSet;

    [Header("Deck Settings")]
    public int duplicatePerType = 2; // PRD: 7종 x 2장 = 14장

    [Header("Hand Settings")]
    public int handSize = 3;

    // 내부 상태
    private List<BlockSO> _deck = new List<BlockSO>();
    private List<BlockSO> _discard = new List<BlockSO>();
    private List<BlockSO> _hand = new List<BlockSO>();

    // 이벤트: UI/다른 매니저가 구독
    public event Action<List<BlockSO>> OnHandChanged;
    public event Action OnDeckRefilled;
    public event Action<BlockSO> OnCardUsed;
    public event Action OnDeckChanged;
    public event Action OnHandEmpty; // 새 이벤트: 손패가 비었을 때

    private System.Random _rng = new System.Random();

    private void Awake()
    {
        Debug.Log("[CardManager] Awake");
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        Debug.Log("[CardManager] Start - initializing deck");
        InitializeDeckFromBaseSet();
        FillInitialHand();

        Debug.Log($"[CardManager] Deck={_deck.Count} Discard={_discard.Count} Hand={_hand.Count}");
    }

    #region Public API

    public IReadOnlyList<BlockSO> GetHand() => _hand.AsReadOnly();

    public IReadOnlyList<BlockSO> GetDeck() => _deck.AsReadOnly();

    public IReadOnlyList<BlockSO> GetDiscard() => _discard.AsReadOnly();

    /// <summary>
    /// [추가] 라운드가 바뀔 때 강제로 손패를 초기화하고 새로 드로우하는 함수
    /// </summary>
    public void ResetHandForNextRound()
    {
        // 1. 현재 손패에 있는 모든 카드를 버린 카드 더미(_discard)로 이동
        if (_hand.Count > 0)
        {
            _discard.AddRange(_hand);
            _hand.Clear();
        }

        // 2. UI 갱신 (빈 손패 상태 알림)
        NotifyHandChanged();

        // 3. 기본 핸드 크기(3장)만큼 새로 드로우
        FillInitialHand();

        Debug.Log("[CardManager] Hand reset for next round complete.");
    }

    /// <summary>
    /// 카드 참조로 사용: 현재 손패에서 해당 카드 객체를 찾아 사용합니다.
    /// 카드를 discard로 보내고 손패에서 제거합니다 (드로우는 하지 않음)
    /// 손패가 비면 OnHandEmpty 이벤트를 발생시킵니다.
    /// </summary>
    public bool UseCardByReference(BlockSO card)
    {
        if (card == null)
        {
            Debug.LogWarning("[CardManager] UseCardByReference called with null");
            return false;
        }

        int idx = _hand.IndexOf(card);
        if (idx < 0)
        {
            Debug.LogWarning($"[CardManager] UseCardByReference: card '{card.name}' not found in hand");
            return false;
        }

        var used = _hand[idx];
        _discard.Add(used);
        _hand.RemoveAt(idx);

        OnCardUsed?.Invoke(used);

        Debug.Log($"[CardManager] UseCardByReference card={used.name} handNow={_hand.Count} deckNow={_deck.Count} discardNow={_discard.Count}");
        NotifyHandChanged();

        // 손패가 비었는지 확인
        if (_hand.Count == 0)
        {
            Debug.Log("[CardManager] Hand is now empty - triggering OnHandEmpty event");
            OnHandEmpty?.Invoke();
        }

        return true;
    }

    /// <summary>
    /// 일반 라인 완성 시 보너스 드로우 (1장)
    /// </summary>
    public void DrawBonusForNormalLine()
    {
        Debug.Log("[CardManager] DrawBonusForNormalLine - drawing 1 card");
        DrawToHandIgnoringLimit(1);
        NotifyHandChanged();
    }

    /// <summary>
    /// 폭탄 포함 라인 완성 시 보너스 드로우 (2장)
    /// </summary>
    public void DrawBonusForBombLine()
    {
        Debug.Log("[CardManager] DrawBonusForBombLine - drawing 2 cards");
        DrawToHandIgnoringLimit(2);
        NotifyHandChanged();
    }

    /// <summary>
    /// 턴 종료 시 호출: 현재 손패 전체를 버리고 새로운 손패를 드로우
    /// </summary>
    public void EndTurnDiscardAndDraw()
    {
        Debug.Log($"[CardManager] EndTurnDiscardAndDraw - discarding {_hand.Count} cards");

        // 현재 손패 전체를 버린 덱으로
        _discard.AddRange(_hand);
        _hand.Clear();

        // 새로운 손패 드로우
        FillInitialHand();

        Debug.Log($"[CardManager] After EndTurnDiscardAndDraw: Hand={_hand.Count} Deck={_deck.Count} Discard={_discard.Count}");
    }

    /// <summary>
    /// 보상 등으로 획득한 새 카드를 덱 순환 시스템에 추가합니다.
    /// 기본적으로 '버린 카드(Discard)' 더미에 추가하여 다음 셔플 때 등장하게 합니다.
    /// </summary>
    public void AddCardToDiscard(BlockSO newCard)
    {
        if (newCard == null)
        {
            Debug.LogWarning("[CardManager] AddCardToDiscard failed: card is null");
            return;
        }

        _discard.Add(newCard);

        Debug.Log($"[CardManager] Added new card '{newCard.name}' to Discard pile.");

        // 덱/버린덱 수량 변화 알림 (UI 갱신용)
        OnDeckChanged?.Invoke();
    }

    #endregion

    #region Internal Helpers

    private void InitializeDeckFromBaseSet()
    {
        _deck.Clear();
        _discard.Clear();
        _hand.Clear();

        if (baseBlockSet == null || baseBlockSet.Length == 0)
        {
            Debug.LogWarning("[CardManager] baseBlockSet is empty. Deck will be empty.");
            return;
        }

        // duplicate each base type duplicatePerType times
        foreach (var b in baseBlockSet)
        {
            for (int i = 0; i < duplicatePerType; i++)
            {
                _deck.Add(b);
            }
        }

        ShuffleDeck();
        OnDeckChanged?.Invoke();
    }

    private void ShuffleDeck()
    {
        int n = _deck.Count;
        for (int i = n - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            var tmp = _deck[i];
            _deck[i] = _deck[j];
            _deck[j] = tmp;
        }
        OnDeckRefilled?.Invoke();
        OnDeckChanged?.Invoke();
    }

    private void FillInitialHand()
    {
        DrawToHandIgnoringLimit(handSize);
        NotifyHandChanged();
    }

    /// <summary>
    /// handSize 제한을 무시하고 지정된 수만큼 드로우 (보너스 드로우용)
    /// </summary>
    private void DrawToHandIgnoringLimit(int count)
    {
        for (int i = 0; i < count; i++)
        {
            DrawSingle();
        }
    }

    private void DrawSingle()
    {
        if (_deck.Count == 0)
        {
            // reshuffle discard into deck
            if (_discard.Count == 0)
            {
                Debug.Log("[CardManager] Deck and discard empty; cannot draw.");
                return;
            }
            _deck.AddRange(_discard);
            _discard.Clear();
            ShuffleDeck();
            Debug.Log("[CardManager] Deck rebuilt from discard and shuffled.");
        }

        if (_deck.Count == 0) return;

        var card = _deck[_deck.Count - 1];
        _deck.RemoveAt(_deck.Count - 1);
        _hand.Add(card);
    }

    private void NotifyHandChanged()
    {
        try
        {
            OnHandChanged?.Invoke(new List<BlockSO>(_hand));
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CardManager] OnHandChanged handler threw: {ex}");
        }
    }

    #endregion
}