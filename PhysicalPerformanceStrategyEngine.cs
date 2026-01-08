using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// Physical Performance — Assessment Engine
///
/// Notes:
/// - Deterministic and explainable (no OpenAI calls).
/// - Outputs assessment-only findings (no tactics or strategies).
/// - Does NOT modify the existing PerformanceAge algorithm or Result fields.
/// </summary>
public static class PhysicalPerformanceStrategyEngine
{
    // ----------------------------
    // Public API
    // ----------------------------

    public static Response Generate(PerformanceAge.Inputs inputs, PerformanceAge.Result computed)
    {
        if (inputs is null) throw new ArgumentNullException(nameof(inputs));
        if (computed is null) throw new ArgumentNullException(nameof(computed));

        var assessments = new List<Assessment>();

        // Be Fit & Mobile
        AddVo2AndHrrAssessment(assessments, inputs, computed);
        AddGaitAssessment(assessments, inputs);

        // Stay Strong & Stable (percentile-driven)
        AddQuadricepsAssessment(assessments, inputs, computed);
        AddPercentileAssessment(assessments, "Stay Strong & Stable", "Grip Strength", inputs.GripStrengthPercentile,
            "Grip strength is a marker of overall strength reserve and predicts functional decline and all-cause mortality.",
            includeMissing: true);
        AddPercentileAssessment(assessments, "Stay Strong & Stable", "Power", inputs.PowerPercentile,
            "Power (rate of force development) supports rapid balance correction and fall prevention and tends to decline faster than strength with aging.",
            includeMissing: true);
        AddBalanceAssessment(assessments, inputs, computed);
        AddChairRiseAssessment(assessments, inputs, computed);

        // Optional / future-facing metrics carried on computed.Result
        AddFloorToStandAssessment(assessments, computed);
        AddPostureAssessment(assessments, computed);
        AddMobilityAssessment(assessments, computed);
        AddTrunkEnduranceAssessment(assessments, computed);

        // Joint integrity / regional strength (currently qualitative strings)
        AddHipStrengthAssessment(assessments, computed);
        AddCalfStrengthAssessment(assessments, computed);
        AddRotatorCuffAssessment(assessments, computed);

        AddImtpAssessment(assessments, computed);

        return new Response(assessments);
    }

    // ----------------------------
    // Output contracts
    // ----------------------------

    public sealed record Response(List<Assessment> Assessments);

    public sealed record Assessment(
        string Domain,
        string Metric,
        string Status,
        string Finding,
        string WhyThisMatters
    );

    // ----------------------------
    // Assessment builders
    // ----------------------------

    private static void AddVo2AndHrrAssessment(List<Assessment> assessments, PerformanceAge.Inputs inputs, PerformanceAge.Result computed)
    {
        var vo2P = inputs.Vo2MaxPercentile;
        var hrr = computed.HeartRateRecovery;

        if (vo2P is null && hrr is null)
        {
            assessments.Add(new Assessment(
                Domain: "Be Fit & Mobile",
                Metric: "Aerobic Fitness (VO₂ Max / HRR)",
                Status: "Data missing",
                Finding: "No VO₂ percentile or HRR value provided",
                WhyThisMatters: "Aerobic capacity and recovery reflect cardiovascular reserve and functional resilience."
            ));
            return;
        }

        bool vo2Triggered = vo2P is not null && vo2P.Value < 75;
        bool hrrTriggered = hrr is not null && hrr.Value <= 20;
        bool anyTrigger = vo2Triggered || hrrTriggered;

        var status = anyTrigger ? "Sub-optimal" : "Optimal";
        var finding = BuildVo2HrrStatus(vo2P, hrr, isTriggered: anyTrigger);

        assessments.Add(new Assessment(
            Domain: "Be Fit & Mobile",
            Metric: "Aerobic Fitness (VO₂ Max / HRR)",
            Status: status,
            Finding: finding,
            WhyThisMatters: "Strong aerobic fitness supports cardiovascular resilience, metabolic efficiency, and long-term functional capacity."
        ));
    }

