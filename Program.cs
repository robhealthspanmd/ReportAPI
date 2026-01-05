using System;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// CORS: MVP = open. Later lock to your Lovable domain.
builder.Services.AddCors(options =>
{
    options.AddPolicy("lovable", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

app.UseCors("lovable");

// Health check
app.MapGet("/", () => Results.Ok(new { status = "ok", service = "ReportAPI" }));

// --- NEW: Metabolic AI endpoint (raw JSON in, structured JSON out) ---
app.MapPost("/api/metabolic-ai", async (JsonElement req) =>
{
    var result = await AiInsights.GenerateMetabolicHealthAlgorithmAsync(req);
    return Results.Ok(result);
});

// PhenoAge endpoint
app.MapPost("/api/phenoage", (PhenoAge.Inputs input) =>
{
    var r = PhenoAge.Calculate(input);
    return Results.Ok(new
    {
        modelVersion = "phenoage-levine-2018-v1",
        xb = r.Xb,
        mortality10Yr = r.Mortality10Yr,
        phenotypicAgeYears = r.PhenotypicAgeYears
    });
});

app.MapPost("/api/healthage", (HealthAge.Inputs input) =>
{
    var r = HealthAge.Calculate(input);
    return Results.Ok(new
    {
        modelVersion = "healthage-v1-excel",
        sumContributionYears = r.SumContributionYears,
        healthAgeUncapped = r.HealthAgeUncapped,
        contributionYearsScaled = r.ContributionYearsScaled,
        healthAgeFinal = r.HealthAgeFinal,
        deltaVsChronoYears = r.DeltaVsChronoYears,
        deltaVsChronoPercent = r.DeltaVsChronoPercent,
        breakdown = new
        {
            z1 = r.Z1_BodyFat,
            z2 = r.Z2_VisceralFat,
            z3 = r.Z3_AppendicularMuscle,
            z4 = r.Z4_BloodPressure,
            z5 = r.Z5_NonHdl,
            z6 = r.Z6_HomaIr,
            z7 = r.Z7_TgHdl,
            z8 = r.Z8_Fib4
        }
    });
});

app.MapPost("/api/performanceage", (PerformanceAge.Inputs input) =>
{
    var r = PerformanceAge.Calculate(input);
    return Results.Ok(new
    {
        modelVersion = "performanceage-v1-excel",
        sumContributionYears = r.SumContributionYears,
        contributionYearsScaled = r.ContributionYearsScaled,
        performanceAge = r.PerformanceAge,
        deltaVsAgeYears = r.DeltaVsAgeYears,
        deltaVsAgePercent = r.DeltaVsAgePercent,
        breakdown = new
        {
            z1 = r.Z1_Vo2Max,
            z2 = r.Z2_Quadriceps,
            z3 = r.Z3_Grip,
            z4 = r.Z4_GaitComfortable,
            z5 = r.Z5_GaitMax,
            z6 = r.Z6_Power,
            z7 = r.Z7_Balance,
            z8 = r.Z8_ChairRise
        }
    });
});

app.MapPost("/api/brainhealth", (BrainHealth.Inputs input) =>
{
    var r = BrainHealth.Calculate(input);
    return Results.Ok(new
    {
        modelVersion = "brainhealth-v2-updated-sheet",
        totalScore = r.TotalScore,
        level = r.Level,
        breakdown = new
        {
            cognitive = r.CognitivePoints,
            depression = r.DepressionPoints,
            anxiety = r.AnxietyPoints,
            resilience = r.ResiliencePoints,
            optimism = r.OptimismPoints,
            meaning = r.MeaningPoints,
            flourishing = r.FlourishingPoints,
            sleep = r.SleepPoints,
            stress = r.StressPoints
        }
    });
});

app.MapPost("/api/cardiology", (Cardiology.Inputs input) =>
{
    var r = Cardiology.Calculate(input); // this is where your algorithm lives
    return Results.Ok(new
    {
        modelVersion = "cardiology-risk-v1",
        riskCategory = r.RiskCategory
    });
});

app.MapPost("/api/report.json", async (ReportRequest req) =>
{
    var pheno = PhenoAge.Calculate(req.PhenoAge);

    var healthInputs = req.HealthAge with { PhenotypicAgeYears = pheno.PhenotypicAgeYears };
    var health = HealthAge.Calculate(healthInputs);

    var performance = PerformanceAge.Calculate(req.PerformanceAge);
    var brain = BrainHealth.Calculate(req.BrainHealth);

    var cardio = req.Cardiology is null ? null : Cardiology.Calculate(req.Cardiology);

    var summaryForAi = new
    {
        chronologicalAgeYears = req.PhenoAge.ChronologicalAgeYears,
        phenotypicAgeYears = pheno.PhenotypicAgeYears,
        mortality10Yr = pheno.Mortality10Yr,
        healthAgeYears = health.HealthAgeFinal,
        healthDeltaYears = health.DeltaVsChronoYears,
        performanceAgeYears = performance.PerformanceAge,
        performanceDeltaYears = performance.DeltaVsAgeYears,
        brainScore = brain.TotalScore,
        brainLevel = brain.Level
    };

    var improvementParagraph = await AiInsights.GenerateImprovementParagraphAsync(summaryForAi);

    var cardiologyInterpretationParagraph =
        cardio is null ? "" : await AiInsights.GenerateCardiologyInterpretationAsync(req, cardio);

    AiInsights.MetabolicHealthAiResult? metabolicAi = null;
    object? metabolicAiInput = null;

try
{
    metabolicAiInput = new
    {
        Age = req.PhenoAge.ChronologicalAgeYears,
        AST = req.HealthAge.Ast,
        ALT = req.HealthAge.Alt,
        Platelets = req.HealthAge.Platelets,
        Triglycerides = req.HealthAge.Triglycerides_mg_dL,
        HDL = req.HealthAge.Hdl_mg_dL,
        FastingGlucose = req.HealthAge.FastingGlucose_mg_dL,
        FastingInsulin = req.HealthAge.FastingInsulin_uIU_mL,
        A1c = req.HealthAge.HemoglobinA1c,
        VisceralFatPercentile = req.HealthAge.VisceralFatPercentile,
        LeanToFatMassRatio = (req.HealthAge.TotalLeanMass is not null && req.HealthAge.TotalFatMass is not null && req.HealthAge.TotalFatMass != 0)
            ? req.HealthAge.TotalLeanMass / req.HealthAge.TotalFatMass
            : (double?)null,
        Sex = req.HealthAge.Sex
    };

    metabolicAi = await AiInsights.GenerateMetabolicHealthAlgorithmAsync(
        JsonSerializer.SerializeToElement(metabolicAiInput)
    );
}
catch
{
    // Do NOT fail the report if AI fails
    metabolicAi = null;
}

    var bytes = JsonReportBuilder.BuildFullReportJson(
        req, pheno, health, performance, brain,
        cardio,
        improvementParagraph,
        cardiologyInterpretationParagraph,
        metabolicAi,
        metabolicAiInput
    );

    var filename = $"Healthspan_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
    return Results.File(bytes, "application/json", filename);
});

app.Run();
