using System;
using UnityEngine;

/// <summary>
/// CombatManager:
/// - GridManager.OnLinesCleared 구독
/// - 기본무기 데미지: (clearedRows.Count + clearedCols.Count) * baseWeaponDamage
/// - 폭탄 해체 보상: 각 폭탄마다 monsterDamageOnDefuse 적용 + OnBombDefused 이벤트 발생
/// - 승패 상태 로그 출력
/// </summary>
public class CombatManager : MonoBehaviour
{
    public static CombatManager Instance { get; private set; }

    [Header("HP")]
    public int playerMaxHP = 100;
    public int monsterMaxHP = 250;

    [Header("Damage Values")]
    public int baseWeaponDamage = 10;         // 줄 클리어 당 기본 데미지
    //public int bombDefuseMonsterDamage = 30; // 폭탄 해체 시 몬스터에 주는 데미지
    public int bombExplosionPlayerDamage = 20; // 폭탄 폭발 데미지

    [Header("Attribute Bonuses (temporary)")]
    [Tooltip("불 속성일 때 블록당 추가 데미지 (Grid 행/열이 Fire이면 해당 라인의 블록 수 * 이 값이 추가됩니다)")]
    public int sordBonusPerBlock = 1;

    [Header("Grid popup (assign prefab with DamagePopup/TMP)")]
    [Tooltip("그리드 위치에 생성할 DamagePopup prefab (TextMeshPro 3D 권장)")]
    public GameObject gridDamagePopupPrefab;
    [Tooltip("그리드 팝업 생성 위치 오프셋(월드 좌표 기준)")]
    public Vector3 gridPopupOffset = new Vector3(0f, 0.2f, 0f);

    [Header("Debug")]
    public int playerHP;
    public int monsterHP;

    public event Action<int> OnBombDefused;

    private GridAttributeMap _attrMap;
    private GridManager _grid;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        playerHP = playerMaxHP;
        monsterHP = monsterMaxHP;

