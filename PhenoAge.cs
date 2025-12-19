using System;

public static class PhenoAge
{
    public sealed record Inputs(
        double ChronologicalAgeYears,      // years
        double Albumin_g_dL,               // g/dL
        double Creatinine_mg_dL,           // mg/dL
        double Glucose_mg_dL,              // mg/dL
        double CRP_mg_L,                   // mg/L
        double LymphocytePercent,          // %
        double MCV_fL,                     // fL
        double RDW_percent,                // %
        double AlkalinePhosphatase_U_L,    // U/L
        double WBC_10e3_per_uL             // 10^3/µL
    );

    public sealed record Result(
        double Xb,
        double Mortality10Yr,
        double PhenotypicAgeYears
    );

    public static Result Calculate(Inputs x)
    {
        // strict checks to prevent NaNs / ln(0)
        if (x.ChronologicalAgeYears <= 0) throw new ArgumentOutOfRangeException(nameof(x.ChronologicalAgeYears));
        if (x.Albumin_g_dL <= 0) throw new ArgumentOutOfRangeException(nameof(x.Albumin_g_dL));
        if (x.Creatinine_mg_dL <= 0) throw new ArgumentOutOfRangeException(nameof(x.Creatinine_mg_dL));
        if (x.Glucose_mg_dL <= 0) throw new ArgumentOutOfRangeException(nameof(x.Glucose_mg_dL));
        if (x.CRP_mg_L <= 0) throw new ArgumentOutOfRangeException(nameof(x.CRP_mg_L), "CRP must be > 0 because ln(CRP) is used.");
        if (x.MCV_fL <= 0) throw new ArgumentOutOfRangeException(nameof(x.MCV_fL));
        if (x.RDW_percent <= 0) throw new ArgumentOutOfRangeException(nameof(x.RDW_percent));
        if (x.AlkalinePhosphatase_U_L <= 0) throw new ArgumentOutOfRangeException(nameof(x.AlkalinePhosphatase_U_L));
        if (x.WBC_10e3_per_uL <= 0) throw new ArgumentOutOfRangeException(nameof(x.WBC_10e3_per_uL));

        // Unit conversions to match the model
        double age_years = x.ChronologicalAgeYears;
        double albumin_g_L = x.Albumin_g_dL * 10.0;
        double creatinine_umol_L = x.Creatinine_mg_dL * 88.4;
        double glucose_mmol_L = x.Glucose_mg_dL * 0.05551;
        double crp_mg_dL = x.CRP_mg_L / 10.0;
        double ln_crp = Math.Log(crp_mg_dL);

        double lymph_pct = x.LymphocytePercent;
        double mcv_fL = x.MCV_fL;
        double rdw_pct = x.RDW_percent;
        double alp_u_L = x.AlkalinePhosphatase_U_L;
        double wbc_10e3_uL = x.WBC_10e3_per_uL;

        // Coefficients
        const double b0 = -19.9067;

        const double w_age = 0.0804;
        const double w_albumin = -0.0336;
        const double w_creatinine = 0.0095;
        const double w_glucose = 0.1953;
        const double w_ln_crp = 0.0954;
        const double w_lymph = -0.0120;
        const double w_mcv = 0.0268;
        const double w_rdw = 0.3306;
        const double w_alp = 0.0019;
        const double w_wbc = 0.0554;

        // linear predictor
        double xb =
            b0 +
            w_age * age_years +
            w_albumin * albumin_g_L +
            w_creatinine * creatinine_umol_L +
            w_glucose * glucose_mmol_L +
            w_ln_crp * ln_crp +
            w_lymph * lymph_pct +
            w_mcv * mcv_fL +
            w_rdw * rdw_pct +
            w_alp * alp_u_L +
            w_wbc * wbc_10e3_uL;

        // 10-year mortality
        const double gamma = 0.0076927;
        const double tMonths = 120.0;

        double exp120g = Math.Exp(tMonths * gamma);
        double hazard = Math.Exp(xb) * (exp120g - 1.0) / gamma;
        double mortality10yr = 1.0 - Math.Exp(-hazard);

        // numerical safety
        mortality10yr = Math.Min(Math.Max(mortality10yr, 1e-15), 1.0 - 1e-15);

        // phenotypic age
        const double c1 = 141.50225;
        const double c2 = 0.090165;
        const double c3 = -0.00553;

        double phenoAge = c1 + (Math.Log(c3 * Math.Log(1.0 - mortality10yr))) / c2;

        return new Result(xb, mortality10yr, phenoAge);
    }
}
