using System;
using System.Text;
using UnityEngine;

/// <summary>
/// 속성의 종류를 정의
/// </summary>
public enum AttributeType
{
    None,
    WoodSord,
    WoodShield,
    Staff,
    Cross,
    BratCandy,
}

[DisallowMultipleComponent]
public class GridAttributeMap : MonoBehaviour
{
    private const int Size = 8;

    // 인스펙터에서 직접 편집 가능하도록 public으로 노출
    private AttributeType[] rows = new AttributeType[Size]; // 가로줄 8개에 각각 부여된 속성 목록
    private AttributeType[] cols = new AttributeType[Size]; // 세로줄 8개에 각각 부여된 속성 목록

    [Tooltip("Play 모드 진입 시 Grid 속성을 Console에 출력합니다.")]
    public bool logOnPlay = true;

    [Tooltip("Editor에서 배열 변경 시 즉시 로그를 출력합니다.")]
    public bool logOnValidateInEditor = false;

    private void OnValidate()
    {
        if (!Application.isPlaying && logOnValidateInEditor)
        {
            Debug.Log(LogAttributes("OnValidate (Editor)"), this);
        }
    }

    private void Awake()
    {
        // Play 모드 진입 시 한 번만 로그
        if (Application.isPlaying && logOnPlay)
        {
            Debug.Log(LogAttributes("Awake (Play)"), this);
        }
    }

    private string LogAttributes(string origin)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[GridAttributeMap] Origin:{origin} Time:{Time.realtimeSinceStartup:F3}s");
        sb.AppendLine($"  Summary: Rows[{rows.Length}] Cols[{cols.Length}]");
        sb.AppendLine("  Rows:");
        for (int i = 0; i < rows.Length; i++)
            sb.AppendLine($"    R{i}: {rows[i]}");
        sb.AppendLine("  Cols:");
        for (int i = 0; i < cols.Length; i++)
            sb.AppendLine($"    C{i}: {cols[i]}");
        return sb.ToString();
    }

    public AttributeType GetRow(int index)
    {
        if (index < 0 || index >= Size) return AttributeType.None;
        return rows[index];
    }

    public AttributeType GetCol(int index)
    {
        if (index < 0 || index >= Size) return AttributeType.None;
        return cols[index];
    }

    public void SetRow(int index, AttributeType value)
    {
        if (index < 0 || index >= Size) return;
        rows[index] = value;
    }

    public void SetCol(int index, AttributeType value)
    {
        if (index < 0 || index >= Size) return;
        cols[index] = value;
    }

    public override string ToString()
    {
        return $"GridAttributeMap Rows:[{string.Join(",", rows)}] Cols:[{string.Join(",", cols)}]";
    }
}