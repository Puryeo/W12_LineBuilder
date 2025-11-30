using UnityEngine;

[CreateAssetMenu(fileName = "HeavyDamageAttackPattern", menuName = "Monsters/HeavyDamageAttackPatternSO")]
public class HeavyDamageAttackPatternSO : AttackPatternSO
{
    [Header("Damage Settings")]
    [Tooltip("플레이어에게 가할 피해량")]
    public int damage = 20;

    [Tooltip("Start of heavy attack 준비 중 HP가 이 값 이상 줄어들면 취소됨")]
    public int cancelThreshold = 10;

    [Header("Groggy Transition")]
    [Tooltip("취소 시 즉시 실행할 그로기 패턴")]
    public AttackPatternSO groggyPattern;

    public HeavyDamageAttackPatternSO()
    {
        attackType = AttackType.HeavyDamage;
    }

    private void OnValidate()
    {
        attackType = AttackType.HeavyDamage;
        cancelThreshold = Mathf.Max(1, cancelThreshold);
    }
}
