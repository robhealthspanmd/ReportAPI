using System;

public static class HealthAge
{
    public sealed record Inputs(
        double ChronologicalAgeYears,     // B3
        double PhenotypicAgeYears,
        string Sex,        // B4 (already computed via PhenoAge endpoint)

        // Z1–Z3 percentiles (0–100)
        double? BodyFatPercentile,               // B15
        double? VisceralFatPercentile,           // B16
        double? AppendicularMusclePercentile,    // B17

        // Z4 blood pressure
        double? SystolicBP,                      // B18
        double? DiastolicBP,                     // C18

        // Z5 Non-HDL + risk group (Low/Moderate/High)
        double? NonHdlMgDl,                      // B19
        string? NonHdlRiskGroup,                 // C19 ("Low", "Moderate", "High")

        // Z6 insulin resistance (HOMA-IR derived): insulin * glucose / 405
        double? FastingInsulin_uIU_mL,           // B20
        double? FastingGlucose_mg_dL,            // C20

        // Z7 TG/HDL ratio derived
        double? Triglycerides_mg_dL,             // B21
        double? Hdl_mg_dL,                       // C21

        // Z8 FIB-4 score
        double? Fib4Score  ,
        double? BodyFatPercentage,
double? TotalFatMass,
double? TotalFatMassPerHeight,
double? VisceralFatMass,
double? TotalLeanMass,
double? TotalLeanMassPerHeight,
double? AppendicularLeanMass,
double? TotalCholesterol,
double? TriglyceridesHdlRatio,
double? Ast,
double? Alt,
double? Platelets,
double? HomaIr,
double? HemoglobinA1c
                      // B22
    );

    public sealed record ZDetail(double PercentOfAge, double ContributionYears);

    public sealed record Result(
        double SumContributionYears,     // ΣZ years (B7)
        double HealthAgeUncapped,        // Pheno + ΣZ (B8)
        double ContributionYearsScaled,  // ΣZ * 0.3 (B9)
        double HealthAgeFinal,           // Pheno + ΣZ*0.3 (B10)
        double DeltaVsChronoYears,       // (B11)
        double DeltaVsChronoPercent,     // (B12)

        // Optional details for debugging/UI
        ZDetail? Z1_BodyFat,
        ZDetail? Z2_VisceralFat,
        ZDetail? Z3_AppendicularMuscle,
        ZDetail? Z4_BloodPressure,
        ZDetail? Z5_NonHdl,
        ZDetail? Z6_HomaIr,
        ZDetail? Z7_TgHdl,
        ZDetail? Z8_Fib4
    );

