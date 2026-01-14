using System;
using System.Text.Json.Serialization;

public static class Cardiology
{
    // -----------------------------
    // Modifiable Heart Health (0–70)
    // -----------------------------
    public static int? CalculateModifiableHeartHealthScore(
        HealthAge.Inputs health,
        PerformanceAge.Inputs performance,
        PhenoAge.Inputs pheno,
        Inputs cardio)
    {
        if (health is null || performance is null || pheno is null || cardio is null)
            return null;

        int? bpScore = ScoreBloodPressure(health.SystolicBP, health.DiastolicBP);
        int? nonHdlScore = ScoreNonHdl(health.NonHdlMgDl, cardio.CoronaryPlaqueSeverity);
        int? homaScore = ScoreHomaIr(GetHomaIr(health));
        int? fitnessScore = ScoreFitnessPercentile(performance.Vo2MaxPercentile);
        int? visceralFatScore = ScoreVisceralFatPercentile(health.VisceralFatPercentile);
        int? leanToFatScore = ScoreLeanToFatRatio(
            health.Sex,
            health.TotalLeanMassPerHeight,
            health.TotalFatMassPerHeight,
            health.TotalLeanMass,
            health.TotalFatMass);
        int? crpScore = ScoreHsCrp(pheno.CRP_mg_L);

        if (!bpScore.HasValue ||
            !nonHdlScore.HasValue ||
            !homaScore.HasValue ||
            !fitnessScore.HasValue ||
            !visceralFatScore.HasValue ||
            !leanToFatScore.HasValue ||
            !crpScore.HasValue)
        {
            return null;
        }

        return bpScore.Value +
               nonHdlScore.Value +
               homaScore.Value +
               fitnessScore.Value +
               visceralFatScore.Value +
               leanToFatScore.Value +
               crpScore.Value;
    }
    // -----------------------------
    // INPUTS (matches frontend payload)
    // -----------------------------
    public sealed record Inputs
    {
        // Plaque (qualitative)
        [JsonPropertyName("carotidPlaqueSeverity")]
        public string? CarotidPlaqueSeverity { get; init; }     // "none" | "mild" | "moderate" | "severe" (free text allowed)

        [JsonPropertyName("coronaryPlaqueSeverity")]
        public string? CoronaryPlaqueSeverity { get; init; }    // "none" | "mild" | "moderate" | "severe" (free text allowed)

        // Coronary calcium
        [JsonPropertyName("cacScore")]
        public double? CacScore { get; init; }

        [JsonPropertyName("cacPercentile")]
        public double? CacPercentile { get; init; }

        // CTA / angiogram (front-end provides both numeric stenosis and an overall qualitative string)
        [JsonPropertyName("ctaMaxStenosisPercent")]
        public double? CtaMaxStenosisPercent { get; init; }

        [JsonPropertyName("ctaOverallResult")]
        public string? CtaOverallResult { get; init; }          // "low" | "moderate" | "high" | "severe" (free text allowed)

        // Functional tests (legacy qualitative)
        [JsonPropertyName("treadmillOverallResult")]
        public string? TreadmillOverallResult { get; init; }    // "low" | "moderate" | "high" | "severe" (free text allowed)

        [JsonPropertyName("echoOverallResult")]
        public string? EchoOverallResult { get; init; }         // "low" | "moderate" | "high" | "severe" (free text allowed)

        [JsonPropertyName("echoDetails")]
        public string? EchoDetails { get; init; }

        // Clinical history (legacy + used for "SEVERE" override in v1 behavior)
        [JsonPropertyName("hasClinicalAscVDHistory")]
        public bool? HasClinicalAscVDHistory { get; init; }

        [JsonPropertyName("clinicalAscVDHistoryDetails")]
        public string? ClinicalAscVDHistoryDetails { get; init; }

        [JsonPropertyName("hasFamilyHistoryPrematureAscVD")]
        public bool? HasFamilyHistoryPrematureAscVD { get; init; }

        [JsonPropertyName("familyHistoryPrematureAscVDDetails")]
        public string? FamilyHistoryPrematureAscVDDetails { get; init; }

        [JsonPropertyName("lipoproteina")]
        public int? Lipoproteina { get; init; }

        [JsonPropertyName("apoB")]
        public double? ApoB { get; init; }

