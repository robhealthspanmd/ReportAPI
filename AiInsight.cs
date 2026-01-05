using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public static class AiInsights
{
    private static readonly HttpClient Http = new();

    // -------- Metabolic AI (RAW JSON in, structured output out) --------
    public static async Task<MetabolicHealthAiResult> GenerateMetabolicHealthAlgorithmAsync(JsonElement metabolicInput)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Missing OPENAI_API_KEY env var.");

        // 1) Extract A1c robustly (and optionally other values if you want later)
        var extractedA1c = TryExtractA1cPercent(metabolicInput);

        // Optional: quick terminal debug
        // Console.WriteLine($"[DEBUG] Extracted A1c% = {(extractedA1c?.ToString() ?? "NULL")}");

        var system = """
You are a metabolic health decision-support and prioritization engine.
You MUST compute derived metrics and categorization EXACTLY and return ONLY valid JSON (no prose, no markdown).

IMPORTANT INPUT MAPPING:
- Prefer EXTRACTED_FIELDS_JSON.a1cPercent when present.
- Otherwise, A1c may appear as:
  - healthAge.hemoglobinA1c
  - healthAge.hemaglobinA1c (misspelling)
  - HealthAge.HemoglobinA1c
  - request.HealthAge.HemoglobinA1c
Use that value as A1c (%).

Derived metrics:
- TG_HDL = Triglycerides / HDL
- HOMA_IR = (FastingGlucose * FastingInsulin) / 405
- FIB4 = (Age * AST) / (Platelets * sqrt(ALT))

Grade each metric as Optimal / Mild / Moderate / Severe using these thresholds:
A1c (%): Optimal <5.2; Mild 5.3–5.6; Moderate 5.7–6.4; Severe >=6.5
Fasting insulin (uIU/mL): Optimal <6; Mild 7–10; Moderate 11–19; Severe >=20
TG/HDL: Optimal <1; Mild 1–2; Moderate 2–3; Severe >3
HOMA-IR: Optimal <1.0; Mild 1.0–2.0; Moderate >2.0–3.0; Severe >3.0
Visceral fat percentile: Optimal <25; Mild 25–49; Moderate 50–74; Severe >=75
Lean-to-Fat Mass Ratio:
 Men: Optimal >=3.2; Mild 2.2–3.19; Moderate 1.4–2.19; Severe <1.4
 Women: Optimal >=2.6; Mild 1.8–2.59; Moderate 1.2–1.79; Severe <1.2
FIB-4: Optimal <1.3; Mild 1.3–1.99; Moderate 2.0–2.66; Severe >=2.67

Counts:
- MildCount / ModerateCount / SevereCount based on graded metrics
- NonOptimalCount = Mild + Moderate + Severe

Flags:
- isolatedA1cElevation = A1c non-optimal AND all other metrics Optimal
- isolatedInsulinResistanceMarker = (FastingInsulin OR HOMA-IR non-optimal) AND A1c, TG/HDL, visceral fat, lean/fat ratio Optimal

Category rules (MUST BE STRICT):
- Optimal Metabolism if NonOptimalCount == 0 OR isolatedA1cElevation
- Mild Metabolic Dysfunction if NonOptimalCount < 3 AND ModerateCount == 0 AND SevereCount == 0
- Metabolic Dysfunction if NonOptimalCount >= 3 OR ModerateCount >= 2 OR SevereCount >= 1

CONSISTENCY REQUIREMENT:
- After computing counts, set metabolicHealthCategory strictly from the rules above.
- If any conflicts exist between counts/flags and the category string, you MUST correct the category to match the rules.

BiggestContributors: rank non-optimal metrics Severe > Moderate > Mild, return up to 3.
TopInterventions: return up to 3 buckets with short safe recommendation text.
Buckets: Fitness, LeanMassRelativeToFat, NutritionOptimization, CGM, SleepOptimization, StressManagement, MedicalTherapy.

Missing data handling:
- still return valid JSON
- derived metrics can be null
- unknown grades should be "Unknown"
- include notes describing missing fields.
""";

        // JSON schema object (actual schema only)
        var outputSchema = new
        {
            type = "object",
            additionalProperties = false,
            required = new[]
            {
                "metabolicHealthCategory","derivedMetrics","grades","counts","flags",
                "biggestContributors","topInterventions","optionalProgramsOffered","notes"
            },
            properties = new
            {
                metabolicHealthCategory = new { type = "string" },
                derivedMetrics = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "tgHdlRatio","homaIr","fib4" },
                    properties = new
                    {
                        tgHdlRatio = new { type = new[] { "number","null" } },
                        homaIr = new { type = new[] { "number","null" } },
                        fib4 = new { type = new[] { "number","null" } }
                    }
                },
                grades = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[]
                    {
                        "a1c","fastingInsulin","tgHdlRatio","homaIr",
                        "visceralFatPercentile","leanToFatMassRatio","fib4"
                    },
                    properties = new
                    {
                        a1c = new { type = "string" },
                        fastingInsulin = new { type = "string" },
                        tgHdlRatio = new { type = "string" },
                        homaIr = new { type = "string" },
                        visceralFatPercentile = new { type = "string" },
                        leanToFatMassRatio = new { type = "string" },
                        fib4 = new { type = "string" }
                    }
                },
                counts = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "mildCount","moderateCount","severeCount","nonOptimalCount" },
                    properties = new
                    {
                        mildCount = new { type = "integer" },
                        moderateCount = new { type = "integer" },
                        severeCount = new { type = "integer" },
                        nonOptimalCount = new { type = "integer" }
                    }
                },
                flags = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "isolatedA1cElevation","isolatedInsulinResistanceMarker" },
                    properties = new
                    {
                        isolatedA1cElevation = new { type = "boolean" },
                        isolatedInsulinResistanceMarker = new { type = "boolean" }
                    }
                },
                biggestContributors = new
                {
                    type = "array",
                    maxItems = 3,
                    items = new
                    {
                        type = "object",
                        additionalProperties = false,
                        required = new[] { "metricName","severity","mechanismLabel" },
                        properties = new
                        {
                            metricName = new { type = "string" },
                            severity = new { type = "string" },
                            mechanismLabel = new { type = "string" }
                        }
                    }
                },
                topInterventions = new
                {
                    type = "array",
                    maxItems = 3,
                    items = new
                    {
                        type = "object",
                        additionalProperties = false,
                        required = new[] { "bucket","recommendationText","priority" },
                        properties = new
                        {
                            bucket = new { type = "string" },
                            recommendationText = new { type = "string" },
                            priority = new { type = "integer" }
                        }
                    }
                },
                optionalProgramsOffered = new { type = "array", items = new { type = "string" } },
                notes = new { type = "array", items = new { type = "string" } }
            }
        };

        // Inject extracted fields (so the model can’t “miss” A1c)
        var extractedFields = new
        {
            a1cPercent = extractedA1c
        };

        var userText =
            "Run the Metabolic Health Algorithm.\n\n" +
            "EXTRACTED_FIELDS_JSON:\n" + JsonSerializer.Serialize(extractedFields) + "\n\n" +
            "INPUT_JSON:\n" + metabolicInput.GetRawText();

        var requestBody = new
        {
            model = "gpt-5.2",
            input = new object[]
            {
                new
                {
                    role = "system",
                    content = new object[]
                    {
                        new { type = "input_text", text = system }
                    }
                },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "input_text", text = userText }
                    }
                }
            },
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "metabolic_health_algorithm_output",
                    strict = true,
                    schema = outputSchema
                }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var outboundJson = JsonSerializer.Serialize(requestBody);

