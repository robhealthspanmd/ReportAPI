using System;
using System.Collections.Generic;

public static class LongevityMindset
{
    public const double ResilienceOptimalThreshold = 3.0;
    public const double OptimismOptimalThreshold = 13.0;
    public const double MeaningPresenceOptimalThreshold = 3.5;
    private const double TrendDelta = 1.0;

    public sealed record Result(
        AssessmentResult Assessment,
        StrategyResult? Strategy
    );

    public sealed record AssessmentResult(
        string OverallStatus,
        DomainResult[] Domains,
        string Summary
    );

    public sealed record DomainResult(
        string Domain,
        double? Score,
        string Status,
        string? Trend
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
        double resilienceScore = inputs.BriefResilienceScale / 6.0;
        double? resiliencePrior = inputs.BriefResilienceScalePrior is null ? null : inputs.BriefResilienceScalePrior.Value / 6.0;
        string? resilienceTrend = resiliencePrior is null ? null : DetermineTrend(resilienceScore, resiliencePrior.Value);
        string resilienceStatus = resilienceScore < ResilienceOptimalThreshold ? "Needs Attention" : "Optimal";

        double optimismScore = inputs.LifeOrientationTest_R;
        string? optimismTrend = inputs.LifeOrientationTestPrior is null ? null : DetermineTrend(optimismScore, inputs.LifeOrientationTestPrior.Value);
        string optimismStatus = optimismScore < OptimismOptimalThreshold ? "Needs Attention" : "Optimal";

        double meaningScore = inputs.MeaningInLifeQuestionnaire / 10.0;
        string? meaningTrend = null;
        string meaningStatus = DetermineMeaningStatus(meaningScore);

        bool hasMissing = false;
        bool anyNeedsAttention = resilienceStatus == "Needs Attention"
                                 || optimismStatus == "Needs Attention"
                                 || meaningStatus == "Needs Attention";

        string overallStatus = hasMissing
            ? "Data Missing"
            : anyNeedsAttention ? "Opportunities Identified" : "Optimal";

        string summary = BuildSummary(overallStatus, hasMissing, new[] { resilienceTrend, optimismTrend, meaningTrend });

        var domains = new[]
        {
            new DomainResult(
                Domain: "Resilience",
                Score: resilienceScore,
                Status: resilienceStatus,
                Trend: resilienceTrend
            ),
            new DomainResult(
                Domain: "Optimism",
                Score: optimismScore,
                Status: optimismStatus,
                Trend: optimismTrend
            ),
            new DomainResult(
                Domain: "Meaning in Life",
                Score: meaningScore,
                Status: meaningStatus,
                Trend: meaningTrend
            )
        };

        StrategyResult? strategy = BuildStrategy(resilienceStatus, optimismStatus, meaningStatus, anyNeedsAttention);

        return new Result(
            Assessment: new AssessmentResult(
                OverallStatus: overallStatus,
                Domains: domains,
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

    private static string DetermineMeaningStatus(double meaningScore)
    {
        bool lowMeaning = meaningScore < MeaningPresenceOptimalThreshold;
        return lowMeaning ? "Needs Attention" : "Optimal";
    }

    private static string BuildSummary(string overallStatus, bool hasMissing, IEnumerable<string?> trends)
    {
        if (hasMissing)
        {
            return "Some longevity mindset measures are missing, so we cannot fully interpret resilience, optimism, and meaning at this time.";
        }

        bool hasTrend = false;
        foreach (var trend in trends)
        {
            if (!string.IsNullOrWhiteSpace(trend))
            {
                hasTrend = true;
                break;
            }
        }

        return overallStatus switch
        {
            "Optimal" when !hasTrend =>
                "Your longevity mindset measures are in healthy ranges. " +
                "This suggests that resilience, optimism, and sense of meaning are supporting your ability to adapt, stay engaged, and sustain long-term health behaviors.",
            "Optimal" =>
                "Your longevity mindset measures are in healthy ranges and have remained stable or improved over time. " +
                "This indicates that your resilience, outlook, and sense of purpose are supporting your ability to adapt, recover from challenges, and stay engaged with long-term health goals.",
            _ when !hasTrend =>
                "Your results suggest opportunities to strengthen aspects of your longevity mindset. " +
                "Lower resilience, optimism, or sense of meaning can make long-term health changes harder to sustain, even when motivation is present.",
            _ =>
                "Your results suggest opportunities to strengthen your longevity mindset, with changes compared to prior assessments. " +
                "Shifts in resilience, optimism, or sense of meaning can occur during periods of stress or transition. " +
                "Addressing these areas early may help support long-term well-being and adaptability."
        };
    }

    private static StrategyResult? BuildStrategy(string resilienceStatus, string optimismStatus, string meaningStatus, bool anyNeedsAttention)
    {
        if (!anyNeedsAttention)
        {
            var preserve = "A protective factor worth preserving is resilience and adaptability. " +
                           "Resilience and optimism support consistency and adaptability over time.";
            if (CountNeedsAttention(resilienceStatus, optimismStatus, meaningStatus) >= 2)
            {
                preserve += " Given the long-term nature of your prevention goals, this mindset is particularly important for sustaining progress.";
            }

            return new StrategyResult(new[]
            {
                new OpportunityResult(
                    Domain: "Longevity Mindset",
                    WhyItMatters: "Resilience, optimism, and sense of meaning support consistency and adaptability over time.",
                    Opportunity: preserve
                )
            });
        }

        var opportunities = new List<OpportunityResult>();
        if (resilienceStatus == "Needs Attention")
        {
            opportunities.Add(new OpportunityResult(
                Domain: "Resilience",
                WhyItMatters: "Lower resilience can make stressors feel more overwhelming and slow recovery from challenges.",
                Opportunity: "An important area to strengthen is resilience and adaptability. Improving this capacity supports emotional stability, adaptive coping, and long-term health."
            ));
        }

        if (optimismStatus == "Needs Attention")
        {
            opportunities.Add(new OpportunityResult(
                Domain: "Optimism",
                WhyItMatters: "Lower optimism is associated with higher stress perception and reduced engagement in health-promoting behaviors.",
                Opportunity: "A high-leverage focus for improving this score is to strengthen optimism and future orientation. This can improve motivation, coping, and long-term well-being."
            ));
        }

        if (meaningStatus == "Needs Attention")
        {
            opportunities.Add(new OpportunityResult(
                Domain: "Meaning in Life",
                WhyItMatters: "A reduced sense of meaning or purpose can affect motivation, engagement, and emotional well-being.",
                Opportunity: "An important area to strengthen is sense of meaning and purpose. This supports resilience, fulfillment, and sustained health behaviors."
            ));
        }

        if (CountNeedsAttention(resilienceStatus, optimismStatus, meaningStatus) >= 2)
        {
            opportunities.Add(new OpportunityResult(
                Domain: "Longevity Mindset",
                WhyItMatters: "When multiple mindset domains need attention, follow-through across health goals becomes more difficult.",
                Opportunity: "An area that may amplify progress across other health domains is resilience and adaptability. This foundation can make long-term health strategies feel more sustainable."
            ));
        }

        return new StrategyResult(opportunities.ToArray());
    }

    private static int CountNeedsAttention(string resilienceStatus, string optimismStatus, string meaningStatus)
    {
        int count = 0;
        if (resilienceStatus == "Needs Attention") count++;
        if (optimismStatus == "Needs Attention") count++;
        if (meaningStatus == "Needs Attention") count++;
        return count;
    }
}
