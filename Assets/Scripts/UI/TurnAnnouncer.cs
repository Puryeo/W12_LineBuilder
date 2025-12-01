using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// 턴 알림 프리팹에 붙는 스크립트
/// 일정 시간 표시 후 페이드아웃되며 자동 파괴됩니다.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class TurnAnnouncer : MonoBehaviour
{
    [Header("References")]
    private TMP_Text text;
    private CanvasGroup canvasGroup;

    [Header("Settings")]
    [Tooltip("텍스트 완전 표시 시간 (초)")]
    public float displayDuration = 1.0f;

    [Tooltip("페이드아웃 지속 시간 (초)")]
    public float fadeDuration = 0.5f;

    [Tooltip("초기 스케일 (펀치 효과용)")]
    public float initialScale = 1.2f;

    [Tooltip("스케일 애니메이션 시간")]
    public float scaleAnimDuration = 0.3f;

    [Header("Debug")]
    [Tooltip("디버그 로그 표시 여부 (런타임에 TurnAnnouncerManager에서 설정됨)")]
    [HideInInspector]
    public bool enableDebugLogs = false;

    private void Awake()
    {
        text = GetComponentInChildren<TMP_Text>();
        canvasGroup = GetComponent<CanvasGroup>();

        if (text == null)
        {
            Debug.LogError("[TurnAnnouncer] TMP_Text component not found in children!", this);
        }

        if (canvasGroup == null)
        {
            Debug.LogError("[TurnAnnouncer] CanvasGroup component not found!", this);
        }
    }

    /// <summary>
    /// 턴 알림 초기화 및 재생
    /// </summary>
    /// <param name="message">표시할 메시지</param>
    public void Initialize(string message)
    {
        if (enableDebugLogs) Debug.Log($"[TurnAnnouncer] Initialize() called with message: '{message}'");

        if (text != null)
        {
            text.text = message;
            if (enableDebugLogs) Debug.Log($"[TurnAnnouncer] Text set to: '{message}'");
        }
        else
        {
            Debug.LogError("[TurnAnnouncer] Text component is NULL!");
        }

        if (canvasGroup != null)
        {
            if (enableDebugLogs) Debug.Log("[TurnAnnouncer] CanvasGroup found, starting coroutine");
        }
        else
        {
            Debug.LogError("[TurnAnnouncer] CanvasGroup is NULL!");
        }

        StartCoroutine(AnnounceCoroutine());
    }

    /// <summary>
    /// 턴 알림 애니메이션 코루틴
    /// 1. 스케일 펀치 (크게 → 작게)
    /// 2. 완전 표시
    /// 3. 페이드아웃
    /// 4. 자동 파괴
    /// </summary>
    private IEnumerator AnnounceCoroutine()
    {
        if (enableDebugLogs) Debug.Log("[TurnAnnouncer] AnnounceCoroutine started");

        // 초기 상태
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
        transform.localScale = Vector3.one * initialScale;
        if (enableDebugLogs) Debug.Log($"[TurnAnnouncer] Initial scale set to {initialScale}");

        // 1. 스케일 펀치 애니메이션 (크게 시작 → 정상 크기)
        float elapsed = 0f;
        while (elapsed < scaleAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / scaleAnimDuration);
            // EaseOutBack 느낌
            float scale = Mathf.Lerp(initialScale, 1f, t);
            transform.localScale = Vector3.one * scale;
            yield return null;
        }
        transform.localScale = Vector3.one;
        if (enableDebugLogs) Debug.Log("[TurnAnnouncer] Scale animation complete");

        // 2. 완전 표시 (대기)
        if (enableDebugLogs) Debug.Log($"[TurnAnnouncer] Displaying for {displayDuration}s");
        yield return new WaitForSeconds(displayDuration);

        // 3. 페이드아웃
        if (enableDebugLogs) Debug.Log($"[TurnAnnouncer] Starting fadeout ({fadeDuration}s)");
        if (canvasGroup != null)
        {
            elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                canvasGroup.alpha = 1f - t;
                yield return null;
            }
            canvasGroup.alpha = 0f;
        }

        // 4. 파괴
        if (enableDebugLogs) Debug.Log("[TurnAnnouncer] Destroying announcer");
        Destroy(gameObject);
    }
}