Console.WriteLine("===== OPENAI PAYLOAD CHECK =====");
Console.WriteLine(outboundJson.Contains("hemoglobinA1c", StringComparison.OrdinalIgnoreCase)
    ? "[YES] Payload contains hemoglobinA1c"
    : "[NO] Payload does NOT contain hemoglobinA1c");

Console.WriteLine(outboundJson.Contains("EXTRACTED_FIELDS_JSON", StringComparison.OrdinalIgnoreCase)
    ? "[YES] Payload contains EXTRACTED_FIELDS_JSON"
    : "[NO] Payload missing EXTRACTED_FIELDS_JSON");

Console.WriteLine("================================");


        using var httpReq = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        httpReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpReq.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var res = await Http.SendAsync(httpReq);
        var resText = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"OpenAI error {(int)res.StatusCode}: {resText}");

        var contentText = ExtractFirstOutputText(resText);
        if (string.IsNullOrWhiteSpace(contentText))
            throw new InvalidOperationException("OpenAI returned empty output text.");

        var result = JsonSerializer.Deserialize<MetabolicHealthAiResult>(
            contentText,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        ) ?? throw new InvalidOperationException("Failed to deserialize metabolic health JSON.");

        // 2) Enforce category consistency server-side (model can’t be trusted to not contradict itself)
        var computedCategory = ComputeCategoryFromCounts(result.Counts, result.Flags);

        if (!string.Equals(result.MetabolicHealthCategory, computedCategory, StringComparison.OrdinalIgnoreCase))
        {
            // Optional: log mismatch for debugging
            // Console.WriteLine($"[WARN] Category mismatch. AI='{result.MetabolicHealthCategory}' Computed='{computedCategory}'. Overriding.");

            result = result with { MetabolicHealthCategory = computedCategory };

            // Also append a note so you can see this happened in your debug dump
            var newNotes = (result.Notes ?? Array.Empty<string>())
                .Concat(new[] { $"[Server override] metabolicHealthCategory corrected to '{computedCategory}' from counts/flags rules." })
                .ToArray();

            result = result with { Notes = newNotes };
        }

        return result;
    }

    // -------- Cardiology narrative --------
    public static async Task<string> GenerateCardiologyInterpretationAsync(ReportRequest request, Cardiology.Result cardioResult)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Missing OPENAI_API_KEY env var.");

        var system = """
You are a physician writing the “Heart and Blood Vessel Health Interpretation and Strategy” section.
You will be given:
- Cardiology findings (inputs) and an already-computed cardiology risk category: LOW, MILD, MODERATE, or SEVERE
- Other relevant vitals/labs/fitness context from the same report (BP, lipids, hs-CRP, VO2 percentile, etc.)
- A field called SpecificCardiologyInstructions written by a clinician.

Hard requirements:
1) You MUST follow SpecificCardiologyInstructions exactly (treat it as clinician orders) but remember, this is for a patient.
2) Use medical language, clear, educated-patient tone.
3) Start by stating what is OPTIMAL vs NOT OPTIMAL based on the findings and numbers.
4) Then provide the “targets” (where their numbers should be) appropriate to risk severity.
5) Do not invent tests or results that are not in the JSON.
6) Multiple paragraphs. No bullet points unless the clinician instructions explicitly request bullets.
""";

        var user = $"""
Write the interpretation section described above.

Targets guidance (use unless clinician instructions override):
- LOW / no disease: emphasize “protect and sustain”; typical goals: LDL/apoB in an optimal range per clinician, BP < 130/80, hs-CRP < 1.0.
- MILD or MODERATE: emphasize reversal/stop progression; BP < 130/80; hs-CRP < 1.0; LDL/apoB target commonly < 70 mg/dL (use clinician instruction if different).
- SEVERE / established disease: aggressive prevention; BP < 120/80; hs-CRP < 1.0; LDL/apoB target commonly < 30 mg/dL (use clinician instruction if different).

DATA (JSON):
{JsonSerializer.Serialize(new { cardiologyRiskCategory = cardioResult, reportRequest = request })}
""";

        var req = new
        {
            model = "gpt-5.2",
            input = new object[]
            {
                new
                {
                    role = "system",
                    content = new object[]
                    {
                        new { type = "input_text", text = system }
                    }
                },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "input_text", text = user }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(req);

        using var msg = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        msg.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var res = await Http.SendAsync(msg);
        var resText = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"OpenAI error {(int)res.StatusCode}: {resText}");

        return ExtractFirstOutputText(resText);
    }

    // -------- General improvement narrative --------
    public static async Task<string> GenerateImprovementParagraphAsync(object modelSummary)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Missing OPENAI_API_KEY env var.");

        var system = """
You write brief, practical medical and wellness improvement guidance.
Constraints:
- Multiple paragraphs
- Focus ONLY on areas labeled non-optimal.
- Diagnose, treat, suggest actions like you are a physician
- Use medical language, actionable habits, you are speaking to an educated patient.
""";

        var user = $"""
Here is a JSON summary of a health report.
Write the paragraph described above.

JSON:
{JsonSerializer.Serialize(modelSummary)}
""";

        var req = new
        {
            model = "gpt-4.1-mini",
            input = new object[]
            {
                new
                {
                    role = "system",
                    content = new object[]
                    {
                        new { type = "input_text", text = system }
                    }
                },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "input_text", text = user }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(req);

        using var msg = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        msg.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var res = await Http.SendAsync(msg);
        var resText = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"OpenAI error {(int)res.StatusCode}: {resText}");

        return ExtractFirstOutputText(resText);
    }

    // -------- Output types (match your schema) --------
    public sealed record MetabolicHealthAiResult(
        string MetabolicHealthCategory,
        DerivedMetrics DerivedMetrics,
        Grades Grades,
        Counts Counts,
        Flags Flags,
        BiggestContributor[] BiggestContributors,
        TopIntervention[] TopInterventions,
        string[] OptionalProgramsOffered,
        string[] Notes
    );

    public sealed record DerivedMetrics(double? TgHdlRatio, double? HomaIr, double? Fib4);

    public sealed record Grades(
        string A1c,
        string FastingInsulin,
        string TgHdlRatio,
        string HomaIr,
        string VisceralFatPercentile,
        string LeanToFatMassRatio,
        string Fib4
    );

    public sealed record Counts(int MildCount, int ModerateCount, int SevereCount, int NonOptimalCount);

    public sealed record Flags(bool IsolatedA1cElevation, bool IsolatedInsulinResistanceMarker);

    public sealed record BiggestContributor(string MetricName, string Severity, string MechanismLabel);

    public sealed record TopIntervention(string Bucket, string RecommendationText, int Priority);

    // -------- Category enforcement (server truth) --------
    private static string ComputeCategoryFromCounts(Counts c, Flags f)
    {
        if (c.NonOptimalCount == 0 || f.IsolatedA1cElevation) return "Optimal Metabolism";
        if (c.NonOptimalCount < 3 && c.ModerateCount == 0 && c.SevereCount == 0) return "Mild Metabolic Dysfunction";
        if (c.NonOptimalCount >= 3 || c.ModerateCount >= 2 || c.SevereCount >= 1) return "Metabolic Dysfunction";
        return "Metabolic Dysfunction";
    }

    // -------- A1c extraction helpers --------
    private static double? TryExtractA1cPercent(JsonElement metabolicInput)
    {
        // Accept either:
        // - root.healthAge.hemoglobinA1c
        // - root.HealthAge.HemoglobinA1c
        // - root.request.healthAge.hemoglobinA1c
        // - root.request.HealthAge.HemoglobinA1c
        // plus misspelling hemaglobinA1c
        var root = metabolicInput;

        if (TryGetPropCI(root, "request", out var req))
            root = req;

        if (!TryGetPropCI(root, "healthAge", out var healthAge) &&
            !TryGetPropCI(root, "HealthAge", out healthAge))
            return null;

        if (TryGetPropCI(healthAge, "hemoglobinA1c", out var a1cEl) ||
            TryGetPropCI(healthAge, "HemoglobinA1c", out a1cEl) ||
            TryGetPropCI(healthAge, "hemaglobinA1c", out a1cEl) ||
            TryGetPropCI(healthAge, "HemaglobinA1c", out a1cEl))
        {
            return ReadDoubleLoose(a1cEl);
        }

        return null;
    }

    private static bool TryGetPropCI(JsonElement obj, string name, out JsonElement value)
    {
        value = default;
        if (obj.ValueKind != JsonValueKind.Object) return false;

        foreach (var p in obj.EnumerateObject())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = p.Value;
                return true;
            }
        }
        return false;
    }

    private static double? ReadDoubleLoose(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.Number => el.GetDouble(),
            JsonValueKind.String => double.TryParse(el.GetString()?.Replace("%", "").Trim(), out var d) ? d : null,
            _ => null
        };
    }

    // -------- Responses API helpers --------
    private static string ExtractFirstOutputText(string responsesApiJson)
    {
        using var doc = JsonDocument.Parse(responsesApiJson);

        if (!doc.RootElement.TryGetProperty("output", out var outputArr))
            return "";

        foreach (var item in outputArr.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var contentArr)) continue;

            foreach (var c in contentArr.EnumerateArray())
            {
                if (c.TryGetProperty("type", out var typeEl) &&
                    typeEl.GetString() == "output_text" &&
                    c.TryGetProperty("text", out var textEl))
                {
                    return textEl.GetString() ?? "";
                }

                // fallback
                if (c.TryGetProperty("text", out var anyText))
                    return anyText.GetString() ?? "";
            }
        }

        return "";
    }
}