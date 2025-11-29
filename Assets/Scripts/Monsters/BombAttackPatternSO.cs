using UnityEngine;

[CreateAssetMenu(fileName = "BombAttackPattern", menuName = "Monsters/BombAttackPatternSO")]
public class BombAttackPatternSO : AttackPatternSO
{
    [Header("Bomb settings")]
    [Tooltip("스폰된 폭탄의 초기 timer")]
    public int startTimer = 3;

    [Tooltip("폭탄의 maxTimer (cap)")]
    public int maxTimer = 6;

    public enum SpawnMode
    {
        RandomGrid,
        SpecificCell,
        AroundMonster
    }

    [Tooltip("스폰 모드")]
    public SpawnMode spawnMode = SpawnMode.RandomGrid;

    [Tooltip("SpecificCell 또는 AroundMonster에서 사용할 오프셋들")]
    public Vector2Int[] offsets;
}