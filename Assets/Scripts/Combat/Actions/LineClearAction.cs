using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 라인 클리어 액션
/// GridManager의 OnLinesCleared 이벤트 발생 시 생성되어 즉시 처리됩니다.
/// 5단계 구조:
/// 1. 라인 클리어 애니메이션 (그리드 이펙트)
/// 2. 데미지 계산
/// 3. 실제 데미지 적용 (TakeDamage 호출, delayDeath=true)
/// 4. 데미지 받은 몬스터들 피격 애니메이션 (병렬 재생)
/// 5. 사망 처리 (OnDied 이벤트 발생)
/// </summary>
public class LineClearAction : IPhaseAction
{
    private readonly GridManager.LineClearResult _result;

    public LineClearAction(GridManager.LineClearResult result)
    {
        _result = result;
    }

    /// <summary>
    /// 라인 클리어는 일반적으로 병렬 실행 가능 (ForceSequential = false)
    /// </summary>
    public bool ForceSequential => false;

    /// <summary>
    /// 라인 클리어 액션 실행 (5단계 구조)
    /// </summary>
    public IEnumerator Play(Action<float> reportDuration)
    {
        float totalTime = 0f;

        if (_result == null)
        {
            Debug.LogWarning("[LineClearAction] Result is null, skipping");
            reportDuration?.Invoke(0f);
            yield break;
        }

        int totalLines = _result.ClearedRows.Count + _result.ClearedCols.Count;
        Debug.Log($"[LineClearAction] Processing line clear: {totalLines} line(s)");

        if (CombatManager.Instance == null)
        {
            Debug.LogWarning("[LineClearAction] CombatManager.Instance is null");
            reportDuration?.Invoke(0f);
            yield break;
        }

        // ========== Phase 1: 라인 클리어 애니메이션 ==========
        float lineClearDuration = 0f;
        CombatManager.Instance.PlayLineClearAnimationHook(
            _result,
            duration => lineClearDuration = duration
        );

        if (lineClearDuration > 0f)
        {
            Debug.Log($"[LineClearAction] Phase 1: Line clear animation ({lineClearDuration:F2}s)");
            yield return new WaitForSeconds(lineClearDuration);
            totalTime += lineClearDuration;
        }

        // ========== Phase 2: 데미지 계산 ==========
        Debug.Log("[LineClearAction] Phase 2: Calculating damage");
        var breakdown = CombatManager.Instance.CalculateLineClearDamage(_result);

        if (breakdown == null)
        {
            Debug.LogWarning("[LineClearAction] Damage calculation failed");
            reportDuration?.Invoke(totalTime);
            yield break;
        }

        // ========== Phase 2.5: 데미지 추적 시작 ==========
        if (MonsterManager.Instance != null)
        {
            MonsterManager.Instance.StartDamageTracking();
        }

        // ========== Phase 3: 실제 데미지 적용 (delayDeath=true) ==========
        Debug.Log("[LineClearAction] Phase 3: Applying damage (delayDeath=true)");
        CombatManager.Instance.ApplyCalculatedDamage(breakdown, _result, delayDeath: true);

        // ========== Phase 3.5: 데미지 받은 몬스터 목록 수집 ==========
        var damagedMonsters = MonsterManager.Instance?.GetDamagedMonsters();

        // ========== Phase 4: 점멸 효과 대기 (TakeDamage에서 자동 재생됨) ==========
        // 주의: 점멸 효과는 Monster.TakeDamage()에서 MonsterUI.PlayFlashEffect()로 자동 재생됩니다.
        // 회전 애니메이션(PlayHitFeedback)은 더 이상 사용하지 않습니다.
        if (damagedMonsters != null && damagedMonsters.Count > 0)
        {
            float maxDuration = 0f;

            // 점멸 효과 지속 시간 계산
            foreach (var (monster, duration) in damagedMonsters)
            {
                if (monster != null && monster.monsterUI != null)
                {
                    // MonsterUI의 flashDuration 사용
                    float flashDuration = monster.monsterUI.flashDuration;
                    maxDuration = UnityEngine.Mathf.Max(maxDuration, flashDuration);
                    Debug.Log($"[LineClearAction] Phase 4: {monster.monsterName} flash effect duration: {flashDuration:F2}s");
                }
            }

            // 가장 긴 점멸 효과만큼 대기
            if (maxDuration > 0f)
            {
                Debug.Log($"[LineClearAction] Phase 4: Waiting for flash effects ({maxDuration:F2}s)");
                yield return new WaitForSeconds(maxDuration);
                totalTime += maxDuration;
            }
        }

        // ========== Phase 5: 지연된 사망 처리 ==========
        if (damagedMonsters != null && damagedMonsters.Count > 0)
        {
            Debug.Log("[LineClearAction] Phase 5: Processing delayed deaths");
            foreach (var (monster, _) in damagedMonsters)
            {
                if (monster != null)
                {
                    monster.ProcessDelayedDeath();
                }
            }
        }

        // 총 시간 보고
        reportDuration?.Invoke(totalTime);
        Debug.Log($"[LineClearAction] Line clear completed in {totalTime:F2}s (damaged monsters: {damagedMonsters?.Count ?? 0})");
    }
}
