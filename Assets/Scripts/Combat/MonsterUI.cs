using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class MonsterUI : MonoBehaviour
{
    [Header("UI References")]
    public Image hpFill;
    public TextMeshProUGUI hpText;
    public TextMeshProUGUI nameText;
    public Button selectButton;

    [Header("Pattern UI")]
    [Tooltip("몬스터 상단에 표시할 공격 아이콘")]
    public Image attackIconImage;
    [Tooltip("패턴의 남은 턴을 표시할 TMP 텍스트")]
    public TextMeshProUGUI turnsText;

    [Header("Behavior")]
    [Tooltip("이 컴포넌트를 몬스터 GameObject에 붙여두면 자동으로 같은 GameObject의 Monster를 찾아 바인드합니다.")]
    public bool autoBindToLocalMonster = true;

    private Monster _monster;
    private int _index = -1;

    private Coroutine _fadeCoroutine;
    private CanvasGroup _canvasGroup;

    public void Initialize(Monster m, int index)
    {
        BindToMonsterInternal(m, index);
    }

    // 외부에서 수동 바인드 호출용 (이름 유지)
    public void BindToMonster(Monster m, int index) => BindToMonsterInternal(m, index);

    // MonsterManager가 호출하는 메서드(인덱스 갱신용)
    public void SetIndex(int index) => _index = index;

    private void BindToMonsterInternal(Monster m, int index)
    {
        // 기존 바인드가 있으면 해제
        if (_monster != null)
        {
            _monster.OnHealthChanged -= OnMonsterHealthChanged;
            _monster.OnDied -= OnMonsterDied;
            if (MonsterManager.Instance != null) MonsterManager.Instance.OnSelectedMonsterChanged -= OnSelectedChanged;
            if (selectButton != null) selectButton.onClick.RemoveListener(OnSelectClicked);
        }

        _monster = m;
        _index = index;

        if (_monster == null) return;

        if (nameText != null) nameText.text = _monster.monsterName;
        UpdateFromMonster();

        _monster.OnHealthChanged += OnMonsterHealthChanged;
        _monster.OnDied += OnMonsterDied;
        if (MonsterManager.Instance != null) MonsterManager.Instance.OnSelectedMonsterChanged += OnSelectedChanged;
        if (selectButton != null) selectButton.onClick.AddListener(OnSelectClicked);

        // 초기 선택 상태 반영
        OnSelectedChanged(MonsterManager.Instance != null ? MonsterManager.Instance.selectedIndex : -1);

        // 초기 패턴 UI 클리어
        ClearPatternUI();
    }

    private void OnEnable()
    {
        if (_monster == null && autoBindToLocalMonster)
        {
            var local = GetComponent<Monster>();
            if (local != null)
            {
                // index는 MonsterManager가 알고 있으므로 -1로 둔다. MonsterManager.Register 시 SetIndex 될 수 있음.
                BindToMonsterInternal(local, -1);
            }
        }
    }

    private void OnDestroy()
    {
        if (_monster != null)
        {
            _monster.OnHealthChanged -= OnMonsterHealthChanged;
            _monster.OnDied -= OnMonsterDied;
        }
        if (MonsterManager.Instance != null) MonsterManager.Instance.OnSelectedMonsterChanged -= OnSelectedChanged;
        if (selectButton != null) selectButton.onClick.RemoveListener(OnSelectClicked);
    }

    private void OnMonsterHealthChanged(int current, int max) => UpdateFromMonster();

    private void UpdateFromMonster()
    {
        if (_monster == null) return;
        float fill = _monster.maxHP > 0 ? (float)_monster.currentHP / _monster.maxHP : 0f;
        if (hpFill != null)
        {
            hpFill.type = Image.Type.Filled;
            hpFill.fillMethod = Image.FillMethod.Horizontal;
            hpFill.fillAmount = Mathf.Clamp01(fill);
        }
        if (hpText != null) hpText.text = $"{_monster.currentHP} / {_monster.maxHP}";
        if (nameText != null && !string.IsNullOrEmpty(_monster.monsterName)) nameText.text = _monster.monsterName;
    }

    private void OnSelectClicked()
    {
        // MonsterManager가 있으면 인덱스로 선택, 아니면 Monster 객체로 선택 시도
        if (MonsterManager.Instance != null && _index >= 0)
            MonsterManager.Instance.SetSelected(_index);
        else if (MonsterManager.Instance != null && _monster != null)
            MonsterManager.Instance.SetSelected(_monster);
    }

    private void OnSelectedChanged(int selectedIndex)
    {
        // 선택 인덱스가 잘 설정되어 있지 않다면 MonsterManager에서 실제 인덱스를 얻어 비교
        int compareIndex = _index;
        if (compareIndex < 0 && MonsterManager.Instance != null && _monster != null)
            compareIndex = MonsterManager.Instance.monsters.IndexOf(_monster);

        bool isSelected = selectedIndex == compareIndex;
        if (selectButton != null) selectButton.interactable = !isSelected;
        var bg = GetComponent<Image>();
        if (bg != null) bg.color = isSelected ? new Color(0.95f, 0.95f, 0.7f) : Color.white;
    }

    private void OnMonsterDied(Monster m)
    {
        // 중복 처리 방지: 구독 해제
        if (_monster != null)
            _monster.OnDied -= OnMonsterDied;

        // 버튼 비활성화 및 리스너 제거
        if (selectButton != null)
        {
            selectButton.interactable = false;
            selectButton.onClick.RemoveListener(OnSelectClicked);
        }

        // UI 페이드아웃 시작 (안전하게)
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        try
        {
            _fadeCoroutine = StartCoroutine(FadeOutAndDisable(0.6f));
        }
        catch
        {
            // 코루틴 시작 실패 시에도 오브젝트 제거 시도 (방어적)
            TryDestroySafely();
        }
    }

    private IEnumerator FadeOutAndDisable(float duration)
    {
        // 안전하게 CanvasGroup 확보 (없으면 추가 시도)
        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                // AddComponent은 에디터/런타임 상황에서도 안전하게 시도
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        float startAlpha = 1f;
        if (_canvasGroup != null)
        {
            // 널 체크 후 접근
            startAlpha = _canvasGroup.alpha;
        }

        float t = 0f;
        while (t < duration)
        {
            // 오브젝트가 이미 파괴되었거나 비활성화되면 중단
            if (this == null || gameObject == null) yield break;

            t += Time.deltaTime;
            float ratio = Mathf.Clamp01(t / duration);
            if (_canvasGroup != null)
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, ratio);
            yield return null;
        }

        if (_canvasGroup != null)
            _canvasGroup.alpha = 0f;

        // 페이드 완료 후 안전하게 파괴
        TryDestroySafely();
    }

    private void TryDestroySafely()
    {
        // gameObject가 남아있을 때만 파괴 시도
        if (this != null && gameObject != null)
        {
            // Destroy 대신 DestroyImmediate는 에디터에서 위험하므로 일반 Destroy 사용
            Destroy(gameObject);
        }
    }

    // ---------------- Pattern UI helpers ----------------

    /// <summary>패턴과 남은 턴으로 UI 바인딩</summary>
    public void BindPattern(AttackPatternSO pattern, int remainingTurns)
    {
        if (attackIconImage != null)
        {
            attackIconImage.sprite = pattern != null ? pattern.attackIcon : null;
            attackIconImage.enabled = pattern != null && pattern.attackIcon != null;
        }
        UpdatePatternTurns(remainingTurns);
    }

    /// <summary>남은 턴만 업데이트</summary>
    public void UpdatePatternTurns(int remainingTurns)
    {
        if (turnsText == null) return;
        if (remainingTurns > 0)
            turnsText.text = remainingTurns.ToString();
        else
            turnsText.text = string.Empty;
    }

    /// <summary>패턴 UI 초기화/클리어</summary>
    public void ClearPatternUI()
    {
        if (attackIconImage != null)
        {
            attackIconImage.sprite = null;
            attackIconImage.enabled = false;
        }
        if (turnsText != null) turnsText.text = string.Empty;
    }
}