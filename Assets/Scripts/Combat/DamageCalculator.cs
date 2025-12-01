using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 확장 가능한 DamageCalculator 스켈레톤.
/// 현재 구현:
///  - baseDamage = (clearedRows.Count + clearedCols.Count) * settings.baseWeaponDamage
///  - attributeDamage: row Fire 속성만 검사하여 보너스 합산 (blocksPerRow * fireBonusPerBlock)
///  - defuseDamage = removedBombs.Count * settings.defuseDamagePerBomb
///  - lightningApplied: 행/열 중 Lightning 존재 여부 (확장 포인트)
///  - finalDamage = preLightningSum * (lightningApplied ? lightningMultiplier : 1)
/// 향후: 열 검사, 셀 단위 속성, 물/풀/번개 규칙을 이 클래스에 추가하세요.
/// </summary>
public static class DamageCalculator
{
    public class Settings
    {
        public int baseWeaponDamage = 10;
        public int sordBonusPerBlock = 1;
        public int lightningMultiplier = 2;

        public int staffAoEDamage = 10; // 스태프 라인당 광역 데미지
        public int crossDamage = 10; // 십자 라인당 추가 데미지
        public int bratCandyBonus = 10; // 개초딩 사탕 추가 데미지 
    }

    public static DamageBreakdown Calculate(GridManager.LineClearResult result, GridManager grid, GridAttributeMap attrMap, Settings settings)
    {
        var d = new DamageBreakdown();
        if (result == null || settings == null) return d;

        int rowsCleared = (result.ClearedRows != null) ? result.ClearedRows.Count : 0;
        int colsCleared = (result.ClearedCols != null) ? result.ClearedCols.Count : 0;

        d.baseDamage = (rowsCleared + colsCleared) * Math.Max(0, settings.baseWeaponDamage);

        int attrDamage = 0;
        int aoeDamageSum = 0;
        bool lightning = false;

        if (attrMap != null && grid != null)
        {
            int width = Math.Max(1, grid.width);
            int height = Math.Max(1, grid.height);
            int perBlock = Math.Max(0, settings.sordBonusPerBlock);

            // 개초딩 사탕 존재 여부 확인
            bool isRowPassiveActive = false;
            bool isColPassiveActive = false;

            for (int r = 0; r < grid.height; r++)
            {
                if (attrMap.GetRow(r) == AttributeType.BratCandy)
                {
                    isRowPassiveActive = true;
                    break;
                }
            }

            for (int c = 0; c < grid.width; c++)
            {
                if (attrMap.GetCol(c) == AttributeType.BratCandy)
                {
                    isColPassiveActive = true;
                    break;
                }
            }

            if (result.ClearedRows != null)
            {
                foreach (var y in result.ClearedRows)
                {
                    if (y < 0 || y >= grid.height) continue;

                    var at = attrMap.GetRow(y);

                    if (at == AttributeType.WoodSord)
                        attrDamage += width * perBlock;

                    if (at == AttributeType.Cross)
                    {
                        attrDamage += settings.crossDamage;
                    }

                    if (at == AttributeType.Staff)
                    {
                        aoeDamageSum += settings.staffAoEDamage;
                    }

                    if (isRowPassiveActive)
                    {
                        attrDamage += settings.bratCandyBonus;
                    }
                }
            }

            if (result.ClearedCols != null)
            {
                foreach (var x in result.ClearedCols)
                {
                    if (x < 0 || x >= grid.width) continue;

                    var at = attrMap.GetCol(x);

                    if (at == AttributeType.WoodSord)
                        attrDamage += height * perBlock;

                    if (at == AttributeType.Cross)
                    {
                        attrDamage += settings.crossDamage;
                    }

                    if (at == AttributeType.Staff)
                    {
                        aoeDamageSum += settings.staffAoEDamage;
                    }

                    if (isColPassiveActive)
                    {
                        attrDamage += settings.bratCandyBonus;
                    }
                }
            }
        }

        d.attributeDamage = attrDamage;
        d.preLightningDamage = d.baseDamage + d.attributeDamage + d.defuseDamage;
        d.lightningApplied = lightning;
        d.finalDamage = d.preLightningDamage * (d.lightningApplied ? Math.Max(1, settings.lightningMultiplier) : 1);
        d.aoeDamage = aoeDamageSum;

        return d;
    }

    // 헬퍼: 해당 행(y)에 폭탄이 있었는지
    private static bool HasBombInRow(List<Vector2Int> bombs, int y)
    {
        if (bombs == null) return false;
        foreach (var b in bombs) if (b.y == y) return true;
        return false;
    }

    // 헬퍼: 해당 열(x)에 폭탄이 있었는지
    private static bool HasBombInCol(List<Vector2Int> bombs, int x)
    {
        if (bombs == null) return false;
        foreach (var b in bombs) if (b.x == x) return true;
        return false;
    }
}