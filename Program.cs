using System;
using System.Text.Json;
using System.Text.Json.Nodes;

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

/// <summary>
/// Full report (JSON) generator.
/// Updated to:
/// - read raw JSON (so extra performance fields are not dropped)
/// - attach new performance fields onto PerformanceAge.Result (no changes to PerformanceAge.Calculate)
/// - compute PhysicalPerformanceStrategyEngine output
/// - inject that engine output into the final JSON (no changes to JsonReportBuilder)
/// </summary>
app.MapPost("/api/report.json", async (HttpContext http) =>
{
    // 1) Read raw JSON body
    JsonElement root;
    try
    {
        root = await JsonSerializer.DeserializeAsync<JsonElement>(http.Request.Body);
    }
    catch
    {
        return Results.BadRequest(new { error = "Invalid JSON body." });
    }

    // 2) Normalize incoming JSON to align UI field names with backend contracts
    JsonDocument normalizedDoc;
    try
    {
        normalizedDoc = NormalizeReportJson(root);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = "Could not normalize request.", details = ex.Message });
    }

    // 3) Deserialize into existing ReportRequest contract (extra fields will be ignored here)
    ReportRequest req;
    try
    {
        req = JsonSerializer.Deserialize<ReportRequest>(normalizedDoc.RootElement.GetRawText(), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Request deserialized to null.");
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = "Could not deserialize request.", details = ex.Message });
    }

    // 4) Pull extra performance fields out of the raw JSON (so we keep what the UI sends)
    var perfExtras = ExtractPerformanceExtras(normalizedDoc.RootElement);

    // 5) Run deterministic calculators (same as before)
    var pheno = PhenoAge.Calculate(req.PhenoAge);

    var healthInputs = req.HealthAge with { PhenotypicAgeYears = pheno.PhenotypicAgeYears };
    var health = HealthAge.Calculate(healthInputs);

    var performanceCore = PerformanceAge.Calculate(req.PerformanceAge);

    // 5) Attach extra metrics to the Result (no change to PerformanceAge.cs)
    var performance = performanceCore with
    {
        Vo2Max = perfExtras.Vo2Max,
        HeartRateRecovery = perfExtras.HeartRateRecovery,
        FloorToStandTest = perfExtras.FloorToStandTest,
        TrunkEndurance = perfExtras.TrunkEndurance,
        PostureAssessment = perfExtras.PostureAssessment,
        HipStrength = perfExtras.HipStrength,
        CalfStrength = perfExtras.CalfStrength,
        RotatorCuffIntegrity = perfExtras.RotatorCuffIntegrity,
        IsometricThighPull = perfExtras.IsometricThighPull,

        // Spec-expansion fields
        MobilityRom = perfExtras.MobilityRom,
        BalanceAssessment = perfExtras.BalanceAssessment,
        ChairRiseFiveTimes = perfExtras.ChairRiseFiveTimes,
        ChairRiseThirtySeconds = perfExtras.ChairRiseThirtySeconds,
        QuadricepsAsymmetryPercent = perfExtras.QuadricepsAsymmetryPercent,
        HipAsymmetryPercent = perfExtras.HipAsymmetryPercent,
        CalfAsymmetryPercent = perfExtras.CalfAsymmetryPercent,
        IsometricThighPullPercentile = perfExtras.IsometricThighPullPercentile,
        RotatorCuffLowestMusclePercentile = perfExtras.RotatorCuffLowestMusclePercentile
    };

    var brain = BrainHealth.Calculate(req.BrainHealth);

    Cardiology.Result? cardio = null;
    if (req.Cardiology is not null)
    {
        var modifiableScore = Cardiology.CalculateModifiableHeartHealthScore(
            req.HealthAge,
            req.PerformanceAge,
            req.PhenoAge,
            req.Cardiology);
        var cardioInputs = req.Cardiology with { ModifiableHeartHealthScore = modifiableScore };
        cardio = Cardiology.Calculate(cardioInputs);
    }

    // 6) Strategy engine (deterministic; only emits strategies if there are triggers)
    var perfStrategy = PhysicalPerformanceStrategyEngine.Generate(req.PerformanceAge, performance);

    // 7) AI summaries (same as before)
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
    AiInsights.ClinicalPreventiveChecklistResult? clinicalPreventiveChecklist = null;
    object? clinicalPreventiveChecklistInput = null;

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
            LeanToFatMassRatio =
                (req.HealthAge.TotalLeanMass is not null &&
                 req.HealthAge.TotalFatMass is not null &&
                 req.HealthAge.TotalFatMass != 0)
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

    try
    {
        clinicalPreventiveChecklistInput = new
        {
            CurrentDateUtc = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            Demographics = new
            {
                AgeYears = req.PhenoAge.ChronologicalAgeYears,
                Sex = req.HealthAge.Sex,
                PregnancyPotential = req.ClinicalData.PregnancyPotential
            },
            ClinicalData = req.ClinicalData,
            HealthAge = new
            {
                Ast = req.HealthAge.Ast,
                Alt = req.HealthAge.Alt,
                Platelets = req.HealthAge.Platelets
            },
            PhenoAge = new
            {
                Wbc10e3PeruL = req.PhenoAge.WBC_10e3_per_uL
            },
            ToxinsLifestyle = new
            {
                Smoking = req.ToxinsLifestyle.Smoking
            }
        };

        clinicalPreventiveChecklist = await AiInsights.GenerateClinicalPreventiveChecklistAsync(
            JsonSerializer.SerializeToElement(clinicalPreventiveChecklistInput)
        );
    }
    catch
    {
        clinicalPreventiveChecklist = null;
    }

    // 8) Build base report JSON bytes (no changes to JsonReportBuilder)
    var bytes = JsonReportBuilder.BuildFullReportJson(
        req, pheno, health, performance, brain,
        cardio,
        improvementParagraph,
        cardiologyInterpretationParagraph,
        metabolicAi,
        metabolicAiInput,
        perfStrategy,
        clinicalPreventiveChecklist,
        clinicalPreventiveChecklistInput
    );

    // 9) Inject the strategy engine output into computed.physicalPerformanceStrategyEngine
    //    (so we don't have to change JsonReportBuilder.cs right now)
    try
    {
        var node = JsonNode.Parse(bytes) as JsonObject;
        var computedNode = node?["computed"] as JsonObject;
        if (computedNode is not null)
        {
            computedNode["physicalPerformanceStrategyEngine"] = JsonSerializer.SerializeToNode(perfStrategy);
            bytes = JsonSerializer.SerializeToUtf8Bytes(node!);
        }
    }
    catch
    {
        // If injection fails, return base report.
    }

    var filename = $"Healthspan_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
    return Results.File(bytes, "application/json", filename);
});

