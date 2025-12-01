using UnityEngine;

/// <summary>
/// Boss 버전의 폭탄 공격 패턴.
/// 기존 BombAttackPatternSO와 유사하게 작동하지만,
/// 폭탄이 배치될 위치 기준 3x3 반경의 블럭을 삭제하고 배치됩니다.
/// </summary>
[CreateAssetMenu(fileName = "BossBombAttackPattern", menuName = "Monsters/BossBombAttackPatternSO")]
public class BossBombAttackPatternSO : BombAttackPatternSO
{
    [Header("Boss Bomb settings")]
    [Tooltip("폭탄 배치 시 삭제할 반경 (기본값: 1 = 3x3 영역)")]
    public int clearRadius = 1;
}
