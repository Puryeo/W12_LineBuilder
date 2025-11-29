using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 입력 차단 관리 시스템
/// 여러 시스템에서 독립적으로 입력을 차단하고 해제할 수 있도록 관리
/// </summary>
public class InputBlocker : MonoBehaviour
{
    public static InputBlocker Instance { get; private set; }

    private readonly HashSet<string> _blockReasons = new HashSet<string>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// 특정 사유로 입력 차단
    /// </summary>
    public void BlockInput(string reason)
    {
        if (string.IsNullOrEmpty(reason))
        {
            Debug.LogWarning("[InputBlocker] BlockInput called with empty reason");
            return;
        }

        bool wasBlocked = _blockReasons.Count > 0;
        _blockReasons.Add(reason);

        if (!wasBlocked)
        {
            Debug.Log($"[InputBlocker] Input blocked by: {reason}");
        }
        else
        {
            Debug.Log($"[InputBlocker] Additional block reason added: {reason} (total: {_blockReasons.Count})");
        }
    }

    /// <summary>
    /// 특정 사유의 입력 차단 해제
    /// </summary>
    public void UnblockInput(string reason)
    {
        if (string.IsNullOrEmpty(reason))
        {
            Debug.LogWarning("[InputBlocker] UnblockInput called with empty reason");
            return;
        }

        _blockReasons.Remove(reason);

        if (_blockReasons.Count == 0)
        {
            Debug.Log($"[InputBlocker] Input fully unblocked (reason: {reason})");
        }
        else
        {
            Debug.Log($"[InputBlocker] Block reason removed: {reason} (remaining: {_blockReasons.Count})");
        }
    }

    /// <summary>
    /// 현재 입력이 차단되었는지 확인
    /// </summary>
    public bool IsInputBlocked()
    {
        return _blockReasons.Count > 0;
    }

    /// <summary>
    /// 모든 입력 차단 강제 해제 (디버그/비상용)
    /// </summary>
    public void ForceUnblockAll()
    {
        Debug.LogWarning("[InputBlocker] Force unblocking all input (this should only be used for debugging)");
        _blockReasons.Clear();
    }

    /// <summary>
    /// 현재 차단 사유 목록 반환 (디버그용)
    /// </summary>
    public IReadOnlyCollection<string> GetBlockReasons()
    {
        return _blockReasons;
    }
}
