using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public static class AiInsights
{
    private static readonly HttpClient Http = new();

    public static async Task<string> GenerateImprovementParagraphAsync(object modelSummary)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Missing OPENAI_API_KEY env var.");

        var system = """
You write brief, practical wellness improvement guidance.
Constraints:
- One paragraph only (3–6 sentences).
- Focus ONLY on areas labeled non-optimal.
- No diagnosis, no medical advice, no medication guidance.
- Use plain language, actionable habits.
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
