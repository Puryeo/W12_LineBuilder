using System;
using UnityEngine;

/// <summary>
/// PatternElement: RoundDataSO에서 사용되는 패턴 엔트리(패턴 SO + weight + repeat)
/// EnemySpawnEntry: 라운드에서 스폰할 몬스터 프리팹과 그 프리팹에 주입할 패턴 풀
/// </summary>
[Serializable]
public class PatternElement
{
    public AttackPatternSO pattern;
    [Tooltip("선택 비중 (>=0). 모든 항목이 0이면 균등 샘플링으로 폴백됩니다.)")]
    public int weight = 1;
    [Tooltip("실행 후 동일 패턴을 자동 재스케줄 할지 여부")]
    public bool repeat = false;
}

[Serializable]
public class EnemySpawnEntry
{
    [Tooltip("스폰할 몬스터 프리팹")]
    public GameObject prefab;
    [Tooltip("프리팹에 주입할 패턴 풀 (라운드 레벨 오버라이드)")]
    public PatternElement[] patterns;
}