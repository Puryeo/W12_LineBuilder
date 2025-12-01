using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MonsterManager : MonoBehaviour
{
    public static MonsterManager Instance { get; private set; }

    public List<Monster> monsters = new List<Monster>();
    public int selectedIndex = -1;

    public event Action<int> OnSelectedMonsterChanged; // index (or -1)

    // 데미지 추적 시스템 (애니메이션 동기화용)
    private List<(Monster monster, float duration)> _damagedMonsters = new List<(Monster, float)>();

    [Header("Optional UI (assign to auto-create UI entries)")]
    public GameObject monsterUIPrefab; // prefab with MonsterUI component
    public Transform uiContainer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 변경: 인덱스를 반환하도록 하여 호출자(Monster)가 할당된 인덱스를 알 수 있게 함.
    public int RegisterMonster(Monster m)
    {
        monsters.Add(m);
        int assignedIndex = monsters.Count - 1;

        // 몬스터 사망 이벤트 구독 (사망 시 자동 타겟 전환 처리)
        if (m != null)
            m.OnDied += OnMonsterDied;

        // 선택 상태가 비어있거나 현재 선택 몬스터가 유효하지 않다면
        if (GetSelectedMonster() == null)
        {
            // 씬에 남아있는(활성) 몬스터 기준으로 첫 번째를 찾아 선택
            SelectFirstActiveMonster();
        }

        return assignedIndex;
    }

    public void UnregisterMonster(Monster m)
    {
        int idx = monsters.IndexOf(m);
        if (idx == -1) return;

        // 구독 해제는 제거 전에 수행
        if (m != null)
            m.OnDied -= OnMonsterDied;

        monsters.RemoveAt(idx);

        if (monsters.Count == 0)
        {
            SetSelected(-1);
        }
        else if (selectedIndex == idx)
        {
            // 제거된 것이 현재 선택된 몬스터였다면 씬에 남은 활성 몬스터 기준으로 재선택 시도
            if (!SelectFirstActiveMonster())
            {
                SetSelected(-1);
            }
        }
        else if (selectedIndex > idx)
        {
            // 리스트에서 제거로 인해 인덱스가 한 칸 당겨졌다면 보정
            selectedIndex = Mathf.Clamp(selectedIndex - 1, -1, monsters.Count - 1);
            OnSelectedMonsterChanged?.Invoke(selectedIndex);
        }
    }

    public Monster GetSelectedMonster() => (selectedIndex >= 0 && selectedIndex < monsters.Count) ? monsters[selectedIndex] : null;

    public void SetSelected(int index)
    {
        if (index < -1 || index >= monsters.Count) return;
        selectedIndex = index;
        Debug.Log($"[MonsterManager] Selected index set to {selectedIndex}");
        OnSelectedMonsterChanged?.Invoke(selectedIndex);
    }

    public void SetSelected(Monster m)
    {
        int idx = monsters.IndexOf(m);
        if (idx != -1) SetSelected(idx);
    }

    public void ApplyDamageToSelected(int amount, bool delayDeath = false)
    {
        var sel = GetSelectedMonster();
        if (sel != null)
            sel.TakeDamage(amount, "MonsterManager.ApplyDamageToSelected", delayDeath);
        else
            Debug.LogWarning("[MonsterManager] No selected monster to receive damage.");
    }

    public void ApplyDamageToIndex(int index, int amount, bool delayDeath = false)
    {
        if (index >= 0 && index < monsters.Count)
            monsters[index].TakeDamage(amount, "MonsterManager.ApplyDamageToIndex", delayDeath);
    }

    public void ApplyAoEDamage(int amount, bool delayDeath = false)
    {
        if (amount <= 0) return;

        // 리스트를 돌면서 살아있는 모든 적에게 대미지
        foreach (var m in monsters)
        {
            if (m != null && m.currentHP > 0)
            {
                m.TakeDamage(amount, "AoE_Staff", delayDeath);
            }
        }
    }

    public bool AreAllMonstersDead()
    {
        if (monsters == null || monsters.Count == 0) return false;
        foreach (var m in monsters)
        {
            if (m != null && m.currentHP > 0) return false;
        }
        return true;
    }

    public void ClearAllMonsters()
    {
        Monster[] monstersToDestroy = monsters.ToArray();

        monsters.Clear();
        selectedIndex = -1;

        foreach (var m in monstersToDestroy)
        {
            if (m != null) Destroy(m.gameObject);
        }
    }

    // 몬스터가 죽었을 때 호출되는 내부 처리기
    private void OnMonsterDied(Monster deadMonster)
    {
        if (deadMonster == null) return;

        // 씬에 남아있는(활성) 몬스터 목록을 장면 기준(형제 인덱스 우선)으로 정렬해서 가져옴
        var activeSorted = GetActiveMonstersSorted();

        if (activeSorted.Count == 0)
        {
            SetSelected(-1);
            return;
        }

        // deadMonster가 activeSorted에 포함되어 있지 않다면(이미 비활성화/제거된 경우) 첫 번째로 선택
        int pos = activeSorted.IndexOf(deadMonster);
        if (pos == -1)
        {
            SetSelected(monsters.IndexOf(activeSorted[0]));
            return;
        }

        // 다음 생존 몬스터 (순환)
        if (activeSorted.Count == 1)
        {
            // deadMonster만 남아있었음 -> 선택 해제
            SetSelected(-1);
            return;
        }

        int nextPos = (pos + 1) % activeSorted.Count;
        var nextMonster = activeSorted[nextPos];
        SetSelected(monsters.IndexOf(nextMonster)); // monsters 리스트에서 해당 몬스터의 인덱스로 선택
    }

    // 씬에 남아있는(활성) 몬스터들을 정렬하여 반환
    private List<Monster> GetActiveMonstersSorted()
    {
        var active = new List<Monster>();
        foreach (var m in monsters)
        {
            if (m != null && m.gameObject != null && m.gameObject.activeInHierarchy && !m.IsDead && m.currentHP > 0)
                active.Add(m);
        }

        if (active.Count == 0) return active;

        // uiContainer에 자식으로 배치되는 경우 siblingIndex 기준 정렬을 우선시함
        if (uiContainer != null)
        {
            active = active
                .OrderBy(m => m.transform.parent == uiContainer ? m.transform.GetSiblingIndex() : int.MaxValue)
                .ThenBy(m => monsters.IndexOf(m)) // 같은 우선순위 내에서는 등록 순서로 보정
                .ToList();
        }
        else
        {
            // uiContainer가 없으면 transform siblingIndex 기반으로 정렬 (같은 부모에서 의미 있음),
            // 부모가 다르면 등록 순서를 사용
            active = active
                .OrderBy(m => m.transform.GetSiblingIndex())
                .ThenBy(m => monsters.IndexOf(m))
                .ToList();
        }

        return active;
    }

    // 씬의 활성 몬스터 중 첫 번째를 찾아 선택 (성공하면 true)
    private bool SelectFirstActiveMonster()
    {
        var activeSorted = GetActiveMonstersSorted();
        if (activeSorted.Count == 0) return false;

        var first = activeSorted[0];
        int idx = monsters.IndexOf(first);
        if (idx != -1)
        {
            SetSelected(idx);
            return true;
        }
        return false;
    }

    #region Damage Tracking System
    /// <summary>
    /// 데미지 추적 시작 (LineClearAction Phase 시작 시 호출)
    /// </summary>
    public void StartDamageTracking()
    {
        _damagedMonsters.Clear();
        Debug.Log("[MonsterManager] Damage tracking started");
    }

    /// <summary>
    /// 데미지를 받은 몬스터 등록 (Monster.TakeDamage에서 호출)
    /// </summary>
    /// <param name="monster">데미지를 받은 몬스터</param>
    /// <param name="duration">애니메이션 지속 시간</param>
    public void TrackDamagedMonster(Monster monster, float duration)
    {
        if (monster == null) return;

        // 중복 체크 (같은 몬스터가 여러 번 등록되지 않도록)
        bool alreadyTracked = false;
        foreach (var (m, d) in _damagedMonsters)
        {
            if (m == monster)
            {
                alreadyTracked = true;
                break;
            }
        }

        if (!alreadyTracked)
        {
            _damagedMonsters.Add((monster, duration));
            Debug.Log($"[MonsterManager] Tracked damaged monster: {monster.monsterName} (duration: {duration}s)");
        }
    }

    /// <summary>
    /// 데미지를 받은 몬스터 목록 반환 (LineClearAction Phase 3.5에서 호출)
    /// </summary>
    /// <returns>데미지 받은 (몬스터, 애니메이션 시간) 리스트</returns>
    public List<(Monster monster, float duration)> GetDamagedMonsters()
    {
        Debug.Log($"[MonsterManager] GetDamagedMonsters: {_damagedMonsters.Count} monster(s) damaged");
        return new List<(Monster, float)>(_damagedMonsters); // 복사본 반환
    }
    #endregion
}