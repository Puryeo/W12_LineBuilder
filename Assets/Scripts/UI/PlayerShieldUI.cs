using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class PlayerShieldUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("원형 아이콘 Image (보통 Sprite 이미지)")]
    public Image shieldCircle;
    [Tooltip("원형 아이콘의 자식으로 두는 TextMeshPro - 실드 수치 표시")]
    public TextMeshProUGUI shieldTextTMP;
    [Tooltip("남은 턴을 표시할 TextMeshPro (선택).")]
    public TextMeshProUGUI remainingTurnsText;

    [Header("Animation")]
    [Tooltip("팝(획득 시) 최대 스케일 배수")]
    public float popScale = 1.35f;
    [Tooltip("스케일 보간 속도")]
    public float scaleLerpSpeed = 8f;

    [Header("Number Animation")]
    [Tooltip("숫자 애니메이션 지속시간(초). 0이면 즉시 반영")]
    public float numberAnimationDuration = 2f;
    [Tooltip("숫자 보간(애니메이션) 사용 여부")]
    public bool animateNumber = true;

    // internal visual state
    private int _targetShield = 0;

    private float _displayedShieldF = 0f;      // 현재 표시중인 실드(부동)
    private float _animationStartValue = 0f;   // 애니메이션 시작시 값
    private float _animationTimeLeft = 0f;     // 애니메이션 남은 시간 (초)

    private float _targetScale = 1f;
    private float _displayedScale = 1f;

    private void OnEnable()
    {
        GameEvents.OnShieldChanged += OnShieldChanged;

        if (PlayerShieldManager.Instance != null)
        {
            PlayerShieldManager.Instance.ClearAllShields();
        }
    }

    private void OnDisable()
    {
        GameEvents.OnShieldChanged -= OnShieldChanged;
    }

    private void Start()
    {
        UpdateVisualImmediate(0);
    }

    private void Update()
    {
        // scale lerp
        _displayedScale = Mathf.Lerp(_displayedScale, _targetScale, Mathf.Clamp01(Time.deltaTime * scaleLerpSpeed));
        if (shieldCircle != null)
            shieldCircle.rectTransform.localScale = Vector3.one * _displayedScale;

        // number animation: linear interpolation over numberAnimationDuration
        if (animateNumber && numberAnimationDuration > 0f && _animationTimeLeft > 0f)
        {
            float dt = Time.deltaTime;
            _animationTimeLeft = Mathf.Max(0f, _animationTimeLeft - dt);
            float progress = 1f - (_animationTimeLeft / numberAnimationDuration); // 0..1
            _displayedShieldF = Mathf.Lerp(_animationStartValue, _targetShield, Mathf.Clamp01(progress));
        }
        else
        {
            // 즉시 반영 또는 애니메이션 완료
            _displayedShieldF = _targetShield;
            _animationTimeLeft = 0f;
        }

        int shown = Mathf.RoundToInt(_displayedShieldF);
        if (shieldTextTMP != null)
            shieldTextTMP.text = shown > 0 ? shown.ToString() : string.Empty;

        // snap small residuals
        if (Mathf.Abs(_displayedScale - 1f) < 0.001f)
        {
            _displayedScale = 1f;
            _targetScale = 1f;
            if (shieldCircle != null)
                shieldCircle.rectTransform.localScale = Vector3.one;
        }
    }

    private void OnShieldChanged(int totalShield, int remainingTurns)
    {
        // update targets and restart numeric animation
        int newTarget = Mathf.Max(0, totalShield);

        // if target changed, restart animation
        if (newTarget != _targetShield)
        {
            _animationStartValue = _displayedShieldF;
            _targetShield = newTarget;
            if (animateNumber && numberAnimationDuration > 0f)
                _animationTimeLeft = numberAnimationDuration;
            else
                _animationTimeLeft = 0f;
        }

        if (_targetShield > 0)
        {
            if (shieldCircle != null && !shieldCircle.gameObject.activeSelf)
                shieldCircle.gameObject.SetActive(true);

            _targetScale = popScale;
        }
        else
        {
            if (shieldCircle != null)
                shieldCircle.gameObject.SetActive(false);
            _targetScale = 1f;
        }

        UpdateRemainingTurnsText(remainingTurns);
    }

    public void UpdateVisualImmediate(int totalShield, int remainingTurns = 0)
    {
        _targetShield = Mathf.Max(0, totalShield);
        _displayedShieldF = _targetShield;
        _animationStartValue = _displayedShieldF;
        _animationTimeLeft = 0f;
        _targetScale = 1f;
        _displayedScale = 1f;

            if (shieldCircle != null)
                shieldCircle.gameObject.SetActive(_targetShield > 0);

        if (shieldTextTMP != null)
            shieldTextTMP.text = _targetShield > 0 ? _targetShield.ToString() : string.Empty;

        UpdateRemainingTurnsText(remainingTurns);
    }

    private void UpdateRemainingTurnsText(int remainingTurns)
    {
        if (remainingTurnsText == null) return;
        remainingTurnsText.text = remainingTurns > 0 ? $"{remainingTurns} 턴" : string.Empty;
    }
}