app.Run();


// -----------------------------
// Helpers (Program.cs-only wiring; no contract changes required)
// -----------------------------

static PerformanceExtras ExtractPerformanceExtras(JsonElement root)
{
    // Expecting a top-level "performanceAge" object alongside "phenoAge", "healthAge", etc.
    if (!TryGetPropertyCaseInsensitive(root, "performanceAge", out var perfObj) ||
        perfObj.ValueKind != JsonValueKind.Object)
    {
        return new PerformanceExtras();
    }

    return new PerformanceExtras
    {
        Vo2Max = ReadDoubleAny(perfObj, "vo2Max", "vo2MaxValue"),
        HeartRateRecovery = ReadDoubleAny(perfObj, "heartRateRecovery", "hrrBpmDrop"),
        FloorToStandTest = ReadStringAny(perfObj, "floorToStandTest", "floorToStandScore"),
        TrunkEndurance = ReadStringAny(perfObj, "trunkEndurance", "trunkEndurancePercentile"),
        PostureAssessment = ReadStringAny(perfObj, "postureAssessment", "tragusToWallDistance"),
        HipStrength = ReadStringAny(perfObj, "hipStrength", "hipStrengthPercentile"),
        CalfStrength = ReadStringAny(perfObj, "calfStrength", "calfStrengthPercentile"),
        RotatorCuffIntegrity = ReadStringAny(perfObj, "rotatorCuffIntegrity", "rotatorCuffLowestPercentile"),
        IsometricThighPull = ReadDoubleAny(perfObj, "isometricThighPull", "imtpForceValue"),

        // Spec-expansion fields (optional)
        MobilityRom = ReadStringAny(perfObj, "mobilityRom", "restrictedMotionPresent"),
        BalanceAssessment = ReadStringAny(perfObj, "balanceAssessment", "ctsibHoldTime", "unableToCompleteCtsib"),
        ChairRiseFiveTimes = ReadStringAny(perfObj, "chairRiseFiveTimes", "sitToStand5xTime"),
        ChairRiseThirtySeconds = ReadStringAny(perfObj, "chairRiseThirtySeconds", "sitToStand30secReps"),
        QuadricepsAsymmetryPercent = ReadDoubleAny(perfObj, "quadricepsAsymmetryPercent", "quadAsymmetryPercent"),
        HipAsymmetryPercent = ReadDoubleAny(perfObj, "hipAsymmetryPercent"),
        CalfAsymmetryPercent = ReadDoubleAny(perfObj, "calfAsymmetryPercent"),
        IsometricThighPullPercentile = ReadDoubleAny(perfObj, "isometricThighPullPercentile", "imtpPercentile"),
        RotatorCuffLowestMusclePercentile = ReadDoubleAny(perfObj, "rotatorCuffLowestMusclePercentile", "rotatorCuffLowestPercentile")
    };
}