    private static void AddGaitAssessment(List<Assessment> assessments, PerformanceAge.Inputs inputs)
    {
        if (inputs.GaitSpeedMaxPercentile is null)
        {
            assessments.Add(new Assessment(
                Domain: "Be Fit & Mobile",
                Metric: "Gait Speed (Maximum)",
                Status: "Data missing",
                Finding: "No maximal gait percentile provided",
                WhyThisMatters: "Maximal walking speed reflects mobility reserve and functional resilience."
            ));
            return;
        }

        var maxP = inputs.GaitSpeedMaxPercentile.Value;
        var comfortableP = inputs.GaitSpeedComfortablePercentile;
        var comfortableNote = comfortableP is null
            ? string.Empty
            : $" (comfortable {comfortableP.Value:0.#}th percentile)";

        if (maxP >= 75)
        {
            assessments.Add(new Assessment(
                Domain: "Be Fit & Mobile",
                Metric: "Gait Speed (Maximum)",
                Status: "Optimal",
                Finding: $"Optimal (≥75th percentile; {maxP:0.#}th){comfortableNote}",
                WhyThisMatters: "Strong maximal walking speed suggests good mobility reserve and functional resilience."
            ));
            return;
        }

        var sev = SeverityFromPercentile(maxP);
        var finding = $"{SeverityLabel(sev)} deficit (max {maxP:0.#}th percentile){comfortableNote}";

        assessments.Add(new Assessment(
            Domain: "Be Fit & Mobile",
            Metric: "Gait Speed (Maximum)",
            Status: "Sub-optimal",
            Finding: finding,
            WhyThisMatters: "Lower maximal gait speed reflects reduced mobility reserve, which can increase fall risk and reduce independence over time."
        ));
    }

    private static void AddQuadricepsAssessment(List<Assessment> assessments, PerformanceAge.Inputs inputs, PerformanceAge.Result computed)
    {
        var p = inputs.QuadricepsStrengthPercentile;
        var asym = computed.QuadricepsAsymmetryPercent;
        var five = computed.ChairRiseFiveTimes;
        var thirty = computed.ChairRiseThirtySeconds;

        bool pctTrigger = p is not null && p.Value < 75;
        bool asymTrigger = asym is not null && asym.Value > 10;
        bool fiveTrigger = IsPercentBelow(five, 75) || ContainsFailFlag(five);
        bool thirtyTrigger = ContainsFailFlag(thirty);

        bool hasData = p is not null || asym is not null || !string.IsNullOrWhiteSpace(five) || !string.IsNullOrWhiteSpace(thirty);
        if (!hasData)
        {
            assessments.Add(new Assessment(
                Domain: "Stay Strong & Stable",
                Metric: "Quadriceps Strength",
                Status: "Data missing",
                Finding: "No quadriceps percentile, asymmetry, or chair rise inputs provided",
                WhyThisMatters: "Quadriceps strength supports stair climbing, chair rise ability, gait efficiency, and fall prevention."
            ));
            return;
        }

        bool triggered = pctTrigger || asymTrigger || fiveTrigger || thirtyTrigger;
        if (!triggered)
        {
            var status = p is not null
                ? $"Optimal (≥75th percentile; {p.Value:0.#}th)"
                : "Optimal";

            assessments.Add(new Assessment(
                Domain: "Stay Strong & Stable",
                Metric: "Quadriceps Strength",
                Status: "Optimal",
                Finding: status,
                WhyThisMatters: "Quadriceps strength (and chair rise performance) is strongly tied to mobility reserve, fall risk, and long-term independence."
            ));
            return;
        }

        int sev = 1;
        if (p is not null) sev = SeverityFromPercentile(p.Value);
        if (asymTrigger || fiveTrigger || thirtyTrigger) sev = Math.Max(sev, 2);

        var triggers = new List<string>();
        if (pctTrigger && p is not null) triggers.Add($"{SeverityLabel(sev)} deficit ({p.Value:0.#}th percentile)");
        if (asymTrigger) triggers.Add($"Asymmetry >10% ({asym:0.#}%)");
        if (fiveTrigger) triggers.Add("5x sit-to-stand below threshold");
        if (thirtyTrigger) triggers.Add("30s sit-to-stand below optimal");

        assessments.Add(new Assessment(
            Domain: "Stay Strong & Stable",
            Metric: "Quadriceps Strength",
            Status: "Sub-optimal",
            Finding: string.Join("; ", triggers),
            WhyThisMatters: "Quadriceps strength helps preserve walking, stair climbing, and the ability to rise from a chair — core capacities that protect against falls and disability."
        ));
    }

