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

    [Header("Line Clear Effects")]
    [Tooltip("라인 클리어 하이라이트 처리기")]
    public LineClearHighlighter lineClearHighlighter;

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

        // LineClearHighlighter 초기화
        if (lineClearHighlighter != null && _grid != null)
        {
            lineClearHighlighter.Initialize(_grid);
        }
    }

    private void OnEnable()
    {
        // NOTE: GridManager.OnLinesCleared 구독은 TurnManager에서 처리합니다.
        // TurnManager가 LineClearAction을 큐에 추가하고, 페이즈 시스템에서 처리합니다.
        // if (GridManager.Instance != null)
        //     GridManager.Instance.OnLinesCleared += HandleLinesCleared;
        // else
        //     Debug.LogWarning("[CombatManager] GridManager.Instance is null on OnEnable.");
    }

    private void OnDisable()
    {
        // NOTE: 이제 GridManager 구독을 하지 않으므로 해제도 불필요
        // if (GridManager.Instance != null)
        //     GridManager.Instance.OnLinesCleared -= HandleLinesCleared;
    }

    /// <summary>
    /// [DEPRECATED] 이 메서드는 더 이상 사용되지 않습니다.
    /// 대신 ApplyLineClearDamage()가 LineClearAction에서 호출됩니다.
    /// </summary>
    [System.Obsolete("Use ApplyLineClearDamage() instead. Called from LineClearAction.")]
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
            crossDamage = 10 // 십자가
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
            TrySpawnGridPopups(result, breakdown.aoeDamage);
            Debug.Log($"[CombatManager] Staff Effect: AoE {breakdown.aoeDamage} damage applied.");
        }

        // 그리드 위치에 팝업 띄우기 (row/col 중심 및 폭탄 위치)
        TrySpawnGridPopups(result, breakdown.finalDamage);

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

    /// <summary>
    /// 플레이어 체력 회복
    /// </summary>
    public void HealPlayer(int amount, string origin = "Heal")
    {
        if (amount <= 0) return;
        int before = playerHP;
        playerHP = Mathf.Min(playerHP + amount, playerMaxHP);

        Debug.Log($"[CombatManager] Player Healed {amount} -> {playerHP}/{playerMaxHP} ({origin})");

        // UI 갱신 이벤트 발생
        GameEvents.RaiseOnPlayerHealthChanged(playerHP, playerMaxHP, origin);
    }

    /// <summary>
    /// 그리드 팝업 생성: 각 행/열 중앙 및 폭탄 위치에 데미지 표시
    /// </summary>
    private void TrySpawnGridPopups(GridManager.LineClearResult result, int damage)
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

                var center = new Vector2Int(width / 2, y);
                var world = _grid.GridToWorld(center) + gridPopupOffset;
                SpawnGridDamagePopup(world, damage);
            }
        }

        // 2) 열(col) 중앙 팝업
        if (result.ClearedCols != null)
        {
            int height = Math.Max(1, _grid.height);
            foreach (var x in result.ClearedCols)
            {
                if (x < 0 || x >= _grid.width) continue;

                var center = new Vector2Int(x, height / 2);
                var world = _grid.GridToWorld(center) + gridPopupOffset;
                SpawnGridDamagePopup(world, damage);
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

    /// <summary>
    /// 개별 팝업 스폰
    /// </summary>
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

    /// <summary>
    /// 플레이어 데미지 적용 (쉴드 우선 흡수)
    /// </summary>
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

    /// <summary>
    /// 몬스터 데미지 적용 (MonsterManager를 통해 선택된 몬스터에 전달)
    /// </summary>
    public void ApplyMonsterDamage(int amount, bool delayDeath = false)
    {
        if (amount <= 0) return;

        // MonsterManager가 있으면 선택된 몬스터에게 데미지 전달하고 기존 단일 HP는 변경하지 않음
        if (MonsterManager.Instance != null)
        {
            MonsterManager.Instance.ApplyDamageToSelected(amount, delayDeath);
            return;
        }

        // 하위 호환: 기존 단일 몬스터 HP 사용
        monsterHP -= amount;
        Debug.Log($"[CombatManager] Monster takes {amount} dmg -> HP {monsterHP}/{monsterMaxHP}");
        CheckWinLose();
    }

    /// <summary>
    /// 승패 체크
    /// </summary>
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
            }
        }

        if (playerHP <= 0)
        {
            playerHP = 0;
            Debug.Log("[CombatManager] LOSE: Player HP <= 0");
        }
    }

    /// <summary>
    /// 데미지 크기에 따라 카메라 쉐이크 트리거
    /// </summary>
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

    /// <summary>
    /// 디버그용: 플레이어 체력 즉시 설정
    /// </summary>
    public void SetPlayerHP(int hp)
    {
        playerHP = Mathf.Clamp(hp, 0, playerMaxHP);
    }

    /// <summary>
    /// 디버그용: 몬스터 체력 즉시 설정
    /// </summary>
    public void SetMonsterHP(int hp)
    {
        // 하위호환: 여전히 단일 monsterHP 세팅 지원
        monsterHP = Mathf.Clamp(hp, 0, monsterMaxHP);
    }

    #region Phase Action Hooks
    /// <summary>
    /// 라인 클리어 애니메이션 훅
    /// LineClearAction.Play()에서 호출됩니다.
    /// LineClearHighlighter를 사용하여 클리어된 라인의 테두리를 하이라이트합니다.
    /// </summary>
    /// <param name="result">라인 클리어 결과</param>
    /// <param name="reportDuration">애니메이션 시간을 보고하는 콜백</param>
    public virtual void PlayLineClearAnimationHook(GridManager.LineClearResult result, Action<float> reportDuration)
    {
        if (lineClearHighlighter != null)
        {
            // LineClearHighlighter 코루틴 시작 (인스펙터 설정 시간만큼 대기)
            StartCoroutine(lineClearHighlighter.HighlightLines(result, reportDuration));
            Debug.Log($"[CombatManager] Playing line clear highlight (duration: {lineClearHighlighter.duration}s)");
        }
        else
        {
            // LineClearHighlighter가 없으면 즉시 완료
            Debug.LogWarning("[CombatManager] LineClearHighlighter is null, skipping animation");
            reportDuration?.Invoke(0f);
        }
    }

    /// <summary>
    /// 몬스터 히트 애니메이션 훅
    /// LineClearAction.Play()에서 호출됩니다.
    /// 데미지는 이미 적용된 상태이므로 점멸 효과의 지속 시간만 보고합니다.
    /// 주의: 점멸 효과는 Monster.TakeDamage()에서 MonsterUI.PlayFlashEffect()로 자동 재생됩니다.
    /// </summary>
    /// <param name="damage">적용된 데미지 (참고용)</param>
    /// <param name="reportDuration">점멸 효과 시간을 보고하는 콜백</param>
    public virtual void PlayMonsterHitAnimationHook(int damage, Action<float> reportDuration)
    {
        if (MonsterManager.Instance != null)
        {
            var selectedMonster = MonsterManager.Instance.GetSelectedMonster();
            if (selectedMonster != null && damage > 0)
            {
                // MonsterUI의 flashDuration 사용
                float duration = 0f;
                if (selectedMonster.monsterUI != null)
                {
                    duration = selectedMonster.monsterUI.flashDuration;
                }

                // 점멸 효과는 TakeDamage()에서 자동 재생되므로 시간만 보고
                reportDuration?.Invoke(duration);
                Debug.Log($"[CombatManager] Flash effect duration for {selectedMonster.monsterName}: {duration:F2}s (damage {damage} already applied)");
                return;
            }
        }

        // 몬스터가 없거나 데미지가 0이면 즉시 완료
        Debug.LogWarning($"[CombatManager] No selected monster found or no damage (damage: {damage})");
        reportDuration?.Invoke(0f);
    }

    /// <summary>
    /// 라인 클리어 데미지 계산 (데미지 적용 없음)
    /// LineClearAction.Play()에서 호출됩니다.
    /// </summary>
    public DamageBreakdown CalculateLineClearDamage(GridManager.LineClearResult result)
    {
        if (result == null)
        {
            Debug.LogWarning("[CombatManager] CalculateLineClearDamage: result is null");
            return null;
        }

        // Settings 구성
        var settings = new DamageCalculator.Settings
        {
            baseWeaponDamage = baseWeaponDamage,
            sordBonusPerBlock = sordBonusPerBlock,
            lightningMultiplier = 2,
            staffAoEDamage = 10,
            crossDamage = 10
        };

        var breakdown = DamageCalculator.Calculate(result, _grid, _attrMap, settings);
        Debug.Log($"[CombatManager] CalculateLineClearDamage: {breakdown}");

        return breakdown;
    }

    /// <summary>
    /// 계산된 데미지 적용 (애니메이션 없음)
    /// LineClearAction.Play()에서 애니메이션 대기 후 호출됩니다.
    /// </summary>
    public void ApplyCalculatedDamage(DamageBreakdown breakdown, GridManager.LineClearResult result, bool delayDeath = false)
    {
        if (breakdown == null)
        {
            Debug.LogWarning("[CombatManager] ApplyCalculatedDamage: breakdown is null");
            return;
        }

        string origin = "CombatManager.ApplyCalculatedDamage";

        // 1) 단일 타겟 데미지 적용
        if (breakdown.finalDamage > 0)
        {
            int monsterBefore = monsterHP;
            ApplyMonsterDamage(breakdown.finalDamage, delayDeath);
            int monsterAfter = monsterHP;

            Debug.Log($"[CombatManager] {origin} Applied finalDamage:{breakdown.finalDamage} (monsterHP now {monsterHP})");

            // CamShake
            string shakeCalled = TriggerCamShakeByDamage(breakdown.finalDamage);

            // Serialize damage event
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

        // 2) 광역(AoE) 데미지 적용
        if (breakdown.aoeDamage > 0 && MonsterManager.Instance != null)
        {
            MonsterManager.Instance.ApplyAoEDamage(breakdown.aoeDamage, delayDeath);
            Debug.Log($"[CombatManager] Staff Effect: AoE {breakdown.aoeDamage} damage applied.");
        }

        // 3) 그리드 팝업 스폰
        if (result != null)
        {
            TrySpawnGridPopups(result, breakdown.finalDamage);
        }

        // 4) 폭탄 해체 이벤트
        if (result != null)
        {
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
        }

        // 5) 승패 체크
        CheckWinLose();

        // 6) 디버그 이벤트
        try { GameEvents.RaiseOnDamageCalculationResolved(breakdown, origin); } catch { }
    }

    /// <summary>
    /// [DEPRECATED] 라인 클리어 데미지 적용
    /// 대신 CalculateLineClearDamage() + PlayMonsterHitAnimationHook() + ApplyCalculatedDamage() 사용
    /// </summary>
    [System.Obsolete("Use CalculateLineClearDamage() + ApplyCalculatedDamage() instead")]
    public void ApplyLineClearDamage(GridManager.LineClearResult result)
    {
        if (result == null) return;

        // Settings 구성
        var settings = new DamageCalculator.Settings
        {
            baseWeaponDamage = baseWeaponDamage,
            sordBonusPerBlock = sordBonusPerBlock,
            lightningMultiplier = 2,
            staffAoEDamage = 10,
            crossDamage = 10
        };

        var breakdown = DamageCalculator.Calculate(result, _grid, _attrMap, settings);

        string origin = "CombatManager.ApplyLineClearDamage";
        Debug.Log($"[CombatManager] {origin} DamageBreakdown: {breakdown}");

        // 1) 단일 타겟 데미지 적용
        if (breakdown.finalDamage > 0)
        {
            int monsterBefore = monsterHP;
            ApplyMonsterDamage(breakdown.finalDamage);
            int monsterAfter = monsterHP;

            Debug.Log($"[CombatManager] {origin} Applied finalDamage:{breakdown.finalDamage} (monsterHP now {monsterHP})");

            // CamShake
            string shakeCalled = TriggerCamShakeByDamage(breakdown.finalDamage);

            // Serialize damage event
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

        // 2) 광역(AoE) 데미지 적용
        if (breakdown.aoeDamage > 0 && MonsterManager.Instance != null)
        {
            MonsterManager.Instance.ApplyAoEDamage(breakdown.aoeDamage);
            Debug.Log($"[CombatManager] Staff Effect: AoE {breakdown.aoeDamage} damage applied.");
        }

        // 3) 그리드 팝업 스폰
        TrySpawnGridPopups(result, breakdown.finalDamage);

        // 4) 폭탄 해체 이벤트
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

        // 5) 승패 체크
        CheckWinLose();

        // 6) 디버그 이벤트
        try { GameEvents.RaiseOnDamageCalculationResolved(breakdown, origin); } catch { }
    }
    #endregion
}