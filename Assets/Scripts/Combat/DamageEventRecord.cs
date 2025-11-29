using System;
using UnityEngine;

[Serializable]
public class DamageEventRecord
{
    public string timestamp;            // ISO8601 UTC
    public string origin;               // 호출자/컨텍스트
    public string target;               // "Player" / "Monster"
    public int amountRequested;         // 원래 요청된 데미지 (예: ApplyPlayerDamage 파라미터)
    public int shieldAbsorbed;          // 실드에 의해 흡수된 양 (플레이어)
    public int amountApplied;           // 실제 적용된 데미지 (HP 감소분)
    public int hpBefore;                // 적용 전 대상 HP (플레이어/몬스터)
    public int hpAfter;                 // 적용 후 대상 HP
    public string breakdown;            // DamageBreakdown.ToLogString(...) 또는 기타 설명 문자열
    public string note;                 // 추가 메모(옵션)

    // 새 필드: 어떤 CamShake 단계가 호출되었는지 기록
    public string shakeCalled;

    public DamageEventRecord()
    {
        timestamp = DateTime.UtcNow.ToString("o");
    }
}