        _grid = GridManager.Instance;
        if (_grid != null)
            _attrMap = _grid.GetComponent<GridAttributeMap>();
    }

    private void OnEnable()
    {
        if (GridManager.Instance != null)
            GridManager.Instance.OnLinesCleared += HandleLinesCleared;
        else
            Debug.LogWarning("[CombatManager] GridManager.Instance is null on OnEnable.");
    }

    private void OnDisable()
    {
        if (GridManager.Instance != null)
            GridManager.Instance.OnLinesCleared -= HandleLinesCleared;
    }

    private void HandleLinesCleared(GridManager.LineClearResult result)
    {
        if (result == null) return;

        // Settings 구성 (향후 UI/데이터로 노출 가능)
        var settings = new DamageCalculator.Settings
        {
            baseWeaponDamage = baseWeaponDamage,
            sordBonusPerBlock = sordBonusPerBlock,
            lightningMultiplier = 2,
            staffAoEDamage = 10,  //스태프
            hammerMultiplier = 2 // 망치
        };

        var breakdown = DamageCalculator.Calculate(result, _grid, _attrMap, settings);

        string origin = "CombatManager.HandleLinesCleared";
        Debug.Log($"[CombatManager] {origin} DamageBreakdown: {breakdown}");

        // 1) 단일 타겟(선택된 적) 대미지 적용
        if (breakdown.finalDamage > 0)
        {
            // 기록용: monster HP 변화 전/후 캡처
            int monsterBefore = monsterHP;
            ApplyMonsterDamage(breakdown.finalDamage);
            int monsterAfter = monsterHP;

            Debug.Log($"[CombatManager] {origin} Applied finalDamage:{breakdown.finalDamage} (monsterHP now {monsterHP})");

            // CamShake 호출 및 반환된 단계명 확보
            string shakeCalled = TriggerCamShakeByDamage(breakdown.finalDamage);

            // Serialize damage event (monster)
            try
            {
                var rec = new DamageEventRecord
                {
                    origin = origin,
                    target = "Monster",
                    amountRequested = breakdown.finalDamage,
                    shieldAbsorbed = 0,
                    amountApplied = Math.Max(0, monsterBefore - monsterAfter),
                    hpBefore = monsterBefore,
                    hpAfter = monsterAfter,
                    breakdown = breakdown != null ? breakdown.ToLogString(origin) : null,
                    note = "Line clear -> monster damage",
                    shakeCalled = shakeCalled
                };
                DamageEventSerializer.AppendRecord(rec);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CombatManager] Damage serialization failed (monster): {ex}");
            }
        }

        // 2) 광역(AoE) 대미지 적용 (스태프 효과)
        if (breakdown.aoeDamage > 0 && MonsterManager.Instance != null)
        {
            MonsterManager.Instance.ApplyAoEDamage(breakdown.aoeDamage);
            Debug.Log($"[CombatManager] Staff Effect: AoE {breakdown.aoeDamage} damage applied.");
        }

        // 그리드 위치에 팝업 띄우기 (row/col 중심 및 폭탄 위치)
        TrySpawnGridPopups(result, settings);

        // 기존 OnBombDefused 이벤트는 그대로 발행(후속 처리: 카드드로우 등)
        int defusedBombs = (result.RemovedBombPositions != null) ? result.RemovedBombPositions.Count : 0;
        if (defusedBombs > 0)
        {
            try
            {
                OnBombDefused?.Invoke(defusedBombs);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CombatManager] OnBombDefused handler threw: {ex}");
            }
        }

        CheckWinLose();

        // 디버그/UI 구독자용 이벤트 (선택)
        try { GameEvents.RaiseOnDamageCalculationResolved(breakdown, origin); } catch { }
    }

    public void HealPlayer(int amount, string origin = "Heal")
    {
        if (amount <= 0) return;
        int before = playerHP;
        playerHP = Mathf.Min(playerHP + amount, playerMaxHP);

        Debug.Log($"[CombatManager] Player Healed {amount} -> {playerHP}/{playerMaxHP} ({origin})");

        // UI 갱신 이벤트 발생
        GameEvents.RaiseOnPlayerHealthChanged(playerHP, playerMaxHP, origin);
    }

    // 그리드 팝업 생성 로직: 각 행/열 중앙 및 폭탄 위치에 해당 구성요소 데미지 표시
    private void TrySpawnGridPopups(GridManager.LineClearResult result, DamageCalculator.Settings settings)
    {
        if (_grid == null) return;
        if (gridDamagePopupPrefab == null) return;

        // 1) 행(row) 중앙 팝업
        if (result.ClearedRows != null)
        {
            int width = Math.Max(1, _grid.width);
            foreach (var y in result.ClearedRows)
            {
                if (y < 0 || y >= _grid.height) continue;
                int dmg = settings.baseWeaponDamage;

                // WoodSord 보너스
                if (_attrMap != null && _attrMap.GetRow(y) == AttributeType.WoodSord)
                    dmg += width * Math.Max(0, settings.sordBonusPerBlock);

                // [수정됨] Hammer 보너스: 해당 줄에 폭탄이 있었을 경우에만 배율 적용
                if (_attrMap != null && _attrMap.GetRow(y) == AttributeType.Hammer)
                {
                    bool hasBombInRow = false;
                    if (result.RemovedBombPositions != null)
                    {
                        foreach (var bombPos in result.RemovedBombPositions)
                        {
                            if (bombPos.y == y)
                            {
                                hasBombInRow = true;
                                break;
                            }
                        }
                    }

                    if (hasBombInRow)
                    {
                        dmg *= settings.hammerMultiplier;
                    }
                }

                var center = new UnityEngine.Vector2Int(width / 2, y);
                var world = _grid.GridToWorld(center) + gridPopupOffset;
                SpawnGridDamagePopup(world, dmg);
            }
        }

        // 2) 열(col) 중앙 팝업
        if (result.ClearedCols != null)
        {
            int height = Math.Max(1, _grid.height);
            foreach (var x in result.ClearedCols)
            {
                if (x < 0 || x >= _grid.width) continue;
                int dmg = settings.baseWeaponDamage;

                // WoodSord 보너스
                if (_attrMap != null && _attrMap.GetCol(x) == AttributeType.WoodSord)
                    dmg += height * Math.Max(0, settings.sordBonusPerBlock);

                // [수정됨] Hammer 보너스: 해당 줄에 폭탄이 있었을 경우에만 배율 적용
                // (기존 코드의 GetRow(x) 오타도 GetCol(x)로 수정했습니다)
                if (_attrMap != null && _attrMap.GetCol(x) == AttributeType.Hammer)
                {
                    bool hasBombInCol = false;
                    if (result.RemovedBombPositions != null)
                    {
                        foreach (var bombPos in result.RemovedBombPositions)
                        {
                            if (bombPos.x == x)
                            {
                                hasBombInCol = true;
                                break;
                            }
                        }
                    }

                    if (hasBombInCol)
                    {
                        dmg *= settings.hammerMultiplier;
                    }
                }

                var center = new UnityEngine.Vector2Int(x, height / 2);
                var world = _grid.GridToWorld(center) + gridPopupOffset;
                SpawnGridDamagePopup(world, dmg);
            }
        }

        // 3) 제거된 폭탄 위치 팝업
        if (result.RemovedBombPositions != null)
        {
            foreach (var p in result.RemovedBombPositions)
            {
                if (p.x < 0 || p.x >= _grid.width || p.y < 0 || p.y >= _grid.height) continue;
                var world = _grid.GridToWorld(p) + gridPopupOffset;
                SpawnGridDamagePopup(world);
            }
        }
    }

    private void SpawnGridDamagePopup(Vector3 worldPos, int amount = -1)
    {
        if (gridDamagePopupPrefab == null) return;
        var go = Instantiate(gridDamagePopupPrefab, worldPos, Quaternion.identity);
        // 독립 오브젝트로 유지
        go.transform.SetParent(null);

        var dp = go.GetComponent<DamagePopup>();
        if (dp != null)
        {
            dp.Initialize(amount);
            return;
        }

        // fallback: TextMeshPro 직접 설정
        var tmp = go.GetComponentInChildren<TMPro.TextMeshPro>();
        if (tmp != null)
        {
            if(amount != -1)
                tmp.text = $"<-{amount}>";
            else
                tmp.text = "폭탄 해제";
        }
    }

    public void ApplyPlayerDamage(int amount, string origin = null)
    {
        if (amount <= 0) return;

        // 진단: 호출자(origin)이 제공되지 않으면 스택트레이스 수집
        if (string.IsNullOrEmpty(origin))
        {
            try
            {
                var st = new System.Diagnostics.StackTrace(1, true);
                origin = $"UnknownCaller (stacktrace below)";
                Debug.Log($"[CombatManager] ApplyPlayerDamage called origin={origin} amount={amount} Time={Time.realtimeSinceStartup:F3}s\n{st}", this);
            }
            catch (Exception)
            {
                origin = "UnknownCaller";
                Debug.Log($"[CombatManager] ApplyPlayerDamage called origin={origin} amount={amount} Time={Time.realtimeSinceStartup:F3}s", this);
            }
        }
        else
        {
            Debug.Log($"[CombatManager] ApplyPlayerDamage called origin={origin} amount={amount} Time={Time.realtimeSinceStartup:F3}s", this);
        }

        int remaining = amount;
        int absorbed = 0;
        int playerBefore = playerHP;

        // 1) 실드로 먼저 흡수
        if (PlayerShieldManager.Instance != null && PlayerShieldManager.Instance.currentShield > 0)
        {
            absorbed = PlayerShieldManager.Instance.ConsumeShield(remaining, origin);
            remaining -= absorbed;
            Debug.Log($"[CombatManager] {origin} 쉴드가 공격 흡수함 {absorbed}. 남은 데미지: {remaining}");
        }

        // 2) 남은 데미지를 플레이어 HP에 적용
        if (remaining > 0)
        {
            playerHP -= remaining;
            Debug.Log($"[CombatManager] Player takes {remaining} dmg -> HP {playerHP}/{playerMaxHP} (origin:{origin})");
            // UI 업데이트 이벤트 (origin 전달)
            GameEvents.RaiseOnPlayerHealthChanged(Mathf.Max(0, playerHP), playerMaxHP, origin);
        }
        else
        {
            Debug.Log($"[CombatManager] {origin} Damage fully absorbed by shield.");
        }

        // CamShake: 남은 데미지 값(0 포함)을 기반으로 단계 호출 및 호출 단계 이름 반환
        string shakeCalled = TriggerCamShakeByDamage(remaining);

        // Serialize damage event (player)
        try
        {
            var rec = new DamageEventRecord
            {
                origin = origin,
                target = "Player",
                amountRequested = amount,
                shieldAbsorbed = absorbed,
                amountApplied = Math.Max(0, playerBefore - playerHP),
                hpBefore = playerBefore,
                hpAfter = playerHP,
                breakdown = null,
                note = "ApplyPlayerDamage",
                shakeCalled = shakeCalled
            };
            DamageEventSerializer.AppendRecord(rec);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CombatManager] Damage serialization failed (player): {ex}");
        }

        CheckWinLose();
    }

    // 변경: 몬스터가 개체로 존재하면 MonsterManager를 통해 선택된 몬스터에 전달.
    public void ApplyMonsterDamage(int amount)
    {
        if (amount <= 0) return;

        // MonsterManager가 있으면 선택된 몬스터에게 데미지 전달하고 기존 단일 HP는 변경하지 않음
        if (MonsterManager.Instance != null)
        {
            MonsterManager.Instance.ApplyDamageToSelected(amount);
            return;
        }

        // 하위 호환: 기존 단일 몬스터 HP 사용
        monsterHP -= amount;
        Debug.Log($"[CombatManager] Monster takes {amount} dmg -> HP {monsterHP}/{monsterMaxHP}");
        CheckWinLose();
    }

    private void CheckWinLose()
    {
        // MonsterManager 존재 시 모든 몬스터 사망 여부로 승리 판단
        if (MonsterManager.Instance != null)
        {
            if (MonsterManager.Instance.AreAllMonstersDead())
            {
                Debug.Log("[CombatManager] WIN: All monsters dead (MonsterManager)");
                
                if(GameFlowManager.Instance != null)
                {
                    GameFlowManager.Instance.OnAllMonstersDefeated();
                }
            }
        }
        else
        {
            if (monsterHP <= 0)
            {
                monsterHP = 0;
                Debug.Log("[CombatManager] WIN: Monster HP <= 0");
                // TODO: GameManager 승리 처리 호출
            }
        }

        if (playerHP <= 0)
        {
            playerHP = 0;
            Debug.Log("[CombatManager] LOSE: Player HP <= 0");
            // TODO: GameManager 패배 처리 호출
        }
    }

    // Replace existing TriggerCamShakeByDamage with this for runtime verification
    private string TriggerCamShakeByDamage(int damage)
    {
        var cm = CamShakeManager.Instance;
        if (cm == null)
        {
            Debug.Log("[CombatManager] TriggerCamShakeByDamage: CamShakeManager.Instance is null");
            return "None";
        }

        try
        {
            string result;
            if (damage <= 0)
            {
                cm.ShakeWeak();
                result = "Weak";
            }
            else if (damage >= cm.strongThreshold)
            {
                cm.ShakeStrong();
                result = "Strong";
            }
            else if (damage >= cm.normalThreshold)
            {
                cm.ShakeNormal();
                result = "Normal";
            }
            else
            {
                cm.ShakeWeak();
                result = "Weak";
            }

            Debug.Log($"[CombatManager] TriggerCamShakeByDamage -> damage={damage}, normalThreshold={cm.normalThreshold}, strongThreshold={cm.strongThreshold}, shakeCalled={result}");
            return result;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CombatManager] CamShake call failed: {ex}");
            return "None";
        }
    }

    // 디버그용: 외부에서 체력 즉시 설정
    public void SetPlayerHP(int hp)
    {
        playerHP = Mathf.Clamp(hp, 0, playerMaxHP);
    }

    public void SetMonsterHP(int hp)
    {
        // 하위호환: 여전히 단일 monsterHP 세팅 지원
        monsterHP = Mathf.Clamp(hp, 0, monsterMaxHP);
    }
}