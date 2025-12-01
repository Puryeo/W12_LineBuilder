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
    /// 실행 준비된 몬스터 리스트 반환 (RemainingTurns <= 0 && !IsDead && active)
    /// TurnManager의 MonsterAttackPhase에서 사용
    /// </summary>
    public List<IMonsterController> GetReadyMonsters()
    {
        var ready = new List<IMonsterController>();

        // 역순으로 순회하면서 null 제거
        for (int i = _registered.Count - 1; i >= 0; i--)
        {
            var ctrl = _registered[i];

            // Null 체크
            if (ctrl == null)
            {
                _registered.RemoveAt(i);
                continue;
            }

            // MonoBehaviour 체크
            var mb = ctrl as MonoBehaviour;
            if (mb == null || mb.gameObject == null || !mb.gameObject.activeSelf)
            {
                continue;
            }

            // Monster 사망 체크
            var monster = mb.GetComponent<Monster>();
            if (monster != null && monster.IsDead)
            {
                continue;
            }

            // RemainingTurns 체크
            if (ctrl.RemainingTurns <= 0)
            {
                ready.Add(ctrl);
            }
        }

        return ready;
    }
}