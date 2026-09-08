using System;

[Serializable]
public sealed class RankingRecord
{
    public string recordId;
    public string playerName;
    public string playedAt;
    public int violationCount;
    public int fineAmount;
    public float clearTimeSeconds;
}

public sealed class RankingResult
{
    public RankingRecord CurrentRecord { get; }
    public int CurrentRank { get; }
    public RankingRecord[] TopRecords { get; }

    public RankingResult(RankingRecord currentRecord, int currentRank, RankingRecord[] topRecords)
    {
        CurrentRecord = currentRecord;
        CurrentRank = currentRank;
        TopRecords = topRecords;
    }
}