    private static void AddPercentileAssessment(
        List<Assessment> assessments,
        string domain,
        string metric,
        double? percentile,
        string why,
        bool includeMissing)
    {
        if (percentile is null)
        {
            if (includeMissing)
            {
                assessments.Add(new Assessment(
                    Domain: domain,
                    Metric: metric,
                    Status: "Data missing",
                    Finding: "No percentile provided",
                    WhyThisMatters: why
                ));
            }
            return;
        }

        var sev = SeverityFromPercentile(percentile.Value);
        if (percentile.Value >= 75)
        {
            assessments.Add(new Assessment(
                Domain: domain,
                Metric: metric,
                Status: "Optimal",
                Finding: $"Optimal (≥75th percentile; {percentile.Value:0.#}th)",
                WhyThisMatters: why
            ));
            return;
        }

        assessments.Add(new Assessment(
            Domain: domain,
            Metric: metric,
            Status: "Sub-optimal",
            Finding: $"{SeverityLabel(sev)} deficit ({percentile.Value:0.#}th percentile)",
            WhyThisMatters: why
        ));
    }

    private static void AddBalanceAssessment(List<Assessment> assessments, PerformanceAge.Inputs inputs, PerformanceAge.Result computed)
    {
        var p = inputs.BalancePercentile;
        bool ctsibTrigger = IndicatesBalanceFailure(computed.BalanceAssessment);
        bool hasCtsibInput = !string.IsNullOrWhiteSpace(computed.BalanceAssessment);

        if (p is null && !ctsibTrigger && !hasCtsibInput)
        {
            assessments.Add(new Assessment(
                Domain: "Stay Strong & Stable",
                Metric: "Balance",
                Status: "Data missing",
                Finding: "No balance percentile or CTSIB assessment provided",
                WhyThisMatters: "Balance protects against falls and supports confidence under fatigue or sensory challenge."
            ));
            return;
        }

        bool optimal = (p is null || p.Value >= 75) && !ctsibTrigger;
        if (optimal)
        {
            var finding = p is null
                ? "Optimal"
                : $"Optimal (≥75th percentile; {p.Value:0.#}th)";
            if (hasCtsibInput && p is null)
            {
                finding = "Optimal (CTSIB without deficits)";
            }

            assessments.Add(new Assessment(
                Domain: "Stay Strong & Stable",
                Metric: "Balance",
                Status: "Optimal",
                Finding: finding,
                WhyThisMatters: "Strong balance protects against falls and supports confidence under fatigue or sensory challenge."
            ));
            return;
        }

        int sev = 1;
        if (p is not null) sev = SeverityFromPercentile(p.Value);
        if (ctsibTrigger) sev = Math.Max(sev, 2);

        var parts = new List<string>();
        if (p is not null && p.Value < 75) parts.Add($"{SeverityLabel(sev)} deficit ({p.Value:0.#}th percentile)");
        if (ctsibTrigger) parts.Add("CTSIB hold deficit (<30s)");

        assessments.Add(new Assessment(
            Domain: "Stay Strong & Stable",
            Metric: "Balance",
            Status: "Sub-optimal",
            Finding: string.Join("; ", parts),
            WhyThisMatters: "Balance deficits are strongly linked to fall risk and reduced functional confidence."
        ));
    }

