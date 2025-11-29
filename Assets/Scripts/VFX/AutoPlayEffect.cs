using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 이펙트 프리팹에 붙여 자동으로 애니메이션/파티클을 재생하고 완료 후 파괴
/// 완료 시점을 추적하여 이벤트로 알림
/// </summary>
public class AutoPlayEffect : MonoBehaviour
{
    [Header("Effect Settings")]
    [Tooltip("이펙트 지속 시간 (초) - 최대 대기 시간")]
    public float duration = 1.0f;

    [Tooltip("Animator가 있으면 재생할 트리거 이름")]
    public string animatorTrigger = "Play";

    [Header("Components (Optional)")]
    [Tooltip("자동 재생할 Animator")]
    public Animator animator;

    [Tooltip("자동 재생할 ParticleSystem")]
    public ParticleSystem particles;

    /// <summary>
    /// 이펙트 완료 시 발생하는 이벤트
    /// </summary>
    public event Action OnEffectComplete;

    private bool _isComplete = false;

    private void Start()
    {
        // 컴포넌트 자동 찾기
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (particles == null)
        {
            particles = GetComponentInChildren<ParticleSystem>();
        }

        // Animator 재생
        if (animator != null && !string.IsNullOrEmpty(animatorTrigger))
        {
            animator.SetTrigger(animatorTrigger);
            Debug.Log($"[AutoPlayEffect] Animator trigger '{animatorTrigger}' activated on {gameObject.name}");
        }

        // ParticleSystem 재생
        if (particles != null)
        {
            particles.Play();
            Debug.Log($"[AutoPlayEffect] ParticleSystem started on {gameObject.name}");
        }

        // 완료 추적 코루틴 시작
        StartCoroutine(TrackCompletionRoutine());
    }

    private IEnumerator TrackCompletionRoutine()
    {
        float startTime = Time.time;
        bool animatorComplete = false;
        bool particlesComplete = false;

        while (Time.time - startTime < duration)
        {
            // Animator 상태 체크
            if (animator != null)
            {
                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.normalizedTime >= 1.0f && !animator.IsInTransition(0))
                {
                    animatorComplete = true;
                }
            }
            else
            {
                animatorComplete = true; // Animator 없으면 완료로 간주
            }

            // ParticleSystem 체크
            if (particles != null)
            {
                if (!particles.IsAlive())
                {
                    particlesComplete = true;
                }
            }
            else
            {
                particlesComplete = true; // ParticleSystem 없으면 완료로 간주
            }

            // 둘 다 완료되면 종료
            if (animatorComplete && particlesComplete)
            {
                Debug.Log($"[AutoPlayEffect] Effect completed early on {gameObject.name} ({Time.time - startTime:F2}s / {duration}s)");
                break;
            }

            yield return null;
        }

        // 완료 처리
        _isComplete = true;

        try
        {
            OnEffectComplete?.Invoke();
            Debug.Log($"[AutoPlayEffect] OnEffectComplete invoked for {gameObject.name}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AutoPlayEffect] Exception in OnEffectComplete: {ex}");
        }

        // 오브젝트 파괴
        Destroy(gameObject);
    }

    /// <summary>
    /// 외부에서 완료 여부 확인 (디버그용)
    /// </summary>
    public bool IsComplete()
    {
        return _isComplete;
    }
}
