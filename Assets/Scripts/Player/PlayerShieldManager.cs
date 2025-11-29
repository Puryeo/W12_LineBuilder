using System;
using UnityEngine;

public class PlayerShieldManager : MonoBehaviour
{
    public static PlayerShieldManager Instance { get; private set; }

    [Header("Shield parameters")]
    [Tooltip("한 턴당 최대 획득량")]
    public int perTurnGainLimit = 8;

    [Tooltip("실드 총합 상한")]
    public int totalCap = 20;

    [Tooltip("실드 지속 턴(획득 시 리셋)")]
    public int defaultDurationTurns = 5;

    [Header("Debug")]
    public int currentShield { get; private set; } = 0;
    public int remainingTurns { get; private set; } = 0;

    // 한 턘 동안 획득한 실드량 합계 (디버그용 누적)
    private int _totalUsed = 0;
    // 한 턴 동안 이미 획득한 실드량 (per-turn 누적)
    private int _perTurnUsed = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    /// <summary>
    /// 지정한 amount 만큼 실드 획득 시도.
    /// 한 턴 누적 제한(perTurnGainLimit)과 totalCap을 적용함.
    /// origin: 로그/이벤트 출처 표기.
    /// 반환값: 실제로 증가한 실드량.
    /// </summary>
    // Replace AddShield method with this diagnostic version
public int AddShield(int amount, string origin = "PlayerShieldManager.AddShield")
{
    if (amount <= 0) return 0;

    // 진단 로그: 호출 스택과 타임스탬프 출력
    try
    {
        var st = new System.Diagnostics.StackTrace(1, true);
        Debug.Log($"[PlayerShieldManager] AddShield called origin={origin} amount={amount} Time={Time.realtimeSinceStartup:F3}s\n{st}", this);
    }
    catch (Exception ex)
    {
        Debug.LogWarning($"[PlayerShieldManager] StackTrace build failed: {ex}");
    }

    int remainingAllowedThisTurn = Mathf.Max(0, perTurnGainLimit - _perTurnUsed);
    int gain = Mathf.Min(amount, remainingAllowedThisTurn);
    if (gain <= 0)
    {
        Debug.Log($"[PlayerShieldManager] {origin} Per-turn limit reached ({_perTurnUsed}/{perTurnGainLimit}). No shield added.");
        return 0;
    }

    int before = currentShield;
    int applied = Mathf.Min(gain, totalCap - currentShield);
    if (applied <= 0)
    {
        Debug.Log($"[PlayerShieldManager] {origin} Total cap reached ({currentShield}/{totalCap}). No shield added.");
        return 0;
    }

    currentShield += applied;
    _totalUsed += applied;
    _perTurnUsed += applied;
    remainingTurns = defaultDurationTurns;

    GameEvents.RaiseOnShieldChanged(currentShield, remainingTurns, origin);
    Debug.Log($"[PlayerShieldManager] {origin} Gain:{applied} (perTurnUsed:{_perTurnUsed}/{perTurnGainLimit}) {before}->{currentShield} RemainTurns:{remainingTurns}");
    return applied;
}

    /// <summary>
    /// 데미지 흡수용: 실드에서 amount만큼 소모.
    /// 반환값: 실제로 흡수한 양(0..amount)
    /// origin: 로그/출처
    /// </summary>
    // Replace ConsumeShield method
public int ConsumeShield(int amount, string origin = "PlayerShieldManager.ConsumeShield")
{
    if (amount <= 0 || currentShield <= 0) return 0;

    // 진단 로그: 호출 스택
    try
    {
        var st = new System.Diagnostics.StackTrace(1, true);
        Debug.Log($"[PlayerShieldManager] ConsumeShield called origin={origin} amount={amount} Time={Time.realtimeSinceStartup:F3}s\n{st}", this);
    }
    catch (Exception ex)
    {
        Debug.LogWarning($"[PlayerShieldManager] StackTrace build failed: {ex}");
    }

    int before = currentShield;
    int applied = Mathf.Min(amount, currentShield);
    currentShield -= applied;

    if (currentShield <= 0)
    {
        currentShield = 0;
        remainingTurns = 0;
    }

    GameEvents.RaiseOnShieldChanged(currentShield, remainingTurns, origin);
    Debug.Log($"[PlayerShieldManager] {origin} Consumed:{applied} {before}->{currentShield} RemainTurns:{remainingTurns}");
    return applied;
}

    /// <summary>
    /// 턴 전환 시 호출: per-turn 누적 리셋 및 remainingTurns 감소 처리.
    /// 호출 위치: TurnManager.AdvanceTurn
    /// </summary>
    public void OnNewTurn(string origin = "PlayerShieldManager.OnNewTurn")
    {
        // 리셋: 한 턴 동안 획득한 양을 0으로 초기화하여 다음 턴에 다시 획득 가능
        _perTurnUsed = 0;

        // remainingTurns 감소 처리
        if (remainingTurns > 0)
        {
            remainingTurns--;
            if (remainingTurns <= 0)
            {
                currentShield = 0;
                remainingTurns = 0;
            }
        }

        GameEvents.RaiseOnShieldChanged(currentShield, remainingTurns, origin);
        Debug.Log($"[PlayerShieldManager] {origin} NewTurn -> Shield:{currentShield} RemainingTurns:{remainingTurns} PerTurnUsed reset.");
    }

    public void ClearAllShields(string origin = "PlayerShieldManager.ClearAllShields")
    {
        currentShield = 0;
        remainingTurns = 0;
        _perTurnUsed = 0;
        _totalUsed = 0;

        // UI 갱신 알림
        GameEvents.RaiseOnShieldChanged(currentShield, remainingTurns, origin);

        Debug.Log($"[PlayerShieldManager] {origin} All shields cleared and reset.");
    }

    // 전체 사용량 (디버그 전용)
    public int GetTotalUsed() => _totalUsed;
    // 테스트용: 현재 per-turn 누적량 조회 (디버그 전용)
    public int GetPerTurnUsed() => _perTurnUsed;
}