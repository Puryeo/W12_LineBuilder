using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 간단한 UI 매니저 (TextMeshPro 버전)
/// - TurnCountText는 TextMeshProUGUI로 연결하세요.
/// - Bomb countdown은 bombCountdownText에 연결하세요.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Turn UI")]
    public TextMeshProUGUI turnCountText;

    [Header("Bomb UI")]
    public TextMeshProUGUI bombCountdownText;

    private void Start()
    {
        if (TurnManager.Instance != null)
        {
            UpdateTurnText(TurnManager.Instance.TurnCount);
            TurnManager.Instance.OnTurnAdvanced += UpdateTurnText;
            TurnManager.Instance.OnBombSpawnCountdownChanged += UpdateBombCountdown;
            UpdateBombCountdown(TurnManager.Instance.TurnsUntilNextBomb);
        }
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnAdvanced -= UpdateTurnText;
            TurnManager.Instance.OnBombSpawnCountdownChanged -= UpdateBombCountdown;
        }
    }

    private void UpdateTurnText(int turn)
    {
        if (turnCountText != null)
            turnCountText.text = $"Turn: {turn}";
    }

    private void UpdateBombCountdown(int turns)
    {
        if (bombCountdownText == null) return;

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