        // Clinician notes / misc (frontend sends these as strings)
        [JsonPropertyName("specificCardiologyInstructions")]
        public string? SpecificCardiologyInstructions { get; init; }

        [JsonPropertyName("ecgDetails")]
        public string? EcgDetails { get; init; }

        [JsonPropertyName("abdominalAortaScreening")]
        public string? AbdominalAortaScreening { get; init; }

        [JsonPropertyName("ettInterpretation")]
        public string? EttInterpretation { get; init; }

        [JsonPropertyName("ettFac")]
        public string? EttFac { get; init; }

        // CTA plaque details (strings)
        // NOTE: frontend currently sends a misspelled key "ctaPlaquQuantification".
        [JsonPropertyName("ctaPlaquQuantification")]
        public string? CtaPlaquQuantification { get; init; }

        // Also accept correct spelling for future-proofing
        [JsonPropertyName("ctaPlaqueQuantification")]
        public string? CtaPlaqueQuantification { get; init; }

        [JsonPropertyName("ctaSoftPlaque")]
        public string? CtaSoftPlaque { get; init; }

        [JsonPropertyName("ctaCalcifiedPlaque")]
        public string? CtaCalcifiedPlaque { get; init; }

        [JsonPropertyName("hardSoftPlaqueRatio")]
        public string? HardSoftPlaqueRatio { get; init; }

        // -----------------------------
        // Optional (v3.2 scoring) inputs
        // -----------------------------
        // These are NOT currently in the provided frontend builder, but the scoring model supports them.
        // They can be added to the frontend later without backend changes.

        [JsonPropertyName("ejectionFractionPercent")]
        public double? EjectionFractionPercent { get; init; }   // EF %

        [JsonPropertyName("heartStructureSeverity")]
        public string? HeartStructureSeverity { get; init; }    // "none" | "mild" | "moderate" | "severe"

        [JsonPropertyName("dukeTreadmillScore")]
        public double? DukeTreadmillScore { get; init; }        // numeric Duke treadmill score

        [JsonPropertyName("ecgSeverity")]
        public string? EcgSeverity { get; init; }               // "normal" | "mild" | "moderate" (AF/flutter) | "lbbb"

        // Optional: modifiable inputs, if another layer computes them upstream.
        [JsonPropertyName("modifiableHeartHealthScore")]
        public int? ModifiableHeartHealthScore { get; init; }   // 0–70
    }

    // -----------------------------
    // OUTPUTS
    // -----------------------------
    public sealed record Result
    {
        // v3.2 fields (new)
        public int BaselineHeartHealthScore { get; init; }          // 0–30 (risk-defining)
        public int PlaqueScore { get; init; }                       // 0–18
        public int CardiacPhysiologyScore { get; init; }            // 0–12
        public string BaselineRiskCategory { get; init; } = "Unknown";  // Low/Mild/Moderate/High
        public string VascularHealthStatus { get; init; } = "Unknown";
        public string CardiacPhysiologyStatus { get; init; } = "Unknown";

        public int? ModifiableHeartHealthScore { get; init; }       // 0–70 (optional at this layer)
        public int HeartHealthScore { get; init; }                  // Baseline(0–30) + Modifiable(0–70), clamped 0–100
        public bool HeartHealthScoreIsPartial { get; init; }        // true if ModifiableHeartHealthScore missing at this layer

        public string RiskExplanation { get; init; } = "";

        // v1 compatibility fields (kept so the rest of the codebase compiles unchanged)
        public string RiskCategory { get; init; } = "LOW";          // LOW/MILD/MODERATE/SEVERE
        public bool TriggeredByClinicalHistory { get; init; }
        public bool TriggeredBySevereFinding { get; init; }
        public bool TriggeredByModerateFinding { get; init; }
        public bool TriggeredByMildFinding { get; init; }
    }

