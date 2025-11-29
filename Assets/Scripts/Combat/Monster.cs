using System;
using UnityEngine;

[DisallowMultipleComponent]
public class Monster : MonoBehaviour
{
    public string monsterName = "Enemy";
    public int maxHP = 100;
    public MonsterUI monsterUI;
    public int monsterIdx;
    [Tooltip("음수 또는 0이면 maxHP로 시작")]
    public int startingHP = -1;

    [NonSerialized] public int currentHP;

    public event Action<int, int> OnHealthChanged; // (current, max)
    public event Action<Monster> OnDied;

    private bool _isDead = false;
    public bool IsDead => _isDead;

    private void Awake()
    {
        currentHP = (startingHP <= 0) ? maxHP : Mathf.Clamp(startingHP, 0, maxHP);

        // 변경: 먼저 MonsterManager에 등록하여 실제 할당된 인덱스를 얻습니다.
        if (MonsterManager.Instance != null)
        {
            int assignedIndex = MonsterManager.Instance.RegisterMonster(this);
            monsterIdx = assignedIndex;
        }
        else
        {
            // MonsterManager가 없으면 기존 monsterIdx 유지
        }

        // MonsterUI는 실제 할당된 인덱스로 초기화
        monsterUI.Initialize(this, monsterIdx);

        OnHealthChanged?.Invoke(currentHP, maxHP);
    }

    private void OnDisable()
    {
        MonsterManager.Instance?.UnregisterMonster(this);
    }

    public void TakeDamage(int amount, string origin = "Monster.TakeDamage")
    {
        if (amount <= 0) return;
        if (_isDead) return;

        int before = currentHP;
        currentHP = Mathf.Clamp(currentHP - amount, 0, maxHP);
        Debug.Log($"[Monster] {monsterName} takes {amount} dmg -> {before} -> {currentHP}/{maxHP} ({origin})");
        OnHealthChanged?.Invoke(currentHP, maxHP);

        if (currentHP == 0 && !_isDead)
        {
            _isDead = true;
            // 이벤트를 통해 외부(Controller / UI 등)가 정리하도록 위임
            OnDied?.Invoke(this);
        }
    }
}