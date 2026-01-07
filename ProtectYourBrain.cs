using System;

public static class ProtectYourBrain
{
    public sealed record AiInput(
        BraincheckContext Braincheck,
        CognitiveAssessment Assessment,
        RiskContext Risk,
        OpportunityTriggers Triggers,
        SourceValues Sources
    );

    public sealed record BraincheckContext(
        double PercentileRaw,
        double PercentileCapped,
        double? PercentilePriorRaw,
        double? PercentilePriorCapped,
        double? PercentileDelta,
        bool ConfirmEvaluateFlag,
        string? ConfirmEvaluateReason
    );

    public sealed record CognitiveAssessment(
        string Classification,
        string Trend
    );

    public sealed record RiskContext(
        string BrainRiskCategory,
        string ApoE4Status,
        string FamilyHistoryDementia,
        double? DementiaOnsetAge,
        bool SignificantDecline
    );

    public sealed record OpportunityTriggers(
        bool ImproveCognitiveFunction,
        bool ImproveSleepQuality,
        bool AddressHearingOrVisionLoss,
        bool OptimizeMetabolicHealth,
        bool OptimizeVascularHealth,
        bool ImproveFitness,
        bool ImproveMuscleMass,
        bool ReduceInflammation,
        bool MinimizeToxicExposures
    );

    public sealed record SourceValues(
        double? PromisSleepDisturbance,
        double? PerceivedStressScore,
        double? SystolicBp,
        double? DiastolicBp,
        double? Vo2MaxPercentile,
        double? AppendicularMusclePercentile,
        double? HomaIr,
        double? FastingGlucose,
        double? HemoglobinA1c,
        double? Triglycerides,
        double? VisceralFatPercentile,
        double? HsCrp,
        string? Smoking,
        string? ChewingTobacco,
        string? Vaping,
        string? OtherNicotineUse,
        string? CannabisUse,
        string? AlcoholIntake,
        double? BloodLeadLevel,
        double? BloodMercury,
        string? HearingStatus,
        string? VisionStatus
    );

    public static AiInput BuildAiInput(
        ReportRequest req,
        BrainHealth.Result brainResult,
        PerformanceAge.Result performance,
        PhenoAge.Result pheno,
        Cardiology.Result? cardio)
    {
        double braincheckCapped = Clamp(req.BrainHealth.CognitiveFunction, 1, 99);
        double? braincheckPriorCapped = req.BrainHealth.CognitiveFunctionPrior is null
            ? null
            : Clamp(req.BrainHealth.CognitiveFunctionPrior.Value, 1, 99);
        double? braincheckDelta = braincheckPriorCapped is null ? null : braincheckCapped - braincheckPriorCapped.Value;

        string classification = braincheckCapped >= 75 ? "Above Average"
            : braincheckCapped >= 25 ? "Average"
            : "Below Average";

        string trend = braincheckPriorCapped is null
            ? "Unknown"
            : braincheckCapped > braincheckPriorCapped.Value ? "Improving"
            : braincheckCapped < braincheckPriorCapped.Value ? "Worsening"
            : "Stable";

        var apoE4Status = NormalizeApoE4Status(req.BrainHealth.ApoE4Status);
        var familyHistory = NormalizeFamilyHistory(req.BrainHealth.FamilyHistoryDementia);
        bool significantDecline = braincheckPriorCapped is not null &&
                                  (braincheckPriorCapped.Value - braincheckCapped) >= 20;

        string brainRiskCategory = CalculateBrainRiskCategory(classification, apoE4Status, familyHistory, significantDecline);

        bool improveCognitive = classification == "Below Average" || brainResult.Flags.BraincheckConfirmEvaluate;
        bool improveSleep = IsSleepDisturbance(req.BrainHealth.PromisSleepDisturbance);
        bool addressHearingVision = false;
        bool optimizeMetabolic = IsMetabolicNotOptimal(req.HealthAge);
        bool optimizeVascular = IsVascularNotOptimal(req.HealthAge, cardio);
        bool improveFitness = IsFitnessNotOptimal(req.PerformanceAge, performance);
        bool improveMuscleMass = IsMuscleMassNotOptimal(req.HealthAge);
        bool reduceInflammation = req.PhenoAge.CRP_mg_L >= 1.0;
        bool minimizeToxins = HasToxinExposure(req.ToxinsLifestyle, req.BrainHealth.PerceivedStressScore);

        return new AiInput(
            Braincheck: new BraincheckContext(
                PercentileRaw: req.BrainHealth.CognitiveFunction,
                PercentileCapped: braincheckCapped,
                PercentilePriorRaw: req.BrainHealth.CognitiveFunctionPrior,
                PercentilePriorCapped: braincheckPriorCapped,
                PercentileDelta: braincheckDelta,
                ConfirmEvaluateFlag: brainResult.Flags.BraincheckConfirmEvaluate,
                ConfirmEvaluateReason: brainResult.Flags.BraincheckReason
            ),
            Assessment: new CognitiveAssessment(
                Classification: classification,
                Trend: trend
            ),
            Risk: new RiskContext(
                BrainRiskCategory: brainRiskCategory,
                ApoE4Status: apoE4Status,
                FamilyHistoryDementia: familyHistory,
                DementiaOnsetAge: req.BrainHealth.DementiaOnsetAge,
                SignificantDecline: significantDecline
            ),
            Triggers: new OpportunityTriggers(
                ImproveCognitiveFunction: improveCognitive,
                ImproveSleepQuality: improveSleep,
                AddressHearingOrVisionLoss: addressHearingVision,
                OptimizeMetabolicHealth: optimizeMetabolic,
                OptimizeVascularHealth: optimizeVascular,
                ImproveFitness: improveFitness,
                ImproveMuscleMass: improveMuscleMass,
                ReduceInflammation: reduceInflammation,
                MinimizeToxicExposures: minimizeToxins
            ),
            Sources: new SourceValues(
                PromisSleepDisturbance: req.BrainHealth.PromisSleepDisturbance,
                PerceivedStressScore: req.BrainHealth.PerceivedStressScore,
                SystolicBp: req.HealthAge.SystolicBP,
                DiastolicBp: req.HealthAge.DiastolicBP,
                Vo2MaxPercentile: req.PerformanceAge.Vo2MaxPercentile,
                AppendicularMusclePercentile: req.HealthAge.AppendicularMusclePercentile,
                HomaIr: req.HealthAge.HomaIr,
                FastingGlucose: req.HealthAge.FastingGlucose_mg_dL,
                HemoglobinA1c: req.HealthAge.HemoglobinA1c,
                Triglycerides: req.HealthAge.Triglycerides_mg_dL,
                VisceralFatPercentile: req.HealthAge.VisceralFatPercentile,
                HsCrp: req.PhenoAge.CRP_mg_L,
                Smoking: req.ToxinsLifestyle.Smoking,
                ChewingTobacco: req.ToxinsLifestyle.ChewingTobacco,
                Vaping: req.ToxinsLifestyle.Vaping,
                OtherNicotineUse: req.ToxinsLifestyle.OtherNicotineUse,
                CannabisUse: req.ToxinsLifestyle.CannabisUse,
                AlcoholIntake: req.ToxinsLifestyle.AlcoholIntake,
                BloodLeadLevel: req.ToxinsLifestyle.BloodLeadLevel,
                BloodMercury: req.ToxinsLifestyle.BloodMercury,
                HearingStatus: null,
                VisionStatus: null
            )
        );
    }