    // -----------------------------
    // V3.2 Calculation
    // -----------------------------
    public static Result Calculate(Inputs x)
    {
        x ??= new Inputs();

        // ---- Normalize inputs ----
        var carotid = ParseSeverity(x.CarotidPlaqueSeverity);
        var coronary = ParseSeverity(x.CoronaryPlaqueSeverity);

        var ctaQual = ParseQual(x.CtaOverallResult);

        var cacScore = ClampOrNull(x.CacScore, 0, 100000);
        var cacPct = ClampOrNull(x.CacPercentile, 0, 100);

        var ctaStenosis = ClampOrNull(x.CtaMaxStenosisPercent, 0, 100);

        // ---- Plaque evidence rules (v3.2 intent) ----
        // "Any plaque" triggers: any imaging evidence of plaque.
        // "Moderate+ plaque" triggers:
        //  - Moderate+ plaque by coronary/carotid severity
        //  - CAC percentile > 25
        //  - CTA/angiogram moderate/severe OR stenosis >= 25%
        bool anyPlaque =
            carotid >= Sev.Mild ||
            coronary >= Sev.Mild ||
            (cacScore.HasValue && cacScore.Value > 0) ||
            (cacPct.HasValue && cacPct.Value > 0) ||
            (ctaStenosis.HasValue && ctaStenosis.Value >= 1) ||
            ctaQual is Qual.Low or Qual.Moderate or Qual.High or Qual.Severe;

        bool moderatePlusPlaque =
            carotid >= Sev.Moderate ||
            coronary >= Sev.Moderate ||
            (cacPct.HasValue && cacPct.Value > 25) ||
            (ctaStenosis.HasValue && ctaStenosis.Value >= 25) ||
            ctaQual is Qual.Moderate or Qual.High or Qual.Severe;

        int plaqueScore = !anyPlaque ? 18 : (moderatePlusPlaque ? 6 : 12);

        // ---- Cardiac physiology scoring (0–12) ----
        // Missing values default to "best/normal" *but* we mark statuses as "Unknown" if key pieces are missing.
        bool missingEf = !x.EjectionFractionPercent.HasValue;
        bool missingStructure = string.IsNullOrWhiteSpace(x.HeartStructureSeverity);
        bool missingDuke = !x.DukeTreadmillScore.HasValue;
        bool missingEcg = string.IsNullOrWhiteSpace(x.EcgSeverity);

        int efScore = ScoreEf(x.EjectionFractionPercent);
        int structureScore = ScoreStructure(x.HeartStructureSeverity);
        int dukeScore = ScoreDuke(x.DukeTreadmillScore);
        int ecgScore = ScoreEcg(x.EcgSeverity);

        int physiologyScore = efScore + structureScore + dukeScore + ecgScore;

        // ---- Baseline (0–30) ----
        int baseline = plaqueScore + physiologyScore;

        // ---- Baseline risk category (Low/Mild/Moderate/High) ----
        var categoryFromScore = CategoryFromBaselineScore(baseline);
        var minFromPlaque = moderatePlusPlaque ? BaseCat.Moderate : (anyPlaque ? BaseCat.Mild : BaseCat.Low);
        var minFromEf = MinCategoryFromEf(x.EjectionFractionPercent);

        var baselineCategory = Max(categoryFromScore, minFromPlaque, minFromEf);

        // ---- Status strings ----
        string vascularStatus = !anyPlaque
            ? "No plaque detected"
            : (moderatePlusPlaque ? "Moderate or greater plaque burden" : "Early/subclinical plaque");

        string physiologyStatus;
        if (missingEf && missingDuke && missingEcg && missingStructure)
            physiologyStatus = "Unknown (insufficient physiology inputs)";
        else if (physiologyScore >= 10)
            physiologyStatus = "Normal";
        else if (physiologyScore >= 7)
            physiologyStatus = "Mild abnormality";
        else if (physiologyScore >= 4)
            physiologyStatus = "Moderate abnormality";
        else
            physiologyStatus = "High concern physiology";

        // ---- Heart Health Score (0–100) ----
        // Per spec: HeartHealthScore = Baseline(0–30) + Modifiable(0–70), clamped 0–100.
        int? modifiable = NormalizeModifiable(x.ModifiableHeartHealthScore);

        bool isPartial = !modifiable.HasValue;
        int total = ClampInt(baseline + (modifiable ?? 0), 0, 100);

        // ---- v1 compatibility mapping ----
        // Keep the old SEVERE behavior for "clinical ASCVD history" so the existing narrative stays consistent.
        bool triggeredClinical = x.HasClinicalAscVDHistory == true;

        string legacyRiskCategory;
        if (triggeredClinical)
        {
            legacyRiskCategory = "SEVERE";
        }
        else
        {
            legacyRiskCategory = baselineCategory switch
            {
                BaseCat.Low => "LOW",
                BaseCat.Mild => "MILD",
                BaseCat.Moderate => "MODERATE",
                BaseCat.High => "SEVERE", // legacy bucket had no HIGH, so map High -> SEVERE
                _ => "LOW"
            };
        }

        // Some useful v1-style triggers (approximate)
        bool trigSevere = triggeredClinical || baselineCategory == BaseCat.High;
        bool trigModerate = baselineCategory == BaseCat.Moderate;
        bool trigMild = baselineCategory == BaseCat.Mild;

        // ---- Explanation (assessment-only, no recommendations) ----
        string explanation = BuildExplanation(
            baselineCategory, baseline, plaqueScore, physiologyScore,
            vascularStatus, physiologyStatus,
            anyPlaque, moderatePlusPlaque,
            missingEf, missingStructure, missingDuke, missingEcg,
            triggeredClinical
        );

        return new Result
        {
            BaselineHeartHealthScore = baseline,
            PlaqueScore = plaqueScore,
            CardiacPhysiologyScore = physiologyScore,
            BaselineRiskCategory = baselineCategory.ToString(),
            VascularHealthStatus = vascularStatus,
            CardiacPhysiologyStatus = physiologyStatus,

            ModifiableHeartHealthScore = modifiable,
            HeartHealthScore = total,
            HeartHealthScoreIsPartial = isPartial,

            RiskExplanation = explanation,

            RiskCategory = legacyRiskCategory,
            TriggeredByClinicalHistory = triggeredClinical,
            TriggeredBySevereFinding = trigSevere,
            TriggeredByModerateFinding = trigModerate,
            TriggeredByMildFinding = trigMild
        };
    }