static bool TryGetPropertyCaseInsensitive(JsonElement obj, string name, out JsonElement value)
{
    foreach (var prop in obj.EnumerateObject())
    {
        if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            value = prop.Value;
            return true;
        }
    }
    value = default;
    return false;
}

static double? ReadDouble(JsonElement obj, string name)
{
    if (!TryGetPropertyCaseInsensitive(obj, name, out var v))
        return null;

    if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d))
        return d;

    if (v.ValueKind == JsonValueKind.String)
    {
        var s = v.GetString();
        if (!string.IsNullOrWhiteSpace(s) && double.TryParse(s, out var parsed))
            return parsed;
    }

    return null;
}

static double? ReadDoubleAny(JsonElement obj, params string[] names)
{
    foreach (var name in names)
    {
        var value = ReadDouble(obj, name);
        if (value.HasValue)
            return value.Value;
    }
    return null;
}

static string? ReadString(JsonElement obj, string name)
{
    if (!TryGetPropertyCaseInsensitive(obj, name, out var v))
        return null;

    return v.ValueKind switch
    {
        JsonValueKind.String => v.GetString(),
        JsonValueKind.Number => v.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => null,
        _ => v.GetRawText()
    };
}

static string? ReadStringAny(JsonElement obj, params string[] names)
{
    foreach (var name in names)
    {
        var value = ReadString(obj, name);
        if (!string.IsNullOrWhiteSpace(value))
            return value;
    }
    return null;
}

static JsonDocument NormalizeReportJson(JsonElement root)
{
    var node = JsonNode.Parse(root.GetRawText()) as JsonObject;
    if (node is null)
        return JsonDocument.Parse(root.GetRawText());

    NormalizePerformanceAge(node);
    return JsonDocument.Parse(node.ToJsonString());
}

static void NormalizePerformanceAge(JsonObject root)
{
    if (!root.TryGetPropertyValue("performanceAge", out var perfNode) ||
        perfNode is not JsonObject perfObj)
    {
        return;
    }

    CopyIfMissing(perfObj, "quadricepsStrengthPercentile", "quadStrengthPercentile");
    CopyIfMissing(perfObj, "gaitSpeedComfortablePercentile", "comfortableGaitSpeedPercentile");
    CopyIfMissing(perfObj, "gaitSpeedMaxPercentile", "maximalGaitSpeedPercentile");
}

static void CopyIfMissing(JsonObject obj, string target, string source)
{
    if (obj.ContainsKey(target))
        return;

    if (obj.TryGetPropertyValue(source, out var value) && value is not null)
    {
        obj[target] = value.DeepClone();
    }
}

sealed class PerformanceExtras
{
    public double? Vo2Max { get; set; }
    public double? HeartRateRecovery { get; set; }
    public string? FloorToStandTest { get; set; }
    public string? TrunkEndurance { get; set; }
    public string? PostureAssessment { get; set; }
    public string? HipStrength { get; set; }
    public string? CalfStrength { get; set; }
    public string? RotatorCuffIntegrity { get; set; }
    public double? IsometricThighPull { get; set; }

    public string? MobilityRom { get; set; }
    public string? BalanceAssessment { get; set; }
    public string? ChairRiseFiveTimes { get; set; }
    public string? ChairRiseThirtySeconds { get; set; }
    public double? QuadricepsAsymmetryPercent { get; set; }
    public double? HipAsymmetryPercent { get; set; }
    public double? CalfAsymmetryPercent { get; set; }
    public double? IsometricThighPullPercentile { get; set; }
    public double? RotatorCuffLowestMusclePercentile { get; set; }
}
