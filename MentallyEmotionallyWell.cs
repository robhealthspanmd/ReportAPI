using System;

public static class MentallyEmotionallyWell
{
    public const double PromisNormalUpper = 55.0;
    public const double StressNormalUpper = 13.0;
    private const double TrendDelta = 1.0;

    public sealed record AiInput(
        AssessmentSummary Assessment,
        DomainSummary Depression,
        DomainSummary Anxiety,
        DomainSummary Stress,
        OpportunityTriggers Triggers,
        SourceValues Sources
    );

    public sealed record AssessmentSummary(
        string OverallStatus,
        string SummaryStatement
    );

    public sealed record DomainSummary(
        string Status,
        string Trend,
        double CurrentScore,
        double? PriorScore
    );

    public sealed record OpportunityTriggers(
        bool DepressionNeedsAttention,
        bool AnxietyNeedsAttention,
        bool StressNeedsAttention,
        bool StressEmphasis
    );

    public sealed record SourceValues(
        string? AssessmentDate
    );

    public static AiInput BuildAiInput(BrainHealth.Inputs inputs)
    {
        var depressionTrend = DetermineTrend(inputs.PromisDepression_8a, inputs.PromisDepressionPrior);
        var anxietyTrend = DetermineTrend(inputs.PromisAnxiety_8a, inputs.PromisAnxietyPrior);
        var stressTrend = DetermineTrend(inputs.PerceivedStressScore, inputs.PerceivedStressScorePrior);

        var depressionStatus = DetermineStatus(inputs.PromisDepression_8a, depressionTrend, PromisNormalUpper);
        var anxietyStatus = DetermineStatus(inputs.PromisAnxiety_8a, anxietyTrend, PromisNormalUpper);
        var stressStatus = DetermineStatus(inputs.PerceivedStressScore, stressTrend, StressNormalUpper);

        bool depressionNeedsAttention = depressionStatus == "Needs Attention";
        bool anxietyNeedsAttention = anxietyStatus == "Needs Attention";
        bool stressNeedsAttention = stressStatus == "Needs Attention";

        string overallStatus = (depressionNeedsAttention || anxietyNeedsAttention || stressNeedsAttention)
            ? "Opportunities Identified"
            : "Optimal";

        string summaryStatement = overallStatus == "Optimal"
            ? "Your depression, anxiety, and stress scores are in healthy ranges, with stable or improving trends. We will continue to monitor over time to ensure they remain at this level."
            : "Your assessment shows opportunities to improve depression, anxiety, or stress levels. Addressing these early can support emotional well-being and protect long-term health.";

        return new AiInput(
            Assessment: new AssessmentSummary(
                OverallStatus: overallStatus,
                SummaryStatement: summaryStatement
            ),
            Depression: new DomainSummary(
                Status: depressionStatus,
                Trend: depressionTrend,
                CurrentScore: inputs.PromisDepression_8a,
                PriorScore: inputs.PromisDepressionPrior
            ),
            Anxiety: new DomainSummary(
                Status: anxietyStatus,
                Trend: anxietyTrend,
                CurrentScore: inputs.PromisAnxiety_8a,
                PriorScore: inputs.PromisAnxietyPrior
            ),
            Stress: new DomainSummary(
                Status: stressStatus,
                Trend: stressTrend,
                CurrentScore: inputs.PerceivedStressScore,
                PriorScore: inputs.PerceivedStressScorePrior
            ),
            Triggers: new OpportunityTriggers(
                DepressionNeedsAttention: depressionNeedsAttention,
                AnxietyNeedsAttention: anxietyNeedsAttention,
                StressNeedsAttention: stressNeedsAttention,
                StressEmphasis: stressNeedsAttention
            ),
            Sources: new SourceValues(
                AssessmentDate: inputs.AssessmentDate
            )
        );
    }

    private static string DetermineTrend(double current, double? prior)
    {
        if (prior is null) return "Unknown";
        if (current <= prior.Value - TrendDelta) return "Improving";
        if (current >= prior.Value + TrendDelta) return "Worsening";
        return "Stable";
    }

    private static string DetermineStatus(double current, string trend, double normalUpper)
    {
        bool elevated = current > normalUpper;
        bool worsening = trend == "Worsening";
        return (elevated || worsening) ? "Needs Attention" : "Optimal";
    }
}
