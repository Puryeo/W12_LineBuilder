using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class AttributeEffectManager : MonoBehaviour
{
    [Tooltip("GridManager에 부착해서 사용하세요.")]
    public GridManager gridManager;

    [Header("Shield / Shield Settings")]
    [Tooltip("쉴드 라인 1개당 지급할 실드량")]
    public int shieldPerLine = 2;
    [Tooltip("쉴드 라인 트리거당 폭탄 타이머 연기를 수행할지 (true = 라인당 ApplyShield 호출)")]
    public bool applyShieldPerLine = true;

    private GridAttributeMap _attrMap;
    private GridManager _grid;

    private void Awake()
    {
        if (gridManager == null)
            gridManager = GetComponent<GridManager>();

        _grid = gridManager ?? GridManager.Instance;
    }

    private void OnEnable()
    {
        if (_grid != null)
            _grid.OnLinesCleared += OnLinesCleared;
    }

    private void OnDisable()
    {
        if (_grid != null)
            _grid.OnLinesCleared -= OnLinesCleared;
    }

    private void Start()
    {
        if (_grid == null)
        {
            Debug.LogWarning("[AttributeEffectManager] GridManager not found in scene.");
            return;
        }

        _attrMap = _grid.GetComponent<GridAttributeMap>();
        if (_attrMap == null)
            Debug.LogWarning("[AttributeEffect] GridAttributeMap not found on GridManager GameObject.");
    }

    private void OnLinesCleared(GridManager.LineClearResult result)
    {
        if (result == null || !result.HasClear) return;

        string origin = "AttributeEffectManager.OnLinesCleared";
        Debug.Log($"[AttributeEffect] Origin:{origin} Time:{Time.realtimeSinceStartup:F3}s Rows:{string.Join(",", result.ClearedRows)} Cols:{string.Join(",", result.ClearedCols)} Removed:{result.RemovedCount}");

        if (_attrMap == null)
        {
            Debug.LogWarning("[AttributeEffect] GridAttributeMap unavailable; skipping attribute effects.");
            return;
        }

        int shieldLineCount = 0;
        int crossLineCount = 0;

        // row counts
        if (result.ClearedRows != null)
        {
            foreach (var y in result.ClearedRows)
            {
                var type = _attrMap.GetRow(y);
                if (type == AttributeType.WoodShield) shieldLineCount++;
                else if (type == AttributeType.Cross) crossLineCount++; // 체크
            }
        }
        // col counts
        if (result.ClearedCols != null)
        {
            foreach (var x in result.ClearedCols)
            {
                var type = _attrMap.GetCol(x);
                if (type == AttributeType.WoodShield) shieldLineCount++;
                else if (type == AttributeType.Cross) crossLineCount++; // 체크
            }
        }

        if (shieldLineCount > 0)
        {
            if (applyShieldPerLine)
            {
                if (BombManager.Instance != null)
                {
                    for (int i = 0; i < shieldLineCount; i++)
                        BombManager.Instance.ApplyShieldToAllBombs();
                }
            }
            else
            {
                BombManager.Instance?.ApplyShieldToAllBombs();
            }

            int shieldGain = shieldLineCount * Math.Max(0, shieldPerLine);
            if (shieldGain > 0)
            {
                if (PlayerShieldManager.Instance != null)
                    PlayerShieldManager.Instance.AddShield(shieldGain, origin);
                else
                    GameEvents.RaiseOnShieldChanged(Mathf.Min(shieldGain, 9999), 5, origin);
            }

            Debug.Log($"[AttributeEffect] {origin} ShieldEffect applied -> ShieldLines:{shieldLineCount} shieldGain:{shieldGain}");
        }

        // 십자가(Cross) 회복 효과 적용
        if (crossLineCount > 0)
        {
            int healAmount = crossLineCount * 5; // 라인당 5 회복
            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.HealPlayer(healAmount, origin);
            }
            Debug.Log($"[AttributeEffect] Cross Effect: Healed {healAmount} HP.");
        }
    }
}