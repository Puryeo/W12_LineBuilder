using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬 단위 중앙 몬스터 패턴 Tick 관리자.
/// TurnManager.AdvanceTurn()에서 호출되도록 사용.
/// </summary>
[DisallowMultipleComponent]
public class MonsterAttackManager : MonoBehaviour
{
    public static MonsterAttackManager Instance { get; private set; }

    private readonly List<IMonsterController> _registered = new List<IMonsterController>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    public void Register(IMonsterController ctrl)
    {
        if (ctrl == null) return;
        if (!_registered.Contains(ctrl)) _registered.Add(ctrl);
    }

    public void Unregister(IMonsterController ctrl)
    {
        if (ctrl == null) return;
        _registered.Remove(ctrl);
    }

    public void TickAll()
    {
        // TurnManager 쪽에서 호출되는 핵심 루틴 — 정상 동작시 과도한 로그는 제거
        for (int i = _registered.Count - 1; i >= 0; i--)
        {
            var ctrl = _registered[i];
            if (ctrl == null) { _registered.RemoveAt(i); continue; }
            try { ctrl.TickTurn(); } catch (System.Exception ex) { Debug.LogError($"[MonsterAttackManager] Exception in TickTurn: {ex}"); }
        }
    }

    /// <summary>
    /// 등록된 몬스터 목록 반환 (스냅샷 복사본)
    /// TurnManager가 코루틴에서 안전하게 순회하기 위해 사용
    /// </summary>
    public List<IMonsterController> GetRegisteredMonsters()
    {
        // 스냅샷 복사: 순회 중 리스트 변경으로부터 안전
        var snapshot = new List<IMonsterController>(_registered.Count);
        for (int i = 0; i < _registered.Count; i++)
        {
            if (_registered[i] != null)
            {
                snapshot.Add(_registered[i]);
            }
        }
        return snapshot;
    }
}