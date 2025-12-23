using System;

public static class Cardiology
{
    public sealed record Inputs(
        // Plaque
        string? CarotidPlaqueSeverity,     // "none" | "mild" | "moderate" | "severe"
        string? CoronaryPlaqueSeverity,    // "none" | "mild" | "moderate" | "severe"

        // Coronary imaging
        double? CacScore,                  // numeric, allow null
        double? CacPercentile,             // numeric 0-100, allow null

        // CTA
        double? CtaMaxStenosisPercent,     // numeric 0-100, allow null
        string? CtaOverallResult,          // "low" | "moderate" | "high" | "severe" (only if no score)

        // Treadmill (overall only)
        string? TreadmillOverallResult,    // "low" | "moderate" | "high" | "severe"

        // Echo (overall only)
        string? EchoOverallResult,         // "low" | "moderate" | "high" | "severe"

        // Clinical history (optional but required by your algorithm)
        bool? HasClinicalAscVDHistory,      // true => SEVERE
        string? ClinicalAscVDHistoryDetails, // free text

        // Free text
        string? SpecificCardiologyInstructions
    );

    public sealed record Result(
        string RiskCategory,               // "LOW" | "MILD" | "MODERATE" | "SEVERE"
        bool TriggeredByClinicalHistory,
        bool TriggeredBySevereFinding,
        bool TriggeredByModerateFinding,
        bool TriggeredByMildFinding
    );

    public static Result Calculate(Inputs x)
    {
        // Normalize inputs
        var carotid = NormSeverity(x.CarotidPlaqueSeverity);
        var coronary = NormSeverity(x.CoronaryPlaqueSeverity);

        var ctaQual = NormQual(x.CtaOverallResult);
        var treadmillQual = NormQual(x.TreadmillOverallResult);
        var echoQual = NormQual(x.EchoOverallResult);

        bool hasAscVD = x.HasClinicalAscVDHistory == true;

        // ---------- SEVERE ----------
        // Any severe finding OR any clinical ASCVD history
        if (hasAscVD)
        {
            return new Result(
                RiskCategory: "SEVERE",
                TriggeredByClinicalHistory: true,
                TriggeredBySevereFinding: false,
                TriggeredByModerateFinding: false,
                TriggeredByMildFinding: false
            );
        }

        bool severe =
            // CTA ≥ 50% stenosis
            (x.CtaMaxStenosisPercent is not null && x.CtaMaxStenosisPercent.Value >= 50.0) ||

            // CAC ≥ 400 OR ≥ 90th percentile
            (x.CacScore is not null && x.CacScore.Value >= 400.0) ||
            (x.CacPercentile is not null && x.CacPercentile.Value >= 90.0) ||

            // Severe plaque
            carotid == "severe" ||
            coronary == "severe" ||

            // If only qualitative results exist, treat "high" or "severe" as severe
            IsQualSevere(ctaQual) ||
            IsQualSevere(treadmillQual) ||
            IsQualSevere(echoQual);

        if (severe)
        {
            return new Result(
                RiskCategory: "SEVERE",
                TriggeredByClinicalHistory: false,
                TriggeredBySevereFinding: true,
                TriggeredByModerateFinding: false,
                TriggeredByMildFinding: false
            );
        }

        // ---------- MODERATE ----------
        bool moderate =
            // CAC 100–399
            (x.CacScore is not null && x.CacScore.Value >= 100.0 && x.CacScore.Value <= 399.0) ||

            // Moderate plaque
            carotid == "moderate" ||
            coronary == "moderate" ||

            // Qualitative moderate
            ctaQual == "moderate" ||
            treadmillQual == "moderate" ||
            echoQual == "moderate";

        if (moderate)
        {
            return new Result(
                RiskCategory: "MODERATE",
                TriggeredByClinicalHistory: false,
                TriggeredBySevereFinding: false,
                TriggeredByModerateFinding: true,
                TriggeredByMildFinding: false
            );
        }

        // ---------- MILD ----------
        bool mild =
            // Any mildly abnormal finding AND no moderate/severe findings (already ensured by earlier returns)
            carotid == "mild" ||
            coronary == "mild" ||

            // Mild coronary plaque proxy: CAC > 0 but < 100
            (x.CacScore is not null && x.CacScore.Value > 0.0 && x.CacScore.Value < 100.0) ||

            // Qualitative "low" treated as mild-abnormal bucket (since it's not "normal" in your dropdown set)
            ctaQual == "low" ||
            treadmillQual == "low" ||
            echoQual == "low";

        if (mild)
        {
            return new Result(
                RiskCategory: "MILD",
                TriggeredByClinicalHistory: false,
                TriggeredBySevereFinding: false,
                TriggeredByModerateFinding: false,
                TriggeredByMildFinding: true
            );
        }

        // ---------- LOW ----------
        // All must be true (with the fields you currently collect):
        // - No carotid plaque
        // - No coronary plaque
        // - CAC = 0 AND CTA normal (we interpret as no stenosis if provided, and no concerning qualitative flag)
        // - Treadmill normal (interpreted as empty/none, since your dropdown doesn't include "normal")
        // - Echo normal (same interpretation)
        bool noCarotidPlaque = carotid == "none";
        bool noCoronaryPlaque = coronary == "none";

        bool cacZero = x.CacScore is not null && x.CacScore.Value == 0.0;
        bool ctaNoStenosis = x.CtaMaxStenosisPercent is null || x.CtaMaxStenosisPercent.Value == 0.0;
        bool ctaNotFlagged = string.IsNullOrWhiteSpace(ctaQual); // if you choose "low" it is treated as mild

        bool treadmillNormalish = string.IsNullOrWhiteSpace(treadmillQual);
        bool echoNormalish = string.IsNullOrWhiteSpace(echoQual);

        if (noCarotidPlaque &&
            noCoronaryPlaque &&
            cacZero &&
            ctaNoStenosis &&
            ctaNotFlagged &&
            treadmillNormalish &&
            echoNormalish)
        {
            return new Result(
                RiskCategory: "LOW",
                TriggeredByClinicalHistory: false,
                TriggeredBySevereFinding: false,
                TriggeredByModerateFinding: false,
                TriggeredByMildFinding: false
            );
        }

        // If everything is blank / ambiguous, default conservative
        return new Result(
            RiskCategory: "MILD",
            TriggeredByClinicalHistory: false,
            TriggeredBySevereFinding: false,
            TriggeredByModerateFinding: false,
            TriggeredByMildFinding: true
        );
    }

    // -------- helpers --------

    private static string NormSeverity(string? s)
    {
        var v = (s ?? "").Trim().ToLowerInvariant();
        return v switch
        {
            "none" or "" => "none",
            "mild" => "mild",
            "moderate" => "moderate",
            "severe" => "severe",
            _ => v // tolerate unexpected values
        };
    }

    private static string NormQual(string? s)
    {
        var v = (s ?? "").Trim().ToLowerInvariant();
        return v switch
        {
            "" => "",
            "low" => "low",
            "moderate" => "moderate",
            "high" => "high",
            "severe" => "severe",
            _ => v
        };
    }

    private static bool IsQualSevere(string qual) => qual is "high" or "severe";
}

