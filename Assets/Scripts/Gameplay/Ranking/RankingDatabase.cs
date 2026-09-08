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
        if (EnsureUniquePlayerNames(data.records))
        {
            SaveDatabase();
        }
    }

    public RankingResult AddRecord(float clearTimeSeconds, int violationCount, int fineAmount)
    {
        return CreateResult(clearTimeSeconds, violationCount, fineAmount, true);
    }

    public RankingResult CreatePreviewRecord(float clearTimeSeconds, int violationCount, int fineAmount)
    {
        return CreateResult(clearTimeSeconds, violationCount, fineAmount, false);
    }

    RankingResult CreateResult(float clearTimeSeconds, int violationCount, int fineAmount, bool saveRecord)
    {
        DateTime playedAt = DateTime.Now;
        RankingRecord[] previousTopRecords = GetRankedRecords(data.records)
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

        List<RankingRecord> rankedRecords = GetRankedRecords(data.records.Append(currentRecord));
        int currentRank = rankedRecords.IndexOf(currentRecord) + 1;

        if (saveRecord)
        {
            data.records = rankedRecords.Take(MaximumRecordCount).ToList();
            SaveDatabase();
        }

        return new RankingResult(
            currentRecord,
            currentRank,
            previousTopRecords,
            saveRecord);
    }

    string CreatePlayerName(DateTime playedAt)
    {
        string prefix = playedAt.ToString("MMddHHmm");
        HashSet<string> existingNames = new HashSet<string>(
            data.records.Where(IsValidRecord).Select(record => record.playerName));

        for (int suffix = 0; ; suffix++)
        {
            string candidate = prefix + suffix;
            if (!existingNames.Contains(candidate))
            {
                return candidate;
            }
        }
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

    static List<RankingRecord> GetRankedRecords(IEnumerable<RankingRecord> records)
    {
        return records
            .Where(IsValidRecord)
            .OrderBy(record => record.clearTimeSeconds)
            .ToList();
    }

    static bool EnsureUniquePlayerNames(IEnumerable<RankingRecord> records)
    {
        bool changed = false;
        HashSet<string> usedNames = new HashSet<string>();

        foreach (RankingRecord record in records)
        {
            if (usedNames.Add(record.playerName))
            {
                continue;
            }

            string prefix = GetPlayerNamePrefix(record);
            for (int suffix = 0; ; suffix++)
            {
                string candidate = prefix + suffix;
                if (!usedNames.Add(candidate))
                {
                    continue;
                }

                record.playerName = candidate;
                changed = true;
                break;
            }
        }

        return changed;
    }

    static string GetPlayerNamePrefix(RankingRecord record)
    {
        if (DateTimeOffset.TryParse(record.playedAt, out DateTimeOffset playedAt))
        {
            return playedAt.ToLocalTime().ToString("MMddHHmm");
        }

        return record.playerName.Length >= 8
            ? record.playerName.Substring(0, 8)
            : "00000000";
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