    public static Result Calculate(Inputs x)
    {
        if (x.ChronologicalAgeYears <= 0) throw new ArgumentOutOfRangeException(nameof(x.ChronologicalAgeYears));
        if (x.PhenotypicAgeYears <= 0) throw new ArgumentOutOfRangeException(nameof(x.PhenotypicAgeYears));

        ZDetail? z1 = MakeZ(x.BodyFatPercentile, p => p > 85 ? 0.08 : p >= 50 ? 0.03 : p >= 26 ? -0.03 : p <= 25 ? -0.08 : (double?)null, x.ChronologicalAgeYears);
        ZDetail? z2 = MakeZ(x.VisceralFatPercentile, p => p > 85 ? 0.15 : p >= 50 ? 0.07 : p >= 21 ? -0.07 : p <= 20 ? -0.15 : (double?)null, x.ChronologicalAgeYears);
        ZDetail? z3 = MakeZ(x.AppendicularMusclePercentile, p => p > 80 ? -0.12 : p >= 50 ? -0.06 : p >= 21 ? 0.06 : p <= 20 ? 0.12 : (double?)null, x.ChronologicalAgeYears);

        ZDetail? z4 = null;
        if (x.SystolicBP is not null && x.DiastolicBP is not null)
        {
            var sys = x.SystolicBP.Value;
            var dia = x.DiastolicBP.Value;

            // From sheet notes:
            // >150 or >90 => +20%
            // >=130 OR >=80 => +10%
            // 116–129 & <80 => −15%
            // <115 & <80 => −20%
            double percent =
                (sys > 150 || dia > 90) ? 0.20 :
                (sys >= 130 || dia >= 80) ? 0.10 :
                (sys < 115 && dia < 80) ? -0.20 :
                (sys >= 116 && sys <= 129 && dia < 80) ? -0.15 :
                0.0;

            z4 = new ZDetail(percent, x.ChronologicalAgeYears * percent);
        }

        ZDetail? z5 = null;
        if (x.NonHdlMgDl is not null && !string.IsNullOrWhiteSpace(x.NonHdlRiskGroup))
        {
            var nonHdl = x.NonHdlMgDl.Value;
            var grp = x.NonHdlRiskGroup!.Trim().ToLowerInvariant();

            double percent = grp switch
            {
                "low" or "1" => // Low risk
                    nonHdl < 130 ? -0.12 :
                    nonHdl <= 159 ? -0.06 :
                    nonHdl <= 189 ? 0.02 :
                    0.12,

                "moderate" or "2" => // Moderate risk
                    nonHdl < 100 ? -0.15 :
                    nonHdl <= 129 ? -0.08 :
                    nonHdl <= 159 ? 0.0 :
                    0.15,

                "high" or "3" => // High risk
                    nonHdl < 50 ? -0.20 :
                    nonHdl <= 74 ? -0.10 :
                    nonHdl <= 99 ? 0.0 :
                    0.20,

                _ => throw new ArgumentException("NonHdlRiskGroup must be Low, Moderate, or High.", nameof(x.NonHdlRiskGroup))
            };

            z5 = new ZDetail(percent, x.ChronologicalAgeYears * percent);
        }

        // Z6 HOMA-IR = insulin * glucose / 405
        ZDetail? z6 = null;
        if (x.FastingInsulin_uIU_mL is not null && x.FastingGlucose_mg_dL is not null)
        {
            var insulin = x.FastingInsulin_uIU_mL.Value;
            var glucose = x.FastingGlucose_mg_dL.Value;
            if (glucose != 0)
            {
                var homa = glucose * insulin / 405.0;
                double percent =
                    homa < 1 ? -0.15 :
                    homa < 2 ? 0.07 :
                    homa < 3 ? 0.0 :
                    homa < 4 ? 0.07 :
                    0.15;

                z6 = new ZDetail(percent, x.ChronologicalAgeYears * percent);
            }
        }

        // Z7 TG/HDL ratio = TG / HDL
        ZDetail? z7 = null;
        if (x.Triglycerides_mg_dL is not null && x.Hdl_mg_dL is not null)
        {
            var tg = x.Triglycerides_mg_dL.Value;
            var hdl = x.Hdl_mg_dL.Value;
            if (hdl != 0)
            {
                var ratio = tg / hdl;
                double percent =
                    ratio < 1 ? -0.10 :
                    ratio < 2 ? -0.05 :
                    ratio < 3 ? 0.0 :
                    ratio < 4 ? 0.05 :
                    0.10;

                z7 = new ZDetail(percent, x.ChronologicalAgeYears * percent);
            }
        }

        // Z8 FIB-4 score
        ZDetail? z8 = null;
        if (x.Fib4Score is not null)
        {
            var fib4 = x.Fib4Score.Value;
            double percent =
                fib4 < 1 ? -0.08 :
                fib4 < 1.3 ? -0.04 :
                fib4 <= 2.66 ? 0.0 :
                fib4 <= 3.24 ? 0.05 :
                0.10;

            z8 = new ZDetail(percent, x.ChronologicalAgeYears * percent);
        }

        double sumYears =
            (z1?.ContributionYears ?? 0) +
            (z2?.ContributionYears ?? 0) +
            (z3?.ContributionYears ?? 0) +
            (z4?.ContributionYears ?? 0) +
            (z5?.ContributionYears ?? 0) +
            (z6?.ContributionYears ?? 0) +
            (z7?.ContributionYears ?? 0) +
            (z8?.ContributionYears ?? 0);

        double healthAgeUncapped = x.PhenotypicAgeYears + sumYears;
        double scaled = sumYears * 0.3;               // <-- matches Excel B9
        double healthAgeFinal = x.PhenotypicAgeYears + scaled;

        double deltaYears = healthAgeFinal - x.ChronologicalAgeYears;
        double deltaPct = (healthAgeFinal / x.ChronologicalAgeYears) - 1.0;

        return new Result(
            SumContributionYears: sumYears,
            HealthAgeUncapped: healthAgeUncapped,
            ContributionYearsScaled: scaled,
            HealthAgeFinal: healthAgeFinal,
            DeltaVsChronoYears: deltaYears,
            DeltaVsChronoPercent: deltaPct,
            Z1_BodyFat: z1,
            Z2_VisceralFat: z2,
            Z3_AppendicularMuscle: z3,
            Z4_BloodPressure: z4,
            Z5_NonHdl: z5,
            Z6_HomaIr: z6,
            Z7_TgHdl: z7,
            Z8_Fib4: z8
        );
    }

    private static ZDetail? MakeZ(double? input, Func<double, double?> pctFn, double chronoAge)
    {
        if (input is null) return null;
        var pct = pctFn(input.Value);
        if (pct is null) return null;
        return new ZDetail(pct.Value, chronoAge * pct.Value);
    }
}
