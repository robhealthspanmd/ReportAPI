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

            // --------------------------------------------------
            // 4) Fill Heart and Blood Vessel Health Assessment Results
            // --------------------------------------------------
            // NOTE: Labels must match the template text exactly or ReplaceLabel will throw.

            // Echocardiogram (overall)
            ReplaceLabel(body, "Echocardiogram:",
                S(request.Cardiology?.EchoOverallResult));

            // Carotid Ultrasound Plaque Assessment
            ReplaceLabel(body, "Carotid Ultrasound Plaque Assessment:",
                S(request.Cardiology?.CarotidPlaqueSeverity));

            // Abdominal Aorta Screen (not collected yet)
            ReplaceLabel(body, "Abdominal Aorta Screen:",
                "—");

            // Electrocardiogram (not collected yet)
            ReplaceLabel(body, "Electrocardiogram:",
                "—");

            // Exercise Stress Test (treadmill overall)
            ReplaceLabel(body, "Exercise Stress Test:",
                S(request.Cardiology?.TreadmillOverallResult));

            // CT Angiogram with plaque composition analysis (CTA overall)
            ReplaceLabel(body, "CT Angiogram:",
                S(request.Cardiology?.CtaOverallResult));

            // CT Calcium Score
            ReplaceLabel(body, "CT Calcium Score:",
                N(request.Cardiology?.CacScore));

            // Vo2 Fitness Test (PerformanceAge input = percentile)
            ReplaceLabel(body, "Vo2 Fitness Test:",
                P(request.PerformanceAge.Vo2MaxPercentile));

            // Blood Pressure
            ReplaceLabel(body, "Blood Pressure:",
                BP(request.HealthAge.SystolicBP, request.HealthAge.DiastolicBP));

            // LDL or apoB Cholesterol (using available Non-HDL input as proxy)
            ReplaceLabel(body, "LDL or apoB Cholesterol:",
                N(request.HealthAge.NonHdlMgDl));

            // Inflammation (hs-CRP from PhenoAge input)
            ReplaceLabel(body, "Inflammation:",
                CRP(request.PhenoAge.CRP_mg_L));

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

        var headers = firstRow.Descendants<TableCell>()
            .Select(c => Normalize(c.InnerText))
            .ToList();

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

    // Existing numeric helpers
    private static string F(double? value, string format)
        => value.HasValue ? value.Value.ToString(format) : "—";

    private static string P(double? value)
        => value.HasValue ? value.Value.ToString("F0") : "—";

    // New helpers for cardiology / vitals
    private static string S(string? value)
        => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private static string N(double? value)
        => value.HasValue ? value.Value.ToString("F0") : "—";

    private static string BP(double? sbp, double? dbp)
    {
        if (!sbp.HasValue || !dbp.HasValue) return "—";
        return $"{sbp.Value:F0}/{dbp.Value:F0} mmHg";
    }

    private static string CRP(double? crpMgL)
    {
        if (!crpMgL.HasValue) return "—";
        return $"{crpMgL.Value:F1} mg/L";
    }
}
