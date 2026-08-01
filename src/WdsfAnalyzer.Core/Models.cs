namespace WdsfAnalyzer.Core;

public sealed record AthleteProfile(Guid AthleteId, string Min, string Name, string Country, string AgeGroup, Uri ProfileUrl);

public sealed record Partnership(string PartnerName, string Country, DateOnly? Joined, string Status);

public sealed record CompetitionEntry(
    string Id,
    DateOnly Date,
    string Status,
    int? Placement,
    int? Points,
    string Type,
    string Discipline,
    string AgeClass,
    string Location,
    Uri RankingUrl,
    Uri ResultsUrl,
    Uri MarksUrl,
    Uri FinalUrl,
    Uri ScoresUrl);

public sealed record CoupleAnalysis(
    AthleteProfile Athlete,
    Partnership? Partnership,
    IReadOnlyList<CompetitionEntry> Competitions,
    int ExcludedCompetitionCount,
    DateTimeOffset LoadedAt,
    bool Refreshed,
    IReadOnlyList<JudgeSummary>? Judges = null);

public sealed record PreliminaryObservation(
    string CompetitionId,
    DateOnly Date,
    string Round,
    string JudgeName,
    string PanelLetter,
    decimal RawValue,
    decimal PanelAverage,
    decimal Deviation);

public sealed record PreliminaryDanceObservation(
    string CompetitionId,
    DateOnly Date,
    string Round,
    string Dance,
    string JudgeName,
    string PanelLetter,
    decimal RawValue,
    decimal PanelMarks);

public sealed record RelativeFinalObservation(
    string CompetitionId,
    DateOnly Date,
    string Dance,
    string JudgeName,
    string PanelLetter,
    decimal JudgeRank,
    decimal AchievedDancePlace,
    decimal Deviation,
    int FinalistCount);

public sealed record AbsoluteFinalObservation(
    string CompetitionId,
    DateOnly Date,
    string Dance,
    string JudgeName,
    string PanelLetter,
    decimal JudgeScore,
    decimal PanelAverage,
    decimal Deviation,
    decimal? DerivedRank,
    decimal AchievedDancePlace,
    int FinalistCount);

public enum JudgeValueKind
{
    Preliminary,
    RelativeFinal
}

public sealed record JudgeCompetitionValue(
    string CompetitionId,
    decimal? PreliminarySupport,
    decimal? FinalSupport,
    int ObservationCount,
    IReadOnlyList<JudgeRoundDetail> Details,
    decimal? FinalDeviation = null);

public sealed record JudgeRoundDetail(
    string Round,
    string? Dance,
    decimal ReferenceValue,
    decimal JudgeValue,
    JudgeValueKind Kind,
    bool IncludedInScore = false);

public sealed record JudgeSummary(
    string Name,
    int CompetitionCount,
    int PreliminaryCompetitionCount,
    int FinalCompetitionCount,
    int ObservationCount,
    decimal? AveragePreliminarySupport,
    decimal? AverageFinalSupport,
    decimal? AverageRelativeFinalDeviation,
    decimal OverallSupport,
    decimal OverallConfidence,
    decimal OverallRanking,
    IReadOnlyDictionary<string, JudgeCompetitionValue> CompetitionValues);

public sealed class WdsfDataException(string message) : Exception(message);