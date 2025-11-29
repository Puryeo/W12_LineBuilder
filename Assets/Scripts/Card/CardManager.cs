using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CardManager:
/// - 덱(deck), 버린덱(discard), 손패(hand) 관리
/// - 덱 초기화(기본 블록 SO 배열을 Inspector에서 지정)
/// - Draw, UseCardAt(index), Rest 로직
/// - 폭탄 해제(CombatManager.OnBombDefused) 구독 시 즉시 드로우 처리 (한 턴 최대 hand 4 허용)
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
    public int maxTemporaryHandSize = 4; // 폭탄 해제시 일시적으로 허용되는 최대 손패

    // 내부 상태
    private List<BlockSO> _deck = new List<BlockSO>();
    private List<BlockSO> _discard = new List<BlockSO>();
    private List<BlockSO> _hand = new List<BlockSO>();

    // 이벤트: UI/다른 매니저가 구독
    public event Action<List<BlockSO>> OnHandChanged;
    public event Action OnDeckRefilled;
    public event Action<BlockSO> OnCardUsed;
    public event Action OnDeckChanged;

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

        // CombatManager의 OnBombDefused 구독 (있을 경우)
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnBombDefused += HandleBombDefused;
            Debug.Log("[CardManager] Subscribed to CombatManager.OnBombDefused");
        }
        else
            Debug.LogWarning("[CardManager] CombatManager.Instance is null on Start. Will attempt subscribe on Enable.");

        Debug.Log($"[CardManager] Deck={_deck.Count} Discard={_discard.Count} Hand={_hand.Count}");
    }

    private void OnEnable()
    {
        Debug.Log("[CardManager] OnEnable");
        if (CombatManager.Instance != null)
            CombatManager.Instance.OnBombDefused += HandleBombDefused;
    }

    private void OnDisable()
    {
        Debug.Log("[CardManager] OnDisable");
        if (CombatManager.Instance != null)
            CombatManager.Instance.OnBombDefused -= HandleBombDefused;
    }

    #region Public API

    public IReadOnlyList<BlockSO> GetHand() => _hand.AsReadOnly();

    public IReadOnlyList<BlockSO> GetDeck() => _deck.AsReadOnly();

    public IReadOnlyList<BlockSO> GetDiscard() => _discard.AsReadOnly();

    /// <summary>
    /// 카드 사용: 해당 인덱스의 카드를 discard로 보내고 손패를 왼쪽으로 당긴 뒤 덱에서 1장 드로우.
    /// 인덱스는 0..handSize-1 (0 = 가장 오래된, PRD 기준)
    /// </summary>
    public bool UseCardAt(int index)
    {
        if (index < 0 || index >= _hand.Count)
        {
            Debug.LogWarning($"[CardManager] UseCardAt invalid index {index}");
            return false;
        }
        var used = _hand[index];
        _discard.Add(used);
        OnCardUsed?.Invoke(used);

        // remove at index and shift left automatically due to List.RemoveAt
        _hand.RemoveAt(index);

        // draw to fill up to normal handSize (not temporary overflow)
        DrawToHand(1);

        Debug.Log($"[CardManager] UseCardAt({index}) used={used.name} handNow={_hand.Count} deckNow={_deck.Count} discardNow={_discard.Count}");
        NotifyHandChanged();
        return true;
    }

    /// <summary>
    /// 카드 참조로 사용: 현재 손패에서 해당 카드 객체를 찾아 사용합니다.
    /// 인덱스 불일치(이벤트로 손패가 변경된 경우)를 방지하기 위해 사용.
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

        Debug.Log($"[CardManager] UseCardByReference -> index {idx} card {card.name}");
        return UseCardAt(idx);
    }

    /// <summary>
    /// Rest: 가장 왼쪽(0번) 카드 버림 -> shift -> 덱에서 1장 드로우
    /// 반환값: true if rest succeeded (hand had at least 1 card), false if nothing to discard
    /// </summary>
    public bool Rest()
    {
        if (_hand.Count == 0)
        {
            // Nothing to discard, but per rules Rest still consumes turn; return false to indicate no card removed.
            Debug.Log("[CardManager] Rest called but hand empty.");
            return false;
        }

        var discarded = _hand[0];
        _discard.Add(discarded);
        _hand.RemoveAt(0);

        DrawToHand(1);

        Debug.Log($"[CardManager] Rest -> discarded {discarded.name} handNow={_hand.Count}");
        NotifyHandChanged();
        return true;
    }

    /// <summary>
    /// 일반 드로우: 덱에서 n장 뽑아 손패에 추가(최대 handSize).
    /// </summary>
    public void Draw(int n = 1)
    {
        for (int i = 0; i < n; i++)
        {
            if (_hand.Count >= handSize) break;
            DrawSingle();
        }
        Debug.Log($"[CardManager] Draw({n}) -> handNow={_hand.Count} deckNow={_deck.Count}");
        NotifyHandChanged();
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
        DrawToHand(handSize);
        NotifyHandChanged();
    }

    private void DrawToHand(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (_hand.Count >= handSize) break;
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

    #region Bomb Defuse Handling

    /// <summary>
    /// CombatManager.OnBombDefused 구독 핸들러
    /// 즉시 드로우: PRD에 따라 폭탄 1개당 카드 1장 즉시 드로우, 일시적 hand 최대 허용은 maxTemporaryHandSize
    /// </summary>
    private void HandleBombDefused(int defusedCount)
    {
        Debug.Log($"[CardManager] HandleBombDefused called defusedCount={defusedCount}");
        if (defusedCount <= 0) return;
        int drawn = 0;
        for (int i = 0; i < defusedCount; i++)
        {
            if (_hand.Count >= maxTemporaryHandSize) break;
            // draw one ignoring normal handSize cap
            if (_deck.Count == 0 && _discard.Count == 0)
            {
                Debug.Log("[CardManager] No cards to draw on bomb defuse.");
                break;
            }
            // ensure deck available
            if (_deck.Count == 0)
            {
                _deck.AddRange(_discard);
                _discard.Clear();
                ShuffleDeck();
            }

            var card = _deck[_deck.Count - 1];
            _deck.RemoveAt(_deck.Count - 1);
            _hand.Add(card);
            drawn++;
        }

        if (drawn > 0)
        {
            Debug.Log($"[CardManager] Bomb defuse drew {drawn} card(s). Hand size now {_hand.Count}.");
            NotifyHandChanged();
        }
    }

    #endregion
}