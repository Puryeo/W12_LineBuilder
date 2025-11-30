using UnityEngine;

[CreateAssetMenu(fileName = "GroggyAttackPattern", menuName = "Monsters/GroggyAttackPatternSO")]
public class GroggyAttackPatternSO : AttackPatternSO
{
    [TextArea]
    [Tooltip("그로기 상태 설명(UI나 로그용)")]
    public string description;

    public GroggyAttackPatternSO()
    {
        attackType = AttackType.Groggy;
        delayTurns = 1;
    }

    private void OnValidate()
    {
        attackType = AttackType.Groggy;
        delayTurns = Mathf.Max(1, delayTurns);
    }
}
