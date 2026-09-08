using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class RankingDatabaseData
{
    public List<RankingRecord> records = new List<RankingRecord>();
}

public sealed class RankingDatabase
{
    const int MaximumRecordCount = 30;
    const int TopDisplayCount = 3;
    const string ResourcePath = "Data/ranking_database";
    const string DatabaseFileName = "ranking_database.json";

    readonly string databasePath;
    RankingDatabaseData data;

    public RankingDatabase()
    {
        databasePath = GetDatabasePath();
        data = LoadDatabase();
    }

    public RankingResult AddRecord(float clearTimeSeconds, int violationCount, int fineAmount)
    {
        DateTime playedAt = DateTime.Now;
        RankingRecord[] previousTopRecords = data.records
            .Where(IsValidRecord)
            .OrderBy(record => record.clearTimeSeconds)
            .ThenBy(record => record.fineAmount)
            .ThenBy(record => record.violationCount)
            .ThenBy(record => record.playedAt, StringComparer.Ordinal)
            .Take(TopDisplayCount)
            .ToArray();
        RankingRecord currentRecord = new RankingRecord
        {
            recordId = Guid.NewGuid().ToString("N"),
            playerName = CreatePlayerName(playedAt),
            playedAt = playedAt.ToString("yyyy-MM-dd'T'HH:mm:sszzz"),
            violationCount = Mathf.Max(0, violationCount),
            fineAmount = Mathf.Max(0, fineAmount),
            clearTimeSeconds = Mathf.Max(0f, clearTimeSeconds)
        };

        List<RankingRecord> rankedRecords = data.records
            .Where(IsValidRecord)
            .Append(currentRecord)
            .OrderBy(record => record.clearTimeSeconds)
            .ThenBy(record => record.fineAmount)
            .ThenBy(record => record.violationCount)
            .ThenBy(record => record.playedAt, StringComparer.Ordinal)
            .ToList();

        int currentRank = rankedRecords.IndexOf(currentRecord) + 1;
        data.records = rankedRecords.Take(MaximumRecordCount).ToList();
        SaveDatabase();

        return new RankingResult(
            currentRecord,
            currentRank,
            previousTopRecords);
    }

    string CreatePlayerName(DateTime playedAt)
    {
        string prefix = playedAt.ToString("MMddHHmm");
        HashSet<string> existingNames = new HashSet<string>(
            data.records.Where(IsValidRecord).Select(record => record.playerName));

        for (int suffix = 0; suffix <= 9; suffix++)
        {
            string candidate = prefix + suffix;
            if (!existingNames.Contains(candidate))
            {
                return candidate;
            }
        }

        return prefix + (playedAt.Second % 10);
    }

    RankingDatabaseData LoadDatabase()
    {
        try
        {
            string json = File.Exists(databasePath)
                ? File.ReadAllText(databasePath)
                : LoadInitialJson();
            RankingDatabaseData loaded = JsonUtility.FromJson<RankingDatabaseData>(json);
            if (loaded != null && loaded.records != null)
            {
                loaded.records = loaded.records
                    .Where(IsValidRecord)
                    .OrderBy(record => record.clearTimeSeconds)
                    .ThenBy(record => record.fineAmount)
                    .ThenBy(record => record.violationCount)
                    .ThenBy(record => record.playedAt, StringComparer.Ordinal)
                    .Take(MaximumRecordCount)
                    .ToList();
                return loaded;
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"[RankingDatabase] ランキングDBを読み込めませんでした: {exception.Message}");
        }

        return new RankingDatabaseData();
    }

    string LoadInitialJson()
    {
        TextAsset initialDatabase = Resources.Load<TextAsset>(ResourcePath);
        return initialDatabase != null ? initialDatabase.text : "{\"records\":[]}";
    }

    void SaveDatabase()
    {
        try
        {
            string directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(databasePath, JsonUtility.ToJson(data, true));
        }
        catch (Exception exception)
        {
            Debug.LogError($"[RankingDatabase] ランキングDBを保存できませんでした: {exception.Message}");
        }
    }

    static bool IsValidRecord(RankingRecord record)
    {
        return record != null &&
            !string.IsNullOrWhiteSpace(record.recordId) &&
            !string.IsNullOrWhiteSpace(record.playerName) &&
            record.clearTimeSeconds >= 0f;
    }

    static string GetDatabasePath()
    {
#if UNITY_EDITOR
        return Path.Combine(Application.dataPath, "Resources", "Data", DatabaseFileName);
#else
        return Path.Combine(Application.persistentDataPath, DatabaseFileName);
#endif
    }
}
