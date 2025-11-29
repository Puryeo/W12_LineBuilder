using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// BombView:
/// - Bomb GameObject에서 타이머 텍스트 관리와 깜빡임 효과 처리.
/// - CreateBombView가 Instantiate한 prefab에 추가되어 사용됩니다.
/// </summary>
[DisallowMultipleComponent]
public class BombView : MonoBehaviour
{
    private SpriteRenderer _sr;
    private TextMeshPro _tm;
    private Coroutine _blinkCoroutine;

    // 초기화 (CreateBombView 호출한 후 바로 호출)
    public void Initialize(int timer)
    {
        _sr = GetComponent<SpriteRenderer>();
        _tm = GetComponentInChildren<TextMeshPro>();
        SetTimer(timer);
    }

    public void SetTimer(int timer)
    {
        if (_tm != null)
            _tm.text = timer.ToString();

        if (timer <= 1)
            StartBlinking();
        else
            StopBlinking();
    }

    public void StartBlinking()
    {
        if (_blinkCoroutine != null) return;
        _blinkCoroutine = StartCoroutine(BlinkRoutine());
    }

    public void StopBlinking()
    {
        if (_blinkCoroutine == null) return;
        StopCoroutine(_blinkCoroutine);
        _blinkCoroutine = null;
        if (_sr != null) _sr.enabled = true;
        if (_tm != null) _tm.enabled = true;
    }

    private IEnumerator BlinkRoutine()
    {
        // 단순 깜빡임: 0.5초 간격으로 보이기/숨기기
        float delay = 0.5f;
        while (true)
        {
            if (_sr != null) _sr.enabled = !_sr.enabled;
            if (_tm != null) _tm.enabled = !_tm.enabled;
            yield return new WaitForSeconds(delay);
        }
    }
}