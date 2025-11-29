using System;
using UnityEngine;

public class EventTester : MonoBehaviour
{
    [Header("DamageBreakdown Sample")]
    public int sampleBaseDamage = 10;
    public int sampleEquipmentAdd = 2;
    public bool sampleLightningApplied = false;

    [Header("Shield Sample")]
    public int sampleShieldTotal = 5;
    public int sampleShieldRemainingTurns = 3;

    [Header("Test Bomb Spawn")]
    public int spawnTimer = 3;
    public int spawnMaxTimer = 6;
    public int spawnCount = 1;
    public string spawnNamePrefix = "TestBomb";

    private int _spawnIndex = 0;

    private void OnEnable()
    {
        GameEvents.OnDamageCalculationResolved += OnDamageResolved;
        GameEvents.OnShieldChanged += OnShieldChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnDamageCalculationResolved -= OnDamageResolved;
        GameEvents.OnShieldChanged -= OnShieldChanged;
    }

    // 기존 Start 발행은 유지하되 에디터 버튼으로도 호출 가능
    private void Start()
    {
        // 기존 샘플 발행을 원치 않으면 주석 처리 가능
        PublishSampleDamageBreakdown("EventTester.Start");
        GameEvents.RaiseOnShieldChanged(sampleShieldTotal, sampleShieldRemainingTurns, "EventTester.Start");
    }

    public void PublishSampleDamageBreakdown(string origin = "EventTester.PublishSampleDamageBreakdown")
    {
        var db = new DamageBreakdown
        {
            baseDamage = sampleBaseDamage,
            equipmentAdd = sampleEquipmentAdd,
            preLightningSum = sampleBaseDamage + sampleEquipmentAdd,
            lightningApplied = sampleLightningApplied,
            finalDamage = sampleLightningApplied ? (sampleBaseDamage + sampleEquipmentAdd) * 2 : (sampleBaseDamage + sampleEquipmentAdd)
        };
        db.additivePerSource["EventTester.Sample"] = sampleBaseDamage - 10; // 예시 소스

        GameEvents.RaiseOnDamageCalculationResolved(db, origin);
        Debug.Log($"EventTester: Published DamageBreakdown (origin={origin})");
    }

    public void PublishSampleShieldChanged(string origin = "EventTester.PublishSampleShieldChanged")
    {
        GameEvents.RaiseOnShieldChanged(sampleShieldTotal, sampleShieldRemainingTurns, origin);
        Debug.Log($"EventTester: Published ShieldChanged (total={sampleShieldTotal}, remaining={sampleShieldRemainingTurns})");
    }

    public void SpawnTestBombs()
    {
        for (int i = 0; i < Math.Max(1, spawnCount); i++)
            SpawnTestBomb(spawnTimer, spawnMaxTimer);
    }

    public GameObject SpawnTestBomb(int timer, int maxTimer)
    {
        _spawnIndex++;
        var go = new GameObject($"{spawnNamePrefix}_{_spawnIndex}");
        go.transform.position = Vector3.zero;
        var b = go.AddComponent<Bomb>();
        b.timer = Mathf.Clamp(timer, 0, maxTimer);
        b.maxTimer = Math.Max(1, maxTimer);
        b.bombId = $"{go.name}-{b.GetInstanceID()}";
        Debug.Log($"EventTester: Spawned {go.name} timer={b.timer}/{b.maxTimer}", go);
        // Bomb.Awake에서 BombManager에 등록됨(Play 모드 또는 런타임에서)
        return go;
    }

    public void ApplyShieldToAllBombs()
    {
        if (BombManager.Instance == null)
        {
            Debug.LogWarning("EventTester: BombManager.Instance is null. Ensure BombManager exists in scene and is active.");
            return;
        }
        BombManager.Instance.ApplyShieldToAllBombs();
        Debug.Log("EventTester: Applied WaterEffect via BombManager");
    }

    public void DestroyAllSpawnedTestBombs()
    {
        var objs = GameObject.FindObjectsOfType<Bomb>();
        int removed = 0;
        foreach (var b in objs)
        {
            if (b == null) continue;
            if (b.gameObject.name.StartsWith(spawnNamePrefix, StringComparison.OrdinalIgnoreCase))
            {
                DestroyImmediate(b.gameObject);
                removed++;
            }
        }
        Debug.Log($"EventTester: Destroyed {removed} test bombs (Editor immediate).");
    }

    private void OnDamageResolved(DamageBreakdown db)
    {
        Debug.Log($"EventTester: OnDamageResolved received -> {db.ToLogString()}", this);
    }

    private void OnShieldChanged(int totalShield, int remainingTurns)
    {
        Debug.Log($"EventTester: OnShieldChanged received -> Shield:{totalShield}, Remaining:{remainingTurns}", this);
    }
}