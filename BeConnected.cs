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

        StrategyResult? strategy = BuildStrategy(statusNeedsAttention, trendWorsening, inputs);

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
            return "Your Flourishing score is in the optimal range. " +
                   "This suggests that your current level of connection, engagement, and sense of purpose is supporting your emotional well-being and overall health, rather than working against it.";
        }

        if (scoreOptimal && hasPrior && (trend == "Stable" || trend == "Improving"))
        {
            return "Your Flourishing score is in the optimal range and has remained stable or improved over time. " +
                   "This indicates that connection and engagement are acting as protective factors for your long-term healthspan.";
        }

        if (!needsAttention && hasPrior)
        {
            return "Your Flourishing score is in the optimal range. " +
                   "This indicates that connection and engagement are acting as protective factors for your long-term healthspan.";
        }

        if (!scoreOptimal && !hasPrior)
        {
            return "Your Flourishing score suggests opportunities to strengthen connection, engagement, or sense of purpose. " +
                   "Lower flourishing can affect motivation, resilience, and long-term health, even when other areas of health appear strong.";
        }

        if (trendWorsening)
        {
            return "Your Flourishing Scale score suggests opportunities to strengthen connection and engagement, and it has declined compared with prior assessments. " +
                   "Changes in flourishing can reflect shifts in relationships, purpose, or life satisfaction. " +
                   "Addressing these areas early may help support resilience and long-term health.";
        }

        return "Your Flourishing score suggests opportunities to strengthen connection, engagement, or sense of purpose. " +
               "Lower flourishing can affect motivation, resilience, and long-term health, even when other areas of health appear strong.";
    }

    private static StrategyResult? BuildStrategy(bool needsAttention, bool trendWorsening, BrainHealth.Inputs inputs)
    {
        if (!needsAttention)
        {
            var preserve = "A protective factor worth preserving is connection and engagement. " +
                           "Your current level of connection is a protective factor that supports resilience and long-term health.";
            if (HasElevatedStress(inputs))
            {
                preserve += " Given your current stress profile, preserving strong connection may be especially important for you.";
            }

            return new StrategyResult(new[]
            {
                new OpportunityResult(
                    Domain: "Flourishing",
                    WhyItMatters: "Connection and engagement act as protective factors that support resilience and long-term health.",
                    Opportunity: preserve
                )
            });
        }

        var strengthen = "An important area to strengthen is connection and engagement. " +
                         "Improving your Flourishing score represents an opportunity to enhance resilience, motivation, and adherence to other health-protective behaviors.";
        if (HasElevatedStressOrDepression(inputs))
        {
            strengthen += " Given your current stress or emotional burden, strengthening connection may be a particularly high-leverage way to support recovery and overall health.";
        }

        if (trendWorsening)
        {
            strengthen += " Because this represents a change from prior assessments, addressing these areas now may help prevent further decline and support recovery.";
        }

        return new StrategyResult(new[]
        {
            new OpportunityResult(
                Domain: "Flourishing",
                WhyItMatters: "Lower flourishing can affect motivation, resilience, and long-term health, even when other areas appear strong.",
                Opportunity: strengthen
            )
        });
    }

    private static bool HasElevatedStress(BrainHealth.Inputs inputs)
    {
        return inputs.PerceivedStressScore > 13;
    }

    private static bool HasElevatedStressOrDepression(BrainHealth.Inputs inputs)
    {
        return inputs.PerceivedStressScore > 13 || inputs.PromisDepression_8a > 55;
    }
}
