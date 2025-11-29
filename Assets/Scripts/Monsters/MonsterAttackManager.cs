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
}