    private static void AddChairRiseAssessment(List<Assessment> assessments, PerformanceAge.Inputs inputs, PerformanceAge.Result computed)
    {
        var p = inputs.ChairRisePercentile;
        bool pctTrigger = p is not null && p.Value < 75;
        bool fiveTrigger = IsPercentBelow(computed.ChairRiseFiveTimes, 75) || ContainsFailFlag(computed.ChairRiseFiveTimes);
        bool thirtyTrigger = ContainsFailFlag(computed.ChairRiseThirtySeconds);

        bool hasData = p is not null || fiveTrigger || thirtyTrigger ||
                       !string.IsNullOrWhiteSpace(computed.ChairRiseFiveTimes) ||
                       !string.IsNullOrWhiteSpace(computed.ChairRiseThirtySeconds);

        if (!hasData)
        {
            assessments.Add(new Assessment(
                Domain: "Stay Strong & Stable",
                Metric: "Chair Rise",
                Status: "Data missing",
                Finding: "No chair rise percentile or performance inputs provided",
                WhyThisMatters: "Chair rise performance reflects lower-body strength and coordination that support independence."
            ));
            return;
        }

        bool optimal = (p is null || p.Value >= 75) && !fiveTrigger && !thirtyTrigger;
        if (optimal)
        {
            var finding = p is null ? "Optimal" : $"Optimal (≥75th percentile; {p.Value:0.#}th)";

            assessments.Add(new Assessment(
                Domain: "Stay Strong & Stable",
                Metric: "Chair Rise",
                Status: "Optimal",
                Finding: finding,
                WhyThisMatters: "Chair rise performance reflects lower-body strength and neuromuscular coordination and is strongly linked to mobility reserve."
            ));
            return;
        }

        int sev = 1;
        if (p is not null) sev = SeverityFromPercentile(p.Value);
        if (fiveTrigger || thirtyTrigger) sev = Math.Max(sev, 2);

        var parts = new List<string>();
        if (pctTrigger && p is not null) parts.Add($"{SeverityLabel(sev)} deficit ({p.Value:0.#}th percentile)");
        if (fiveTrigger) parts.Add("5x sit-to-stand below threshold");
        if (thirtyTrigger) parts.Add("30s sit-to-stand below optimal");

        assessments.Add(new Assessment(
            Domain: "Stay Strong & Stable",
            Metric: "Chair Rise",
            Status: "Sub-optimal",
            Finding: string.Join("; ", parts),
            WhyThisMatters: "Chair rise performance reflects lower-body strength and neuromuscular coordination and is strongly linked to mobility reserve."
        ));
    }

    private static void AddFloorToStandAssessment(List<Assessment> assessments, PerformanceAge.Result computed)
    {
        if (string.IsNullOrWhiteSpace(computed.FloorToStandTest)) return;

        var score = ParseFirstNumber(computed.FloorToStandTest);
        if (score is null)
        {
            assessments.Add(new Assessment(
                Domain: "Stay Strong & Stable",
                Metric: "Floor-to-Stand",
                Status: "Informational",
                Finding: $"Value provided but unable to parse score ({computed.FloorToStandTest.Trim()})",
                WhyThisMatters: "Floor-to-stand ability reflects mobility, strength, and coordination that protect independence over time."
            ));
            return;
        }

        if (score.Value >= 8)
        {
            assessments.Add(new Assessment(
                Domain: "Stay Strong & Stable",
                Metric: "Floor-to-Stand",
                Status: "Optimal",
                Finding: $"Optimal (score {score.Value:0.#}/10)",
                WhyThisMatters: "Strong floor-to-stand ability reflects mobility, strength, and coordination that protect independence over time."
            ));
            return;
        }

        assessments.Add(new Assessment(
            Domain: "Stay Strong & Stable",
            Metric: "Floor-to-Stand",
            Status: "Sub-optimal",
            Finding: $"Task deficit (score {score.Value:0.#}/10)",
            WhyThisMatters: "Difficulty transitioning from the floor can predict loss of independence and higher fall risk, especially as demands increase."
        ));
    }

    private static void AddPostureAssessment(List<Assessment> assessments, PerformanceAge.Result computed)
    {
        if (string.IsNullOrWhiteSpace(computed.PostureAssessment)) return;

        var cm = ParseFirstNumber(computed.PostureAssessment);
        if (cm is null)
        {
            assessments.Add(new Assessment(
                Domain: "Stay Strong & Stable",
                Metric: "Posture (Tragus-to-Wall)",
                Status: "Informational",
                Finding: $"Value provided but unable to parse distance ({computed.PostureAssessment.Trim()})",
                WhyThisMatters: "Postural alignment supports efficient breathing mechanics and reduces compensatory loading."
            ));
            return;
        }

        if (cm.Value <= 10)
        {
            assessments.Add(new Assessment(
                Domain: "Stay Strong & Stable",
                Metric: "Posture (Tragus-to-Wall)",
                Status: "Optimal",
                Finding: $"Optimal (≤10 cm; ~{cm.Value:0.#} cm)",
                WhyThisMatters: "Good postural alignment supports efficient breathing mechanics and reduces compensatory loading."
            ));
            return;
        }

        assessments.Add(new Assessment(
            Domain: "Stay Strong & Stable",
            Metric: "Posture (Tragus-to-Wall)",
            Status: "Sub-optimal",
            Finding: $"Postural offset (>10 cm; ~{cm.Value:0.#} cm)",
            WhyThisMatters: "Postural offsets can shift joint loading and contribute to neck/shoulder discomfort, limited overhead capacity, and inefficient breathing."
        ));
    }

