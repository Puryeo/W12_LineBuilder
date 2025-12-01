using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class Monster : MonoBehaviour
{
    public string monsterName = "Enemy";
    public int maxHP = 100;
    public MonsterUI monsterUI;
    public int monsterIdx;
    [Tooltip("음수 또는 0이면 maxHP로 시작")]
    public int startingHP = -1;

    [Header("Hit Feedback Settings")]
    [Tooltip("피격 애니메이션 지속 시간 (초)")]
    public float hitDuration = 1.0f;
    [Tooltip("피격 시 좌우 회전 속도 (높을수록 빠름)")]
    public float rotationSpeed = 10f;
    [Tooltip("피격 시 최대 회전 각도")]
    public float rotationAmount = 15f;
    [Tooltip("피격 시 위로 올라가는 최대 높이")]
    public float floatHeight = 0.3f;

    [Header("Attack Animation Settings")]
    [Tooltip("공격 시 왼쪽으로 회전할 각도 (음수, 예: -15)")]
    public float attackRotationAngle = -15f;
    [Tooltip("공격 애니메이션 총 지속 시간 (회전 → 복귀)")]
    public float attackAnimationDuration = 0.5f;

    [NonSerialized] public int currentHP;

    public event Action<int, int> OnHealthChanged; // (current, max)
    public event Action<Monster> OnDied;

    private bool _isDead = false;
    public bool IsDead => _isDead;

    // 코루틴 안전성 메커니즘
    private Coroutine _hitFeedbackCoroutine = null;
    private Coroutine _attackAnimationCoroutine = null;

    private void Awake()
    {
        currentHP = (startingHP <= 0) ? maxHP : Mathf.Clamp(startingHP, 0, maxHP);

        // 변경: 먼저 MonsterManager에 등록하여 실제 할당된 인덱스를 얻습니다.
        if (MonsterManager.Instance != null)
        {
            int assignedIndex = MonsterManager.Instance.RegisterMonster(this);
            monsterIdx = assignedIndex;
        }
        else
        {
            // MonsterManager가 없으면 기존 monsterIdx 유지
        }

        // MonsterUI는 실제 할당된 인덱스로 초기화
        monsterUI.Initialize(this, monsterIdx);

        OnHealthChanged?.Invoke(currentHP, maxHP);
    }

    private void OnDisable()
    {
        // 코루틴 정리
        if (_hitFeedbackCoroutine != null)
        {
            StopCoroutine(_hitFeedbackCoroutine);
            _hitFeedbackCoroutine = null;
        }

        if (_attackAnimationCoroutine != null)
        {
            StopCoroutine(_attackAnimationCoroutine);
            _attackAnimationCoroutine = null;
        }

        MonsterManager.Instance?.UnregisterMonster(this);
    }

    public void TakeDamage(int amount, string origin = "Monster.TakeDamage", bool delayDeath = false)
    {
        if (amount <= 0) return;
        if (_isDead) return;

        int before = currentHP;
        currentHP = Mathf.Clamp(currentHP - amount, 0, maxHP);
        Debug.Log($"[Monster] {monsterName} takes {amount} dmg -> {before} -> {currentHP}/{maxHP} ({origin}, delayDeath={delayDeath})");
        OnHealthChanged?.Invoke(currentHP, maxHP);

        // 피격 시 UI 점멸 효과 재생
        if (amount > 0 && monsterUI != null)
        {
            monsterUI.PlayFlashEffect();
        }

        if (currentHP == 0 && !_isDead)
        {
            _isDead = true;

            if (delayDeath)
            {
                // 애니메이션을 위해 사망 이벤트 지연
                Debug.Log($"[Monster] {monsterName} death delayed for animation");

                // MonsterManager에 데미지 받은 몬스터로 등록
                if (MonsterManager.Instance != null)
                {
                    MonsterManager.Instance.TrackDamagedMonster(this, hitDuration);
                }
            }
            else
            {
                // 즉시 사망 처리
                OnDied?.Invoke(this);
            }
        }
        else if (amount > 0 && delayDeath)
        {
            // HP가 0이 아니어도 데미지를 받았으면 추적 (애니메이션용)
            if (MonsterManager.Instance != null)
            {
                MonsterManager.Instance.TrackDamagedMonster(this, hitDuration);
            }
        }
    }

    /// <summary>
    /// 지연된 사망 처리 (애니메이션 완료 후 호출)
    /// </summary>
    public void ProcessDelayedDeath()
    {
        if (_isDead)
        {
            Debug.Log($"[Monster] {monsterName} processing delayed death");
            OnDied?.Invoke(this);
        }
    }

    /// <summary>
    /// 피격 피드백 애니메이션 재생 (Transform 기반)
    /// - 좌우 회전 (Sin 기반 진동)
    /// - 위로 올라갔다가 내려오는 포물선 운동
    /// </summary>
    /// <param name="duration">애니메이션 지속 시간 (초)</param>
    /// <param name="onComplete">애니메이션 완료 시 호출될 콜백</param>
    public void PlayHitFeedback(float duration, Action onComplete)
    {
        // 안전성 메커니즘 1: 중복 방지 - 기존 코루틴 정리
        if (_hitFeedbackCoroutine != null)
        {
            StopCoroutine(_hitFeedbackCoroutine);
            _hitFeedbackCoroutine = null;
        }

        // 안전성 메커니즘 2: 사망 체크
        if (_isDead)
        {
            Debug.LogWarning($"[Monster] {monsterName} is dead, skipping hit feedback");
            onComplete?.Invoke();
            return;
        }

        // 안전성 메커니즘 4: Transform null 체크
        if (transform == null)
        {
            Debug.LogWarning($"[Monster] {monsterName} transform is null, skipping hit feedback");
            onComplete?.Invoke();
            return;
        }

        _hitFeedbackCoroutine = StartCoroutine(HitFeedbackCoroutine(duration, onComplete));
    }

    /// <summary>
    /// 피격 애니메이션 재생 후 데미지 적용
    /// 애니메이션이 끝나면 자동으로 TakeDamage()를 호출합니다.
    /// 각 몬스터가 자신의 애니메이션 시간을 결정하고, 완료 시 실제 걸린 시간을 보고합니다.
    /// </summary>
    /// <param name="damage">적용할 데미지</param>
    /// <param name="reportDuration">실제 걸린 시간을 보고하는 콜백</param>
    public void PlayHitFeedbackWithDamage(int damage, Action<float> reportDuration)
    {
        // 안전성 메커니즘 1: 중복 방지 - 기존 코루틴 정리
        if (_hitFeedbackCoroutine != null)
        {
            StopCoroutine(_hitFeedbackCoroutine);
            _hitFeedbackCoroutine = null;
        }

        // 안전성 메커니즘 2: 사망 체크
        if (_isDead)
        {
            Debug.LogWarning($"[Monster] {monsterName} is dead, skipping hit feedback with damage");
            reportDuration?.Invoke(0f);
            return;
        }

        // 안전성 메커니즘 4: Transform null 체크
        if (transform == null)
        {
            Debug.LogWarning($"[Monster] {monsterName} transform is null, skipping hit feedback with damage");
            reportDuration?.Invoke(0f);
            return;
        }

        _hitFeedbackCoroutine = StartCoroutine(HitFeedbackWithDamageCoroutine(damage, reportDuration));
    }

    /// <summary>
    /// 피격 피드백 코루틴
    /// 안전성 메커니즘:
    /// 1. 중복 방지 (PlayHitFeedback에서 처리)
    /// 2. 사망 체크 (코루틴 내부에서 매 프레임 확인)
    /// 3. OnDisable 정리 (OnDisable에서 StopCoroutine)
    /// 4. Transform null 체크
    /// 5. 코루틴 참조 정리 (완료 시 null로 설정)
    /// </summary>
    private IEnumerator HitFeedbackCoroutine(float duration, Action onComplete)
    {
        float elapsed = 0f;
        Vector3 originalPosition = transform.localPosition;
        Quaternion originalRotation = transform.localRotation;

        while (elapsed < duration)
        {
            // 안전성 메커니즘 2: 코루틴 내부 사망 체크
            if (_isDead)
            {
                Debug.Log($"[Monster] {monsterName} died during hit feedback, stopping animation");
                break;
            }

            // 안전성 메커니즘 4: Transform null 체크
            if (transform == null)
            {
                Debug.LogWarning($"[Monster] {monsterName} transform became null during hit feedback");
                break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // 좌우 회전 (Sin 기반 진동)
            float rotationAngle = Mathf.Sin(elapsed * rotationSpeed) * rotationAmount;
            transform.localRotation = originalRotation * Quaternion.Euler(0f, 0f, rotationAngle);

            // 위로 올라갔다가 내려오는 포물선 운동 (Sin을 이용한 부드러운 포물선)
            float verticalOffset = Mathf.Sin(t * Mathf.PI) * floatHeight;
            transform.localPosition = originalPosition + new Vector3(0f, verticalOffset, 0f);

            yield return null;
        }

        // 원래 상태로 복원
        if (transform != null)
        {
            transform.localPosition = originalPosition;
            transform.localRotation = originalRotation;
        }

        // 안전성 메커니즘 5: 코루틴 참조 정리
        _hitFeedbackCoroutine = null;

        // 완료 콜백 호출
        onComplete?.Invoke();
    }

    /// <summary>
    /// 피격 피드백 애니메이션 + 데미지 적용 코루틴
    /// 안전성 메커니즘:
    /// 1. 중복 방지 (PlayHitFeedbackWithDamage에서 처리)
    /// 2. 사망 체크 (코루틴 내부에서 매 프레임 확인)
    /// 3. OnDisable 정리 (OnDisable에서 StopCoroutine)
    /// 4. Transform null 체크
    /// 5. 코루틴 참조 정리 (완료 시 null로 설정)
    /// </summary>
    private IEnumerator HitFeedbackWithDamageCoroutine(int damage, Action<float> reportDuration)
    {
        float startTime = Time.time;
        Vector3 originalPosition = transform.localPosition;
        Quaternion originalRotation = transform.localRotation;

        // 애니메이션 지속 시간 (인스펙터에서 설정 가능)
        float duration = hitDuration;
        float elapsed = 0f;

        // 애니메이션 재생
        while (elapsed < duration)
        {
            // 안전성 메커니즘 2: 코루틴 내부 사망 체크
            if (_isDead)
            {
                Debug.Log($"[Monster] {monsterName} died during hit feedback, stopping animation");
                break;
            }

            // 안전성 메커니즘 4: Transform null 체크
            if (transform == null)
            {
                Debug.LogWarning($"[Monster] {monsterName} transform became null during hit feedback");
                break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // 좌우 회전 (Sin 기반 진동)
            float rotationAngle = Mathf.Sin(elapsed * rotationSpeed) * rotationAmount;
            transform.localRotation = originalRotation * Quaternion.Euler(0f, 0f, rotationAngle);

            // 위로 올라갔다가 내려오는 포물선 운동
            float verticalOffset = Mathf.Sin(t * Mathf.PI) * floatHeight;
            transform.localPosition = originalPosition + new Vector3(0f, verticalOffset, 0f);

            yield return null;
        }

        // 원래 상태로 복원
        if (transform != null)
        {
            transform.localPosition = originalPosition;
            transform.localRotation = originalRotation;
        }

        // 애니메이션 끝나면 데미지 적용
        if (!_isDead && damage > 0)
        {
            TakeDamage(damage, "Monster.HitFeedback");
            Debug.Log($"[Monster] {monsterName} applied {damage} damage after hit animation");
        }

        // 안전성 메커니즘 5: 코루틴 참조 정리
        _hitFeedbackCoroutine = null;

        // 실제 걸린 시간 보고
        float actualDuration = Time.time - startTime;
        reportDuration?.Invoke(actualDuration);
        Debug.Log($"[Monster] {monsterName} hit feedback completed in {actualDuration:F2}s");
    }

    /// <summary>
    /// 공격 애니메이션 재생 (왼쪽 회전 후 복귀)
    /// </summary>
    public void PlayAttackAnimation()
    {
        // 중복 방지
        if (_attackAnimationCoroutine != null)
        {
            StopCoroutine(_attackAnimationCoroutine);
            _attackAnimationCoroutine = null;
        }

        // 사망 체크
        if (_isDead)
        {
            Debug.LogWarning($"[Monster] {monsterName} is dead, skipping attack animation");
            return;
        }

        // Transform null 체크
        if (transform == null)
        {
            Debug.LogWarning($"[Monster] {monsterName} transform is null, skipping attack animation");
            return;
        }

        _attackAnimationCoroutine = StartCoroutine(AttackAnimationCoroutine());
    }

    /// <summary>
    /// 공격 애니메이션 코루틴 - 왼쪽으로 회전 후 원위치 복귀
    /// </summary>
    private IEnumerator AttackAnimationCoroutine()
    {
        Quaternion originalRotation = transform.localRotation;
        float elapsed = 0f;
        float halfDuration = attackAnimationDuration * 0.5f;

        // 1단계: 왼쪽으로 회전 (0 → attackRotationAngle)
        while (elapsed < halfDuration)
        {
            if (_isDead || transform == null)
            {
                break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);

            // EaseOutQuad를 사용하여 부드러운 회전
            float smoothT = 1f - (1f - t) * (1f - t);
            float currentAngle = Mathf.Lerp(0f, attackRotationAngle, smoothT);

            transform.localRotation = originalRotation * Quaternion.Euler(0f, 0f, currentAngle);

            yield return null;
        }

        // 2단계: 원위치로 복귀 (attackRotationAngle → 0)
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            if (_isDead || transform == null)
            {
                break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);

            // EaseInQuad를 사용하여 부드러운 복귀
            float smoothT = t * t;
            float currentAngle = Mathf.Lerp(attackRotationAngle, 0f, smoothT);

            transform.localRotation = originalRotation * Quaternion.Euler(0f, 0f, currentAngle);

            yield return null;
        }

        // 원래 상태로 복원
        if (transform != null)
        {
            transform.localRotation = originalRotation;
        }

        // 코루틴 참조 정리
        _attackAnimationCoroutine = null;
    }
}