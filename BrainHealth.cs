using System;

public static class BrainHealth
{
    public sealed record Inputs(
        double CognitiveFunction,          // BrainCheck percentile (0–100 raw)
        double PromisDepression_8a,         // PROMIS Depression T-score
        double PromisAnxiety_8a,            // PROMIS Anxiety T-score
        double BriefResilienceScale,        // BRS total (6–30)
        double LifeOrientationTest_R,       // B8 (range 0–24)
        double MeaningInLifeQuestionnaire,  // MLQ total (10–70)
        double FlourishingScale,            // Flourishing total (8–56)
        double PromisSleepDisturbance,      // PROMIS Sleep Disturbance T-score
        double PerceivedStressScore,        // PSS raw score (0–40 typical)
        double? CognitiveFunctionPrior,     // BrainCheck percentile prior (optional)
        string? ApoE4Status = null,         // Genetic risk (optional)
        string? FamilyHistoryDementia = null, // Family history (optional)
        double? DementiaOnsetAge = null,    // Family history onset age (optional)
        string? AssessmentDate = null,      // Mental & emotional assessment date (optional)
        double? PromisDepressionPrior = null,
        double? PromisAnxietyPrior = null,
        double? PerceivedStressScorePrior = null,
        double? FlourishingScalePrior = null,
        double? BriefResilienceScalePrior = null,
        double? LifeOrientationTestPrior = null,
        double? MeaningInLifePresence = null,
        double? MeaningInLifeSearch = null,
        double? MeaningInLifePresencePrior = null,
        double? MeaningInLifeSearchPrior = null
    );

    public sealed record Result(
        double CognitivePoints,
        double DepressionPoints,
        double AnxietyPoints,
        double ResiliencePoints,
        double OptimismPoints,
        double MeaningPoints,
        double FlourishingPoints,
        double SleepPoints,
        double StressPoints,
        double TotalScore,
        string Level,
        Subscores Subscores,
        WeightedPoints WeightedPoints,
        Flags Flags,
        InputsNormalized InputsNormalized
    );

    public sealed record Subscores(
        double Cognitive,
        double Depression,
        double Anxiety,
        double Stress,
        double Sleep,
        double Resilience,
        double Optimism,
        double Meaning,
        double Flourishing
    );

    public sealed record WeightedPoints(
        double Cognitive,
        double Depression,
        double Anxiety,
        double Stress,
        double Sleep,
        double Resilience,
        double Optimism,
        double Meaning,
        double Flourishing
    );

    public sealed record Flags(
        bool BraincheckConfirmEvaluate,
        string? BraincheckReason
    );

    public sealed record InputsNormalized(
        double BraincheckPercentileCapped,
        double? BraincheckPercentilePriorCapped
    );

    public static Result Calculate(Inputs x)
    {
        static double Clamp(double v, double min, double max) => v < min ? min : (v > max ? max : v);

        double braincheckPct = Clamp(x.CognitiveFunction, 1, 99);
        double? braincheckPctPrior = x.CognitiveFunctionPrior is null ? null : Clamp(x.CognitiveFunctionPrior.Value, 1, 99);

        double cognitiveSub = braincheckPct;

        double depressionSub = x.PromisDepression_8a <= 55 ? 100 :
            x.PromisDepression_8a < 60 ? 90 :
            x.PromisDepression_8a < 70 ? 40 : 20;

        double anxietySub = x.PromisAnxiety_8a <= 55 ? 100 :
            x.PromisAnxiety_8a < 60 ? 90 :
            x.PromisAnxiety_8a < 70 ? 40 : 20;

        double stressSub = x.PerceivedStressScore <= 13 ? 100 :
            x.PerceivedStressScore <= 26 ? 70 : 30;

        double sleepSub = x.PromisSleepDisturbance <= 55 ? 100 :
            x.PromisSleepDisturbance < 60 ? 90 :
            x.PromisSleepDisturbance < 70 ? 40 : 20;

        double brsMean = x.BriefResilienceScale / 6.0;
        double resilienceSub = brsMean > 4.31 ? 100 :
            brsMean >= 3.0 && brsMean <= 4.3 ? 70 : 30;

        double optimismSub = x.LifeOrientationTest_R >= 19 ? 100 :
            x.LifeOrientationTest_R >= 13 ? 70 : 30;

        double mlqMean = x.MeaningInLifeQuestionnaire / 10.0;
        double meaningSub = mlqMean > 5.5 ? 100 :
            mlqMean >= 3.5 && mlqMean <= 5.4 ? 70 : 30;

        double flourishingSub = x.FlourishingScale > 50 ? 100 :
            x.FlourishingScale >= 38 && x.FlourishingScale <= 49 ? 70 : 30;

        double cognitivePts = cognitiveSub * 0.30;
        double depressionPts = depressionSub * 0.12;
        double anxietyPts = anxietySub * 0.10;
        double stressPts = stressSub * 0.10;
        double sleepPts = sleepSub * 0.10;
        double resiliencePts = resilienceSub * 0.10;
        double optimismPts = optimismSub * 0.06;
        double meaningPts = meaningSub * 0.06;
        double flourishingPts = flourishingSub * 0.06;

        double total =
            cognitivePts + depressionPts + anxietyPts + stressPts + sleepPts +
            resiliencePts + optimismPts + meaningPts + flourishingPts;

        bool braincheckLowFlag = braincheckPct < 20;
        bool braincheckDropFlag = false;
        if (braincheckPctPrior is not null)
        {
            double drop = braincheckPctPrior.Value - braincheckPct;
            braincheckDropFlag = drop >= 20;
        }

        bool confirmEvaluate = braincheckLowFlag || braincheckDropFlag;
        string? confirmReason = confirmEvaluate
            ? (braincheckLowFlag && braincheckDropFlag ? "LOW_AND_DROP" :
               braincheckLowFlag ? "LOW_UNDER_20TH" : "DROP_20_OR_MORE")
            : null;

        string level =
            total >= 85 ? "Optimal" :
            total >= 70 ? "Healthy" :
            "Needs Attention";

        return new Result(
            CognitivePoints: cognitivePts,
            DepressionPoints: depressionPts,
            AnxietyPoints: anxietyPts,
            ResiliencePoints: resiliencePts,
            OptimismPoints: optimismPts,
            MeaningPoints: meaningPts,
            FlourishingPoints: flourishingPts,
            SleepPoints: sleepPts,
            StressPoints: stressPts,
            TotalScore: total,
            Level: level,
            Subscores: new Subscores(
                Cognitive: cognitiveSub,
                Depression: depressionSub,
                Anxiety: anxietySub,
                Stress: stressSub,
                Sleep: sleepSub,
                Resilience: resilienceSub,
                Optimism: optimismSub,
                Meaning: meaningSub,
                Flourishing: flourishingSub
            ),
            WeightedPoints: new WeightedPoints(
                Cognitive: cognitivePts,
                Depression: depressionPts,
                Anxiety: anxietyPts,
                Stress: stressPts,
                Sleep: sleepPts,
                Resilience: resiliencePts,
                Optimism: optimismPts,
                Meaning: meaningPts,
                Flourishing: flourishingPts
            ),
            Flags: new Flags(
                BraincheckConfirmEvaluate: confirmEvaluate,
                BraincheckReason: confirmReason
            ),
            InputsNormalized: new InputsNormalized(
                BraincheckPercentileCapped: braincheckPct,
                BraincheckPercentilePriorCapped: braincheckPctPrior
            )
        );
    }
}
