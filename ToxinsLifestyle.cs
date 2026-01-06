using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

public static class ToxinsLifestyle
{
    private const double LeadUpperLimit = 3.5;
    private const double MercuryUpperLimit = 10.0;

    // NOTE: Numeric-only lab inputs for blood lead/mercury (no object wrapper).
    public sealed record Inputs(
        string? AlcoholIntake,
        string? AlcoholDrinksPerWeek,
        string? Smoking,
        string? ChewingTobacco,
        string? Vaping,
        string? OtherNicotineUse,
        string? CannabisUse,
        string? ScreenTime,
        string? UltraProcessedFoodIntake,
        string? MedicationsOrSupplementsImpact,
        string? PhysicalEnvironmentImpact,
        string? MediaExposureImpact,
        string? StressfulEnvironmentsOrRelationshipsImpact,
        double? BloodLeadLevel,
        double? BloodMercury
    );

    public static Result Evaluate(Inputs? inputs, double? perceivedStressScore)
    {
        inputs ??= new Inputs(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);

        var exposures = new List<Exposure>();
        var opportunities = new List<Opportunity>();

        bool stressNotOptimal = perceivedStressScore.HasValue && perceivedStressScore.Value > 13;

        bool tobaccoExposure = IsCurrentUse(inputs.Smoking)
                               || IsCurrentUse(inputs.ChewingTobacco)
                               || IsCurrentUse(inputs.Vaping)
                               || IsCurrentUse(inputs.OtherNicotineUse);

        if (tobaccoExposure)
        {
            exposures.Add(new Exposure(
                "tobacco-nicotine",
                "Tobacco / Nicotine",
                "Objective",
                "Any current use",
                false
            ));

            opportunities.Add(new Opportunity(
                "tobacco-nicotine",
                "Tobacco / Nicotine",
                "Tobacco and nicotine exposure are associated with accelerated vascular disease, lung disease, and cognitive decline. The optimal level for long-term cardiovascular and brain health is complete avoidance. Reducing or eliminating exposure is a high-impact opportunity to improve healthspan."
            ));
        }

        double? alcoholPerWeek = inputs.AlcoholDrinksPerWeek ?? ParseDrinksPerWeek(inputs.AlcoholIntake);
        if (alcoholPerWeek.HasValue && alcoholPerWeek.Value > 7)
        {
            exposures.Add(new Exposure(
                "alcohol",
                "Alcohol",
                "Objective",
                "More than 7 drinks per week",
                false
            ));

            opportunities.Add(new Opportunity(
                "alcohol",
                "Alcohol",
                "Alcohol intake above moderate levels can negatively affect blood pressure, metabolic health, sleep quality, and long-term disease risk. The optimal level for healthspan is no more than 7 drinks per week. Even modest reductions can provide meaningful benefits."
            ));
        }

        if (IsSubjectiveExposure(inputs.CannabisUse))
        {
            exposures.Add(new Exposure(
                "cannabis",
                "Cannabis",
                "Subjective",
                "Possibly or yes",
                false
            ));

            opportunities.Add(new Opportunity(
                "cannabis",
                "Cannabis",
                "Chronic cannabis use—particularly smoked forms—may affect lung health, cardiovascular strain, memory, and motivation. The optimal level for brain and cardiovascular health is minimal or no use."
            ));
        }

        bool screenTimeExposure = IsScreenTimeExposure(inputs.ScreenTime);
        if (screenTimeExposure)
        {
            exposures.Add(new Exposure(
                "screen-time",
                "Screen Time",
                "Subjective",
                "Possibly or yes (derived from hours per day)",
                false
            ));

            opportunities.Add(new Opportunity(
                "screen-time",
                "Screen Time",
                "Excessive screen time can contribute to sedentary behavior, sleep disruption, and increased stress. The optimal pattern supports balance, prioritizing physical activity, in-person connection, and restorative sleep."
            ));
        }

        if (IsSubjectiveExposure(inputs.UltraProcessedFoodIntake))
        {
            exposures.Add(new Exposure(
                "processed-foods",
                "Processed Foods & Beverages",
                "Subjective",
                "Possibly or yes",
                false
            ));

            opportunities.Add(new Opportunity(
                "processed-foods",
                "Processed Foods & Beverages",
                "Highly processed foods and beverages can increase metabolic and inflammatory stress. Minimizing intake may support better metabolic, cardiovascular, and brain health."
            ));
        }

        if (IsSubjectiveExposure(inputs.MedicationsOrSupplementsImpact))
        {
            exposures.Add(new Exposure(
                "medications-supplements",
                "Medications / Supplements",
                "Subjective",
                "Possibly or yes",
                false
            ));

            opportunities.Add(new Opportunity(
                "medications-supplements",
                "Medications / Supplements",
                "Some medications or supplements may create unintended strain when not well matched to individual needs. Periodic review to ensure necessity, safety, and appropriate use can help reduce cumulative stress on the body."
            ));
        }

        if (IsSubjectiveExposure(inputs.PhysicalEnvironmentImpact))
        {
            exposures.Add(new Exposure(
                "environmental",
                "Environmental Exposures",
                "Subjective",
                "Possibly or yes",
                false
            ));

            opportunities.Add(new Opportunity(
                "environmental",
                "Environmental Exposures",
                "Environmental exposures such as air pollution, chemicals, or occupational hazards can contribute to cumulative physiologic stress. Reducing exposure where feasible may support long-term health."
            ));
        }

        if (IsSubjectiveExposure(inputs.MediaExposureImpact))
        {
            exposures.Add(new Exposure(
                "media",
                "Media Exposure",
                "Subjective",
                "Possibly or yes",
                false
            ));

            opportunities.Add(new Opportunity(
                "media",
                "Media Exposure",
                "Chronic exposure to distressing or negative media can contribute to emotional and physiologic stress. Reducing exposure may support mental resilience and overall well-being."
            ));
        }

        bool stressExposure = IsSubjectiveExposure(inputs.StressfulEnvironmentsOrRelationshipsImpact);
        bool stressAmplified = stressExposure && stressNotOptimal;
        if (stressExposure)
        {
            exposures.Add(new Exposure(
                "stressful-environments",
                "Stressful Environments or Relationships",
                "Subjective",
                "Possibly or yes",
                stressAmplified
            ));

            opportunities.Add(new Opportunity(
                "stressful-environments",
                "Stressful Environments or Relationships",
                "Ongoing exposure to stressful environments or relationships can contribute to chronic stress, which affects cardiovascular, metabolic, and brain health. Identifying and reducing these stressors where possible may be an important opportunity to support healthspan."
            ));
        }

        if (IsLabExposure(inputs.BloodLeadLevel, LeadUpperLimit))
        {
            exposures.Add(new Exposure(
                "lead",
                "Lead (Blood Lead Level)",
                "Lab",
                "High flag or above reference range",
                false
            ));

            opportunities.Add(new Opportunity(
                "lead",
                "Lead (Blood Lead Level)",
                "Your lead level is above the lab’s normal reference range, which suggests recent or ongoing lead exposure. Even low-level lead exposure is associated with adverse health effects, and the optimal level is as low as possible. Next steps typically include confirming the result, identifying likely exposure sources, and reducing exposure.",
                new[]
                {
                    "Confirm: repeat venous blood lead to confirm and trend.",
                    "Source review: occupation or hobby exposure (construction, shooting ranges, stained glass, ceramics, fishing weights).",
                    "Source review: older housing or renovations, plumbing, or well water."
                },
                new[]
                {
                    "If markedly elevated (well above lab upper limit or rising on repeat), consider occupational/environmental health evaluation and toxicology input.",
                    "Management decisions depend on level and clinical context; public health thresholds vary."
                }
            ));
        }

        if (IsLabExposure(inputs.BloodMercury, MercuryUpperLimit))
        {
            exposures.Add(new Exposure(
                "mercury",
                "Mercury (Blood)",
                "Lab",
                "High flag or above reference range",
                false
            ));

            opportunities.Add(new Opportunity(
                "mercury",
                "Mercury (Blood)",
                "Your mercury level is above the lab’s normal reference range, suggesting increased mercury exposure. The optimal level is as low as possible. Next steps typically include confirming the result, identifying exposure sources (often dietary fish/seafood or occupational), and reducing exposure where feasible.",
                new[]
                {
                    "Confirm: repeat level to confirm and trend (especially if unexpected).",
                    "Source review: high-mercury seafood intake patterns.",
                    "Source review: occupational exposures (dental or industrial)."
                },
                new[]
                {
                    "If levels are significantly elevated or symptoms suggest toxicity, consider further evaluation (speciation/exposure pathway assessment) and specialist input."
                },
                "Note: blood mercury reflects recent exposure and can be influenced by organic vs inorganic forms; interpretation is context-dependent."
            ));
        }

        var orderedOpportunities = opportunities
            .Select(opportunity => (opportunity, rank: OpportunityRank(opportunity.Key, stressAmplified)))
            .OrderBy(item => item.rank)
            .Select(item => item.opportunity)
            .ToList();

        var overallStatus = exposures.Count == 0
            ? "No Potential Harmful Exposures Identified"
            : "Potential Harmful Exposures Identified";

        var summary = exposures.Count == 0
            ? "No potential harmful or toxic exposures were identified based on current inputs."
            : "Potential exposures were identified based on self-report and available lab data.";

        return new Result(
            overallStatus,
            summary,
            exposures,
            orderedOpportunities,
            stressAmplified
        );
    }

