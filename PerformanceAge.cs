using System;

public static class PerformanceAge
{
    public sealed record Inputs(
        double ChronologicalAgeYears,

        double? Vo2MaxPercentile,                 // Z1
        double? QuadricepsStrengthPercentile,     // Z2
        double? GripStrengthPercentile,           // Z3
        double? GaitSpeedComfortablePercentile,   // Z4
        double? GaitSpeedMaxPercentile,           // Z5
        double? PowerPercentile,                  // Z6
        double? BalancePercentile,                // Z7
        double? ChairRisePercentile               // Z8
    );

    public sealed record ZDetail(double PercentOfAge, double ContributionYears);

    public sealed record Result(
        double SumContributionYears,      // SUM(F13:F20)
        double ContributionYearsScaled,   // 0.3 * Sum
        double PerformanceAge,            // Age + scaled
        double DeltaVsAgeYears,           // PerformanceAge - Age
        double DeltaVsAgePercent,         // PerformanceAge / Age - 1

        ZDetail? Z1_Vo2Max,
        ZDetail? Z2_Quadriceps,
        ZDetail? Z3_Grip,
        ZDetail? Z4_GaitComfortable,
        ZDetail? Z5_GaitMax,
        ZDetail? Z6_Power,
        ZDetail? Z7_Balance,
        ZDetail? Z8_ChairRise,

    // ---- Additional Performance Metrics (not yet used in the percentile-based calculation) ----
    // Included so future scoring expansions can use them without changing the existing output fields above.
    double? Vo2Max = null,                 // e.g., ml/kg/min
    double? HeartRateRecovery = null,      // e.g., bpm drop
    string? FloorToStandTest = null,
    string? TrunkEndurance = null,
    string? PostureAssessment = null,
    string? HipStrength = null,
    string? CalfStrength = null,
    string? RotatorCuffIntegrity = null,
    double? IsometricThighPull = null
    );

    public static Result Calculate(Inputs x)
    {
        if (x.ChronologicalAgeYears <= 0) throw new ArgumentOutOfRangeException(nameof(x.ChronologicalAgeYears));

        var z1 = MakeZ(x.Vo2MaxPercentile, Vo2Pct, x.ChronologicalAgeYears);
        var z2 = MakeZ(x.QuadricepsStrengthPercentile, Std10_25_50_75, x.ChronologicalAgeYears);
        var z3 = MakeZ(x.GripStrengthPercentile, GripPct, x.ChronologicalAgeYears);
        var z4 = MakeZ(x.GaitSpeedComfortablePercentile, GripPct, x.ChronologicalAgeYears); // same buckets as grip
        var z5 = MakeZ(x.GaitSpeedMaxPercentile, Std10_25_50_75, x.ChronologicalAgeYears);
        var z6 = MakeZ(x.PowerPercentile, Std10_25_50_75, x.ChronologicalAgeYears);
        var z7 = MakeZ(x.BalancePercentile, BalancePct, x.ChronologicalAgeYears);
        var z8 = MakeZ(x.ChairRisePercentile, Std10_25_50_75, x.ChronologicalAgeYears);

        double sumYears =
            (z1?.ContributionYears ?? 0) +
            (z2?.ContributionYears ?? 0) +
            (z3?.ContributionYears ?? 0) +
            (z4?.ContributionYears ?? 0) +
            (z5?.ContributionYears ?? 0) +
            (z6?.ContributionYears ?? 0) +
            (z7?.ContributionYears ?? 0) +
            (z8?.ContributionYears ?? 0);

        // Excel outputs:
        // B6 = sumYears
        // B7 = B6 * 0.3
        // B8 = Age + B7
        double scaled = sumYears * 0.3;
        double perfAge = x.ChronologicalAgeYears + scaled;

        double deltaYears = perfAge - x.ChronologicalAgeYears;
        double deltaPct = (perfAge / x.ChronologicalAgeYears) - 1.0;

        return new Result(
            SumContributionYears: sumYears,
            ContributionYearsScaled: scaled,
            PerformanceAge: perfAge,
            DeltaVsAgeYears: deltaYears,
            DeltaVsAgePercent: deltaPct,
            Z1_Vo2Max: z1,
            Z2_Quadriceps: z2,
            Z3_Grip: z3,
            Z4_GaitComfortable: z4,
            Z5_GaitMax: z5,
            Z6_Power: z6,
            Z7_Balance: z7,
            Z8_ChairRise: z8
        );
    }

    private static ZDetail? MakeZ(double? percentile, Func<double, double> pctFn, double age)
    {
        if (percentile is null) return null;
        var p = percentile.Value;
        var pct = pctFn(p);
        return new ZDetail(pct, age * pct);
    }

    // Excel E13:
    // <25 => +0.25
    // <50 => +0.10
    // <75 => -0.05
    // <97.5 => -0.15
    // else => -0.25
    private static double Vo2Pct(double p) =>
        p < 25 ? 0.25 :
        p < 50 ? 0.10 :
        p < 75 ? -0.05 :
        p < 97.5 ? -0.15 :
        -0.25;

    // Excel E14/E17/E18/E20:
    // <10 => +0.10
    // <25 => +0.05
    // <50 => 0
    // <75 => -0.05
    // else => -0.10
    private static double Std10_25_50_75(double p) =>
        p < 10 ? 0.10 :
        p < 25 ? 0.05 :
        p < 50 ? 0.0 :
        p < 75 ? -0.05 :
        -0.10;

    // Excel E15/E16:
    // <10 => +0.15
    // <25 => +0.07
    // <50 => 0
    // <75 => -0.07
    // else => -0.15
    private static double GripPct(double p) =>
        p < 10 ? 0.15 :
        p < 25 ? 0.07 :
        p < 50 ? 0.0 :
        p < 75 ? -0.07 :
        -0.15;

    // Excel E19:
    // <10 => +0.05
    // <25 => +0.025
    // <50 => 0
    // <75 => -0.025
    // else => -0.05
    private static double BalancePct(double p) =>
        p < 10 ? 0.05 :
        p < 25 ? 0.025 :
        p < 50 ? 0.0 :
        p < 75 ? -0.025 :
        -0.05;
}
