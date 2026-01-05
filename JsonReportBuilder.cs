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
        object? metabolicAiInput = null
    )
    {
        // Keep output stable + frontend-friendly.
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var payload = new
        {
            meta = new
            {
                generatedAtUtc = DateTime.UtcNow,
                schemaVersion = "report-json-v1"
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
                brainHealth = brain,
                cardiology = cardio
            },

            scores = new
            {
                healthspanAge = health.HealthAgeFinal,
                physicalPerformanceAge = performance.PerformanceAge,
                brainHealthScore = brain.TotalScore,
                cardiologyRiskCategory = cardio?.RiskCategory
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
