using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// Physical Performance — Context-Anchored Strategy Engine
///
/// Non-negotiable gate:
///   This engine must not emit strategies/tactics unless there is a sub-optimal finding.
///
/// Notes:
/// - This is intentionally deterministic and explainable (no OpenAI calls).
/// - It does NOT modify the existing PerformanceAge algorithm or Result fields.
/// - Today, it relies primarily on percentile-based inputs + a few optional raw/qualitative fields
///   that were added to PerformanceAge.Result to support future scoring expansions.
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

        var findings = new List<Finding>();

        // Be Fit & Mobile
        AddGaitFinding(findings, inputs);
        AddVo2AndHrrFinding(findings, inputs, computed);

        // Stay Strong & Stable (percentile-driven)
        AddQuadricepsFinding(findings, inputs, computed);
        AddGripFinding(findings, inputs);
        AddPowerFinding(findings, inputs);
        AddBalanceFinding(findings, inputs, computed);
        AddChairRiseFinding(findings, inputs, computed);

        // Optional / future-facing metrics carried on computed.Result
        AddFloorToStandFinding(findings, computed);
        AddPostureFinding(findings, computed);
        AddMobilityFinding(findings, computed);
        AddTrunkEnduranceFinding(findings, computed);

        // Joint integrity / regional strength (currently qualitative strings)
        AddHipStrengthFinding(findings, computed);
        AddCalfStrengthFinding(findings, computed);
        AddRotatorCuffFinding(findings, computed);

        AddImtpFinding(findings, computed);

        // Global strength (IMTP) — today we only have a raw number, so we treat it as informational unless
        // the qualitative field explicitly flags an issue. If you later add an IMTP percentile, plug it into
        // AddPercentileFinding.
        if (computed.IsometricThighPull is not null)
        {
            // No trigger unless there is an explicit issue flag.
            // Keeping this non-triggering avoids generic advice.
        }

        // Build output
        var triggered = findings.Where(f => f.IsTriggered).ToList();
        var optimal = findings.Where(f => f.IsOptimalCandidate).ToList();

        if (triggered.Count == 0)
        {
            // All findings optimal (or unassessable): reassurance + monitoring only.
            return new Response(
                Strategies: new List<Strategy>(),
                Reassurances: BuildReassurances(optimal)
            );
        }

        // Rank and select up to 3 strategies
        var top = triggered
            .OrderByDescending(f => f.TotalRankScore)
            .ThenBy(f => f.Domain)
            .ThenBy(f => f.Metric)
            .Take(3)
            .Select(ToStrategy)
            .ToList();

        return new Response(
            Strategies: top,
            Reassurances: BuildReassurances(optimal)
        );
    }

    // ----------------------------
    // Output contracts
    // ----------------------------

    public sealed record Response(
        List<Strategy> Strategies,
        List<Reassurance> Reassurances
    );

    public sealed record Strategy(
        string Domain,
        string TriggerMetric,
        string TriggerFinding,
        string WhyThisMatters,
        string StrategyStatement,
        List<TacticModule> TacticModules
    );

    public sealed record TacticModule(
        string Title,
        string Rationale,
        List<string> Tactics
    );

    public sealed record Reassurance(
        string Domain,
        string Metric,
        string Status,
        string ReassuranceText
    );

    // ----------------------------
    // Internal finding model
    // ----------------------------

    private sealed record Finding(
        string Domain,
        string Metric,
        string TriggerFinding,
        string Why,
        bool IsTriggered,
        bool IsOptimalCandidate,
        int SeverityRank,
        int ImpactRank,
        int LeverageRank,
        int TotalRankScore,
        Func<Strategy> BuildStrategy
    );

    private static Strategy ToStrategy(Finding f) => f.BuildStrategy();

    // ----------------------------
    // Findings builders
    // ----------------------------

    // ---- Metric-specific builders matching the spec ----

    private static void AddQuadricepsFinding(List<Finding> findings, PerformanceAge.Inputs inputs, PerformanceAge.Result computed)
    {
        // Trigger: <75th percentile OR asymmetry >10% OR <75% on 5x sit-to-stand OR below-optimal on 30s sit-to-stand
        var p = inputs.QuadricepsStrengthPercentile;
        var asym = computed.QuadricepsAsymmetryPercent;
        var five = computed.ChairRiseFiveTimes;
        var thirty = computed.ChairRiseThirtySeconds;

        bool pctTrigger = p is not null && p.Value < 75;
        bool asymTrigger = asym is not null && asym.Value > 10;
        bool fiveTrigger = IsPercentBelow(five, 75) || ContainsFailFlag(five);
        bool thirtyTrigger = ContainsFailFlag(thirty); // no norms provided; allow explicit flag from UI

        bool isOptimalPct = p is not null && p.Value >= 75;
        bool triggered = pctTrigger || asymTrigger || fiveTrigger || thirtyTrigger;

        if (!triggered)
        {
            // Only reassurance if we have at least one assessed indicator.
            if (p is null && asym is null && string.IsNullOrWhiteSpace(five) && string.IsNullOrWhiteSpace(thirty)) return;

            var status = p is not null
                ? $"Optimal (≥75th percentile; {p.Value:0.#}th)"
                : "Optimal";

            findings.Add(new Finding(
                Domain: "Stay Strong & Stable",
                Metric: "Quadriceps Strength",
                TriggerFinding: status,
                Why: "Lower-body strength is a primary driver of stair climbing, rising from a chair, gait efficiency, and fall prevention.",
                IsTriggered: false,
                IsOptimalCandidate: true,
                SeverityRank: 0,
                ImpactRank: 4,
                LeverageRank: 2,
                TotalRankScore: 0,
                BuildStrategy: () => throw new InvalidOperationException("No strategy for optimal quadriceps.")
            ));
            return;
        }

        int sev = 1;
        if (p is not null) sev = SeverityFromPercentile(p.Value);
        if (asymTrigger) sev = Math.Max(sev, 2);
        if (fiveTrigger || thirtyTrigger) sev = Math.Max(sev, 2);

        var triggers = new List<string>();
        if (pctTrigger && p is not null) triggers.Add($"{SeverityLabel(sev)} deficit ({p.Value:0.#}th percentile)");
        if (asymTrigger) triggers.Add($"Asymmetry >10% ({asym:0.#}%)");
        if (fiveTrigger) triggers.Add("5x sit-to-stand below threshold");
        if (thirtyTrigger) triggers.Add("30s sit-to-stand below optimal");

        var triggerFinding = string.Join("; ", triggers);

        findings.Add(new Finding(
            Domain: "Stay Strong & Stable",
            Metric: "Quadriceps Strength",
            TriggerFinding: triggerFinding,
            Why: "Quadriceps strength (and chair rise performance) is strongly tied to mobility reserve, fall risk, and long-term independence.",
            IsTriggered: true,
            IsOptimalCandidate: false,
            SeverityRank: sev,
            ImpactRank: 4,
            LeverageRank: 2,
            TotalRankScore: (sev * 100) + (4 * 10) + 2,
            BuildStrategy: () => new Strategy(
                Domain: "Stay Strong & Stable",
                TriggerMetric: "Quadriceps Strength / Chair Rise",
                TriggerFinding: triggerFinding,
                WhyThisMatters: "Quadriceps strength helps preserve walking, stair climbing, and the ability to rise from a chair — core capacities that protect against falls and disability.",
                StrategyStatement: "Improving quadriceps strength appears to be a high-leverage opportunity to strengthen mobility reserve and reduce fall and disability risk.",
                TacticModules: new List<TacticModule>
                {
                    new("Progressive Lower-Extremity Loading",
                        "Building strength safely reduces compensation patterns and improves day-to-day function.",
                        new List<string>
                        {
                            "Use progressive resistance work that targets the lower extremities (knee-dominant patterns) within tolerable ranges.",
                            "Bias high-quality mechanics and gradual progression rather than aggressive loading.",
                            "If you have prior injury or surgery, choose exercises that respect those constraints and progress conservatively."
                        })
                }
            )
        ));
    }

    private static void AddGripFinding(List<Finding> findings, PerformanceAge.Inputs inputs)
    {
        AddPercentileFinding(findings,
            domain: "Stay Strong & Stable",
            metric: "Grip Strength",
            percentile: inputs.GripStrengthPercentile,
            impactRank: 2,
            leverageRank: 2,
            why: "Grip strength is a marker of overall strength reserve and predicts functional decline and all-cause mortality.");
    }

    private static void AddPowerFinding(List<Finding> findings, PerformanceAge.Inputs inputs)
    {
        AddPercentileFinding(findings,
            domain: "Stay Strong & Stable",
            metric: "Power",
            percentile: inputs.PowerPercentile,
            impactRank: 2,
            leverageRank: 2,
            why: "Power (rate of force development) supports rapid balance correction and fall prevention and tends to decline faster than strength with aging.");
    }

    private static void AddBalanceFinding(List<Finding> findings, PerformanceAge.Inputs inputs, PerformanceAge.Result computed)
    {
        // Trigger: <75th percentile OR inability to hold modified CTSIB positions for 30 seconds
        var p = inputs.BalancePercentile;
        bool pctTrigger = p is not null && p.Value < 75;
        bool ctsibTrigger = IndicatesBalanceFailure(computed.BalanceAssessment);

        if (p is null && !ctsibTrigger) return;

        bool optimal = p is not null && p.Value >= 75 && !ctsibTrigger;
        if (optimal)
        {
            findings.Add(new Finding(
                Domain: "Stay Strong & Stable",
                Metric: "Balance",
                TriggerFinding: $"Optimal (≥75th percentile; {p!.Value:0.#}th)",
                Why: "Strong balance protects against falls and supports confidence under fatigue or sensory challenge.",
                IsTriggered: false,
                IsOptimalCandidate: true,
                SeverityRank: 0,
                ImpactRank: 3,
                LeverageRank: 2,
                TotalRankScore: 0,
                BuildStrategy: () => throw new InvalidOperationException("No strategy for optimal balance.")
            ));
            return;
        }

        int sev = 1;
        if (p is not null) sev = SeverityFromPercentile(p.Value);
        if (ctsibTrigger) sev = Math.Max(sev, 2);

        var parts = new List<string>();
        if (p is not null && p.Value < 75) parts.Add($"{SeverityLabel(sev)} deficit ({p.Value:0.#}th percentile)");
        if (ctsibTrigger) parts.Add("CTSIB hold deficit (<30s)");
        var triggerFinding = string.Join("; ", parts);

        findings.Add(new Finding(
            Domain: "Stay Strong & Stable",
            Metric: "Balance",
            TriggerFinding: triggerFinding,
            Why: "Balance deficits are strongly linked to fall risk and reduced functional confidence.",
            IsTriggered: true,
            IsOptimalCandidate: false,
            SeverityRank: sev,
            ImpactRank: 3,
            LeverageRank: 2,
            TotalRankScore: (sev * 100) + (3 * 10) + 2,
            BuildStrategy: () => new Strategy(
                Domain: "Stay Strong & Stable",
                TriggerMetric: "Balance",
                TriggerFinding: triggerFinding,
                WhyThisMatters: "Balance is a direct predictor of fall risk and functional confidence, especially under fatigue or sensory challenge.",
                StrategyStatement: "Improving your balance represents a meaningful opportunity to reduce fall risk by progressively training stability under increasing sensory and mechanical challenge.",
                TacticModules: BuildDefaultTactics("Balance")
            )
        ));
    }

    private static void AddChairRiseFinding(List<Finding> findings, PerformanceAge.Inputs inputs, PerformanceAge.Result computed)
    {
        // Trigger: ChairRisePercentile <75 OR explicit 5x/30s chair rise failure indicators
        var p = inputs.ChairRisePercentile;
        bool pctTrigger = p is not null && p.Value < 75;
        bool fiveTrigger = IsPercentBelow(computed.ChairRiseFiveTimes, 75) || ContainsFailFlag(computed.ChairRiseFiveTimes);
        bool thirtyTrigger = ContainsFailFlag(computed.ChairRiseThirtySeconds);

        if (p is null && !fiveTrigger && !thirtyTrigger) return;

        bool optimal = (p is null || p.Value >= 75) && !fiveTrigger && !thirtyTrigger;
        if (optimal)
        {
            findings.Add(new Finding(
                Domain: "Stay Strong & Stable",
                Metric: "Chair Rise",
                TriggerFinding: p is null ? "Optimal" : $"Optimal (≥75th percentile; {p.Value:0.#}th)",
                Why: "Chair rise performance reflects lower-body strength and coordination that support independence.",
                IsTriggered: false,
                IsOptimalCandidate: true,
                SeverityRank: 0,
                ImpactRank: 3,
                LeverageRank: 2,
                TotalRankScore: 0,
                BuildStrategy: () => throw new InvalidOperationException("No strategy for optimal chair rise.")
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
        var triggerFinding = string.Join("; ", parts);

        findings.Add(new Finding(
            Domain: "Stay Strong & Stable",
            Metric: "Chair Rise",
            TriggerFinding: triggerFinding,
            Why: "Chair rise performance reflects lower-body strength and neuromuscular coordination and is strongly linked to mobility reserve.",
            IsTriggered: true,
            IsOptimalCandidate: false,
            SeverityRank: sev,
            ImpactRank: 3,
            LeverageRank: 2,
            TotalRankScore: (sev * 100) + (3 * 10) + 2,
            BuildStrategy: () => new Strategy(
                Domain: "Stay Strong & Stable",
                TriggerMetric: "Chair Rise Tests",
                TriggerFinding: triggerFinding,
                WhyThisMatters: "Chair rise tests are practical markers of lower-body strength and coordination, often preceding noticeable mobility decline.",
                StrategyStatement: "Improve sit-to-stand ability by pairing targeted lower-body strength with repeated practice of the rising pattern to build efficiency and reserve.",
                TacticModules: BuildDefaultTactics("Chair Rise")
            )
        ));
    }

    private static void AddHipStrengthFinding(List<Finding> findings, PerformanceAge.Result computed)
    {
        // Trigger: <75th percentile OR asymmetry >10% (we currently support asymmetry via numeric field)
        bool asymTrigger = computed.HipAsymmetryPercent is not null && computed.HipAsymmetryPercent.Value > 10;
        bool qualTrigger = IsQualitativeIssue(computed.HipStrength);

        if (!asymTrigger && !qualTrigger)
        {
            if (!string.IsNullOrWhiteSpace(computed.HipStrength) || computed.HipAsymmetryPercent is not null)
            {
                findings.Add(new Finding(
                    Domain: "Stay Strong & Stable",
                    Metric: "Hip Strength",
                    TriggerFinding: "Optimal",
                    Why: "Hip strength supports gait efficiency, balance, and trunk stability.",
                    IsTriggered: false,
                    IsOptimalCandidate: true,
                    SeverityRank: 0,
                    ImpactRank: 2,
                    LeverageRank: 2,
                    TotalRankScore: 0,
                    BuildStrategy: () => throw new InvalidOperationException("No strategy for optimal hip strength.")
                ));
            }
            return;
        }

        var parts = new List<string>();
        if (asymTrigger) parts.Add($"Asymmetry >10% ({computed.HipAsymmetryPercent:0.#}%)");
        if (qualTrigger) parts.Add("Reported deficit / limitation");
        var triggerFinding = string.Join("; ", parts);

        findings.Add(new Finding(
            Domain: "Stay Strong & Stable",
            Metric: "Hip Strength",
            TriggerFinding: triggerFinding,
            Why: "Hip strength deficits and asymmetry can drive compensation patterns, reduce gait efficiency, and impair balance.",
            IsTriggered: true,
            IsOptimalCandidate: false,
            SeverityRank: 2,
            ImpactRank: 2,
            LeverageRank: 2,
            TotalRankScore: (2 * 100) + (2 * 10) + 2,
            BuildStrategy: () => new Strategy(
                Domain: "Stay Strong & Stable",
                TriggerMetric: "Hip Strength",
                TriggerFinding: triggerFinding,
                WhyThisMatters: "Hip abductors and extensors support pelvic stability, gait mechanics, and balance — especially under fatigue.",
                StrategyStatement: "Improve hip strength (and reduce asymmetry when present) to enhance gait efficiency and balance while reducing compensation-related strain.",
                TacticModules: new List<TacticModule>
                {
                    new("Targeted Hip Strengthening",
                        "Progressive strengthening of identified weak patterns can meaningfully improve stability and efficiency.",
                        new List<string>
                        {
                            "Prioritize hip abduction/extension strength with progressive resistance and strict form.",
                            "If asymmetry is present, emphasize controlled work on the weaker side without turning sessions into punishment.",
                            "Progress challenge only when technique remains stable and symptoms stay calm."
                        })
                }
            )
        ));
    }

    private static void AddCalfStrengthFinding(List<Finding> findings, PerformanceAge.Result computed)
    {
        // Trigger: <75th percentile OR asymmetry >10% (supported via numeric field)
        bool asymTrigger = computed.CalfAsymmetryPercent is not null && computed.CalfAsymmetryPercent.Value > 10;
        bool qualTrigger = IsQualitativeIssue(computed.CalfStrength);

        if (!asymTrigger && !qualTrigger)
        {
            if (!string.IsNullOrWhiteSpace(computed.CalfStrength) || computed.CalfAsymmetryPercent is not null)
            {
                findings.Add(new Finding(
                    Domain: "Stay Strong & Stable",
                    Metric: "Calf Strength",
                    TriggerFinding: "Optimal",
                    Why: "Calf strength supports propulsion, balance corrections, and sustained walking capacity.",
                    IsTriggered: false,
                    IsOptimalCandidate: true,
                    SeverityRank: 0,
                    ImpactRank: 2,
                    LeverageRank: 1,
                    TotalRankScore: 0,
                    BuildStrategy: () => throw new InvalidOperationException("No strategy for optimal calf strength.")
                ));
            }
            return;
        }

        var parts = new List<string>();
        if (asymTrigger) parts.Add($"Asymmetry >10% ({computed.CalfAsymmetryPercent:0.#}%)");
        if (qualTrigger) parts.Add("Reported deficit / limitation");
        var triggerFinding = string.Join("; ", parts);

        findings.Add(new Finding(
            Domain: "Stay Strong & Stable",
            Metric: "Calf Strength",
            TriggerFinding: triggerFinding,
            Why: "Calf deficits and asymmetry can reduce walking reserve and increase compensation and overuse risk.",
            IsTriggered: true,
            IsOptimalCandidate: false,
            SeverityRank: 2,
            ImpactRank: 2,
            LeverageRank: 1,
            TotalRankScore: (2 * 100) + (2 * 10) + 1,
            BuildStrategy: () => new Strategy(
                Domain: "Stay Strong & Stable",
                TriggerMetric: "Calf Strength",
                TriggerFinding: triggerFinding,
                WhyThisMatters: "Calves contribute to propulsion and balance corrections — especially during faster walking, uneven terrain, and fatigue.",
                StrategyStatement: "Improve calf strength and control (and reduce asymmetry when present) to support walking reserve and balance.",
                TacticModules: new List<TacticModule>
                {
                    new("Calf Strength + Control",
                        "Progressing from double-limb to single-limb and emphasizing control supports function and resilience.",
                        new List<string>
                        {
                            "Build controlled calf strength through progressive loading, including eccentric control when tolerated.",
                            "If asymmetry is present, add a small volume bias to the weaker side.",
                            "Keep progressions gradual; pain spikes are a signal to regress and rebuild."
                        })
                }
            )
        ));
    }

    private static void AddRotatorCuffFinding(List<Finding> findings, PerformanceAge.Result computed)
    {
        // Trigger: <75th percentile in any muscle OR qualitative issue flag
        bool pctTrigger = computed.RotatorCuffLowestMusclePercentile is not null && computed.RotatorCuffLowestMusclePercentile.Value < 75;
        bool qualTrigger = IsQualitativeIssue(computed.RotatorCuffIntegrity);

        if (!pctTrigger && !qualTrigger)
        {
            if (!string.IsNullOrWhiteSpace(computed.RotatorCuffIntegrity) || computed.RotatorCuffLowestMusclePercentile is not null)
            {
                findings.Add(new Finding(
                    Domain: "Stay Strong & Stable",
                    Metric: "Rotator Cuff Integrity",
                    TriggerFinding: "Optimal",
                    Why: "Shoulder stability supports pain-free loading and preserves upper-extremity function over time.",
                    IsTriggered: false,
                    IsOptimalCandidate: true,
                    SeverityRank: 0,
                    ImpactRank: 1,
                    LeverageRank: 1,
                    TotalRankScore: 0,
                    BuildStrategy: () => throw new InvalidOperationException("No strategy for optimal rotator cuff.")
                ));
            }
            return;
        }

		// Severity: floor-to-stand has no percentile input in this model, so treat as qualitative severity.
		int sev = 1;

        var p = computed.RotatorCuffLowestMusclePercentile;
        var parts = new List<string>();
        if (pctTrigger && p is not null) parts.Add($"Sub-optimal (lowest muscle {p.Value:0.#}th percentile)");
        if (qualTrigger) parts.Add("Reported deficit / limitation");
        var triggerFinding = string.Join("; ", parts);

		findings.Add(new Finding(
            Domain: "Stay Strong & Stable",
            Metric: "Rotator Cuff Integrity",
            TriggerFinding: triggerFinding,
            Why: "Shoulder stability deficits can limit pain-free training and daily tasks and may increase overuse risk.",
            IsTriggered: true,
            IsOptimalCandidate: false,
            SeverityRank: 1,
            ImpactRank: 1,
            LeverageRank: 1,
            TotalRankScore: (1 * 100) + (1 * 10) + 1,
            BuildStrategy: () => new Strategy(
                Domain: "Stay Strong & Stable",
                TriggerMetric: "Rotator Cuff Integrity",
                TriggerFinding: triggerFinding,
                WhyThisMatters: "Rotator cuff and scapular control protect shoulder joints and help preserve pain-free overhead and pushing/pulling capacity.",
                StrategyStatement: "Improve shoulder stability by strengthening the rotator cuff and scapular control patterns with progressive loading.",
                TacticModules: new List<TacticModule>
                {
                    new("Stability + Control",
                        "Building control first makes loading safer and more durable.",
                        new List<string>
                        {
                            "Emphasize targeted rotator cuff work and scapular control patterns within comfortable ranges.",
                            "Progress load gradually while keeping motion quality high and irritation low.",
                            "Pair with posture work if forward-shoulder posture is contributing to symptoms."
                        })
                }
            )
        ));
    }

    private static void AddImtpFinding(List<Finding> findings, PerformanceAge.Result computed)
    {
        // Trigger: <75th percentile (if percentile is provided)
        var p = computed.IsometricThighPullPercentile;
        if (p is null) return;

        bool optimal = p.Value >= 75;
        if (optimal)
        {
            findings.Add(new Finding(
                Domain: "Stay Strong & Stable",
                Metric: "Global Strength (IMTP)",
                TriggerFinding: $"Optimal (≥75th percentile; {p.Value:0.#}th)",
                Why: "Global strength reserve supports independence and resilience under stress and injury.",
                IsTriggered: false,
                IsOptimalCandidate: true,
                SeverityRank: 0,
                ImpactRank: 3,
                LeverageRank: 2,
                TotalRankScore: 0,
                BuildStrategy: () => throw new InvalidOperationException("No strategy for optimal IMTP.")
            ));
            return;
        }

        int sev = SeverityFromPercentile(p.Value);
        var triggerFinding = $"{SeverityLabel(sev)} deficit ({p.Value:0.#}th percentile)";

        findings.Add(new Finding(
            Domain: "Stay Strong & Stable",
            Metric: "Global Strength (IMTP)",
            TriggerFinding: triggerFinding,
            Why: "Lower global strength reserve can reduce functional capacity and resilience and raises the importance of progressive foundational strength work.",
            IsTriggered: true,
            IsOptimalCandidate: false,
            SeverityRank: sev,
            ImpactRank: 3,
            LeverageRank: 2,
            TotalRankScore: (sev * 100) + (3 * 10) + 2,
            BuildStrategy: () => new Strategy(
                Domain: "Stay Strong & Stable",
                TriggerMetric: "IMTP",
                TriggerFinding: triggerFinding,
                WhyThisMatters: "Global strength reserve supports independence and overall resilience and makes other training safer and more effective.",
                StrategyStatement: "Improving global strength capacity appears to be a foundational opportunity to increase resilience and preserve long-term independence.",
                TacticModules: new List<TacticModule>
                {
                    new("Foundational Strength",
                        "Progressive compound and accessory strength work raises overall capacity.",
                        new List<string>
                        {
                            "Build strength with progressive loading using compound patterns while maintaining high-quality mechanics.",
                            "Use accessory work to address weak links that limit output or tolerance.",
                            "Progress conservatively to keep recovery stable and reduce injury risk."
                        })
                }
            )
        ));
    }

    private static void AddPercentileFinding(
        List<Finding> findings,
        string domain,
        string metric,
        double? percentile,
        int impactRank,
        int leverageRank,
        string why)
    {
        if (percentile is null)
        {
            return;
        }

        var sev = SeverityFromPercentile(percentile.Value);
        var isOptimal = percentile.Value >= 75;
        var triggered = !isOptimal;

        // If optimal, we only allow reassurance, no strategy.
        var triggerFinding = isOptimal
            ? $"Optimal (≥75th percentile; {percentile.Value:0.#}th)"
            : $"{SeverityLabel(sev)} deficit ({percentile.Value:0.#}th percentile)";

        findings.Add(new Finding(
            Domain: domain,
            Metric: metric,
            TriggerFinding: triggerFinding,
            Why: why,
            IsTriggered: triggered,
            IsOptimalCandidate: isOptimal,
            SeverityRank: sev,
            ImpactRank: impactRank,
            LeverageRank: leverageRank,
            TotalRankScore: (sev * 100) + (impactRank * 10) + leverageRank,
            BuildStrategy: () => new Strategy(
                Domain: domain,
                TriggerMetric: metric,
                TriggerFinding: triggerFinding,
                WhyThisMatters: why,
                StrategyStatement: BuildDefaultStrategyStatement(metric),
                TacticModules: BuildDefaultTactics(metric)
            )
        ));
    }

    private static void AddGaitFinding(List<Finding> findings, PerformanceAge.Inputs inputs)
    {
        // Non-negotiable rule: gait strategy only if maximal gait < 75th percentile.
        if (inputs.GaitSpeedMaxPercentile is null)
        {
            return;
        }

        var maxP = inputs.GaitSpeedMaxPercentile.Value;
        var maxOptimal = maxP >= 75;

        // Comfortable gait refines ranking but cannot independently trigger.
        var comfortableP = inputs.GaitSpeedComfortablePercentile;
        var comfortableNote = comfortableP is null
            ? ""
            : $" (comfortable {comfortableP.Value:0.#}th percentile)";

        if (maxOptimal)
        {
            findings.Add(new Finding(
                Domain: "Be Fit & Mobile",
                Metric: "Gait Speed (Maximum)",
                TriggerFinding: $"Optimal (≥75th percentile; {maxP:0.#}th){comfortableNote}",
                Why: "Strong maximal walking speed suggests good mobility reserve and functional resilience.",
                IsTriggered: false,
                IsOptimalCandidate: true,
                SeverityRank: 0,
                ImpactRank: 3,
                LeverageRank: 2,
                TotalRankScore: 0,
                BuildStrategy: () => throw new InvalidOperationException("No strategy for optimal gait.")
            ));
            return;
        }

        var sev = SeverityFromPercentile(maxP);
        var triggerFinding = $"{SeverityLabel(sev)} deficit (max {maxP:0.#}th percentile){comfortableNote}";

        // Ranking: if comfortable is also low, bump impact slightly.
        int impactRank = 3;
        if (comfortableP is not null && comfortableP.Value < 50) impactRank = 4; // still capped via score formula

        findings.Add(new Finding(
            Domain: "Be Fit & Mobile",
            Metric: "Gait Capacity",
            TriggerFinding: triggerFinding,
            Why: "Lower maximal gait speed reflects reduced mobility reserve, which can increase fall risk and reduce independence over time.",
            IsTriggered: true,
            IsOptimalCandidate: false,
            SeverityRank: sev,
            ImpactRank: impactRank,
            LeverageRank: 2,
            TotalRankScore: (sev * 100) + (impactRank * 10) + 2,
            BuildStrategy: () => new Strategy(
                Domain: "Be Fit & Mobile",
                TriggerMetric: "Gait Speed (Maximum)",
                TriggerFinding: triggerFinding,
                WhyThisMatters: "Maximal walking speed is a direct marker of mobility reserve — the ‘extra gear’ you rely on under fatigue, stress, or uneven terrain.",
                StrategyStatement: "Improve mobility reserve by practicing controlled, faster-paced walking (or equivalent locomotion) with planned recovery so speed can rise without provoking pain.",
                TacticModules: new List<TacticModule>
                {
                    new("Speed Reserve Practice",
                        "Building a safe ‘speed buffer’ improves functional resilience and reduces fall risk.",
                        new List<string>
                        {
                            "Use short bouts of brisk walking where form stays crisp, followed by easy recovery walking.",
                            "Prioritize smooth mechanics (quiet foot strike, stable pelvis, relaxed shoulders) over ‘pushing through.’",
                            "Progress by adding total brisk time or slightly increasing pace only when recovery stays easy."
                        })
                }
            )
        ));
    }

    private static void AddVo2AndHrrFinding(List<Finding> findings, PerformanceAge.Inputs inputs, PerformanceAge.Result computed)
    {
        var vo2P = inputs.Vo2MaxPercentile;
        var hrr = computed.HeartRateRecovery;

        // Non-negotiable trigger: VO2 <75 OR HRR <= 20 bpm
        bool vo2Triggered = vo2P is not null && vo2P.Value < 75;
        bool hrrTriggered = hrr is not null && hrr.Value <= 20;

        // If no VO2 percentile provided, we cannot trigger off VO2; still allow HRR trigger if present.
        bool anyTrigger = vo2Triggered || hrrTriggered;

        // If both are absent, nothing to evaluate.
        if (vo2P is null && hrr is null)
        {
            return;
        }

        // Optimal suppression: VO2 >=75 AND HRR normal (or not provided)
        bool vo2Optimal = vo2P is not null && vo2P.Value >= 75;
        bool hrrNormalOrMissing = hrr is null || hrr.Value > 20;

        if (!anyTrigger && vo2Optimal && hrrNormalOrMissing)
        {
            findings.Add(new Finding(
                Domain: "Be Fit & Mobile",
                Metric: "Aerobic Fitness (VO₂ Max / HRR)",
                TriggerFinding: BuildVo2HrrStatus(vo2P, hrr, isTriggered: false),
                Why: "Strong aerobic fitness supports cardiovascular resilience, metabolic efficiency, and long-term functional capacity.",
                IsTriggered: false,
                IsOptimalCandidate: true,
                SeverityRank: 0,
                ImpactRank: 4,
                LeverageRank: 3,
                TotalRankScore: 0,
                BuildStrategy: () => throw new InvalidOperationException("No strategy for optimal VO2/HRR.")
            ));
            return;
        }

        // Triggered case
        int sev = 1;
        if (vo2P is not null) sev = SeverityFromPercentile(vo2P.Value);
        if (hrrTriggered && (vo2P is null || vo2P.Value >= 50))
        {
            // HRR impairment with borderline/unknown VO2 should still rank moderately.
            sev = Math.Max(sev, 2);
        }

        var triggerFinding = BuildVo2HrrStatus(vo2P, hrr, isTriggered: true);

        // Impact/leverage: aerobic capacity is high leverage.
        int impactRank = 5;
        int leverageRank = 3;

        findings.Add(new Finding(
            Domain: "Be Fit & Mobile",
            Metric: "VO₂ Max / Heart Rate Recovery",
            TriggerFinding: triggerFinding,
            Why: "Lower aerobic fitness or slow recovery reduces physiologic reserve — making daily tasks harder and reducing resilience under stress, illness, or injury.",
            IsTriggered: true,
            IsOptimalCandidate: false,
            SeverityRank: sev,
            ImpactRank: impactRank,
            LeverageRank: leverageRank,
            TotalRankScore: (sev * 100) + (impactRank * 10) + leverageRank,
            BuildStrategy: () => BuildVo2Strategy(vo2P, hrr)
        ));
    }

    private static void AddFloorToStandFinding(List<Finding> findings, PerformanceAge.Result computed)
    {
        if (string.IsNullOrWhiteSpace(computed.FloorToStandTest)) return;

        // Trigger: Score < 8/10
        // Accept either "8"/"8/10"/"Score: 7" etc.
        var score = ParseFirstNumber(computed.FloorToStandTest);
        if (score is null)
        {
            // If we can’t parse, do not trigger (avoids generic advice).
            return;
        }

        bool optimal = score.Value >= 8;
        if (optimal)
        {
            findings.Add(new Finding(
                Domain: "Stay Strong & Stable",
                Metric: "Floor-to-Stand",
                TriggerFinding: $"Optimal (score {score.Value:0.#}/10)",
                Why: "Strong floor-to-stand ability reflects mobility, strength, and coordination that protect independence over time.",
                IsTriggered: false,
                IsOptimalCandidate: true,
                SeverityRank: 0,
                ImpactRank: 3,
                LeverageRank: 2,
                TotalRankScore: 0,
                BuildStrategy: () => throw new InvalidOperationException("No strategy for optimal floor-to-stand.")
            ));
            return;
        }

		// Severity: floor-to-stand has no percentile input in this model, so treat as qualitative severity.
		int sev = 1;

        findings.Add(new Finding(
            Domain: "Stay Strong & Stable",
            Metric: "Floor-to-Stand",
            TriggerFinding: $"Task deficit (score {score.Value:0.#}/10)",
            Why: "Difficulty transitioning from the floor can predict loss of independence and higher fall risk, especially as demands increase.",
            IsTriggered: true,
            IsOptimalCandidate: false,
            SeverityRank: 2,
            ImpactRank: 4,
            LeverageRank: 2,
            TotalRankScore: (2 * 100) + (4 * 10) + 2,
            BuildStrategy: () => new Strategy(
                Domain: "Stay Strong & Stable",
                TriggerMetric: "Floor-to-Stand",
                TriggerFinding: $"Score {score.Value:0.#}/10 (trigger <8/10)",
                WhyThisMatters: "Floor-to-stand is a practical proxy for real-world mobility, strength, and coordination — it’s ‘fall recovery’ capacity.",
                StrategyStatement: "Improve floor-to-stand capacity by practicing the movement pattern progressively, prioritizing control and confidence over speed.",
                TacticModules: new List<TacticModule>
                {
                    new("Pattern Practice",
                        "Practicing the specific transition builds coordination and reduces fear/avoidance.",
                        new List<string>
                        {
                            "Break the movement into steps (kneel → half-kneel → stand) and smooth the transitions.",
                            "Use supports (chair/bench) as needed, reducing reliance as control improves.",
                            "Add light loaded carries or get-ups only once pain-free and consistent."
                        })
                }
            )
        ));
    }

    private static void AddPostureFinding(List<Finding> findings, PerformanceAge.Result computed)
    {
        if (string.IsNullOrWhiteSpace(computed.PostureAssessment)) return;

        // Trigger: Tragus-to-wall > 10 cm (accept numeric in string)
        var cm = ParseFirstNumber(computed.PostureAssessment);
        if (cm is null)
        {
            // If not parsable, do not trigger (avoids generic posture advice).
            return;
        }

        bool triggered = cm.Value > 10;
        bool optimal = !triggered;

        if (optimal)
        {
            findings.Add(new Finding(
                Domain: "Stay Strong & Stable",
                Metric: "Posture (Tragus-to-Wall)",
                TriggerFinding: $"Optimal (≤10 cm; ~{cm.Value:0.#} cm)",
                Why: "Good postural alignment supports efficient breathing mechanics and reduces compensatory loading.",
                IsTriggered: false,
                IsOptimalCandidate: true,
                SeverityRank: 0,
                ImpactRank: 1,
                LeverageRank: 1,
                TotalRankScore: 0,
                BuildStrategy: () => throw new InvalidOperationException("No strategy for optimal posture.")
            ));
            return;
        }

        // Severity: posture trigger is based on a measurement threshold (cm), not a percentile input.
        int sev = 1;

        findings.Add(new Finding(
            Domain: "Stay Strong & Stable",
            Metric: "Posture (Tragus-to-Wall)",
            TriggerFinding: $"Postural offset (>10 cm; ~{cm.Value:0.#} cm)",
            Why: "Postural offsets can shift joint loading and contribute to neck/shoulder discomfort, limited overhead capacity, and inefficient breathing.",
            IsTriggered: true,
            IsOptimalCandidate: false,
            SeverityRank: 1,
            ImpactRank: 2,
            LeverageRank: 1,
            TotalRankScore: (1 * 100) + (2 * 10) + 1,
            BuildStrategy: () => new Strategy(
                Domain: "Stay Strong & Stable",
                TriggerMetric: "Posture (Tragus-to-Wall)",
                TriggerFinding: $"~{cm.Value:0.#} cm (trigger >10 cm)",
                WhyThisMatters: "Improving alignment can reduce compensations and unlock more efficient strength and aerobic work.",
                StrategyStatement: "Improve postural alignment by pairing thoracic mobility with targeted upper-back endurance and frequent low-dose postural resets.",
                TacticModules: new List<TacticModule>
                {
                    new("Mobility + Endurance Pairing",
                        "Mobility creates range; endurance ‘keeps’ it.",
                        new List<string>
                        {
                            "Emphasize thoracic extension and controlled scapular movement within comfortable ranges.",
                            "Build upper-back endurance (light, high-quality reps) to maintain neutral posture longer.",
                            "Use brief posture ‘check-ins’ during the day to reduce accumulated forward-head drift."
                        })
                }
            )
        ));
    }

    private static void AddMobilityFinding(List<Finding> findings, PerformanceAge.Result computed)
    {
        // Spec trigger: pain or restricted motion.
        // Prefer the explicit MobilityRom field if provided; otherwise fall back to other qualitative fields.
        bool explicitIssue = IsQualitativeIssue(computed.MobilityRom);
        bool inferredIssue = !explicitIssue && (
            IsQualitativeIssue(computed.PostureAssessment) ||
            IsQualitativeIssue(computed.HipStrength) ||
            IsQualitativeIssue(computed.CalfStrength) ||
            IsQualitativeIssue(computed.RotatorCuffIntegrity)
        );

        if (!explicitIssue && !inferredIssue)
        {
            // If MobilityRom is explicitly provided and looks normal, allow reassurance.
            if (!string.IsNullOrWhiteSpace(computed.MobilityRom))
            {
                findings.Add(new Finding(
                    Domain: "Stay Strong & Stable",
                    Metric: "Mobility / ROM",
                    TriggerFinding: "Optimal",
                    Why: "Good mobility and pain-free range supports efficient strength and aerobic work.",
                    IsTriggered: false,
                    IsOptimalCandidate: true,
                    SeverityRank: 0,
                    ImpactRank: 2,
                    LeverageRank: 2,
                    TotalRankScore: 0,
                    BuildStrategy: () => throw new InvalidOperationException("No strategy for optimal mobility/ROM.")
                ));
            }
            return;
        }

        findings.Add(new Finding(
            Domain: "Stay Strong & Stable",
            Metric: "Mobility / ROM",
            TriggerFinding: string.IsNullOrWhiteSpace(computed.MobilityRom) ? "Pain or restricted motion (inferred)" : computed.MobilityRom.Trim(),
            Why: "Mobility restrictions can drive compensations that increase injury risk and limit training options.",
            IsTriggered: true,
            IsOptimalCandidate: false,
            SeverityRank: 1,
            ImpactRank: 2,
            LeverageRank: 2,
            TotalRankScore: (1 * 100) + (2 * 10) + 2,
            BuildStrategy: () => new Strategy(
                Domain: "Stay Strong & Stable",
                TriggerMetric: "Mobility / ROM",
                TriggerFinding: "Pain/restriction reported",
                WhyThisMatters: "Restoring comfortable range can reduce compensations and enable safer strength and aerobic progression.",
                StrategyStatement: "Address the limiting joint(s) with pain-free mobility work and gradual loading through end ranges to rebuild tolerance.",
                TacticModules: new List<TacticModule>
                {
                    new("Pain-Free Range Expansion",
                        "The goal is to expand comfortable range first, then load it.",
                        new List<string>
                        {
                            "Stay in ranges that feel ‘challenging but safe’ — avoid sharp pain.",
                            "Progress from isometrics → slow eccentrics → full-range loading as tolerance improves.",
                            "If a movement reliably flares pain, regress the range or reduce load and rebuild."
                        })
                }
            )
        ));
    }

    private static void AddTrunkEnduranceFinding(List<Finding> findings, PerformanceAge.Result computed)
    {
        if (string.IsNullOrWhiteSpace(computed.TrunkEndurance)) return;

        // Trigger: <75th percentile OR explicit limitation language.
        bool pctTrigger = IsPercentBelow(computed.TrunkEndurance, 75);
        bool qualTrigger = IsQualitativeIssue(computed.TrunkEndurance);

        if (!pctTrigger && !qualTrigger)
        {
            findings.Add(new Finding(
                Domain: "Stay Strong & Stable",
                Metric: "Trunk Endurance",
                TriggerFinding: "Optimal",
                Why: "Trunk endurance supports safe force transfer, posture under fatigue, and reduces compensation-driven overuse.",
                IsTriggered: false,
                IsOptimalCandidate: true,
                SeverityRank: 0,
                ImpactRank: 2,
                LeverageRank: 2,
                TotalRankScore: 0,
                BuildStrategy: () => throw new InvalidOperationException("No strategy for optimal trunk endurance.")
            ));
            return;
        }

		// Severity: percentile-based deficits get higher severity than qualitative-only flags.
		int sev = pctTrigger ? 2 : 1;

        findings.Add(new Finding(
            Domain: "Stay Strong & Stable",
            Metric: "Trunk Endurance",
            TriggerFinding: pctTrigger ? "Sub-optimal trunk endurance (<75th percentile)" : "Sub-optimal trunk endurance (reported)",
            Why: "Trunk endurance supports safe force transfer, posture under fatigue, and reduces compensation-driven overuse.",
            IsTriggered: true,
            IsOptimalCandidate: false,
            SeverityRank: sev,
            ImpactRank: 2,
            LeverageRank: 2,
            TotalRankScore: (sev * 100) + (2 * 10) + 2,
            BuildStrategy: () => new Strategy(
                Domain: "Stay Strong & Stable",
                TriggerMetric: "Trunk Endurance",
                TriggerFinding: "Reported deficit",
                WhyThisMatters: "Better trunk endurance improves movement efficiency and lowers injury risk as intensity rises.",
                StrategyStatement: "Build trunk endurance with progressive anti-extension/anti-rotation work and bracing practice under low fatigue.",
                TacticModules: new List<TacticModule>
                {
                    new("Endurance First",
                        "Endurance capacity creates a stable platform for strength and gait mechanics.",
                        new List<string>
                        {
                            "Prioritize high-quality holds/reps over intensity; stop before form degrades.",
                            "Train multiple vectors (front/side/rotation) to reduce weak-link compensation.",
                            "Layer trunk control into carries and hinging patterns once baseline endurance improves."
                        })
                }
            )
        ));
    }

    private static void AddQualitativeIssueFinding(
        List<Finding> findings,
        string domain,
        string metric,
        string? qualitative,
        int impactRank,
        int leverageRank,
        string why)
    {
        if (string.IsNullOrWhiteSpace(qualitative)) return;
        if (!IsQualitativeIssue(qualitative))
        {
            // If it reads like “normal/ok”, treat as reassurance-eligible.
            findings.Add(new Finding(
                Domain: domain,
                Metric: metric,
                TriggerFinding: "Optimal / no issues reported",
                Why: why,
                IsTriggered: false,
                IsOptimalCandidate: true,
                SeverityRank: 0,
                ImpactRank: impactRank,
                LeverageRank: leverageRank,
                TotalRankScore: 0,
                BuildStrategy: () => throw new InvalidOperationException("No strategy for optimal qualitative metric.")
            ));
            return;
        }

        findings.Add(new Finding(
            Domain: domain,
            Metric: metric,
            TriggerFinding: "Pain / restriction / weakness reported",
            Why: why,
            IsTriggered: true,
            IsOptimalCandidate: false,
            SeverityRank: 1,
            ImpactRank: impactRank,
            LeverageRank: leverageRank,
            TotalRankScore: (1 * 100) + (impactRank * 10) + leverageRank,
            BuildStrategy: () => new Strategy(
                Domain: domain,
                TriggerMetric: metric,
                TriggerFinding: "Issue reported",
                WhyThisMatters: why,
                StrategyStatement: $"Address the limiting factor in {metric.ToLowerInvariant()} with pain-free patterning first, then progressive loading once control and tolerance improve.",
                TacticModules: new List<TacticModule>
                {
                    new("Stability + Capacity",
                        "Improve control first, then add load and volume to make the change durable.",
                        new List<string>
                        {
                            "Start with pain-free isometrics and controlled range work.",
                            "Progress to slow eccentrics and full-range strength when symptoms stay calm.",
                            "Keep the rest of training supportive (avoid movements that repeatedly flare the area)."
                        })
                }
            )
        ));
    }

    // ----------------------------
    // Strategy text helpers
    // ----------------------------

    private static Strategy BuildVo2Strategy(double? vo2Percentile, double? hrr)
    {
        bool vo2Optimal = vo2Percentile is not null && vo2Percentile.Value >= 75;
        bool hrrImpaired = hrr is not null && hrr.Value <= 20;

        // If VO2 is optimal but HRR impaired → Zone 2 emphasis, conservative intensity.
        if (vo2Optimal && hrrImpaired)
        {
            var finding = BuildVo2HrrStatus(vo2Percentile, hrr, isTriggered: true);
            return new Strategy(
                Domain: "Be Fit & Mobile",
                TriggerMetric: "Heart Rate Recovery",
                TriggerFinding: finding,
                WhyThisMatters: "Slow recovery can reflect limited autonomic resilience and reduced tolerance for repeated high-demand efforts.",
                StrategyStatement: "Maintain aerobic fitness while improving recovery by prioritizing steady aerobic work and using higher intensity sparingly until recovery improves.",
                TacticModules: new List<TacticModule>
                {
                    new("Aerobic Base Priority",
                        "Steady aerobic training improves efficiency and often improves recovery capacity.",
                        new List<string>
                        {
                            "Prioritize consistent, moderate efforts you can sustain without ‘spiking’ effort.",
                            "Track recovery quality (HRR trends, perceived recovery) rather than chasing intensity.",
                            "Re-introduce intervals only when recovery improves and fatigue is stable."
                        })
                }
            );
        }

        // Standard VO2/HRR strategy
        return new Strategy(
            Domain: "Be Fit & Mobile",
            TriggerMetric: "VO₂ Max / Heart Rate Recovery",
            TriggerFinding: BuildVo2HrrStatus(vo2Percentile, hrr, isTriggered: true),
            WhyThisMatters: "Improving aerobic capacity is one of the highest-leverage levers for healthspan: it supports metabolic flexibility, cardiovascular resilience, and daily functional reserve.",
            StrategyStatement: "Enhance aerobic capacity by building a steady aerobic base and layering in carefully dosed higher-intensity work to raise VO₂ Max while supporting recovery.",
            TacticModules: new List<TacticModule>
            {
                new("Build an Aerobic Base",
                    "Steady aerobic work builds endurance reserve and improves efficiency.",
                    new List<string>
                    {
                        "Accumulate consistent moderate-intensity work where breathing is controlled and sustainable.",
                        "Progress volume gradually while keeping effort stable.",
                        "Use low-impact modalities if joints are limiting (bike/row/elliptical) to maintain consistency."
                    }),
                new("Layer Carefully Dosed Intensity",
                    "A small amount of intensity can improve maximal capacity when recovery tolerates it.",
                    new List<string>
                    {
                        "Introduce short, controlled intervals with full recovery between bouts.",
                        "Keep intensity proportional — the goal is adaptation, not exhaustion.",
                        "If recovery worsens, reduce interval frequency or intensity and rebuild base volume."
                    })
            }
        );
    }

    private static string BuildDefaultStrategyStatement(string metric) => metric switch
    {
        "Quadriceps Strength" => "Improve lower-body strength by emphasizing controlled knee-dominant patterns and progressive loading once technique is stable.",
        "Grip Strength" => "Improve overall strength capacity with progressive full-body training while adding targeted grip work to close the gap.",
        "Power" => "Improve power by building strength first, then adding faster concentric intent and controlled plyometric progressions.",
        "Balance" => "Improve balance by training stability under gradually increasing sensory and mechanical challenge, without provoking fear or pain.",
        "Chair Rise" => "Improve sit-to-stand capacity by pairing lower-body strength with repeated, high-quality practice of the rising pattern.",
        _ => "Target the limiting capability with progressive practice and loading while keeping symptom response calm."
    };

    private static List<TacticModule> BuildDefaultTactics(string metric) => metric switch
    {
        "Quadriceps Strength" => new()
        {
            new("Strength Foundation",
                "Quadriceps strength improves stair-climbing, chair rise, and gait reserve.",
                new List<string>
                {
                    "Use controlled knee-dominant work (split squats, step-ups, leg press) within pain-free ranges.",
                    "Progress load or reps slowly; keep form strict and knee tracking stable.",
                    "If knee pain is present, bias tempo/isometrics and reduce depth until tolerated."
                })
        },
        "Grip Strength" => new()
        {
            new("Carry + Hold Progressions",
                "Grip responds well to frequent, submaximal exposure.",
                new List<string>
                {
                    "Use loaded carries or timed holds at a challenging but controlled intensity.",
                    "Train both crush grip (closing) and support grip (holding) over the week.",
                    "Keep shoulders down/back to avoid turning grip work into neck tension."
                })
        },
        "Power" => new()
        {
            new("Speed Intent",
                "Power is strength expressed quickly — improve rate of force development.",
                new List<string>
                {
                    "Use lighter loads with ‘move fast’ intent while staying controlled.",
                    "Progress to low-volume plyometrics only if landings are quiet and joints tolerate it.",
                    "Prioritize recovery between high-power sets to preserve speed output."
                })
        },
        "Balance" => new()
        {
            new("Progressive Stability",
                "Balance improves through progressive exposure to challenge.",
                new List<string>
                {
                    "Start with stable single-leg holds, then add head turns, reach patterns, or softer surfaces.",
                    "Keep sessions frequent and short rather than occasional and brutal.",
                    "If dizziness or pain occurs, regress the challenge and rebuild confidence."
                })
        },
        "Chair Rise" => new()
        {
            new("Pattern + Strength",
                "Practice the specific pattern while improving strength to make it easier.",
                new List<string>
                {
                    "Use repeated sit-to-stand practice with consistent tempo and alignment.",
                    "Increase challenge by lowering chair height gradually or adding light load when form stays clean.",
                    "Pair with lower-body strength work to raise the ‘ceiling’ of the pattern."
                })
        },
        _ => new List<TacticModule>()
    };

    // ----------------------------
    // Reassurance logic
    // ----------------------------

    private static List<Reassurance> BuildReassurances(List<Finding> optimalCandidates)
    {
        // Spec: prefer domain-level reassurance over metric-level, and keep it short (1–2).
        // We treat a domain as “reassurance-worthy” if it has at least 2 independently optimal metrics.
        var groups = optimalCandidates
            .GroupBy(f => f.Domain)
            .OrderByDescending(g => g.Count())
            .ToList();

        var outList = new List<Reassurance>();

        // 1) Domain-level first
        foreach (var g in groups)
        {
            if (outList.Count >= 2) break;
            if (g.Count() < 2) continue;

            outList.Add(new Reassurance(
                Domain: g.Key,
                Metric: "Domain",
                Status: "Optimal",
                ReassuranceText: BuildDomainReassuranceText(g.Key)
            ));
        }

        // 2) If we still have room, add a single metric-level reassurance
        if (outList.Count < 2)
        {
            var pick = optimalCandidates
                .OrderByDescending(f => f.ImpactRank)
                .ThenByDescending(f => f.LeverageRank)
                .FirstOrDefault();

            if (pick is not null)
            {
                outList.Add(new Reassurance(
                    Domain: pick.Domain,
                    Metric: pick.Metric,
                    Status: "Optimal",
                    ReassuranceText: BuildMetricReassuranceText(pick.Domain, pick.Metric)
                ));
            }
        }

        // Cap at 2 no matter what.
        return outList.Take(2).ToList();
    }

    private static string BuildDomainReassuranceText(string domain) => domain switch
    {
        "Be Fit & Mobile" => "Be Fit & Mobile looks strong overall. Keep consistency high — it’s the maintenance dose that protects your reserve.",
        "Stay Strong & Stable" => "Stay Strong & Stable looks strong overall. Maintain it with steady strength + balance work so it stays a protective asset.",
        _ => "This domain looks strong overall. Maintain it so it stays a protective asset over time."
    };

    private static string BuildMetricReassuranceText(string domain, string metric)
    {
        if (domain == "Be Fit & Mobile")
            return "This marker looks strong. Keep it topped up with consistent training and smart recovery.";

        return "This marker looks strong. Maintain it with steady practice so it stays protective over time.";
    }

    // ----------------------------
    // Severity + parsing helpers
    // ----------------------------

    private static int SeverityFromPercentile(double p)
    {
        // Optimal: >=75 => 0 (not a deficit)
        if (p >= 75) return 0;
        if (p >= 50) return 1; // mild
        if (p >= 26) return 2; // moderate
        return 3;              // severe
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
        // Look for explicit percent values (e.g., "72%", "Percent: 60")
        if (t.Contains('%'))
        {
            var n = ParseFirstNumber(t);
            return n is not null && n.Value < threshold;
        }
        // Allow formats like "0.72" to mean 72% if it looks like a ratio
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

        // Conservative: only interpret as CTSIB failure if the text suggests CTSIB-like testing.
        if (!t.Contains("ctsib") && !t.Contains("sensory") && !t.Contains("foam") && !t.Contains("eyes closed") && !t.Contains("modified"))
        {
            return false;
        }

        // If any explicitly recorded hold time is <30 seconds, trigger.
        // Examples: "position 4: 18s", "hold 25 sec", "20s" etc.
        if (t.Contains("s"))
        {
            var n = ParseFirstNumber(balanceAssessment);
            if (n is not null && n.Value < 30) return true;
        }

        // Also allow explicit failure language.
        return ContainsFailFlag(balanceAssessment);
    }

    private static double? ParseFirstNumber(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        // Extract the first token that parses as a double (handles "8/10" -> 8)
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

        // Treat explicit “normal/ok” language as non-issue.
        if (t is "normal" or "none" or "no" or "ok" or "okay" or "good" or "wnl" or "within normal limits")
            return false;

        // Trigger words
        string[] triggers =
        {
            "pain", "hurt", "aching", "ache", "restricted", "limit", "limited", "stiff", "tight",
            "weak", "unstable", "instability", "cannot", "can't", "unable", "reduced", "poor"
        };

        return triggers.Any(t.Contains);
    }
}
