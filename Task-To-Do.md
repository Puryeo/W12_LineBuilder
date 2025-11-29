# TenTrix Prototype — Task-To-Do

## 개요
PRD 기준으로 현재 코드베이스와 남은 작업을 정리한 우선순위 목록입니다.
각 항목은 담당 파일/권장 위치와 구현 요약을 포함합니다.

---

## 우선순위 A (즉시 필요한 핵심)
1. TurnManager 구현
   - 파일: Assets/Scripts/Turn/TurnManager.cs
   - 역할: 턴 흐름 제어(Input → Resolution → Tick), 턴 카운트, Rest 처리, RestStreak 관리, 폭탄 스폰 타이밍 제어.
   - 이벤트: OnTick, 구독: GridManager.OnBlockPlaced → AdvanceTurn 호출.

2. GridManager: 폭탄 관련 API 추가
   - 파일: Assets/Scripts/Grid/GridManager.cs (기존 파일 수정)
   - 추가 메서드:
     - SpawnBombAt(Vector2Int pos, int timer)
     - SpawnRandomBomb(int timer)
     - TickBombTimers() -> List<Vector2Int> explodedPositions
   - 뷰: 폭탄 뷰(간단한 타이머 오버레이) 생성/갱신/삭제 로직 추가.

3. Turn <-> Grid <-> Combat 연동
   - 턴 종료(Tick)에서:
     - 폭탄 타이머 감소 및 폭발 처리 (CombatManager.ApplyPlayerDamage)
     - 폭탄 스폰 주기(6~7 턴 랜덤)

---

## 우선순위 B (핵심 완성 후)
4. RestStreak -> Grid Collapse
   - GridManager.ClearAll(out removedCount) 호출 및 플레이어 자해 데미지 적용: removedCount * 5
   - RestStreak 리셋 및 UI 상태 갱신

5. Bomb 해체 보상 보완
   - GridManager.RemoveLines 결과(RemovedBombPositions)를 CombatManager가 이미 처리
   - CardManager.OnBombDefused 구독(이미 구현됨)과의 연동 확인

6. GameManager 구현
   - 게임 상태(Start/Play/Win/Lose), CombatManager.CheckWinLose 호출 연동, UIManager 통지

7. UIManager / HUD
   - 몬스터/플레이어 HP 바, 턴 카운트, 손패, 쉬기 버튼(●●● 표시), 폭탄 오버레이 깜빡임 등

8. Equipment / Weapon 슬롯
   - 슬롯별 발동 규칙(행/열 N) 및 장비 데이터 구조

---

## 우선순위 C (폴리시)
9. 폭탄 뷰 시각 효과(깜빡임/애니메이션)
10. Floating damage text, 팝업(승/패)
11. 입력/UX 개선(핸드 스냅백, 드래그 UX polish)

---

## 구현 노트
- 현재 코드베이스: GridManager/InteractionManager/CardManager/CombatManager 등 핵심 모듈은 존재.
- 남은 핵심은 "턴 흐름"과 "폭탄 생성·타이머·폭발"의 게임 루프 통합.
- 우선 구현 범위: TurnManager + GridManager 폭탄 API 추가로 게임 루프 동작 확인.

---

## 다음 액션
1. TurnManager 구현 시작 (턴 흐름, Rest 처리, 폭탄 스폰/타이머 처리)
2. GridManager에 폭탄 스폰/타이머 API 추가
3. 테스트: 배치 → AdvanceTurn → 폭탄 타이머 동작 및 폭발 데미지 확인
