using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public static class AiInsights
{
    private static readonly HttpClient Http = new();
    public static async Task<string> GenerateCardiologyInterpretationAsync(
        ReportRequest request,
        Cardiology.Result cardioResult
    )
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Missing OPENAI_API_KEY env var.");

        // IMPORTANT: this will force the model to obey the clinician instructions,
        // and to structure output the way you described.
        var system = """
You are a physician writing the “Heart and Blood Vessel Health Interpretation and Strategy” section.
You will be given:
- Cardiology findings (inputs) and an already-computed cardiology risk category: LOW, MILD, MODERATE, or SEVERE
- Other relevant vitals/labs/fitness context from the same report (BP, lipids, hs-CRP, VO2 percentile, etc.)
- A field called SpecificCardiologyInstructions written by a clinician.

Hard requirements:
1) You MUST follow SpecificCardiologyInstructions exactly (treat it as clinician orders).
2) Use medical language, clear, educated-patient tone.
3) Start by stating what is OPTIMAL vs NOT OPTIMAL based on the findings and numbers.
4) Then provide the “targets” (where their numbers should be) appropriate to risk severity.
5) Do not invent tests or results that are not in the JSON.
6) Multiple paragraphs. No bullet points unless the clinician instructions explicitly request bullets.
""";

        // We pass ALL data: request object + cardioResult + explicit “targets rule” guidance.
        // You can tweak these thresholds later; for now this mirrors your template language.
        var user = $"""
Write the interpretation section described above.

Targets guidance (use unless clinician instructions override):
- LOW / no disease: emphasize “protect and sustain”; typical goals: LDL/apoB in an optimal range per clinician, BP < 130/80, hs-CRP < 1.0.
- MILD or MODERATE: emphasize reversal/stop progression; BP < 130/80; hs-CRP < 1.0; LDL/apoB target commonly < 70 mg/dL (use clinician instruction if different).
- SEVERE / established disease: aggressive prevention; BP < 120/80; hs-CRP < 1.0; LDL/apoB target commonly < 30 mg/dL (use clinician instruction if different).

DATA (JSON):
{JsonSerializer.Serialize(new
{
    cardiologyRiskCategory = cardioResult,
    reportRequest = request
})}
""";

        var req = new
        {
            model = "gpt-4.1-mini",
            input = new object[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user }
            }
        };

        var json = JsonSerializer.Serialize(req);
        using var msg = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        msg.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var res = await Http.SendAsync(msg);
        var resText = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            throw new Exception($"OpenAI error {(int)res.StatusCode}: {resText}");

        using var doc = JsonDocument.Parse(resText);

        var output = doc.RootElement.GetProperty("output");
        foreach (var item in output.EnumerateArray())
        {
            if (item.TryGetProperty("content", out var contentArr))
            {
                foreach (var c in contentArr.EnumerateArray())
                {
                    if (c.TryGetProperty("text", out var textEl))
                        return textEl.GetString() ?? "";
                }
            }
        }

        return "";
    }
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
                new { role = "system", content = system },
                new { role = "user", content = user }
            }
        };

        var json = JsonSerializer.Serialize(req);
        using var msg = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        msg.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var res = await Http.SendAsync(msg);
        var resText = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            throw new Exception($"OpenAI error {(int)res.StatusCode}: {resText}");

        using var doc = JsonDocument.Parse(resText);

        // Responses API returns output arrays; this extracts the first text chunk.
        // (Good enough for production; we can harden later.)
        var output = doc.RootElement.GetProperty("output");
        foreach (var item in output.EnumerateArray())
        {
            if (item.TryGetProperty("content", out var contentArr))
            {
                foreach (var c in contentArr.EnumerateArray())
                {
                    if (c.TryGetProperty("text", out var textEl))
                        return textEl.GetString() ?? "";
                }
            }
        }

        return "";
    }
}
