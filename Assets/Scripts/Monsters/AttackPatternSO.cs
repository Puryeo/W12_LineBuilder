using UnityEngine;

public enum AttackType
{
    Bomb,
    Damage,
    HeavyDamage,
    Groggy,
    DisableSlot
}

/// <summary>
/// 몬스터 공격 패턴의 기본 ScriptableObject.
/// attackIcon: MonsterUI에 표시될 스프라이트.
/// </summary>
[CreateAssetMenu(fileName = "AttackPattern", menuName = "Monsters/AttackPatternSO")]
public class AttackPatternSO : ScriptableObject
{
    public string displayName;
    public AttackType attackType = AttackType.Damage;
    [Tooltip("이 패턴 실행까지 남은 기본 턴 수")]
    public int delayTurns = 1;
    [Tooltip("패턴이 반복 가능한가")]
    public bool repeat = false;
    [Tooltip("선택 시 가중치(선택 전략에서 사용)")]
    public float weight = 1f;

    [Tooltip("몬스터 상단 UI에 표시할 공격 아이콘")]
    public Sprite attackIcon;

    [Header("Pattern Description")]
    [Tooltip("패턴 아이콘 호버 시 표시될 설명 (ExplainPanel)")]
    [TextArea(3, 5)]
    public string patternDescription;

    [Header("Phase Execution Settings")]
    [Tooltip("이 패턴이 강제로 직렬 실행을 요구하는지 (전역 Parallel 모드여도 Sequential로 다운그레이드)")]
    public bool forceSequential = false;

    [Tooltip("이 패턴의 예상 실행 시간 (초) - 디자이너 참고용")]
    public float estimatedDuration = 1.0f;
}

    

