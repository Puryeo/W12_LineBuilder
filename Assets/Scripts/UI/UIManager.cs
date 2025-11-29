using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 간단한 UI 매니저 (TextMeshPro 버전)
/// - TurnCountText는 TextMeshProUGUI로 연결하세요.
/// - Rest 표시용 이미지는 restDots 배열에 연결하세요.
/// - Bomb countdown은 bombCountdownText에 연결하세요.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Turn / Rest UI")]
    public TextMeshProUGUI turnCountText;     // Canvas 내 TurnCountText (TextMeshProUGUI)
    public Image[] restDots;                  // Rest 상태 표시용 ● ○ ○ (size = maxRestStreak)

    [Header("Bomb UI")]
    public TextMeshProUGUI bombCountdownText; // "Next Bomb: N" 표시

    private void Start()
    {
        if (TurnManager.Instance != null)
        {
            UpdateTurnText(TurnManager.Instance.TurnCount);
            TurnManager.Instance.OnTurnAdvanced += UpdateTurnText;
            TurnManager.Instance.OnRestStreakChanged += UpdateRestDots;
            TurnManager.Instance.OnBombSpawnCountdownChanged += UpdateBombCountdown;

            // 초기값 반영
            UpdateBombCountdown(TurnManager.Instance.TurnsUntilNextBomb);
        }
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnAdvanced -= UpdateTurnText;
            TurnManager.Instance.OnRestStreakChanged -= UpdateRestDots;
            TurnManager.Instance.OnBombSpawnCountdownChanged -= UpdateBombCountdown;
        }
    }

    private void UpdateTurnText(int turn)
    {
        if (turnCountText != null)
            turnCountText.text = $"Turn: {turn}";
    }

    private void UpdateRestDots(int streak)
    {
        if (restDots == null) return;
        for (int i = 0; i < restDots.Length; i++)
        {
            if (restDots[i] == null) continue;
            // 활성된 점은 불투명(예: 흰색), 비활성은 반투명
            restDots[i].color = (i < streak) ? Color.white : new Color(1f, 1f, 1f, 0.25f);
        }
    }

    private void UpdateBombCountdown(int turns)
    {
        if (bombCountdownText == null) return;

        // 자동 스폰이 비활성화 되어 있으면 텍스트를 숨김 (공간 유지가 필요하면 빈 문자열 설정)
        if (BombManager.Instance != null && BombManager.Instance.enableAutoSpawn == false)
        {
            bombCountdownText.text = string.Empty;
            return;
        }

        if (turns <= 0)
            bombCountdownText.text = "적의 공격!";
        else
            bombCountdownText.text = $"적의 다음 공격: {turns}턴 뒤";
    }
}