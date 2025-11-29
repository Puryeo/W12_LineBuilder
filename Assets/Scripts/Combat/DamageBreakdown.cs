using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class DamageBreakdown
{
    // 기존 필드
    public int baseDamage;
    public int attributeDamage;
    public int defuseDamage;
    public int aoeDamage; // 광역 데미지

    // 호환성 필드: tests / 다른 코드에서 기대하는 이름들
    public int equipmentAdd = 0;
    public Dictionary<string, int> additivePerSource = new Dictionary<string, int>();

    // 내부 저장용 필드 (이전 구현에서 사용하던 이름 유지)
    public int preLightningDamage;

    // 외부에서 기대하는 이름: preLightningSum
    public int preLightningSum
    {
        get => preLightningDamage;
        set => preLightningDamage = value;
    }

    public bool lightningApplied;
    public int finalDamage;

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append($"DamageBreakdown(base:{baseDamage}, attr:{attributeDamage}, defuse:{defuseDamage}, equip:{equipmentAdd}, preLightning:{preLightningDamage}, lightning:{lightningApplied}, final:{finalDamage})");
        if (additivePerSource != null && additivePerSource.Count > 0)
        {
            sb.Append(" additivePerSource:{");
            bool first = true;
            foreach (var kv in additivePerSource)
            {
                if (!first) sb.Append(", ");
                sb.Append($"{kv.Key}:{kv.Value}");
                first = false;
            }
            sb.Append("}");
        }
        return sb.ToString();
    }

    // 추가: 테스트/로그용 포맷된 출력 제공
    // origin은 선택적이며 호출 지점을 전달할 때 사용합니다.
    public string ToLogString(string origin = null)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(origin))
            sb.AppendLine($"[DamageBreakdown] Origin:{origin} Time:{Time.realtimeSinceStartup:F3}s");

        sb.AppendLine($"  baseDamage: {baseDamage}");
        sb.AppendLine($"  attributeDamage: {attributeDamage}");
        sb.AppendLine($"  defuseDamage: {defuseDamage}");
        sb.AppendLine($"  equipmentAdd: {equipmentAdd}");
        sb.AppendLine($"  preLightningSum: {preLightningDamage}");
        sb.AppendLine($"  lightningApplied: {lightningApplied}");
        sb.AppendLine($"  finalDamage: {finalDamage}");

        if (additivePerSource != null && additivePerSource.Count > 0)
        {
            sb.Append("  additivePerSource: ");
            bool first = true;
            foreach (var kv in additivePerSource)
            {
                if (!first) sb.Append(", ");
                sb.Append($"{kv.Key}:{kv.Value}");
                first = false;
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}