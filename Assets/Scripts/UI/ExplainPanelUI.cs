using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ExplainPanelUI : MonoBehaviour
{
    public static ExplainPanelUI Instance;

    [Header("Prefab (must contain an Image for icon and a TextMeshProUGUI or Text for description)")]
    public GameObject panelPrefab;

    private GameObject _panelInstance;
    private Image _iconImage;
    private TextMeshProUGUI _descriptionTMP;
    private Text _descriptionLegacy;
    private RectTransform _panelRect;
    private Canvas _canvas;
    private Camera _uiCamera; // null for ScreenSpaceOverlay
    private CanvasGroup _panelCanvasGroup;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        Instance = this;

        _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null) _canvas = FindObjectOfType<Canvas>();

        if (_canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceCamera)
            _uiCamera = _canvas.worldCamera;
    }

    private void EnsureInstance()
    {
        if (_panelInstance != null) return;
        if (panelPrefab == null || _canvas == null) return;

        _panelInstance = Instantiate(panelPrefab, _canvas.transform);
        _panelInstance.SetActive(false);
        _panelRect = _panelInstance.GetComponent<RectTransform>();

        // CanvasGroup: 있으면 사용, 없으면 추가
        _panelCanvasGroup = _panelInstance.GetComponent<CanvasGroup>();
        if (_panelCanvasGroup == null) _panelCanvasGroup = _panelInstance.AddComponent<CanvasGroup>();

        // 패널이 포인터 이벤트를 가로채지 않도록 (깜박임 방지)
        _panelCanvasGroup.blocksRaycasts = false;

        // Icon 찾기: 이름 "Icon" 우선, 없으면 첫 Image 사용
        _iconImage = _panelInstance.transform.Find("Icon")?.GetComponent<Image>()
                     ?? _panelInstance.GetComponentInChildren<Image>();

        // Description 찾기: 우선 TextMeshProUGUI, 없으면 legacy Text로 폴백
        _descriptionTMP = _panelInstance.transform.Find("Description")?.GetComponent<TextMeshProUGUI>()
                          ?? _panelInstance.GetComponentInChildren<TextMeshProUGUI>();

        if (_descriptionTMP == null)
        {
            _descriptionLegacy = _panelInstance.transform.Find("Description")?.GetComponent<Text>()
                                 ?? _panelInstance.GetComponentInChildren<Text>();
        }
    }

    /// <summary>
    /// 기존: 화면 좌표 기반 Show(호환 유지)
    /// </summary>
    public void Show(Sprite iconSprite, string description, Vector2 screenPosition)
    {
        EnsureInstance();
        if (_panelInstance == null) return;

        if (_iconImage != null) _iconImage.sprite = iconSprite;

        if (_descriptionTMP != null)
            _descriptionTMP.text = description ?? string.Empty;
        else if (_descriptionLegacy != null)
            _descriptionLegacy.text = description ?? string.Empty;

        RectTransform canvasRect = _canvas.transform as RectTransform;
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, _uiCamera, out localPoint))
        {
            const float offsetX = 12f;
            const float offsetY = -12f;
            _panelRect.anchoredPosition = localPoint + new Vector2(offsetX, offsetY);
        }

        _panelInstance.SetActive(true);
        _panelInstance.transform.SetAsLastSibling();
    }

    /// <summary>
    /// 새 오버로드: 대상 RectTransform(아이콘 기준)으로 패널 위치 설정.
    /// offset: 캔버스 로컬 오프셋 (기본 우측하단)
    /// </summary>
    public void Show(Sprite iconSprite, string description, RectTransform target, Vector2 offset = default)
    {
        EnsureInstance();
        if (_panelInstance == null || target == null) return;

        if (_iconImage != null) _iconImage.sprite = iconSprite;

        if (_descriptionTMP != null)
            _descriptionTMP.text = description ?? string.Empty;
        else if (_descriptionLegacy != null)
            _descriptionLegacy.text = description ?? string.Empty;

        // 기본 offset 지정
        if (offset == default) offset = new Vector2(12f, -12f);

        RectTransform canvasRect = _canvas.transform as RectTransform;

        // 대상의 world top-right corner를 기준으로 위치 계산
        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners);
        // corners: 0 = bottom-left, 1 = top-left, 2 = top-right, 3 = bottom-right
        Vector3 anchorWorld = corners[2];

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(_uiCamera, anchorWorld);
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, _uiCamera, out localPoint))
        {
            _panelRect.anchoredPosition = localPoint + offset;
        }

        _panelInstance.SetActive(true);
        _panelInstance.transform.SetAsLastSibling();
    }

    public void Hide()
    {
        if (_panelInstance != null)
            _panelInstance.SetActive(false);
    }
}