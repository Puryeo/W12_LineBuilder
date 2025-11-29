using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class PlayerHealthUI : MonoBehaviour
{
    [Header("References")]
    public Image healthBarFill;           // 채워지는 Image (fillAmount 사용 권장)
    public TextMeshProUGUI healthText;    // "HP 80 / 100" 등
    public GameObject GameOverPanel;

    [Header("Initial")]
    public int initialMax = 100;
    public int initialCurrent = 100;

    [Header("Animation")]
    [Tooltip("보간 속도. 값이 클수록 빠르게 변함.")]
    public float lerpSpeed = 6f;
    [Tooltip("숫자도 부드럽게 변경할지 여부")]
    public bool animateNumeric = true;

    [Header("Options")]
    public bool useFillAmount = true;     // Image.type = Filled 사용 권장
    public string healthFormat = "HP {0} / {1}";

    private int _targetCurrent;
    private int _targetMax;

    private float _displayedFill = 1f;
    private float _displayedCurrentF = 0f;

    private void OnEnable()
    {
        GameEvents.OnPlayerHealthChanged += OnPlayerHealthChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerHealthChanged -= OnPlayerHealthChanged;
    }

    private void Start()
    {
        // 초기값 적용 (초기 표시가 0/100으로 보이는 문제 해결)
        _targetMax = Mathf.Max(1, initialMax);
        _targetCurrent = Mathf.Clamp(initialCurrent, 0, _targetMax);

        _displayedCurrentF = _targetCurrent;
        _displayedFill = _targetMax > 0 ? (float)_targetCurrent / _targetMax : 0f;

        ApplyImmediateUI(_targetCurrent, _targetMax);
    }

    // 이벤트로 체력 변경이 들어오면 목표값만 갱신
    public void OnPlayerHealthChanged(int current, int max)
    {
        _targetMax = Mathf.Max(1, max);
        _targetCurrent = Mathf.Clamp(current, 0, _targetMax);

        if(_targetCurrent <= 0)
        {
            // 게임 오버 처리
            if (GameOverPanel != null)
            {
                GameOverPanel.SetActive(true);
                GameFlowManager.Instance.currentRoundIndex = 0; // 라운드 초기화
            }
        }
    }

    private void Update()
    {
        // 보간된 값으로 UI 갱신
        float targetFill = _targetMax > 0 ? (float)_targetCurrent / _targetMax : 0f;
        _displayedFill = Mathf.Lerp(_displayedFill, targetFill, Mathf.Clamp01(Time.deltaTime * lerpSpeed));

        if (healthBarFill != null)
        {
            if (useFillAmount)
            {
                healthBarFill.type = Image.Type.Filled;
                healthBarFill.fillMethod = Image.FillMethod.Horizontal;
                healthBarFill.fillAmount = _displayedFill;
            }
            else
            {
                // width 방식(대체) - 유지하되 권장은 fillAmount
                var rt = healthBarFill.GetComponent<RectTransform>();
                if (rt != null && rt.parent != null)
                {
                    var parentRt = rt.parent.GetComponent<RectTransform>();
                    if (parentRt != null)
                    {
                        float w = parentRt.rect.width * _displayedFill;
                        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(0.01f, w));
                    }
                }
            }
        }

        if (animateNumeric)
        {
            _displayedCurrentF = Mathf.Lerp(_displayedCurrentF, _targetCurrent, Mathf.Clamp01(Time.deltaTime * lerpSpeed));
            int displayedInt = Mathf.RoundToInt(_displayedCurrentF);
            if (healthText != null)
                healthText.text = string.Format(healthFormat, displayedInt, _targetMax);
        }
        else
        {
            if (healthText != null)
                healthText.text = string.Format(healthFormat, _targetCurrent, _targetMax);
        }
    }

    private void ApplyImmediateUI(int current, int max)
    {
        if (healthText != null)
            healthText.text = string.Format(healthFormat, current, max);

        if (healthBarFill != null)
        {
            float f = max > 0 ? (float)current / max : 0f;
            if (useFillAmount)
            {
                healthBarFill.type = Image.Type.Filled;
                healthBarFill.fillMethod = Image.FillMethod.Horizontal;
                healthBarFill.fillAmount = f;
            }
            else
            {
                var rt = healthBarFill.GetComponent<RectTransform>();
                if (rt != null && rt.parent != null)
                {
                    var parentRt = rt.parent.GetComponent<RectTransform>();
                    if (parentRt != null)
                    {
                        float w = parentRt.rect.width * f;
                        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(0.01f, w));
                    }
                }
            }
        }
    }

    // 외부에서 즉시 값 세팅이 필요하면 호출
    public void SetHealthImmediate(int current, int max)
    {
        _targetMax = Mathf.Max(1, max);
        _targetCurrent = Mathf.Clamp(current, 0, _targetMax);
        _displayedCurrentF = _targetCurrent;
        _displayedFill = _targetMax > 0 ? (float)_targetCurrent / _targetMax : 0f;
        ApplyImmediateUI(_targetCurrent, _targetMax);
    }
}