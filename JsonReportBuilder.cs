using System;
using System.Text;
using System.Text.Json;

/// <summary>
/// Builds the full report as a single JSON document (UTF-8 bytes).
/// Intended to be the canonical "source of truth" for any downstream renderer (web, PDF, etc.).
/// </summary>
public static class JsonReportBuilder
{
    public static byte[] BuildFullReportJson(
        ReportRequest req,
        PhenoAge.Result pheno,
        HealthAge.Result health,
        PerformanceAge.Result performance,
        BrainHealth.Result brain,
        Cardiology.Result? cardio,
        string improvementParagraph,
        string cardiologyInterpretationParagraph,
        AiInsights.MetabolicHealthAiResult? metabolicAi,
        object? metabolicAiInput = null,
        PhysicalPerformanceStrategyEngine.Response? physicalPerformanceStrategyEngine = null
    )
    {
        // Keep output stable + frontend-friendly.
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // "Base report section" recreated below with the new computed field included.
        var payload = new
        {
            meta = new
            {
                generatedAtUtc = DateTime.UtcNow,
                schemaVersion = "report-json-v2"
            },

            inputs = new
            {
                phenoAge = req.PhenoAge,
                healthAge = req.HealthAge,
                performanceAge = req.PerformanceAge,
                brainHealth = req.BrainHealth,
                cardiology = req.Cardiology,
                clinicalData = req.ClinicalData,
                toxinsLifestyle = req.ToxinsLifestyle
            },

            computed = new
            {
                phenoAge = pheno,
                healthAge = health,
                performanceAge = performance,
                physicalPerformanceStrategyEngine = physicalPerformanceStrategyEngine,
                brainHealth = brain,
                cardiology = cardio,
                toxinsLifestyle = ToxinsLifestyle.Evaluate(req.ToxinsLifestyle, req.BrainHealth.PerceivedStressScore)
            },

            scores = new
{
    healthspanAge = health.HealthAgeFinal,
    physicalPerformanceAge = performance.PerformanceAge,
    brainHealthScore = brain.TotalScore,

    // Legacy (kept for backward compatibility)
    cardiologyRiskCategory = cardio?.RiskCategory,

    // Heart Health v3.2 assessment outputs (included when cardiology was computed)
    cardiology = cardio == null ? null : new
    {
        assessment = new
        {
            heartHealthScore = cardio.HeartHealthScore,
            heartHealthScoreIsPartial = cardio.HeartHealthScoreIsPartial,

            baselineHeartHealthScore = cardio.BaselineHeartHealthScore,
            plaqueScore = cardio.PlaqueScore,
            cardiacPhysiologyScore = cardio.CardiacPhysiologyScore,

            baselineRiskCategory = cardio.BaselineRiskCategory,
            vascularHealthStatus = cardio.VascularHealthStatus,
            cardiacPhysiologyStatus = cardio.CardiacPhysiologyStatus,

            // Optional / diagnostic text (assessment only; no recommendations)
            riskExplanation = cardio.RiskExplanation,

            // Triggers (useful for UI badges + debugging)
            triggeredByClinicalHistory = cardio.TriggeredByClinicalHistory,
            triggeredBySevereFinding = cardio.TriggeredBySevereFinding,
            triggeredByModerateFinding = cardio.TriggeredByModerateFinding,
            triggeredByMildFinding = cardio.TriggeredByMildFinding
        }
    }
},

            ai = new
            {
                improvementParagraph = improvementParagraph ?? string.Empty,
                cardiologyInterpretationParagraph = cardiologyInterpretationParagraph ?? string.Empty,
                metabolicAssessment = metabolicAi,
                metabolicAssessmentInput = metabolicAiInput
            }
        };

        var json = JsonSerializer.Serialize(payload, options);
        return Encoding.UTF8.GetBytes(json);
    }
}
