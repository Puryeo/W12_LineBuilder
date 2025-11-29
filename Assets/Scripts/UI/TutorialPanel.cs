using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

[DisallowMultipleComponent]
public class TutorialPanel : MonoBehaviour
{
    [Header("Hierarchy")]
    public GameObject panelRoot;                       // 패널 루트 (SetActive로 열고 닫음)
    [Tooltip("슬라이드로 사용할 GameObject들(예: TutorialIMG (1), TutorialIMG (2)). 비워두면 panelRoot의 자식 Image 오브젝트로 자동 채움")]
    public List<GameObject> slides = new List<GameObject>();

    [Header("Controls")]
    public Button prevButton;
    public Button nextButton;
    public TextMeshProUGUI pageLabelTMP;               // 선택사항: "Page {0} / {1}" 표시용
    public Text pageLabelLegacy;                       // 선택사항: Legacy Text 사용 시

    [Header("Behavior")]
    public bool startHiddenOnAwake = true;
    public bool initializeSlidesFromChildren = true;   // slides 비어있을 때 panelRoot 자식으로 자동 채움
    public string pageLabelFormat = "Page {0} / {1}";

    [Header("Events")]
    public UnityEvent OnOpened;
    public UnityEvent OnClosed;
    public UnityEvent<int> OnSlideChanged; // current index

    private int _currentIndex = 0;

    private void Awake()
    {
        if (panelRoot == null) panelRoot = gameObject;

        // 버튼 리스너 연결
        if (prevButton != null) prevButton.onClick.AddListener(OnPrevClicked);
        if (nextButton != null) nextButton.onClick.AddListener(OnNextClicked);

        // slides가 비어있고 자동 초기화 허용일 경우 panelRoot의 Image가 붙은 자식들을 슬라이드로 사용
        if (slides == null) slides = new List<GameObject>();
        if (slides.Count == 0 && initializeSlidesFromChildren && panelRoot != null)
        {
            slides = new List<GameObject>();
            for (int i = 0; i < panelRoot.transform.childCount; i++)
            {
                var child = panelRoot.transform.GetChild(i).gameObject;
                if (child.GetComponent<Image>() != null || child.GetComponentInChildren<Image>() != null)
                {
                    slides.Add(child);
                }
            }
        }

        // 기본 인덱스 설정
        _currentIndex = 0;

        // 모든 슬라이드는 기본적으로 비활성화
        foreach (var obj in slides)
        {
            if (obj != null) obj.SetActive(false);
        }

        // 패널이 숨겨진 상태라면 element 0은 active(true)로 두어야 함 (요구사항)
        if (startHiddenOnAwake)
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            if (slides != null && slides.Count > 0 && slides[0] != null)
                slides[0].SetActive(true); // 부모 비활성 상태여도 내부 상태는 true로 유지
        }
        else
        {
            // 패널이 보이는 상태로 시작하면 첫 슬라이드 보여줌
            if (panelRoot != null) panelRoot.SetActive(true);
            RefreshUI();
        }
    }

    private void OnDestroy()
    {
        if (prevButton != null) prevButton.onClick.RemoveListener(OnPrevClicked);
        if (nextButton != null) nextButton.onClick.RemoveListener(OnNextClicked);
    }

    // PUBLIC API

    public void Open(int startIndex = 0)
    {
        if (slides == null || slides.Count == 0)
        {
            Debug.LogWarning("[TutorialPanel] No slides available to open.");
            panelRoot.SetActive(false);
            return;
        }

        // 항상 첫 화면으로 열리도록 기본 startIndex를 사용 (요구: 다시 열면 0번째 보여야 함)
        _currentIndex = Mathf.Clamp(startIndex, 0, slides.Count - 1);
        panelRoot.SetActive(true);
        RefreshUI();
        OnOpened?.Invoke();
    }

    public void Close()
    {
        // 패널을 닫을 때는 내부 슬라이드 상태를 "기본 상태"로 맞춤:
        // - element 0은 true, 나머지는 false
        if (slides != null && slides.Count > 0)
        {
            for (int i = 0; i < slides.Count; i++)
            {
                var s = slides[i];
                if (s == null) continue;
                s.SetActive(i == 0);
            }
            _currentIndex = 0;
        }

        panelRoot.SetActive(false);
        OnClosed?.Invoke();
    }

    // 런타임에 slides를 교체
    public void SetSlides(List<GameObject> newSlides, int openAt = -1)
    {
        // 기존 슬라이드 모두 비활성화
        if (slides != null)
        {
            foreach (var s in slides)
                if (s != null) s.SetActive(false);
        }

        slides = newSlides ?? new List<GameObject>();

        // 새 슬라이드도 기본적으로 비활성화
        foreach (var s in slides)
            if (s != null) s.SetActive(false);

        // 패널이 닫힌 상태라면 기본 상태로 0번째만 true로 세팅
        if (panelRoot != null && !panelRoot.activeSelf)
        {
            if (slides.Count > 0 && slides[0] != null)
                slides[0].SetActive(true);
            _currentIndex = 0;
        }

        if (openAt >= 0)
            Open(openAt);
    }

    // INTERNAL (버튼 연결)
    private void OnPrevClicked()
    {
        if (slides == null || slides.Count == 0) return;
        if (_currentIndex <= 0) return;
        _currentIndex--;
        RefreshUI();
    }

    private void OnNextClicked()
    {
        if (slides == null || slides.Count == 0) return;
        if (_currentIndex < slides.Count - 1)
        {
            _currentIndex++;
            RefreshUI();
        }
        else
        {
            Close();
        }
    }

    private void RefreshUI()
    {
        if (slides == null) return;

        for (int i = 0; i < slides.Count; i++)
        {
            var obj = slides[i];
            if (obj == null) continue;
            obj.SetActive(i == _currentIndex);
        }

        if (prevButton != null)
            prevButton.interactable = (_currentIndex > 0);

        bool isLast = (_currentIndex == slides.Count - 1);
        SetNextButtonLabel(isLast ? "닫기" : "다음");

        string labelText = slides.Count > 0 ? string.Format(pageLabelFormat, _currentIndex + 1, slides.Count) : string.Empty;
        if (pageLabelTMP != null) pageLabelTMP.text = labelText;
        if (pageLabelLegacy != null) pageLabelLegacy.text = labelText;

        OnSlideChanged?.Invoke(_currentIndex);
    }

    private void SetNextButtonLabel(string text)
    {
        if (nextButton == null) return;
        var tmp = nextButton.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) { tmp.text = text; return; }
        var legacy = nextButton.GetComponentInChildren<Text>();
        if (legacy != null) legacy.text = text;
    }
}