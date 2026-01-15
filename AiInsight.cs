using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public static class AiInsights
{
    private static readonly HttpClient Http = new();

    // -------- Clinical preventive checklist (structured JSON) --------
    public static async Task<ClinicalPreventiveChecklistResult> GenerateClinicalPreventiveChecklistAsync(JsonElement preventiveInput)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Missing OPENAI_API_KEY env var.");

        var system = """
You are a preventive surveillance checklist engine for clinical reporting.
You MUST apply the algorithm exactly as provided and return ONLY valid JSON (no prose, no markdown).
Never use the dash character in the output.

Purpose:
- Track whether a patient is up to date on core preventive surveillance domains.
- Identify assessment-anchored opportunities to reduce future disease risk.
- This is NOT a score. It is a structured preventive checklist.

Status enum (use exactly):
- optimal_or_up_to_date
- needs_attention
- data_missing

Global rules:
- Each domain outputs status, key_findings[], and opportunities[].
- opportunities[] must be empty unless status != optimal_or_up_to_date.
- Do NOT invent dates or results. Use provided values or mark data_missing.
- Use the provided currentDateUtc (YYYY-MM-DD) for due/overdue comparisons.

Input mapping notes:
- demographics.ageYears (number), demographics.sex (string), demographics.pregnancyPotential (boolean|null).
- labs: healthAge.ast, healthAge.alt, healthAge.platelets; phenoAge.wbc10e3PeruL.
- clinicalData contains nested domain inputs (cancerScreening, thyroid, sexHormoneHealth, kidney, liverGi, bloodHealth, boneHealth, vaccinations, supplements).

Domain logic:
1) Cancer screening
For each screening type entry:
- If lastCompletedDate exists and nextDueDate exists:
   - If nextDueDate < currentDateUtc -> needs_attention for that screening.
   - Else -> optimal_or_up_to_date for that screening.
- If lastCompletedDate exists and nextDueDate missing -> data_missing (schedule unknown).
- If no data at all -> data_missing.
Assessment:
- If all required screenings are optimal_or_up_to_date -> domain status optimal_or_up_to_date.
- Else if any screening needs_attention -> domain status needs_attention.
- Else -> data_missing.
Key findings should summarize each screening status with dates (if available).
Opportunities:
- If any needs_attention: "You are missing recommended screening: ...".
- If up to date but has upcoming nextDueDate within 3 months: "Your next recommended screenings are due: ...".
Advanced options (total body MRI, genetic testing, MCED):
- Only include opportunities if high-risk flags exist (eligibilityFlags includes family/genetic risk)
  OR cancerScreening.wantsAdvancedScreening == true
  OR cancerScreening.discussAdvancedOptions == true.

2) Thyroid
TSH optimal: 0.5–2.5 mIU/L.
- If TSH missing -> data_missing.
- If TSH optimal -> optimal_or_up_to_date with key finding.
- If TSH elevated (>2.5) -> needs_attention.
- If TSH suppressed (<0.5) -> needs_attention.
Opportunities per spec:
- Always recommend repeat TSH and add Free T4, Free T3, TgAb, TPO if not available when abnormal.
- If on thyroid meds: suggest clinician-directed adjustment.
- If T4/T3 normal and symptoms minimal: monitor.
- If persistent elevation + symptoms or T4/T3 low/trending down: consider hormone replacement discussion.

3) Sex hormone health (contextual)
- If labs present AND symptom/context present: status per clinician-configured thresholds (if unknown, set data_missing).
- If labs present but symptom/context missing -> data_missing for interpretation context.
- If symptoms flagged and no labs -> needs_attention (testing opportunity).
Opportunities:
- If abnormal labs or symptoms suggest imbalance: recommend clinical review and repeat/expanded testing.
- If on hormone therapy: recommend monitoring plan and safety labs.
- If normal and asymptomatic: continue monitoring.

4) Kidney
Optimal: eGFR > 60 AND UACR < 30 mg/g.
- If both optimal -> optimal_or_up_to_date.
- If either abnormal -> needs_attention.
- If missing either -> data_missing.
Opportunities:
- If UACR >=30: albuminuria suggests early kidney/vascular stress -> confirm and address drivers.
- If eGFR <60: monitor trends and evaluate contributors.
- If abnormal: repeat labs to confirm and trend.

5) Liver / GI
Optimal: AST < 30 AND ALT < 30 (use healthAge ast/alt if clinicalData not provided).
- If both optimal -> optimal_or_up_to_date.
- If either elevated -> needs_attention.
- If missing -> data_missing.
Opportunities:
- Mild elevation: repeat to confirm; review contributors (metabolic health, alcohol, meds/supplements).
- Persistent elevation: consider hepatic workup (imaging, hepatitis screening, fibrosis assessment).

6) Blood health
Ranges:
- Hemoglobin: Men 13.5–17.5 g/dL; Women 12.0–15.5 g/dL.
- WBC: 4.0–11.0 (10^3/µL). Input is phenoAge.wbc10e3PeruL.
- Platelets: 150–450 (10^3/µL) from healthAge.platelets if available.
Classification:
- All in range -> optimal_or_up_to_date.
- Any out of range -> needs_attention.
- Missing CBC values -> data_missing.
Opportunities:
- If hemoglobin low: evaluate iron/B12/folate and bleeding risk.
- If WBC or platelets abnormal: repeat to confirm; evaluate causes.

7) Bone health
T-score:
- >= -1.0 optimal
- -1.0 to -2.49 needs_attention (osteopenia)
- <= -2.5 needs_attention (osteoporosis)
- If missing DEXA and age/sex suggests eligibility -> data_missing.
Opportunities:
- If osteopenia/osteoporosis: clinician review and prevention strategy.
- If missing but eligible: recommend DEXA scheduling.

8) Vaccinations
- If vaccinationStatus indicates up to date -> optimal_or_up_to_date.
- If missing items -> needs_attention.
- If unknown -> data_missing.
Opportunities:
- If missing: list vaccines due/missing.
- If up to date: list next due.

9) Supplements (surveillance)
- TakesSupplements yes/no.
- SupplementsThirdPartyTested yes/no/unknown.
- SupplementsRecommendedBy text.
Classification:
- If takes supplements yes and third-party tested is false/unknown -> needs_attention.
- If takes supplements yes and third-party tested true -> optimal_or_up_to_date.
- If takes supplements no -> optimal_or_up_to_date.
- If missing -> data_missing.
Opportunities:
- If not third-party tested: suggest review and third-party testing preference.
""";

        var outputSchema = new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "assessment", "strategyOpportunities" },
            properties = new
            {
                assessment = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        additionalProperties = false,
                        required = new[] { "domain", "status", "keyFindings", "nextDue" },
                        properties = new
                        {
                            domain = new { type = "string" },
                            status = new { type = "string" },
                            keyFindings = new { type = "array", items = new { type = "string" } },
                            nextDue = new { type = new[] { "string", "null" } }
                        }
                    }
                },
                strategyOpportunities = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        additionalProperties = false,
                        required = new[] { "domain", "opportunities" },
                        properties = new
                        {
                            domain = new { type = "string" },
                            opportunities = new
                            {
                                type = "array",
                                items = new
                                {
                                    type = "object",
                                    additionalProperties = false,
                                    required = new[] { "domain", "triggerFinding", "whyItMatters", "nextStep", "timing" },
                                    properties = new
                                    {
                                        domain = new { type = "string" },
                                        triggerFinding = new { type = "string" },
                                        whyItMatters = new { type = "string" },
                                        nextStep = new { type = "string" },
                                        timing = new { type = "string" }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        var userText =
            "Run the Clinical Preventive Checklist Algorithm.\n\n" +
            "INPUT_JSON:\n" + preventiveInput.GetRawText();

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
                    name = "clinical_preventive_checklist_output",
                    strict = true,
                    schema = outputSchema
                }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);

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

        return JsonSerializer.Deserialize<ClinicalPreventiveChecklistResult>(
            contentText,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        ) ?? throw new InvalidOperationException("Failed to deserialize clinical preventive checklist JSON.");
    }

    // -------- Protect Your Brain (structured JSON) --------
    public static async Task<ProtectYourBrainResult> GenerateProtectYourBrainAsync(JsonElement brainInput)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Missing OPENAI_API_KEY env var.");

        var system = """
You are a clinical brain health assessment and protection strategy engine.
You MUST apply the Protect Your Brain algorithm and return ONLY valid JSON (no prose, no markdown).
Never use the dash character in the output.

Use the provided input fields to:
- confirm the cognitive classification (Above Average, Average, Below Average),
- confirm the brain risk category (Lower, Intermediate, Higher),
- use the trend label (Improving, Stable, Worsening, Unknown),
- include genetic/family context only if known,
- generate an interpretation that avoids deterministic or fear-based framing.

Assessment rules:
- Cognitive function is BrainCheck percentile (already capped in input).
- Trend modifies interpretation language but never replaces the current classification.
- If trend is "Unknown", do not mention trend in the interpretation.
- Higher Risk if cognition is Below Average OR ApoE4 homozygous OR significant decline.
- Intermediate Risk if cognition is Average/Above AND ApoE4 or family history is present.
- Lower Risk if cognition is Average/Above AND no genetic/family risk.

Strategy rules:
- Only include opportunities when the corresponding trigger is true.
- Strategy is direction, not instruction: do not list techniques, exercises, programs, or habits.
- Use leverage language that names what to preserve or strengthen and why it matters for this person.
- If no triggers are true, return an empty opportunities array.
- Use intent phrases and capacity focus terms from the strategy language system below.

Strategy language system:
Intent phrases:
“A meaningful opportunity for you is to”
“A high-leverage focus for improving this score is to”
“An important area to strengthen is”
“A protective factor worth preserving is”
“An area that may amplify progress across other health domains is”
Capacity focus terms:
connection and engagement
resilience and adaptability
emotional balance
stress load regulation
optimism and future orientation
sense of meaning and purpose
psychological flexibility
recovery capacity
consistency and follow-through
Outcome framing:
supports long-term health behaviors
reinforces resilience under stress
improves capacity to sustain change
reduces physiologic stress burden
supports cardiovascular and cognitive health
enhances recovery and adaptability
acts as a protective factor over time
Context amplifiers (optional, only when supported by data):
given your current stress profile
in the context of your cardiovascular risk
as you work on multiple health domains
given the long-term nature of your prevention goals
because emotional load can affect sleep and metabolism

Output structure:
{
  "assessment": {
    "cognitiveClassification": string,
    "braincheckPercentile": number,
    "trend": string,
    "brainRiskCategory": string,
    "geneticFamilyContext": string|null,
    "interpretation": string
  },
  "strategy": {
    "opportunities": [
      {
        "domain": string,
        "trigger": string,
        "whyItMatters": string,
        "opportunity": string
      }
    ]
  }
}
""";

        var outputSchema = new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "assessment", "strategy" },
            properties = new
            {
                assessment = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[]
                    {
                        "cognitiveClassification",
                        "braincheckPercentile",
                        "trend",
                        "brainRiskCategory",
                        "geneticFamilyContext",
                        "interpretation"
                    },
                    properties = new
                    {
                        cognitiveClassification = new { type = "string" },
                        braincheckPercentile = new { type = "number" },
                        trend = new { type = "string" },
                        brainRiskCategory = new { type = "string" },
                        geneticFamilyContext = new { type = new[] { "string", "null" } },
                        interpretation = new { type = "string" }
                    }
                },
                strategy = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "opportunities" },
                    properties = new
                    {
                        opportunities = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                additionalProperties = false,
                                required = new[] { "domain", "trigger", "whyItMatters", "opportunity" },
                                properties = new
                                {
                                    domain = new { type = "string" },
                                    trigger = new { type = "string" },
                                    whyItMatters = new { type = "string" },
                                    opportunity = new { type = "string" }
                                }
                            }
                        }
                    }
                }
            }
        };

        var userText =
            "Run the Protect Your Brain algorithm and produce the assessment + strategy output.\n\n" +
            "INPUT_JSON:\n" + brainInput.GetRawText();

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
                    name = "protect_your_brain_output",
                    strict = true,
                    schema = outputSchema
                }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);

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

        return JsonSerializer.Deserialize<ProtectYourBrainResult>(
            contentText,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        ) ?? throw new InvalidOperationException("Failed to deserialize Protect Your Brain JSON.");
    }

    // -------- Mentally & Emotionally Well (structured JSON) --------
    public static async Task<MentallyEmotionallyWellResult> GenerateMentallyEmotionallyWellAsync(JsonElement mentalInput)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Missing OPENAI_API_KEY env var.");

        var system = """
You are a mental and emotional well-being assessment engine.
You MUST apply the provided algorithm inputs and return ONLY valid JSON (no prose, no markdown).
Never use the dash character in the output.

Assessment rules:
- Use the provided domain status and trends for depression, anxiety, and stress.
- Trend modifies interpretation language but never overrides the current status.
- If a domain trend is "Unknown", omit trend language for that domain in the interpretation.
- Overall status is provided as Optimal or Opportunities Identified.
- Use neutral, non-judgmental language and avoid pathologizing normal emotional states.

Strategy rules:
- Include an opportunity only when the trigger is true.
- Strategy is direction, not instruction: do not list techniques, exercises, programs, or habits.
- Use leverage language that names what to preserve or strengthen and why it matters for this person.
- Emphasize early detection, progress, and follow-up.
- If stressEmphasis is true, ensure stress opportunity text is slightly emphasized.
- Use intent phrases and capacity focus terms from the strategy language system below.

Strategy language system:
Intent phrases:
“A meaningful opportunity for you is to”
“A high-leverage focus for improving this score is to”
“An important area to strengthen is”
“A protective factor worth preserving is”
“An area that may amplify progress across other health domains is”
Capacity focus terms:
connection and engagement
resilience and adaptability
emotional balance
stress load regulation
optimism and future orientation
sense of meaning and purpose
psychological flexibility
recovery capacity
consistency and follow-through
Outcome framing:
supports long-term health behaviors
reinforces resilience under stress
improves capacity to sustain change
reduces physiologic stress burden
supports cardiovascular and cognitive health
enhances recovery and adaptability
acts as a protective factor over time
Context amplifiers (optional, only when supported by data):
given your current stress profile
in the context of your cardiovascular risk
as you work on multiple health domains
given the long-term nature of your prevention goals
because emotional load can affect sleep and metabolism

Assessment templates:
Optimal:
Your depression, anxiety, and stress scores are in healthy ranges.
This suggests your emotional state is currently supporting, rather than undermining, physical health and recovery.
Needs Attention:
Your results show elevated depression, anxiety, or stress.
Emotional strain can quietly amplify physical risk and make recovery and behavior change more difficult if left unaddressed.

Strategy templates:
Optimal:
Strategy: Maintain emotional balance.
Emotional stability supports sleep quality, metabolic regulation, cardiovascular health, and cognitive resilience.
Needs Attention:
Strategy: Reduce emotional load to support whole body health.
Improving emotional well being may reduce physiologic stress on the heart, brain, and metabolism, and improve the effectiveness of other health strategies.
If heart or brain risk is elevated:
Given your cardiovascular or cognitive risk profile, addressing emotional strain may be especially protective.

Output structure:
{
  "assessment": {
    "overallStatus": string,
    "summaryStatement": string,
    "domains": [
      {
        "domain": string,
        "status": string,
        "trend": string
      }
    ]
  },
  "strategy": {
    "opportunities": [
      {
        "domain": string,
        "whyItMatters": string,
        "opportunity": string
      }
    ]
  }
}
""";

        var outputSchema = new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "assessment", "strategy" },
            properties = new
            {
                assessment = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "overallStatus", "summaryStatement", "domains" },
                    properties = new
                    {
                        overallStatus = new { type = "string" },
                        summaryStatement = new { type = "string" },
                        domains = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                additionalProperties = false,
                                required = new[] { "domain", "status", "trend" },
                                properties = new
                                {
                                    domain = new { type = "string" },
                                    status = new { type = "string" },
                                    trend = new { type = "string" }
                                }
                            }
                        }
                    }
                },
                strategy = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "opportunities" },
                    properties = new
                    {
                        opportunities = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                additionalProperties = false,
                                required = new[] { "domain", "whyItMatters", "opportunity" },
                                properties = new
                                {
                                    domain = new { type = "string" },
                                    whyItMatters = new { type = "string" },
                                    opportunity = new { type = "string" }
                                }
                            }
                        }
                    }
                }
            }
        };

        var userText =
            "Run the Mentally & Emotionally Well algorithm and produce assessment + strategy output.\n\n" +
            "INPUT_JSON:\n" + mentalInput.GetRawText();

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
                    name = "mentally_emotionally_well_output",
                    strict = true,
                    schema = outputSchema
                }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);

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

        return JsonSerializer.Deserialize<MentallyEmotionallyWellResult>(
            contentText,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        ) ?? throw new InvalidOperationException("Failed to deserialize Mentally & Emotionally Well JSON.");
    }

    // -------- Metabolic AI (RAW JSON in, structured output out) --------
    public static async Task<MetabolicHealthAiAlgorithmResult> GenerateMetabolicHealthAlgorithmAsync(JsonElement metabolicInput)
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
Never use the dash character in the output.

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

