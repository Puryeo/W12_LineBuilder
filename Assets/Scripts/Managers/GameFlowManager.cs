using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 게임의 전체 흐름(정비 -> 전투 -> 보상 -> 다음 라운드)을 제어합니다.
/// SpawnMonstersForCurrentRound()에서 RoundDataSO.enemySpawnEntries를 우선 사용하여
/// 몬스터 프리팹에 패턴 풀을 주입합니다.
/// </summary>
[DefaultExecutionOrder(-100)] // 다른 매니저보다 먼저 초기화
public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    public enum GameState { Preparation, Battle, Victory, GameOver }

    [Header("Rounds")]
    public List<RoundDataSO> rounds; // 인스펙터에서 라운드 데이터 할당
    public int currentRoundIndex = 0;

    [Header("UI References")]
    [Tooltip("정비 단계에서 보여질 인벤토리 패널 (오른쪽 영역)")]
    public GameObject preparationPanel;
    [Tooltip("전투 단계에서 보여질 몬스터 패널 등 (오른쪽 영역)")]
    public GameObject battlePanel;
    [Tooltip("라운드 시작 버튼 (정비 단계에서 사용)")]
    public Button startRoundButton;
    public RewardPopupUI rewardPopup;
    public GameObject gameClearPanel;

    [Header("Managers")]
    public MonsterManager monsterManager;
    public AttributeInventoryUI inventoryUI; // 아래 3번 항목에서 작성

    [Header("Debug Info")]
    public GameState currentState;

    [Header("Initial Equipment Setup")]
    [Tooltip("게임 시작 시 첫 라운드에서 자동으로 장비를 장착할지 여부")]
    public bool autoEquipFirstRound = true;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void Start()
    {
        if (startRoundButton != null)
            startRoundButton.onClick.AddListener(OnStartRoundClicked);

        // 인벤토리 초기화
        if (inventoryUI != null)
        {
            inventoryUI.Initialize();
            inventoryUI.SetupTestItems();
        }

        // 게임 시작 시 첫 라운드는 PreparationPanel을 표시하지 않고 바로 전투 시작
        if (autoEquipFirstRound && currentRoundIndex == 0)
        {
            // 첫 라운드: 자동 장비 장착 후 바로 전투 시작
            AutoEquipInitialAttributes();
            StartBattle();
        }
        else
        {
            // 일반적인 정비 단계 시작 (2라운드 이후)
            StartPreparation();
        }
    }

    /// <summary>
    /// 첫 라운드 시작 시 자동으로 Row에 칼, Column에 방패를 장착합니다.
    /// </summary>
    private void AutoEquipInitialAttributes()
    {
        // GridAttributeMap 찾기
        GridAttributeMap attributeMap = null;
        if (GridManager.Instance != null)
        {
            attributeMap = GridManager.Instance.GetComponent<GridAttributeMap>();
        }

        if (attributeMap == null)
        {
            Debug.LogWarning("[GameFlowManager] GridAttributeMap을 찾을 수 없습니다. 자동 장착을 건너뜁니다.");
            return;
        }

        // Row 슬롯에 칼(WoodSord) 장착
        for (int i = 0; i < 8; i++)
        {
            attributeMap.SetRow(i, AttributeType.WoodSord);
        }

        // Column 슬롯에 방패(WoodShield) 장착
        for (int i = 0; i < 8; i++)
        {
            attributeMap.SetCol(i, AttributeType.WoodShield);
        }

        Debug.Log("[GameFlowManager] 첫 라운드 자동 장비 장착 완료: Row=칼, Column=방패");

        // 모든 GridHeaderSlotUI의 시각적 업데이트
        UpdateAllHeaderSlotVisuals();
    }

    /// <summary>
    /// 씬에 있는 모든 GridHeaderSlotUI의 시각적 표현을 갱신합니다.
    /// </summary>
    private void UpdateAllHeaderSlotVisuals()
    {
        GridHeaderSlotUI[] allSlots = FindObjectsOfType<GridHeaderSlotUI>();
        foreach (var slot in allSlots)
        {
            slot.UpdateVisual();
        }
    }

    /// <summary>
    /// 정비 단계 시작: 몬스터 UI 숨김, 인벤토리 표시, 그리드 슬롯 수정 가능
    /// </summary>
    public void StartPreparation()
    {
        currentState = GameState.Preparation;

        // UI 전환
        if (preparationPanel != null) preparationPanel.SetActive(true);
        if (battlePanel != null) battlePanel.SetActive(false);
        if (startRoundButton != null) startRoundButton.gameObject.SetActive(true);

        Debug.Log($"[GameFlow] Round {currentRoundIndex + 1} Preparation Started.");
    }

    /// <summary>
    /// 라운드 시작 버튼 클릭 시 호출
    /// </summary>
    private void OnStartRoundClicked()
    {
        if (currentState != GameState.Preparation) return;
        StartBattle();
    }

    /// <summary>
    /// 전투 단계 시작: 인벤토리 숨김, 몬스터 스폰, 전투 시작
    /// </summary>
    public void StartBattle()
    {
        currentState = GameState.Battle;

        // UI 전환
        if (preparationPanel != null) preparationPanel.SetActive(false);
        if (battlePanel != null) battlePanel.SetActive(true);
        if (startRoundButton != null) startRoundButton.gameObject.SetActive(false);

        // 자동 폭탄 스폰 비활성화: 몬스터 패턴에 의해 폭탄만 생성되도록 전환
        if (BombManager.Instance != null)
            BombManager.Instance.enableAutoSpawn = false;

        // 몬스터 스폰
        SpawnMonstersForCurrentRound();

        Debug.Log($"[GameFlow] Round {currentRoundIndex + 1} Battle Started.");
    }

    private void SpawnMonstersForCurrentRound()
    {
        if (monsterManager == null) monsterManager = MonsterManager.Instance;

        // 기존 몬스터 정리 (안전장치)
        monsterManager.ClearAllMonsters();

        if (currentRoundIndex < rounds.Count)
        {
            var data = rounds[currentRoundIndex];
            if (data != null && data.enemiesToSpawn != null)
            {
                foreach (var prefab in data.enemiesToSpawn)
                {
                    Instantiate(prefab, monsterManager.uiContainer);
                }
            }
        }
    }

    /// <summary>
    /// 적 전멸 시 호출 (CombatManager 등에서 호출)
    /// </summary>
    public void OnAllMonstersDefeated()
    {
        if (currentState != GameState.Battle) return;

        Debug.Log("[GameFlow] Victory!");
        currentState = GameState.Victory;

        // 1. 그리드 초기화 (블록 및 데이터 제거)
        if (GridManager.Instance != null)
        {
            GridManager.Instance.ClearAllGridEntities();
        }

        if (MonsterManager.Instance != null)
        {
            MonsterManager.Instance.ClearAllMonsters();
        }

        if (BombManager.Instance != null)
        {
            BombManager.Instance.ClearAllBombs();
        }

        if (currentRoundIndex == rounds.Count - 1)
        {
            if (gameClearPanel != null)
            {
                gameClearPanel.SetActive(true);
                currentRoundIndex = 0;
                return;
            }
        }

        if (rewardPopup != null)
        {
            rewardPopup.gameObject.SetActive(true);
            rewardPopup.SetCallBack(OnRewardConfirmed);
        }
        else
        {
            // 팝업이 없으면 그냥 바로 넘어감 (안전장치)
            OnRewardConfirmed();
        }
    }

    /// <summary>
    /// 보상 팝업에서 '확인'을 눌렀을 때 실행되는 콜백
    /// </summary>
    private void OnRewardConfirmed()
    {
        // 다음 라운드 로직 진행
        currentRoundIndex++;

        if (currentRoundIndex < rounds.Count)
        {
            // 핸드 초기화
            if (CardManager.Instance != null)
            {
                CardManager.Instance.ResetHandForNextRound();
            }

            // 2라운드부터는 정비 단계 표시
            StartPreparation();
        }
        else
        {
            // 게임 클리어 처리
            Debug.Log("All rounds finished! Game Clear.");
            if (gameClearPanel != null)
                gameClearPanel.SetActive(true);
        }
    }

    /// <summary>
    /// 플레이어 사망 시
    /// </summary>
    public void OnGameOver()
    {
        currentState = GameState.GameOver;
        // 게임 오버 화면
        // 재시작 버튼 등 활성화
    }
}