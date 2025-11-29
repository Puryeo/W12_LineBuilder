/// <summary>
/// 턴 진행 단계를 나타내는 열거형
/// </summary>
public enum TurnPhase
{
    TurnStart,          // 턴 시작
    ShieldReset,        // 실드 리셋
    BombExplosion,      // 폭탄 폭발 처리
    MonsterAttack,      // 몬스터 공격
    BombAutoSpawn,      // 자동 폭탄 스폰
    TurnEnd             // 턴 종료
}
