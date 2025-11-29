using UnityEngine;

[CreateAssetMenu(fileName = "DamageAttackPattern", menuName = "Monsters/DamageAttackPatternSO")]
public class DamageAttackPatternSO : AttackPatternSO
{
    [Header("Damage settings")]
    [Tooltip("플레이어에게 가할 대미지")]
    public int damage = 10;

    [Tooltip("이 패턴은 실드에 의해 흡수됩니다. true이면 실드가 먼저 소모되고 남은 피해만 플레이어 HP에 적용됩니다. " +
             "현재 실드 무시(bypass) 옵션은 지원하지 않습니다.")]
    public bool affectsShield = true;
}