    private static double Clamp(double v, double min, double max) => v < min ? min : (v > max ? max : v);

    private static string NormalizeApoE4Status(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Unknown";
        var lowered = value.Trim().ToLowerInvariant();
        if (lowered.Contains("homo")) return "ApoE4 homozygous";
        if (lowered.Contains("hetero")) return "ApoE4 heterozygous";
        if (lowered.Contains("not") || lowered.Contains("unknown")) return "Unknown";
        return value.Trim();
    }

    private static string NormalizeFamilyHistory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Unknown";
        var lowered = value.Trim().ToLowerInvariant();
        if (lowered.StartsWith("y")) return "Yes";
        if (lowered.StartsWith("n")) return "No";
        if (lowered.Contains("unknown")) return "Unknown";
        return value.Trim();
    }

    private static string CalculateBrainRiskCategory(
        string classification,
        string apoE4Status,
        string familyHistory,
        bool significantDecline)
    {
        bool hasGeneticRisk = apoE4Status == "ApoE4 heterozygous" || apoE4Status == "ApoE4 homozygous";
        bool isHomozygous = apoE4Status == "ApoE4 homozygous";
        bool hasFamilyRisk = familyHistory == "Yes";

        if (classification == "Below Average" || isHomozygous || significantDecline)
        {
            return "Higher Risk";
        }

        if ((classification == "Above Average" || classification == "Average") && (hasGeneticRisk || hasFamilyRisk))
        {
            return "Intermediate Risk";
        }

        return "Lower Risk";
    }

    private static bool IsSleepDisturbance(double? value)
    {
        if (value is null) return false;
        var score = value.Value;
        if (score <= 10)
        {
            return score >= 6;
        }
        return score >= 60;
    }

    private static bool IsMetabolicNotOptimal(HealthAge.Inputs inputs)
    {
        if (inputs.HomaIr is not null && inputs.HomaIr.Value >= 2.0) return true;
        if (inputs.FastingGlucose_mg_dL is not null && inputs.FastingGlucose_mg_dL.Value >= 100) return true;
        if (inputs.HemoglobinA1c is not null && inputs.HemoglobinA1c.Value >= 5.7) return true;
        if (inputs.Triglycerides_mg_dL is not null && inputs.Triglycerides_mg_dL.Value >= 150) return true;
        if (inputs.VisceralFatPercentile is not null && inputs.VisceralFatPercentile.Value >= 75) return true;
        return false;
    }

    private static bool IsVascularNotOptimal(HealthAge.Inputs inputs, Cardiology.Result? cardio)
    {
        if (inputs.SystolicBP is not null && inputs.SystolicBP.Value >= 130) return true;
        if (inputs.DiastolicBP is not null && inputs.DiastolicBP.Value >= 80) return true;
        if (cardio is not null && !string.Equals(cardio.RiskCategory, "LOW", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return false;
    }

    private static bool IsFitnessNotOptimal(PerformanceAge.Inputs inputs, PerformanceAge.Result result)
    {
        if (inputs.Vo2MaxPercentile is not null && inputs.Vo2MaxPercentile.Value < 75) return true;
        return result.DeltaVsAgeYears > 0;
    }

    private static bool IsMuscleMassNotOptimal(HealthAge.Inputs inputs)
    {
        if (inputs.AppendicularMusclePercentile is not null && inputs.AppendicularMusclePercentile.Value < 50) return true;
        return false;
    }

    private static bool HasToxinExposure(ToxinsLifestyle.Inputs inputs, double? perceivedStressScore)
    {
        var eval = ToxinsLifestyle.Evaluate(inputs, perceivedStressScore);
        return eval.Exposures.Count > 0;
    }
}
