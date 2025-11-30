using UnityEngine;

public enum AttackType
{
    Bomb,
    Damage,
    HeavyDamage,
    Groggy
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
}
