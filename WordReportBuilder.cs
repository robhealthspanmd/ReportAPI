using System;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

public static class WordReportBuilder
{
    public static byte[] BuildFullReport(
        ReportRequest request,
        PhenoAge.Result pheno,
        HealthAge.Result health,
        PerformanceAge.Result performance,
        BrainHealth.Result brain,
        string improvementParagraph
    )
    {
        var templatePath = Path.Combine(AppContext.BaseDirectory, "HealthspanAssessment.docx");
        if (!File.Exists(templatePath))
            throw new FileNotFoundException("HealthspanAssessment.docx not found in output directory.");

        using var ms = new MemoryStream();
        using (var fs = File.OpenRead(templatePath))
            fs.CopyTo(ms);

        ms.Position = 0;

        using (var doc = WordprocessingDocument.Open(ms, true))
        {
            var body = doc.MainDocumentPart!.Document.Body!;

            // --------------------------------------------------
            // 1) Fill "Your Three Healthspan Scores" table
            // --------------------------------------------------
            var scoresTable = body
                .Descendants<Table>()
                .First(t => IsScoresTable(t));

            foreach (var row in scoresTable.Descendants<TableRow>())
            {
                var cells = row.Descendants<TableCell>().ToList();
                if (cells.Count < 2) continue;

                var label = Normalize(cells[0].InnerText);

                if (label == "healthspan age")
                    SetCell(cells[1], $"{health.HealthAgeFinal:F1}");

                if (label == "physical performance age")
                    SetCell(cells[1], $"{performance.PerformanceAge:F1}");

                if (label == "brain health score")
                    SetCell(cells[1], $"{brain.TotalScore:F0}");
            }

            // --------------------------------------------------
            // 2) Insert AI summary paragraph under the table
            // --------------------------------------------------
            scoresTable.InsertAfterSelf(
                new Paragraph(
                    new Run(
                        new Text(improvementParagraph ?? "")
                        {
                            Space = SpaceProcessingModeValues.Preserve
                        }
                    )
                )
            );

            // --------------------------------------------------
            // 3) Fill Metabolic Health Results section
            // --------------------------------------------------
            ReplaceLabel(body, "Hemoglobin A1c:", "—");

ReplaceLabel(body, "Fasting insulin:",
    F(request.HealthAge.FastingInsulin_uIU_mL, "F1"));

ReplaceLabel(body, "HDL cholesterol:",
    F(request.HealthAge.Hdl_mg_dL, "F0"));

ReplaceLabel(body, "Triglycerides:",
    F(request.HealthAge.Triglycerides_mg_dL, "F0"));

ReplaceLabel(body, "Visceral fat percentile:",
    P(request.HealthAge.VisceralFatPercentile));

ReplaceLabel(body, "Body fat percentile:",
    P(request.HealthAge.BodyFatPercentile));

ReplaceLabel(body, "Lean mass:",
    P(request.HealthAge.AppendicularMusclePercentile));


            doc.MainDocumentPart.Document.Save();
        }

        return ms.ToArray();
    }

    // --------------------------------------------------
    // Helpers
    // --------------------------------------------------

    private static bool IsScoresTable(Table table)
    {
        var firstRow = table.Descendants<TableRow>().FirstOrDefault();
        if (firstRow == null) return false;

        var headers = firstRow.Descendants<TableCell>().Select(c => Normalize(c.InnerText)).ToList();

        return headers.Count >= 3
            && headers[0] == "score"
            && headers[1] == "your value"
            && headers[2] == "optimal range";
    }

    private static void ReplaceLabel(Body body, string label, string value)
    {
        foreach (var p in body.Descendants<Paragraph>())
        {
            var text = string.Concat(p.Descendants<Text>().Select(t => t.Text));
            if (!text.Contains(label)) continue;

            var first = p.Descendants<Text>().First(t => t.Text.Contains(label));
            first.Text = $"{label} {value}";

            foreach (var extra in p.Descendants<Text>().Skip(1).ToList())
                extra.Remove();

            return;
        }

        throw new InvalidOperationException($"Label '{label}' not found in template.");
    }

    private static void SetCell(TableCell cell, string value)
    {
        var texts = cell.Descendants<Text>().ToList();

        if (!texts.Any())
        {
            cell.RemoveAllChildren<Paragraph>();
            cell.AppendChild(
                new Paragraph(
                    new Run(
                        new Text(value) { Space = SpaceProcessingModeValues.Preserve }
                    )
                )
            );
            return;
        }

        texts[0].Text = value;
        foreach (var t in texts.Skip(1)) t.Remove();
    }

    private static string Normalize(string s)
        => (s ?? "").Trim().ToLowerInvariant();
        private static string F(double? value, string format)
    => value.HasValue ? value.Value.ToString(format) : "—";

private static string P(double? value)
    => value.HasValue ? value.Value.ToString("F0") : "—";
}
