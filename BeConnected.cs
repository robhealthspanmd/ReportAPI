using System;

public static class BeConnected
{
    private const double OptimalThreshold = 45.0;
    private const double TrendDelta = 1.0;

    public sealed record Result(
        AssessmentResult Assessment,
        StrategyResult? Strategy
    );

    public sealed record AssessmentResult(
        double FlourishingScore,
        string Status,
        string? Trend,
        string Summary
    );

    public sealed record StrategyResult(
        OpportunityResult[] Opportunities
    );

    public sealed record OpportunityResult(
        string Domain,
        string WhyItMatters,
        string Opportunity
    );

    public static Result BuildResult(BrainHealth.Inputs inputs)
    {
        double score = inputs.FlourishingScale;
        double? prior = inputs.FlourishingScalePrior;
        string? trend = prior is null ? null : DetermineTrend(score, prior.Value);

        bool scoreOptimal = score > OptimalThreshold;
        bool trendWorsening = trend == "Worsening";
        bool statusNeedsAttention = !scoreOptimal || trendWorsening;

        string status = statusNeedsAttention ? "Opportunities Identified" : "Optimal";

        string summary = BuildSummary(score, prior, trend, statusNeedsAttention);

        StrategyResult? strategy = statusNeedsAttention
            ? new StrategyResult(new[]
            {
                new OpportunityResult(
                    Domain: "Flourishing",
                    WhyItMatters: "Lower flourishing scores can reflect reduced connection, engagement, or sense of purpose. These factors are closely linked to emotional resilience and physical health.",
                    Opportunity: BuildOpportunity(trendWorsening)
                )
            })
            : null;

        return new Result(
            Assessment: new AssessmentResult(
                FlourishingScore: score,
                Status: status,
                Trend: trend,
                Summary: summary
            ),
            Strategy: strategy
        );
    }

    private static string DetermineTrend(double current, double prior)
    {
        if (current >= prior + TrendDelta) return "Improving";
        if (current <= prior - TrendDelta) return "Worsening";
        return "Stable";
    }

    private static string BuildSummary(double score, double? prior, string? trend, bool needsAttention)
    {
        bool hasPrior = prior is not null;
        bool scoreOptimal = score > OptimalThreshold;
        bool trendWorsening = trend == "Worsening";

        if (scoreOptimal && !hasPrior)
        {
            return "Your Flourishing Scale score is in the optimal range. " +
                   "This means your life currently reflects a strong sense of purpose, satisfaction, and connection. " +
                   "These factors support emotional well-being and resilience and are associated with better physical health and longevity over time.";
        }

        if (scoreOptimal && hasPrior && (trend == "Stable" || trend == "Improving"))
        {
            return "Your Flourishing Scale score is in the optimal range and has remained stable or improved over time. " +
                   "This suggests that your sense of purpose, engagement, and connection is supporting your overall well-being and reinforcing your long-term health and longevity goals.";
        }

        if (!needsAttention && hasPrior)
        {
            return "Your Flourishing Scale score is in the optimal range. " +
                   "These results suggest strong connection, engagement, and sense of purpose that support emotional well-being and long-term health.";
        }

        if (!scoreOptimal && !hasPrior)
        {
            return "Your Flourishing Scale score suggests opportunities to strengthen connection, engagement, or sense of purpose. " +
                   "Flourishing reflects how supported, connected, and fulfilled you feel in daily life. " +
                   "Improving these areas can positively influence emotional health, cognitive resilience, and physical health over time.";
        }

        if (trendWorsening)
        {
            return "Your Flourishing Scale score suggests opportunities to strengthen connection and engagement, and it has declined compared with prior assessments. " +
                   "Changes in flourishing can reflect shifts in relationships, purpose, or life satisfaction. " +
                   "Addressing these areas early may help support resilience and long-term health.";
        }

        return "Your Flourishing Scale score suggests opportunities to strengthen connection, engagement, or sense of purpose. " +
               "Flourishing reflects how supported, connected, and fulfilled you feel in daily life. " +
               "Improving these areas can positively influence emotional health, cognitive resilience, and physical health over time.";
    }

    private static string BuildOpportunity(bool trendWorsening)
    {
        var baseText = "Lower flourishing scores can reflect reduced connection, engagement, or sense of purpose. " +
                       "These factors are closely linked to emotional resilience and physical health. " +
                       "Strengthening connection and meaning represents an opportunity to support your overall healthspan.";

        if (!trendWorsening)
        {
            return baseText;
        }

        return baseText +
               " Because this represents a change from prior assessments, addressing these areas now may help prevent further decline and support recovery.";
    }
}
