using System;
using System.Collections;

/// <summary>
/// 턴 페이즈 액션 인터페이스
/// 각 액션이 자체 애니메이션/이펙트를 처리하고, 완료 시 걸린 시간을 보고합니다.
/// </summary>
public interface IPhaseAction
{
    /// <summary>
    /// 액션 실행 (애니메이션/이펙트 포함)
    /// </summary>
    /// <param name="reportDuration">완료 시 걸린 시간(초)을 보고하는 콜백</param>
    /// <returns>코루틴 (완료까지 대기)</returns>
    IEnumerator Play(Action<float> reportDuration);

    /// <summary>
    /// 이 액션이 강제로 직렬 실행을 요구하는지 여부
    /// true면 전역 Parallel 모드여도 Sequential로 다운그레이드
    /// </summary>
    bool ForceSequential { get; }
}