Recommendation constraint:
- Only recommend actions tied directly to assessed metrics in the input (A1c, fasting insulin, TG/HDL, HOMA-IR, visceral fat percentile, lean-to-fat ratio, FIB-4).
- Do NOT suggest specific dietary changes (e.g., reduce carbs, cut sugar) or lifestyle changes that require unassessed details.
- When nutrition is relevant, keep recommendations high-level and framed as "review dietary patterns with a clinician" rather than prescribing changes.

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

        var result = JsonSerializer.Deserialize<MetabolicHealthAiAlgorithmResult>(
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

    // -------- Metabolic opportunity narratives --------
    public static async Task<string[]> GenerateMetabolicOpportunityParagraphsAsync(
        JsonElement metabolicInput,
        MetabolicHealthAiAlgorithmResult algorithmResult)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Missing OPENAI_API_KEY env var.");

        var system = """
You are a physician writing short opportunity paragraphs for a metabolic health report.
Never use the dash character in the output.
You will receive the computed metabolic algorithm output plus the original input JSON.

Hard requirements:
1) Write one short paragraph per opportunity in the provided TopInterventions list, in the same order.
2) Each paragraph should be 2 to 4 sentences, patient-friendly, and clinically accurate.
3) Do not include bullet points or numbering.
4) Do not invent tests or results not present in the data.
5) Avoid specific diet prescriptions; keep nutrition guidance high-level (review patterns with clinician).
6) If TopInterventions is empty, return an empty array.
""";

        var user = $"""
Write short opportunity paragraphs for the TopInterventions.

DATA (JSON):
{JsonSerializer.Serialize(new
{
    metabolicInput = metabolicInput,
    algorithmResult = algorithmResult
})}
""";

        var outputSchema = new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "opportunityParagraphs" },
            properties = new
            {
                opportunityParagraphs = new
                {
                    type = "array",
                    maxItems = 3,
                    items = new { type = "string" }
                }
            }
        };

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
            },
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "metabolic_opportunity_paragraphs_output",
                    strict = true,
                    schema = outputSchema
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

        var contentText = ExtractFirstOutputText(resText);
        if (string.IsNullOrWhiteSpace(contentText))
            throw new InvalidOperationException("OpenAI returned empty output text.");

        var parsed = JsonSerializer.Deserialize<MetabolicOpportunityParagraphsOutput>(
            contentText,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        ) ?? throw new InvalidOperationException("Failed to deserialize metabolic opportunity paragraphs.");

        return parsed.OpportunityParagraphs ?? Array.Empty<string>();
    }

    // -------- Cardiology narrative --------
    public static async Task<string> GenerateCardiologyInterpretationAsync(ReportRequest request, Cardiology.Result cardioResult)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Missing OPENAI_API_KEY env var.");

        var system = """
You are a physician writing the “Heart and Blood Vessel Health Interpretation and Strategy” section.
Never use the dash character in the output.
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

    // -------- Physical performance narrative --------
    public static Task<string> GenerateFitnessMobilityAssessmentAsync(ReportRequest request, PerformanceAge.Result performanceResult)
    {
        var data = new
        {
            chronologicalAgeYears = request.PhenoAge.ChronologicalAgeYears,
            performanceAgeYears = performanceResult.PerformanceAge,
            performanceDeltaYears = performanceResult.DeltaVsAgeYears,
            performanceDeltaPercent = performanceResult.DeltaVsAgePercent,
            tests = new
            {
                request.PerformanceAge.Vo2MaxPercentile,
                performanceResult.HeartRateRecovery,
                request.PerformanceAge.GaitSpeedComfortablePercentile,
                request.PerformanceAge.GaitSpeedMaxPercentile,
                performanceResult.TrunkEndurance,
                performanceResult.PostureAssessment,
                performanceResult.FloorToStandTest,
                performanceResult.MobilityRom
            }
        };

        return GeneratePhysicalPerformanceAssessmentAsync("Fitness and Mobility", data);
    }

    public static Task<string> GenerateStrengthStabilityAssessmentAsync(ReportRequest request, PerformanceAge.Result performanceResult)
    {
        var data = new
        {
            chronologicalAgeYears = request.PhenoAge.ChronologicalAgeYears,
            performanceAgeYears = performanceResult.PerformanceAge,
            performanceDeltaYears = performanceResult.DeltaVsAgeYears,
            performanceDeltaPercent = performanceResult.DeltaVsAgePercent,
            tests = new
            {
                request.PerformanceAge.QuadricepsStrengthPercentile,
                performanceResult.HipStrength,
                performanceResult.CalfStrength,
                performanceResult.RotatorCuffIntegrity,
                performanceResult.IsometricThighPullPercentile,
                performanceResult.IsometricThighPull,
                request.PerformanceAge.GripStrengthPercentile,
                request.PerformanceAge.PowerPercentile,
                request.PerformanceAge.BalancePercentile,
                request.PerformanceAge.ChairRisePercentile,
                performanceResult.ChairRiseFiveTimes,
                performanceResult.ChairRiseThirtySeconds
            }
        };

        return GeneratePhysicalPerformanceAssessmentAsync("Strength and Stability", data);
    }

    private static async Task<string> GeneratePhysicalPerformanceAssessmentAsync(string domainName, object data)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Missing OPENAI_API_KEY env var.");

        var system = $"""
