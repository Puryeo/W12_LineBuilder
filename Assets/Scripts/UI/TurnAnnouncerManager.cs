using UnityEngine;

/// <summary>
/// 턴 알림을 관리하는 매니저
/// TurnManager의 이벤트를 구독하여 "내 턴", "적의 턴" 프리팹을 스폰합니다.
/// </summary>
public class TurnAnnouncerManager : MonoBehaviour
{
    [Header("Prefab Settings")]
    [Tooltip("턴 알림 프리팹 (TurnAnnouncer 스크립트 포함)")]
    public GameObject turnAnnouncerPrefab;

    [Tooltip("스폰할 Canvas 또는 부모 Transform (null이면 this.transform 사용)")]
    public Transform canvasParent;

    [Header("Messages")]
    [Tooltip("플레이어 턴 메시지")]
    public string playerTurnMessage = "내 턴";

    [Tooltip("적 턴 메시지")]
    public string enemyTurnMessage = "적의 턴";

    [Header("Debug")]
    [Tooltip("디버그 로그 표시 여부")]
    public bool enableDebugLogs = false;

    private void OnEnable()
    {
        if (enableDebugLogs) Debug.Log("[TurnAnnouncerManager] OnEnable called");

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnPlayerTurnStart += ShowPlayerTurn;
            TurnManager.Instance.OnEnemyTurnStart += ShowEnemyTurn;
            if (enableDebugLogs) Debug.Log("[TurnAnnouncerManager] Successfully subscribed to TurnManager events");
        }
        else
        {
            Debug.LogWarning("[TurnAnnouncerManager] TurnManager.Instance is null on OnEnable - will retry in Start");
        }
    }

    private void Start()
    {
        // OnEnable에서 구독 실패했을 경우 재시도
        if (TurnManager.Instance != null)
        {
            // 이미 구독되어 있을 수 있으므로 먼저 해제
            TurnManager.Instance.OnPlayerTurnStart -= ShowPlayerTurn;
            TurnManager.Instance.OnEnemyTurnStart -= ShowEnemyTurn;

            // 재구독
            TurnManager.Instance.OnPlayerTurnStart += ShowPlayerTurn;
            TurnManager.Instance.OnEnemyTurnStart += ShowEnemyTurn;
            if (enableDebugLogs) Debug.Log("[TurnAnnouncerManager] Subscribed to TurnManager events in Start");
        }
        else
        {
            Debug.LogError("[TurnAnnouncerManager] TurnManager.Instance is still null in Start!");
        }
    }

    private void OnDisable()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnPlayerTurnStart -= ShowPlayerTurn;
            TurnManager.Instance.OnEnemyTurnStart -= ShowEnemyTurn;
        }
    }

    private void ShowPlayerTurn()
    {
        if (enableDebugLogs) Debug.Log("[TurnAnnouncerManager] ShowPlayerTurn() called!");
        SpawnAnnouncer(playerTurnMessage);
    }

    private void ShowEnemyTurn()
    {
        if (enableDebugLogs) Debug.Log("[TurnAnnouncerManager] ShowEnemyTurn() called!");
        SpawnAnnouncer(enemyTurnMessage);
    }

    /// <summary>
    /// 턴 알림 프리팹 스폰
    /// </summary>
    private void SpawnAnnouncer(string message)
    {
        if (enableDebugLogs) Debug.Log($"[TurnAnnouncerManager] SpawnAnnouncer() called with message: '{message}'");

        if (turnAnnouncerPrefab == null)
        {
            Debug.LogError("[TurnAnnouncerManager] turnAnnouncerPrefab is NULL! Please assign it in the inspector.");
            return;
        }

        Transform parent = canvasParent != null ? canvasParent : transform;
        if (enableDebugLogs) Debug.Log($"[TurnAnnouncerManager] Parent: {parent.name}, Canvas Parent set: {canvasParent != null}");

        var announcer = Instantiate(turnAnnouncerPrefab, parent);
        if (enableDebugLogs) Debug.Log($"[TurnAnnouncerManager] Instantiated announcer: {announcer.name}");

        // 화면 정가운데 배치 (RectTransform 사용)
        var rectTransform = announcer.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;

            // 최상위 레이어로 설정 (다른 UI 위에 표시)
            rectTransform.SetAsLastSibling();
            if (enableDebugLogs) Debug.Log($"[TurnAnnouncerManager] RectTransform configured at position: {rectTransform.anchoredPosition}");
        }
        else
        {
            Debug.LogWarning("[TurnAnnouncerManager] RectTransform not found on announcer");
        }

        // 초기화 (디버그 로그 설정도 전달)
        var turnAnnouncerScript = announcer.GetComponent<TurnAnnouncer>();
        if (turnAnnouncerScript != null)
        {
            turnAnnouncerScript.enableDebugLogs = enableDebugLogs; // 디버그 로그 설정 전달
            turnAnnouncerScript.Initialize(message);
            if (enableDebugLogs) Debug.Log($"[TurnAnnouncerManager] Successfully spawned and initialized announcer: '{message}'");
        }
        else
        {
            Debug.LogError("[TurnAnnouncerManager] TurnAnnouncer component NOT FOUND on prefab!");
        }
    }
}