    private static void AddMobilityAssessment(List<Assessment> assessments, PerformanceAge.Result computed)
    {
        bool explicitIssue = IsQualitativeIssue(computed.MobilityRom);
        bool inferredIssue = !explicitIssue && (
            IsQualitativeIssue(computed.PostureAssessment) ||
            IsQualitativeIssue(computed.HipStrength) ||
            IsQualitativeIssue(computed.CalfStrength) ||
            IsQualitativeIssue(computed.RotatorCuffIntegrity)
        );

        if (!explicitIssue && !inferredIssue)
        {
            if (!string.IsNullOrWhiteSpace(computed.MobilityRom))
            {
                assessments.Add(new Assessment(
                    Domain: "Stay Strong & Stable",
                    Metric: "Mobility / ROM",
                    Status: "Optimal",
                    Finding: "Optimal",
                    WhyThisMatters: "Good mobility and pain-free range supports efficient strength and aerobic work."
                ));
            }
            return;
        }

        assessments.Add(new Assessment(
            Domain: "Stay Strong & Stable",
            Metric: "Mobility / ROM",
            Status: "Sub-optimal",
            Finding: string.IsNullOrWhiteSpace(computed.MobilityRom) ? "Pain or restricted motion (inferred)" : computed.MobilityRom.Trim(),
            WhyThisMatters: "Mobility restrictions can drive compensations that increase injury risk and limit training options."
        ));
    }

    private static void AddTrunkEnduranceAssessment(List<Assessment> assessments, PerformanceAge.Result computed)
    {
        if (string.IsNullOrWhiteSpace(computed.TrunkEndurance)) return;

        bool pctTrigger = IsPercentBelow(computed.TrunkEndurance, 75);
        bool qualTrigger = IsQualitativeIssue(computed.TrunkEndurance);

        if (!pctTrigger && !qualTrigger)
        {
            assessments.Add(new Assessment(
                Domain: "Stay Strong & Stable",
                Metric: "Trunk Endurance",
                Status: "Optimal",
                Finding: "Optimal",
                WhyThisMatters: "Trunk endurance supports safe force transfer, posture under fatigue, and reduces compensation-driven overuse."
            ));
            return;
        }

        var finding = pctTrigger
            ? "Sub-optimal trunk endurance (<75th percentile)"
            : "Sub-optimal trunk endurance (reported)";

        assessments.Add(new Assessment(
            Domain: "Stay Strong & Stable",
            Metric: "Trunk Endurance",
            Status: "Sub-optimal",
            Finding: finding,
            WhyThisMatters: "Trunk endurance supports safe force transfer, posture under fatigue, and reduces compensation-driven overuse."
        ));
    }

    private static void AddHipStrengthAssessment(List<Assessment> assessments, PerformanceAge.Result computed)
    {
        bool asymTrigger = computed.HipAsymmetryPercent is not null && computed.HipAsymmetryPercent.Value > 10;
        bool qualTrigger = IsQualitativeIssue(computed.HipStrength);

        if (!asymTrigger && !qualTrigger)
        {
            if (!string.IsNullOrWhiteSpace(computed.HipStrength) || computed.HipAsymmetryPercent is not null)
            {
                assessments.Add(new Assessment(
                    Domain: "Stay Strong & Stable",
                    Metric: "Hip Strength",
                    Status: "Optimal",
                    Finding: "Optimal",
                    WhyThisMatters: "Hip strength supports gait efficiency, balance, and trunk stability."
                ));
            }
            return;
        }

        var parts = new List<string>();
        if (asymTrigger) parts.Add($"Asymmetry >10% ({computed.HipAsymmetryPercent:0.#}%)");
        if (qualTrigger) parts.Add("Reported deficit / limitation");

        assessments.Add(new Assessment(
            Domain: "Stay Strong & Stable",
            Metric: "Hip Strength",
            Status: "Sub-optimal",
            Finding: string.Join("; ", parts),
            WhyThisMatters: "Hip strength deficits and asymmetry can drive compensation patterns, reduce gait efficiency, and impair balance."
        ));
    }

