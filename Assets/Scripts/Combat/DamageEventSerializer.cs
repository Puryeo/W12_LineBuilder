using System;
using System.IO;
using UnityEngine;

public static class DamageEventSerializer
{
    // 런타임/디버그용 토글: 필요 시 false로 끌 수 있음
    public static bool Enabled = true;

    // 파일명: persistentDataPath 하위에 저장 (jsonl)
    private static string FilePath
    {
        get
        {
            string dir = Path.Combine(Application.persistentDataPath, "DamageLogs");
            try
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            }
            catch (Exception)
            {
                // 디렉토리 생성 실패해도 이후 File IO에서 예외가 처리됩니다.
            }
            return Path.Combine(dir, "damage_events.jsonl");
        }
    }

    public static void AppendRecord(DamageEventRecord record)
    {
        if (!Enabled || record == null) return;

        try
        {
            string json = JsonUtility.ToJson(record);
            File.AppendAllText(FilePath, json + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DamageEventSerializer] Failed to append record: {ex}");
        }
    }

    // 유틸: 테스트/디버그용으로 현재 로그 파일을 읽어 반환합니다. 파일이 없으면 빈 문자열 반환.
    public static string ReadAll()
    {
        try
        {
            if (!File.Exists(FilePath)) return string.Empty;
            return File.ReadAllText(FilePath);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DamageEventSerializer] Failed to read records: {ex}");
            return string.Empty;
        }
    }

    // 유틸: 로그 파일 삭제 (디버그/테스트에서 사용)
    public static void ClearLog()
    {
        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DamageEventSerializer] Failed to clear log: {ex}");
        }
    }
}