You are a clinician writing the “{domainName} Assessment” section.
Never use the dash character in the output.
This is an assessment-only narrative: do not prescribe exercise tactics, workouts, or step-by-step plans.
You will be given performance inputs and computed outputs relevant to {domainName}.

Hard requirements:
1) Use medical language and a clear, educated-patient tone.
2) Start by stating what is OPTIMAL vs NOT OPTIMAL based on the findings and numbers.
3) Focus on interpretation and clinical meaning; do not give specific tactics or training prescriptions.
4) Do not invent tests or results that are not in the JSON.
5) Only discuss the tests provided for {domainName}. Do not reference other physical performance domains.
6) Multiple paragraphs. No bullet points.
""";

        var user = $"""
Write the {domainName} assessment described above.

DATA (JSON):
{JsonSerializer.Serialize(data)}
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
Never use the dash character in the output.
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
    public sealed record MetabolicHealthAiAlgorithmResult(
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

    public sealed record MetabolicHealthAiResult(
        string MetabolicHealthCategory,
        DerivedMetrics DerivedMetrics,
        Grades Grades,
        Counts Counts,
        Flags Flags,
        BiggestContributor[] BiggestContributors,
        string[] OpportunityParagraphs,
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

    public sealed record MetabolicOpportunityParagraphsOutput(string[] OpportunityParagraphs);

    public sealed record ClinicalPreventiveChecklistResult(
        ClinicalPreventiveAssessment[] Assessment,
        ClinicalPreventiveStrategy[] StrategyOpportunities
    );

    public sealed record ClinicalPreventiveAssessment(
        string Domain,
        string Status,
        string[] KeyFindings,
        string? NextDue
    );

    public sealed record ClinicalPreventiveStrategy(
        string Domain,
        ClinicalPreventiveOpportunity[] Opportunities
    );

    public sealed record ClinicalPreventiveOpportunity(
        string Domain,
        string TriggerFinding,
        string WhyItMatters,
        string NextStep,
        string Timing
    );

    public sealed record ProtectYourBrainResult(
        ProtectYourBrainAssessment Assessment,
        ProtectYourBrainStrategy Strategy
    );

    public sealed record ProtectYourBrainAssessment(
        string CognitiveClassification,
        double BraincheckPercentile,
        string Trend,
        string BrainRiskCategory,
        string? GeneticFamilyContext,
        string Interpretation
    );

    public sealed record ProtectYourBrainStrategy(
        ProtectYourBrainOpportunity[] Opportunities
    );

    public sealed record ProtectYourBrainOpportunity(
        string Domain,
        string Trigger,
        string WhyItMatters,
        string Opportunity
    );

    public sealed record MentallyEmotionallyWellResult(
        MentallyEmotionallyWellAssessment Assessment,
        MentallyEmotionallyWellStrategy Strategy
    );

    public sealed record MentallyEmotionallyWellAssessment(
        string OverallStatus,
        string SummaryStatement,
        MentallyEmotionallyWellDomain[] Domains
    );

    public sealed record MentallyEmotionallyWellDomain(
        string Domain,
        string Status,
        string Trend
    );

    public sealed record MentallyEmotionallyWellStrategy(
        MentallyEmotionallyWellOpportunity[] Opportunities
    );

    public sealed record MentallyEmotionallyWellOpportunity(
        string Domain,
        string WhyItMatters,
        string Opportunity
    );

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
