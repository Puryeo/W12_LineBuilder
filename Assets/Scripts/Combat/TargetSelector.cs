using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TargetSelector : MonoBehaviour
{
    [Header("씬에 배치한 몬스터(Inspector에서 2개 할당)")]
    public List<Monster> monsters = new List<Monster>(2);

    [Header("UI 버튼들 (Inspector에서 몬스터 순서대로 2개 할당)")]
    public List<Button> targetButtons = new List<Button>(2);

    [Header("디버그/초기 선택")]
    public int selectedIndex = 0;

    private void Start()
    {
        // 버튼에 선택 리스너 연결 (Inspector에서 수동 연결해도 됨)
        for (int i = 0; i < targetButtons.Count; i++)
        {
            int idx = i;
            if (targetButtons[i] != null)
                targetButtons[i].onClick.AddListener(() => SelectByIndex(idx));
        }

        // 초기 선택 보정
        if (monsters.Count > 0)
        {
            selectedIndex = Mathf.Clamp(selectedIndex, 0, monsters.Count - 1);
        }
        else
        {
            selectedIndex = -1;
        }

        UpdateButtonStates();
    }

    public void SelectByIndex(int index)
    {
        if (index < 0 || index >= monsters.Count) return;
        selectedIndex = index;
        Debug.Log($"[TargetSelector] Selected monster #{selectedIndex} -> {monsters[selectedIndex]?.name}");
        UpdateButtonStates();
    }

    public void ApplyDamageToSelected(int amount)
    {
        if (selectedIndex < 0 || selectedIndex >= monsters.Count)
        {
            Debug.LogWarning("[TargetSelector] No target selected.");
            return;
        }

        var target = monsters[selectedIndex];
        if (target == null)
        {
            Debug.LogWarning("[TargetSelector] Selected monster reference is null.");
            return;
        }

        target.TakeDamage(amount, "TargetSelector.ApplyDamageToSelected");
    }

    // 유니티 버튼에서 사용할 수 있게 공개된 메서드 (예: 데미지 10)
    public void ApplyDamageToSelected_10() => ApplyDamageToSelected(10);
    public void ApplyDamageToSelected_30() => ApplyDamageToSelected(30);

    private void UpdateButtonStates()
    {
        for (int i = 0; i < targetButtons.Count; i++)
        {
            if (targetButtons[i] == null) continue;

            // 선택된 버튼은 비활성화(또는 스타일 변경)
            targetButtons[i].interactable = (i != selectedIndex);

            // 시각적 강조: 색 변경 (간단)
            var colors = targetButtons[i].colors;
            colors.normalColor = (i == selectedIndex) ? new Color(0.8f, 0.8f, 0.8f) : Color.white;
            targetButtons[i].colors = colors;
        }
    }
}