    // -----------------------------
    // Helpers / Scoring
    // -----------------------------
    private enum Sev { Unknown = 0, None = 1, Mild = 2, Moderate = 3, Severe = 4 }
    private enum Qual { Unknown = 0, Low = 1, Moderate = 2, High = 3, Severe = 4 }
    private enum BaseCat { Low = 0, Mild = 1, Moderate = 2, High = 3 }

    private static Sev ParseSeverity(string? s)
    {
        var v = (s ?? "").Trim().ToLowerInvariant();
        if (v == "") return Sev.Unknown;
        if (v.Contains("none") || v == "no" || v == "0") return Sev.None;
        if (v.Contains("mild")) return Sev.Mild;
        if (v.Contains("moderate") || v.Contains("mod")) return Sev.Moderate;
        if (v.Contains("severe") || v.Contains("high")) return Sev.Severe;
        return Sev.Unknown;
    }

    private static Qual ParseQual(string? s)
    {
        var v = (s ?? "").Trim().ToLowerInvariant();
        if (v == "") return Qual.Unknown;
        return v switch
        {
            "low" => Qual.Low,
            "moderate" => Qual.Moderate,
            "high" => Qual.High,
            "severe" => Qual.Severe,
            _ => Qual.Unknown
        };
    }

    private static double? ClampOrNull(double? v, double min, double max)
    {
        if (!v.HasValue) return null;
        if (double.IsNaN(v.Value) || double.IsInfinity(v.Value)) return null;
        if (v.Value < min) return min;
        if (v.Value > max) return max;
        return v.Value;
    }

    private static int ClampInt(int v, int min, int max) => v < min ? min : (v > max ? max : v);

    private static int ScoreEf(double? efPct)
    {
        if (!efPct.HasValue) return 4;
        var ef = efPct.Value;
        if (ef > 52) return 4;
        if (ef >= 45) return 3;
        if (ef >= 35) return 2;
        if (ef >= 25) return 1;
        return 0;
    }

    private static int ScoreStructure(string? structureSeverity)
    {
        var sev = ParseSeverity(structureSeverity);
        if (sev == Sev.Unknown || sev == Sev.None) return 2;
        if (sev == Sev.Mild) return 1;
        return 0;
    }

    private static int ScoreDuke(double? duke)
    {
        if (!duke.HasValue) return 4;
        var d = duke.Value;
        if (d >= 5) return 4;
        if (d >= 0) return 3;
        if (d >= -5) return 2;
        if (d >= -10) return 1;
        return 0;
    }

