using System;
using System.Collections.Generic;

public static class LongevityMindset
{
    public const double ResilienceOptimalThreshold = 3.0;
    public const double OptimismOptimalThreshold = 13.0;
    public const double MeaningPresenceOptimalThreshold = 3.5;
    public const double MeaningSearchHighThreshold = 5.5;
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

        double? presenceScore = inputs.MeaningInLifePresence;
        double? presenceMean = presenceScore is null ? null : presenceScore.Value / 5.0;
        double? searchScore = inputs.MeaningInLifeSearch;
        double? searchMean = searchScore is null ? null : searchScore.Value / 5.0;

        double? presencePrior = inputs.MeaningInLifePresencePrior is null ? null : inputs.MeaningInLifePresencePrior.Value / 5.0;
        double? searchPrior = inputs.MeaningInLifeSearchPrior is null ? null : inputs.MeaningInLifeSearchPrior.Value / 5.0;
        string? meaningTrend = presencePrior is null && searchPrior is null
            ? null
            : DetermineMeaningTrend(presenceMean, presencePrior, searchMean, searchPrior);

        string meaningStatus = DetermineMeaningStatus(presenceMean, searchMean);

        bool hasMissing = presenceScore is null || searchScore is null;
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
                Score: presenceMean,
                Status: meaningStatus,
                Trend: meaningTrend
            )
        };

        StrategyResult? strategy = anyNeedsAttention
            ? new StrategyResult(BuildOpportunities(resilienceStatus, optimismStatus, meaningStatus, overallStatus))
            : null;

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

    private static string DetermineMeaningTrend(
        double? presenceMean,
        double? presencePrior,
        double? searchMean,
        double? searchPrior)
    {
        if (presenceMean is null && searchMean is null) return "Unknown";
        if (presencePrior is null && searchPrior is null) return "Unknown";

        bool presenceImproved = presenceMean is not null && presencePrior is not null && presenceMean >= presencePrior + TrendDelta;
        bool presenceWorsened = presenceMean is not null && presencePrior is not null && presenceMean <= presencePrior - TrendDelta;
        bool searchWorsened = searchMean is not null && searchPrior is not null && searchMean >= searchPrior + TrendDelta;
        bool searchImproved = searchMean is not null && searchPrior is not null && searchMean <= searchPrior - TrendDelta;

        if (presenceImproved || searchImproved) return "Improving";
        if (presenceWorsened || searchWorsened) return "Worsening";
        return "Stable";
    }

    private static string DetermineMeaningStatus(double? presenceMean, double? searchMean)
    {
        if (presenceMean is null || searchMean is null) return "Data Missing";
        bool lowPresence = presenceMean < MeaningPresenceOptimalThreshold;
        bool highSearchLowPresence = searchMean >= MeaningSearchHighThreshold && presenceMean < MeaningSearchHighThreshold;
        return (lowPresence || highSearchLowPresence) ? "Needs Attention" : "Optimal";
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
                "This suggests you currently approach life with resilience, optimism, and a sense of meaning—qualities that support adaptability, engagement, and long-term health as you age.",
            "Optimal" =>
                "Your longevity mindset measures are in healthy ranges and have remained stable or improved over time. " +
                "This indicates that your resilience, outlook, and sense of purpose are supporting your ability to adapt, recover from challenges, and stay engaged with long-term health goals.",
            _ when !hasTrend =>
                "Your results suggest opportunities to strengthen aspects of your longevity mindset. " +
                "Longevity mindset reflects how resilient, optimistic, and purposeful you feel. " +
                "Improving these areas can support emotional well-being, recovery from stress, and healthy aging over time.",
            _ =>
                "Your results suggest opportunities to strengthen your longevity mindset, with changes compared to prior assessments. " +
                "Shifts in resilience, optimism, or sense of meaning can occur during periods of stress or transition. " +
                "Addressing these areas early may help support long-term well-being and adaptability."
        };
    }

    private static OpportunityResult[] BuildOpportunities(string resilienceStatus, string optimismStatus, string meaningStatus, string overallStatus)
    {
        var opportunities = new List<OpportunityResult>();

        if (resilienceStatus == "Needs Attention")
        {
            opportunities.Add(new OpportunityResult(
                Domain: "Resilience",
                WhyItMatters: "Lower resilience can make stressors feel more overwhelming and slow recovery from challenges.",
                Opportunity: "Strengthening resilience supports emotional stability, adaptive coping, and long-term health."
            ));
        }

        if (optimismStatus == "Needs Attention")
        {
            opportunities.Add(new OpportunityResult(
                Domain: "Optimism",
                WhyItMatters: "Lower optimism is associated with higher stress perception and reduced engagement in health-promoting behaviors.",
                Opportunity: "Supporting a more optimistic outlook can improve motivation, coping, and long-term well-being."
            ));
        }

        if (meaningStatus == "Needs Attention")
        {
            opportunities.Add(new OpportunityResult(
                Domain: "Meaning in Life",
                WhyItMatters: "A reduced sense of meaning or purpose can affect motivation, engagement, and emotional well-being.",
                Opportunity: "Strengthening clarity of purpose may support resilience, fulfillment, and sustained health behaviors."
            ));
        }

        return opportunities.ToArray();
    }
}
