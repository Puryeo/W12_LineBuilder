using UnityEngine;
using UnityEngine.Serialization;
using System;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using UnityEngine.Scripting;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Bomb: 개별 폭탄 인스턴스(게임오브젝트 또는 Grid 기반).
/// - gridPos: Grid에 배치된 폭탄일 경우 위치를 저장.
/// - isGridBomb: Grid 관리 폭탄 플래그.
/// </summary>
[DisallowMultipleComponent]
public class Bomb : MonoBehaviour
{
    [Tooltip("현재 남은 턴 수")]
    public int timer = 3;

    [Tooltip("타이머 상한 (캡)")]
    public int maxTimer = 6;

    [Tooltip("선택적 식별자(로그용) — 비워두면 GameObject.name+instanceID 사용)")]
    public string bombId;

    [Tooltip("이 폭탄이 GridManager에 배치된 폭탄인지 여부")]
    public Vector2Int gridPos;

    [Tooltip("Grid에 의해 관리되는 폭탄이면 true")]
    public bool isGridBomb = false;

    private void Reset()
    {
        if (string.IsNullOrEmpty(bombId))
            bombId = $"{gameObject.name}-{GetInstanceID()}";
    }

    private void Awake()
    {
        if (string.IsNullOrEmpty(bombId))
            bombId = $"{gameObject.name}-{GetInstanceID()}";

        // BombManager에 등록 (BombManager는 같은 씬에 있어야 함)
        BombManager.Instance?.Register(this);
    }

    private void OnDestroy()
    {
        // BombManager에서 등록 해제
        BombManager.Instance?.Unregister(this);
    }

    public void InitializeGridBomb(Vector2Int pos, int startTimer, int maxT)
    {
        isGridBomb = true;
        gridPos = pos;
        timer = Mathf.Clamp(startTimer, 0, maxT);
        maxTimer = Math.Max(1, maxT);
        bombId = $"{(gameObject.name ?? "GridBomb")}-{gridPos.x},{gridPos.y}-{GetInstanceID()}";
    }

    public void SetTimer(int newTimer)
    {
        timer = Mathf.Clamp(newTimer, 0, maxTimer);
    }

    public void IncrementTimer(int amount = 1)
    {
        int before = timer;
        timer = Mathf.Min(timer + amount, maxTimer);
        if (before != timer)
            Debug.Log($"[Bomb] {bombId} timer {before} -> {timer}", this);
    }

    public override string ToString()
    {
        return $"Bomb[{bombId}] timer={timer}/{maxTimer} grid={gridPos} gridFlag={isGridBomb}";
    }
}