    private static int ScoreEcg(string? ecgSeverity)
    {
        var v = (ecgSeverity ?? "").Trim().ToLowerInvariant();
        if (v == "") return 2;
        if (v.Contains("normal")) return 2;
        if (v.Contains("lbbb")) return 1;
        if (v.Contains("mild")) return 1;
        if (v.Contains("af") || v.Contains("flutter") || v.Contains("moderate")) return 0;
        return 1;
    }

    private static BaseCat CategoryFromBaselineScore(int baseline)
    {
        if (baseline >= 28) return BaseCat.Low;
        if (baseline >= 22) return BaseCat.Mild;
        if (baseline >= 17) return BaseCat.Moderate;
        return BaseCat.High;
    }

    private static BaseCat MinCategoryFromEf(double? efPct)
    {
        if (!efPct.HasValue) return BaseCat.Low;
        var ef = efPct.Value;
        if (ef >= 52) return BaseCat.Low;
        if (ef >= 45) return BaseCat.Mild;
        if (ef >= 35) return BaseCat.Moderate;
        return BaseCat.High;
    }

    private static BaseCat Max(BaseCat a, BaseCat b, BaseCat c)
    {
        var max = a;
        if (b > max) max = b;
        if (c > max) max = c;
        return max;
    }

    private static int? NormalizeModifiable(int? mod)
    {
        if (!mod.HasValue) return null;
        var v = mod.Value;
        if (v < 0) v = 0;
        if (v > 70) v = 70;
        return v;
    }

    private static int? ScoreBloodPressure(double? systolic, double? diastolic)
    {
        if (!systolic.HasValue || !diastolic.HasValue)
            return null;

        double sys = systolic.Value;
        double dia = diastolic.Value;

        if (sys < 120 && dia < 80) return 10;
        if (sys >= 120 && sys <= 129 && dia < 80) return 7;
        if ((sys >= 130 && sys <= 139) || (dia >= 80 && dia <= 89)) return 4;
        if (sys >= 140 || dia >= 90) return 0;

        return null;
    }

    private static int? ScoreNonHdl(double? nonHdlMgDl, string? coronaryPlaqueSeverity)
    {
        if (!nonHdlMgDl.HasValue)
            return null;

        var riskCategory = MapPlaqueRiskCategory(coronaryPlaqueSeverity);
        if (riskCategory is null)
            return null;

        double nonHdl = nonHdlMgDl.Value;

        return riskCategory switch
        {
            PlaqueRiskCategory.Low => nonHdl < 100 ? 10 :
                                      nonHdl <= 129 ? 7 :
                                      nonHdl <= 159 ? 4 : 0,
            PlaqueRiskCategory.Intermediate => nonHdl < 90 ? 10 :
                                               nonHdl <= 119 ? 7 :
                                               nonHdl <= 149 ? 4 : 0,
            PlaqueRiskCategory.High => nonHdl < 60 ? 10 :
                                        nonHdl <= 89 ? 7 :
                                        nonHdl <= 119 ? 4 : 0,
            _ => null
        };
    }

    private static int? ScoreHomaIr(double? homaIr)
    {
        if (!homaIr.HasValue)
            return null;

        double homa = homaIr.Value;
        if (homa < 1.0) return 10;
        if (homa <= 2.0) return 7;
        if (homa <= 3.0) return 4;
        return 0;
    }

    private static int? ScoreFitnessPercentile(double? fitnessPercentile)
    {
        if (!fitnessPercentile.HasValue)
            return null;

        double pct = fitnessPercentile.Value;
        if (pct > 97.5) return 10;
        if (pct >= 75) return 7;
        if (pct >= 50) return 4;
        return 0;
    }

    private static int? ScoreVisceralFatPercentile(double? visceralFatPercentile)
    {
        if (!visceralFatPercentile.HasValue)
            return null;

        double pct = visceralFatPercentile.Value;
        if (pct < 25) return 10;
        if (pct <= 49) return 7;
        if (pct <= 74) return 4;
        return 0;
    }

