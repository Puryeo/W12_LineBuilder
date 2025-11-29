using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "TenTrix/RoundData", fileName = "RoundData_1")]
public class RoundDataSO : ScriptableObject
{
    [Header("Round Settings")]
    public int roundNumber; // 1, 2, ...

    [Header("Enemies")]
    [Tooltip("이 라운드에 등장할 몬스터 프리팹들")]
    public List<GameObject> enemiesToSpawn;

    [Header("Rewards (Logic Only)")]
    [Tooltip("라운드 클리어 시 지급할 보상 (추후 구현)")]
    public int rewardGold = 100;
}