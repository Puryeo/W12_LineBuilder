using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// 간단한 월드 텍스트 데미지 팝업:
/// - TextMeshPro(3D) 를 사용하여 월드에 텍스트를 띄움
/// - 위로 부드럽게 떠올라가며 페이드아웃 후 자동 제거
/// - prefab 구성: Root (GameObject) + TextMeshPro (3D) component
/// - Root에 이 스크립트를 추가하고 prefab으로 저장한 뒤 Monster.damagePopupPrefab 또는 CombatManager.gridDamagePopupPrefab에 할당하세요.
/// </summary>
[DisallowMultipleComponent]
public class DamagePopup : MonoBehaviour
{
    [Header("References")]
    public TextMeshPro text; // prefab의 TextMeshPro (3D) 참조

    [Header("Animation")]
    public float floatDistance = 0.6f;
    public float duration = 1.0f;
    public float initialScale = 1.0f;
    public float finalScale = 1.0f;
    public Vector3 randomOffsetRange = new Vector3(0.2f, 0.1f, 0f);

    [Header("Highlight")]
    [Tooltip("이 값보다 큰 데미지는 highlightColor로 표시됩니다.")]
    public int highlightThreshold = 20;
   
    [Tooltip("highlight 시 사용할 색상")]
    public Color highlightColor = Color.yellow;
    [Tooltip("highlight 사용 여부")]
    public bool useHighlight = true;

    [Header("Rendering (Sorting)")]
    [Tooltip("빈 문자열이면 변경하지 않습니다. 예: 'UI' 또는 'Popup'")]
    public string sortingLayerName = "";
    [Tooltip("정수값. 클수록 앞에 렌더링됩니다.")]
    public int sortingOrder = 100;

    private Color _defaultColor; // prefab에 설정된 기본색
    private Color _startColor;   // 애니메이션 시작시 사용할 색 (초기 알파 포함)

    private void Awake()
    {
        if (text == null) text = GetComponent<TextMeshPro>();
        if (text != null) _defaultColor = text.color;
    }

    /// <summary>
    /// amount: 표시할 양 (음수/양수 표기 규칙은 호출자에서 결정)
    /// </summary>
    public void Initialize(int amount)
    {
        if (text != null)
        {
            // 텍스트 내용 (요청 포맷)
            if(amount != -1)
                text.text = $"{amount}!";
            else
                text.text = $"폭탄 해제!";
            text.alignment = TextAlignmentOptions.Center;

            // 색상 결정: highlight 조건 충족 시 highlightColor 사용, 아니면 prefab 기본색 사용
            if (useHighlight && amount > highlightThreshold)
                text.color = highlightColor;
            else
                text.color = _defaultColor;

            // 렌더러 정렬 설정 (TextMeshPro(3D)는 MeshRenderer를 사용)
            var rend = text.GetComponent<Renderer>();
            if (rend != null)
            {
                if (!string.IsNullOrEmpty(sortingLayerName))
                    rend.sortingLayerName = sortingLayerName;
                rend.sortingOrder = sortingOrder;
            }

            // 시작 색상 저장 (알파 포함)
            _startColor = text.color;
        }

        // 약간의 랜덤 오프셋으로 겹침 억제
        transform.position += new Vector3(
            Random.Range(-randomOffsetRange.x, randomOffsetRange.x),
            Random.Range(0f, randomOffsetRange.y),
            0f);

        transform.localScale = Vector3.one * initialScale;

        StartCoroutine(AnimateAndDestroy());
    }

    private IEnumerator AnimateAndDestroy()
    {
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + Vector3.up * floatDistance;

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);

            // 위치 보간(부드럽게)
            transform.position = Vector3.Lerp(startPos, endPos, t);

            // 스케일(선택)
            float scale = Mathf.Lerp(initialScale, finalScale, t);
            transform.localScale = Vector3.one * scale;

            // 페이드 아웃 (startAlpha -> 0)
            if (text != null)
            {
                var c = text.color;
                c.a = Mathf.Lerp(_startColor.a, 0f, t);
                text.color = c;
            }

            // 항상 카메라를 향하게(빌보드)
            var cam = Camera.main;
            if (cam != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }
}