using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class CamShakeManager : MonoBehaviour
{
    public static CamShakeManager Instance { get; private set; }

    public enum ShakeStrength { Weak, Normal, Strong }

    [Header("Damage thresholds (inclusive)")]
    public int strongThreshold = 20;
    public int normalThreshold = 11; // 11..(strongThreshold-1) => Normal, 0..10 => Weak

    [Header("Weak profile")]
    public float weakDuration = 0.25f;
    public float weakAmplitude = 0.04f;
    public float weakFrequency = 20f;

    [Header("Normal profile")]
    public float normalDuration = 0.4f;
    public float normalAmplitude = 0.08f;
    public float normalFrequency = 18f;

    [Header("Strong profile")]
    public float strongDuration = 0.7f;
    public float strongAmplitude = 0.18f;
    public float strongFrequency = 12f;

    private Transform _camTransform;
    private Vector3 _originalLocalPos;
    private Coroutine _shakeCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        if (Camera.main != null)
        {
            _camTransform = Camera.main.transform;
            _originalLocalPos = _camTransform.localPosition;
        }
        else
        {
            Debug.LogWarning("[CamShakeManager] Camera.main is null. Shake will be no-op until a camera is available.");
        }
    }

    private void OnEnable()
    {
        // 안전: 카메라가 런타임에 생성/변경될 수 있으니 보장해둠
        if (_camTransform == null && Camera.main != null)
        {
            _camTransform = Camera.main.transform;
            _originalLocalPos = _camTransform.localPosition;
        }
    }

    public void ShakeByDamage(int damage)
    {
        if (damage >= strongThreshold) ShakeStrong();
        else if (damage >= normalThreshold) ShakeNormal();
        else ShakeWeak();
    }

    public void ShakeWeak() => StartShake(weakDuration, weakAmplitude, weakFrequency);
    public void ShakeNormal() => StartShake(normalDuration, normalAmplitude, normalFrequency);
    public void ShakeStrong() => StartShake(strongDuration, strongAmplitude, strongFrequency);

    public void StartShake(float duration, float amplitude, float frequency)
    {
        if (_camTransform == null) return;
        if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
        _shakeCoroutine = StartCoroutine(DoShake(duration, amplitude, frequency));
    }

    private IEnumerator DoShake(float duration, float amplitude, float frequency)
    {
        float elapsed = 0f;
        Vector3 origin = _originalLocalPos;
        // 안전: 카메라가 runtime에 변경될 경우 원위치 재설정
        if (_camTransform != null) origin = _camTransform.localPosition;

        while (elapsed < duration)
        {
            if (_camTransform == null) yield break;
            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / duration);
            float damper = 1f - ratio; // 점점 줄어듦

            float t = Time.time * frequency;
            float x = (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f * amplitude * damper;
            float y = (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f * amplitude * damper;

            _camTransform.localPosition = origin + new Vector3(x, y, 0f);
            yield return null;
        }

        if (_camTransform != null)
            _camTransform.localPosition = origin;

        _shakeCoroutine = null;
    }
}