    private static int? ScoreLeanToFatRatio(
        string? sex,
        double? leanMassPerHeight,
        double? fatMassPerHeight,
        double? leanMass,
        double? fatMass)
    {
        if (string.IsNullOrWhiteSpace(sex))
            return null;

        double? ratio = null;
        if (leanMassPerHeight.HasValue && fatMassPerHeight.HasValue && fatMassPerHeight.Value != 0)
            ratio = leanMassPerHeight.Value / fatMassPerHeight.Value;
        else if (leanMass.HasValue && fatMass.HasValue && fatMass.Value != 0)
            ratio = leanMass.Value / fatMass.Value;

        if (!ratio.HasValue)
            return null;

        bool isMale = sex.Trim().Equals("male", StringComparison.OrdinalIgnoreCase);

        if (isMale)
        {
            if (ratio.Value >= 3.2) return 10;
            if (ratio.Value >= 2.2) return 7;
            if (ratio.Value >= 1.4) return 4;
            return 0;
        }

        if (ratio.Value >= 2.6) return 10;
        if (ratio.Value >= 1.8) return 7;
        if (ratio.Value >= 1.2) return 4;
        return 0;
    }

    private static int? ScoreHsCrp(double? crpMgL)
    {
        if (!crpMgL.HasValue)
            return null;

        double crp = crpMgL.Value;
        if (crp < 1.0) return 10;
        if (crp < 2.0) return 7;
        if (crp < 3.0) return 4;
        return 0;
    }

    private static double? GetHomaIr(HealthAge.Inputs health)
    {
        if (health.HomaIr.HasValue)
            return health.HomaIr.Value;

        if (health.FastingGlucose_mg_dL.HasValue &&
            health.FastingInsulin_uIU_mL.HasValue &&
            health.FastingGlucose_mg_dL.Value != 0)
        {
            return health.FastingGlucose_mg_dL.Value *
                   health.FastingInsulin_uIU_mL.Value /
                   405.0;
        }

        return null;
    }

    private enum PlaqueRiskCategory
    {
        Low,
        Intermediate,
        High
    }

    private static PlaqueRiskCategory? MapPlaqueRiskCategory(string? coronaryPlaqueSeverity)
    {
        if (string.IsNullOrWhiteSpace(coronaryPlaqueSeverity))
            return null;

        string severity = coronaryPlaqueSeverity.Trim().ToLowerInvariant();
        return severity switch
        {
            "none" => PlaqueRiskCategory.Low,
            "mild" => PlaqueRiskCategory.Intermediate,
            "moderate" => PlaqueRiskCategory.High,
            "severe" => PlaqueRiskCategory.High,
            _ => null
        };
    }

    private static string BuildExplanation(
        BaseCat baselineCategory,
        int baselineScore,
        int plaqueScore,
        int physiologyScore,
        string vascularStatus,
        string physiologyStatus,
        bool anyPlaque,
        bool moderatePlusPlaque,
        bool missingEf,
        bool missingStructure,
        bool missingDuke,
        bool missingEcg,
        bool triggeredClinicalAscVD
    )
    {
        string catText = baselineCategory switch
        {
            BaseCat.Low => "Low baseline risk",
            BaseCat.Mild => "Mild baseline risk",
            BaseCat.Moderate => "Moderate baseline risk",
            BaseCat.High => "High baseline risk",
            _ => "Baseline risk"
        };

        string why = $"{catText} based on vascular findings and cardiac physiology. " +
                     $"Baseline score {baselineScore}/30 (Plaque {plaqueScore}/18, Physiology {physiologyScore}/12). " +
                     $"Vascular status: {vascularStatus}. Cardiac physiology status: {physiologyStatus}.";

        if (triggeredClinicalAscVD)
        {
            why += " Clinical ASCVD history was indicated, which elevates overall concern regardless of imaging score.";
        }

        int missingCount = (missingEf ? 1 : 0) + (missingStructure ? 1 : 0) + (missingDuke ? 1 : 0) + (missingEcg ? 1 : 0);
        if (missingCount >= 3)
        {
            why += " Note: detailed cardiac physiology inputs (EF/structure/Duke/ECG) were not fully provided, so physiology scoring may be incomplete.";
        }

        if (!anyPlaque)
            why += " No imaging evidence of plaque was detected in the provided inputs.";
        else if (moderatePlusPlaque)
            why += " Moderate-or-greater plaque criteria were met (e.g., moderate plaque, CAC percentile >25, or CTA stenosis ≥25%).";

        return why;
    }
}