    private static bool IsCurrentUse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "current" or "occasional" or "yes" or "cigarettes" or "vaping" or "smokeless" or "other";
    }

    private static bool IsSubjectiveExposure(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "possibly" or "possible" or "yes" or "occasional" or "current" or "maybe";
    }

    private static bool IsScreenTimeExposure(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var hours))
        {
            return hours >= 6;
        }

        return IsSubjectiveExposure(value);
    }

    private static bool IsLabExposure(double? value, double upperLimit)
    {
        if (!value.HasValue)
        {
            return false;
        }

        return value.Value > upperLimit;
    }

    private static double? ParseDrinksPerWeek(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var matches = Regex.Matches(input, @"\d+(\.\d+)?");
        if (matches.Count == 0)
        {
            return null;
        }

        var values = matches
            .Select(match => double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : (double?)null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();

        return values.Count == 0 ? null : values.Max();
    }

    private static int OpportunityRank(string key, bool stressAmplified)
    {
        return key switch
        {
            "lead" => 1,
            "mercury" => 2,
            "tobacco-nicotine" => 3,
            "alcohol" => 4,
            "stressful-environments" when stressAmplified => 5,
            "cannabis" => 6,
            "screen-time" => 7,
            "processed-foods" => 8,
            "medications-supplements" => 9,
            "environmental" => 10,
            "media" => 11,
            "stressful-environments" => 12,
            _ => 99
        };
    }
}
