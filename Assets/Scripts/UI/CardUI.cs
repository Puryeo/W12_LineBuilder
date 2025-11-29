using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class CardUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public BlockSO cardData;
    public Image image;
    public Image iconImage;
    public Canvas rootCanvas; // hand UI가 속한 Canvas (Screen Space - Overlay 권장)

    private GameObject _dragIcon;

    // 원본 카드 visibility 제어용
    private CanvasGroup _origCanvasGroup;
    private float _origAlpha = 1f;
    private bool _origBlocksRaycasts = true;

    public void Initialize(BlockSO data, Sprite defaultSprite)
    {
        cardData = data;
        image = GetComponent<Image>();
        if (image != null)
        {
            image.sprite = defaultSprite;
            image.color = (data != null) ? data.Color : Color.white;
        }
        if (iconImage != null)
        {
            var targetSprite = (data != null && data.previewSprite != null) ? data.previewSprite : defaultSprite;
            iconImage.sprite = targetSprite;
            iconImage.color = Color.white;
            iconImage.type = Image.Type.Simple;
            iconImage.preserveAspect = true;
        }
        // try to locate root canvas if not assigned
        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();

        // ensure CanvasGroup exists to control visibility without breaking layout
        _origCanvasGroup = GetComponent<CanvasGroup>();
        if (_origCanvasGroup == null)
        {
            _origCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            // runtime 추가: prefab asset 은 변경되지 않음
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (cardData == null)
        {
            Debug.LogWarning("[CardUI] OnBeginDrag: cardData is null");
            return;
        }

        // store original canvas group state and disable raycasts on original
        if (_origCanvasGroup != null)
        {
            _origAlpha = _origCanvasGroup.alpha;
            _origBlocksRaycasts = _origCanvasGroup.blocksRaycasts;
            _origCanvasGroup.blocksRaycasts = false;
        }

        // create drag icon under canvas (or fallback to this.transform)
        var parent = (rootCanvas != null) ? rootCanvas.transform : transform;
        _dragIcon = new GameObject("DragIcon");
        _dragIcon.transform.SetParent(parent, false);
        var img = _dragIcon.AddComponent<Image>();
        img.sprite = image != null ? image.sprite : null;
        img.color = image != null ? image.color : Color.white;
        img.raycastTarget = false;
        var rt = _dragIcon.GetComponent<RectTransform>();
        if (image != null)
            rt.sizeDelta = image.rectTransform.sizeDelta;
        SetDragIconPosition(eventData);

        // Let InteractionManager handle ghost/rotation while dragging from UI
        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.StartExternalDrag(cardData);
        }
        else
        {
            // fallback if InteractionManager missing
            Debug.LogWarning("[CardUI] InteractionManager.Instance is null - fallback to direct ghost.Show");
            // show ghost preview if available (will be updated in OnDrag)
            if (InteractionManager.Instance != null && InteractionManager.Instance.ghost != null)
            {
                InteractionManager.Instance.ghost.Show(cardData);
            }
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragIcon != null)
            SetDragIconPosition(eventData);

        // rotation via right-click during drag (UI drag path)
        if (Input.GetMouseButtonDown(1))
        {
            InteractionManager.Instance?.RotateGhostClockwiseExternal();
        }

        // Update ghost preview and hide/show original & drag icon based on grid bounds
        if (InteractionManager.Instance != null && InteractionManager.Instance.grid != null && InteractionManager.Instance.ghost != null)
        {
            var cam = Camera.main;
            if (cam == null) cam = InteractionManager.Instance.GetComponentInParent<Canvas>()?.worldCamera;
            Vector3 world = cam != null ? cam.ScreenToWorldPoint(new Vector3(eventData.position.x, eventData.position.y, cam.nearClipPlane)) : (Vector3)eventData.position;
            world.z = 0f;

            var grid = InteractionManager.Instance.grid;
            var cell = grid.WorldToGrid(world);
            bool inBounds = grid.InBounds(cell);

            if (inBounds)
            {
                bool can = grid.CanPlace(cardData, cell, InteractionManager.Instance.GhostRotationSteps);
                InteractionManager.Instance.ghost.UpdatePreview(cardData, cell, can, InteractionManager.Instance.GhostRotationSteps);

                // HIDE UI while ghost active
                if (_dragIcon != null && _dragIcon.activeSelf) _dragIcon.SetActive(false);
                if (_origCanvasGroup != null) _origCanvasGroup.alpha = 0f;
            }
            else
            {
                // OUTSIDE: show UI again
                InteractionManager.Instance.ghost.Hide();
                if (_dragIcon != null && !_dragIcon.activeSelf) _dragIcon.SetActive(true);
                if (_origCanvasGroup != null) _origCanvasGroup.alpha = _origAlpha;
            }
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_dragIcon == null)
        {
            // still ensure ghost hidden
            if (InteractionManager.Instance != null && InteractionManager.Instance.ghost != null)
                InteractionManager.Instance.ghost.Hide();
            return;
        }

        // Try to place at screen pos using current rotation from InteractionManager
        bool placed = false;
        if (InteractionManager.Instance != null)
        {
            placed = InteractionManager.Instance.TryPlaceAtScreen(cardData, eventData.position, InteractionManager.Instance.GhostRotationSteps);
        }
        else
        {
            // fallback
            placed = false;
        }

        if (placed)
        {
            // consume card from hand
            if (CardManager.Instance != null)
                CardManager.Instance.UseCardByReference(cardData);
        }

        // End external drag state
        InteractionManager.Instance?.EndExternalDrag();

        // Restore original canvas group state
        if (_origCanvasGroup != null)
        {
            _origCanvasGroup.alpha = _origAlpha;
            _origCanvasGroup.blocksRaycasts = _origBlocksRaycasts;
        }

        // Destroy drag icon
        Destroy(_dragIcon);
        _dragIcon = null;
    }

    private void SetDragIconPosition(PointerEventData eventData)
    {
        if (_dragIcon == null) return;
        var rt = _dragIcon.GetComponent<RectTransform>();
        Vector2 pos;
        Canvas canvas = rootCanvas;
        if (canvas == null && InteractionManager.Instance != null) canvas = InteractionManager.Instance.GetComponentInParent<Canvas>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas != null ? (RectTransform)canvas.transform : (RectTransform)transform,
            eventData.position,
            (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null,
            out pos);
        rt.anchoredPosition = pos;
    }
}