    private static void AddCalfStrengthAssessment(List<Assessment> assessments, PerformanceAge.Result computed)
    {
        bool asymTrigger = computed.CalfAsymmetryPercent is not null && computed.CalfAsymmetryPercent.Value > 10;
        bool qualTrigger = IsQualitativeIssue(computed.CalfStrength);

        if (!asymTrigger && !qualTrigger)
        {
            if (!string.IsNullOrWhiteSpace(computed.CalfStrength) || computed.CalfAsymmetryPercent is not null)
            {
                assessments.Add(new Assessment(
                    Domain: "Stay Strong & Stable",
                    Metric: "Calf Strength",
                    Status: "Optimal",
                    Finding: "Optimal",
                    WhyThisMatters: "Calf strength supports propulsion, balance corrections, and sustained walking capacity."
                ));
            }
            return;
        }

        var parts = new List<string>();
        if (asymTrigger) parts.Add($"Asymmetry >10% ({computed.CalfAsymmetryPercent:0.#}%)");
        if (qualTrigger) parts.Add("Reported deficit / limitation");

        assessments.Add(new Assessment(
            Domain: "Stay Strong & Stable",
            Metric: "Calf Strength",
            Status: "Sub-optimal",
            Finding: string.Join("; ", parts),
            WhyThisMatters: "Calf deficits and asymmetry can reduce walking reserve and increase compensation and overuse risk."
        ));
    }

    private static void AddRotatorCuffAssessment(List<Assessment> assessments, PerformanceAge.Result computed)
    {
        bool pctTrigger = computed.RotatorCuffLowestMusclePercentile is not null && computed.RotatorCuffLowestMusclePercentile.Value < 75;
        bool qualTrigger = IsQualitativeIssue(computed.RotatorCuffIntegrity);

        if (!pctTrigger && !qualTrigger)
        {
            if (!string.IsNullOrWhiteSpace(computed.RotatorCuffIntegrity) || computed.RotatorCuffLowestMusclePercentile is not null)
            {
                assessments.Add(new Assessment(
                    Domain: "Stay Strong & Stable",
                    Metric: "Rotator Cuff Integrity",
                    Status: "Optimal",
                    Finding: "Optimal",
                    WhyThisMatters: "Shoulder stability supports pain-free loading and preserves upper-extremity function over time."
                ));
            }
            return;
        }

        var parts = new List<string>();
        var p = computed.RotatorCuffLowestMusclePercentile;
        if (pctTrigger && p is not null) parts.Add($"Sub-optimal (lowest muscle {p.Value:0.#}th percentile)");
        if (qualTrigger) parts.Add("Reported deficit / limitation");

        assessments.Add(new Assessment(
            Domain: "Stay Strong & Stable",
            Metric: "Rotator Cuff Integrity",
            Status: "Sub-optimal",
            Finding: string.Join("; ", parts),
            WhyThisMatters: "Shoulder stability deficits can limit pain-free training and daily tasks and may increase overuse risk."
        ));
    }

