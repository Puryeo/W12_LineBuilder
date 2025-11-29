using System.Collections;
using UnityEngine;

/// <summary>
/// MonsterController의 공개 인터페이스(설계용).
/// 구현체는 패턴 선택/스케줄/틱/실행을 제공해야 함.
/// MonsterUI와 바인딩되는 프로퍼티 포함.
/// </summary>
public interface IMonsterController
{
    AttackPatternSO CurrentPattern { get; }
    int RemainingTurns { get; }

    /// <summary>패턴을 스케줄(몬스터가 해당 패턴을 선택해 남은 턴 세팅)</summary>
    void SchedulePattern(AttackPatternSO pattern);

    /// <summary>예약된 패턴 취소(있으면)</summary>
    void CancelScheduledPattern();

    /// <summary>턴이 진행될 때 호출할 것 — 내부 remainingTurns 감소 및 UI 업데이트</summary>
    void TickTurn();

    /// <summary>즉시 패턴 실행(보통 remainingTurns<=0일 때 호출) - 레거시, 동기 방식</summary>
    void ExecutePattern();

    /// <summary>패턴 실행 준비가 되었는지 확인 (RemainingTurns <= 0 && 패턴 존재)</summary>
    bool IsReadyToExecute();

    /// <summary>패턴을 코루틴으로 실행 (애니메이션 대기 포함)</summary>
    IEnumerator ExecutePatternRoutine();

    /// <summary>몬스터가 죽었는지 확인</summary>
    bool IsDead();
}