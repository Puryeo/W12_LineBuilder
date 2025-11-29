using System;
using UnityEngine;

public static class GameEvents
{
    public static event Action<object[]> OnLineCleared;
    public static event Action<DamageBreakdown> OnDamageCalculationResolved;
    public static event Action<int, int> OnShieldChanged; // (totalShield, remainingTurns)

    // 새 이벤트: 플레이어 체력 변경 (current, max)
    public static event Action<int, int> OnPlayerHealthChanged;

    public static void RaiseOnLineCleared(object[] payload, string origin)
    {
        try
        {
            Debug.Log($"[GameEvents] RaiseOnLineCleared Origin:{origin} Time:{Time.realtimeSinceStartup:F3}s PayloadType:{payload?.GetType().Name}", null);
            OnLineCleared?.Invoke(payload);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameEvents] Exception in RaiseOnLineCleared: {ex}");
        }
    }

    public static void RaiseOnDamageCalculationResolved(DamageBreakdown breakdown, string origin)
    {
        try
        {
            Debug.Log($"[GameEvents] RaiseOnDamageCalculationResolved Origin:{origin} Time:{Time.realtimeSinceStartup:F3}s FinalDamage:{breakdown?.finalDamage}", null);
            OnDamageCalculationResolved?.Invoke(breakdown);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameEvents] Exception in RaiseOnDamageCalculationResolved: {ex}");
        }
    }

    public static void RaiseOnShieldChanged(int totalShield, int remainingTurns, string origin)
    {
        try
        {
            Debug.Log($"[GameEvents] RaiseOnShieldChanged Origin:{origin} Time:{Time.realtimeSinceStartup:F3}s Shield:{totalShield} RemainingTurns:{remainingTurns}", null);
            OnShieldChanged?.Invoke(totalShield, remainingTurns);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameEvents] Exception in RaiseOnShieldChanged: {ex}");
        }
    }

    // 새: 플레이어 체력 변경 안전 발행
    public static void RaiseOnPlayerHealthChanged(int current, int max, string origin)
    {
        try
        {
            Debug.Log($"[GameEvents] RaiseOnPlayerHealthChanged Origin:{origin} Time:{Time.realtimeSinceStartup:F3}s Current:{current} Max:{max}", null);
            OnPlayerHealthChanged?.Invoke(current, max);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameEvents] Exception in RaiseOnPlayerHealthChanged: {ex}");
        }
    }
}