    private static void AddImtpAssessment(List<Assessment> assessments, PerformanceAge.Result computed)
    {
        var p = computed.IsometricThighPullPercentile;
        if (p is null)
        {
            if (computed.IsometricThighPull is not null)
            {
                assessments.Add(new Assessment(
                    Domain: "Stay Strong & Stable",
                    Metric: "Global Strength (IMTP)",
                    Status: "Informational",
                    Finding: $"Raw IMTP value provided ({computed.IsometricThighPull:0.#}); percentile not available",
                    WhyThisMatters: "Global strength reserve supports independence and resilience under stress and injury."
                ));
            }
            return;
        }

        if (p.Value >= 75)
        {
            assessments.Add(new Assessment(
                Domain: "Stay Strong & Stable",
                Metric: "Global Strength (IMTP)",
                Status: "Optimal",
                Finding: $"Optimal (≥75th percentile; {p.Value:0.#}th)",
                WhyThisMatters: "Global strength reserve supports independence and resilience under stress and injury."
            ));
            return;
        }

        int sev = SeverityFromPercentile(p.Value);
        var finding = $"{SeverityLabel(sev)} deficit ({p.Value:0.#}th percentile)";

        assessments.Add(new Assessment(
            Domain: "Stay Strong & Stable",
            Metric: "Global Strength (IMTP)",
            Status: "Sub-optimal",
            Finding: finding,
            WhyThisMatters: "Lower global strength reserve can reduce functional capacity and resilience and raises the importance of foundational strength capacity."
        ));
    }

    // ----------------------------
    // Severity + parsing helpers
    // ----------------------------

    private static int SeverityFromPercentile(double p)
    {
        if (p >= 75) return 0;
        if (p >= 50) return 1;
        if (p >= 26) return 2;
        return 3;
    }

    private static string SeverityLabel(int severity) => severity switch
    {
        1 => "Mild",
        2 => "Moderate",
        3 => "Severe",
        _ => "Optimal"
    };

    private static string BuildVo2HrrStatus(double? vo2Percentile, double? hrr, bool isTriggered)
    {
        var parts = new List<string>();
        if (vo2Percentile is not null)
        {
            parts.Add($"VO₂ {vo2Percentile.Value:0.#}th percentile");
        }
        if (hrr is not null)
        {
            parts.Add($"HRR {hrr.Value:0.#} bpm");
        }
        var joined = string.Join(", ", parts);
        if (string.IsNullOrWhiteSpace(joined)) joined = "VO₂/HRR data provided";
        return isTriggered ? $"Sub-optimal: {joined}" : $"Optimal: {joined}";
    }

    private static bool IsPercentBelow(string? text, double threshold)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim();
        if (t.Contains('%'))
        {
            var n = ParseFirstNumber(t);
            return n is not null && n.Value < threshold;
        }
        var raw = ParseFirstNumber(t);
        if (raw is null) return false;
        if (raw.Value > 0 && raw.Value <= 1.0) return (raw.Value * 100.0) < threshold;
        return false;
    }

    private static bool ContainsFailFlag(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim().ToLowerInvariant();
        string[] flags = { "below", "suboptimal", "sub-optimal", "fail", "failed", "unable", "couldn't", "couldnt", "<", "under" };
        return flags.Any(t.Contains);
    }

    private static bool IndicatesBalanceFailure(string? balanceAssessment)
    {
        if (string.IsNullOrWhiteSpace(balanceAssessment)) return false;
        var t = balanceAssessment.ToLowerInvariant();

        if (!t.Contains("ctsib") && !t.Contains("sensory") && !t.Contains("foam") && !t.Contains("eyes closed") && !t.Contains("modified"))
        {
            return false;
        }

        if (t.Contains("s"))
        {
            var n = ParseFirstNumber(balanceAssessment);
            if (n is not null && n.Value < 30) return true;
        }

        return ContainsFailFlag(balanceAssessment);
    }

    private static double? ParseFirstNumber(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var chars = text.ToCharArray();
        var buf = new List<char>();
        bool inNumber = false;
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            bool isNumChar = char.IsDigit(c) || c == '.' || c == '-';
            if (isNumChar)
            {
                inNumber = true;
                buf.Add(c);
            }
            else if (inNumber)
            {
                break;
            }
        }

        if (buf.Count == 0) return null;
        var s = new string(buf.ToArray());
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
        {
            return val;
        }
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out val))
        {
            return val;
        }
        return null;
    }

    private static bool IsQualitativeIssue(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim().ToLowerInvariant();

        if (t is "normal" or "none" or "no" or "ok" or "okay" or "good" or "wnl" or "within normal limits")
            return false;

        string[] triggers =
        {
            "pain", "hurt", "aching", "ache", "restricted", "limit", "limited", "stiff", "tight",
            "weak", "unstable", "instability", "cannot", "can't", "unable", "reduced", "poor"
        };

        return triggers.Any(t.Contains);
    }
}
