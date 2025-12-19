using System;
using System.IO;
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
        BrainHealth.Result brain
    )
    {
        using var ms = new MemoryStream();

        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body!;

            // Title
            body.Append(ParagraphOf("Healthspan Report", bold: true, fontSizeHalfPoints: 40));
            body.Append(ParagraphOf($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC", italic: true));

            body.Append(Spacer());

            // ---- Summary block ----
            body.Append(SectionHeader("Summary"));
            body.Append(BuildTwoColumnTable(
                ("Phenotypic Age (years)", pheno.PhenotypicAgeYears.ToString("F1")),
                ("10-year Mortality Risk", pheno.Mortality10Yr.ToString("P2")),
                ("Health Age (years)", health.HealthAgeFinal.ToString("F1")),
                ("Performance Age (years)", performance.PerformanceAge.ToString("F1")),
                ("Brain Health Score", brain.TotalScore.ToString("F1")),
                ("Brain Health Level", brain.Level)
            ));

            body.Append(Spacer());

            // ---- Phenotypic Age ----
            body.Append(SectionHeader("Phenotypic Age"));
            body.Append(ParagraphOf($"Phenotypic Age (years): {pheno.PhenotypicAgeYears:F1}", bold: true));
            body.Append(ParagraphOf($"10-year Mortality Risk: {pheno.Mortality10Yr:P2}"));
            body.Append(ParagraphOf("Model Version: phenoage-levine-2018-v1"));

            body.Append(Spacer());

            body.Append(SubHeader("Phenotypic Age Inputs"));
            body.Append(BuildTwoColumnTable(
                ("Chronological Age (years)", request.PhenoAge.ChronologicalAgeYears.ToString("F1")),
                ("Albumin (g/dL)", request.PhenoAge.Albumin_g_dL.ToString("F2")),
                ("Creatinine (mg/dL)", request.PhenoAge.Creatinine_mg_dL.ToString("F2")),
                ("Glucose (mg/dL)", request.PhenoAge.Glucose_mg_dL.ToString("F1")),
                ("CRP (mg/L)", request.PhenoAge.CRP_mg_L.ToString("F2")),
                ("Lymphocytes (%)", request.PhenoAge.LymphocytePercent.ToString("F1")),
                ("MCV (fL)", request.PhenoAge.MCV_fL.ToString("F1")),
                ("RDW (%)", request.PhenoAge.RDW_percent.ToString("F1")),
                ("Alk Phos (U/L)", request.PhenoAge.AlkalinePhosphatase_U_L.ToString("F1")),
                ("WBC (10^3/µL)", request.PhenoAge.WBC_10e3_per_uL.ToString("F2"))
            ));

            body.Append(Spacer());

            // ---- Health Age ----
            body.Append(SectionHeader("Health Age"));
            body.Append(ParagraphOf($"Health Age (years): {health.HealthAgeFinal:F1}", bold: true));
            body.Append(ParagraphOf($"Δ vs Chronological (years): {health.DeltaVsChronoYears:F1}"));
            body.Append(ParagraphOf($"Δ vs Chronological (%): {health.DeltaVsChronoPercent:P1}"));
            body.Append(ParagraphOf($"Contribution (scaled): {health.ContributionYearsScaled:F2} years"));

            body.Append(Spacer());

            body.Append(SubHeader("Health Age Breakdown (percent-of-age)"));
            body.Append(BuildTwoColumnTable(
                ("Z1 Body Fat", FormatZ(health.Z1_BodyFat)),
                ("Z2 Visceral Fat", FormatZ(health.Z2_VisceralFat)),
                ("Z3 Appendicular Muscle", FormatZ(health.Z3_AppendicularMuscle)),
                ("Z4 Blood Pressure", FormatZ(health.Z4_BloodPressure)),
                ("Z5 Non-HDL", FormatZ(health.Z5_NonHdl)),
                ("Z6 HOMA-IR", FormatZ(health.Z6_HomaIr)),
                ("Z7 TG/HDL", FormatZ(health.Z7_TgHdl)),
                ("Z8 FIB-4", FormatZ(health.Z8_Fib4))
            ));

            body.Append(Spacer());

            // ---- Performance Age ----
            body.Append(SectionHeader("Performance Age"));
            body.Append(ParagraphOf($"Performance Age (years): {performance.PerformanceAge:F1}", bold: true));
            body.Append(ParagraphOf($"Δ vs Chronological (years): {performance.DeltaVsAgeYears:F1}"));
            body.Append(ParagraphOf($"Δ vs Chronological (%): {performance.DeltaVsAgePercent:P1}"));
            body.Append(ParagraphOf($"Contribution (scaled): {performance.ContributionYearsScaled:F2} years"));

            body.Append(Spacer());

            body.Append(SubHeader("Performance Age Breakdown (percent-of-age)"));
            body.Append(BuildTwoColumnTable(
                ("Z1 VO2max", FormatZ(performance.Z1_Vo2Max)),
                ("Z2 Quadriceps Strength", FormatZ(performance.Z2_Quadriceps)),
                ("Z3 Grip Strength", FormatZ(performance.Z3_Grip)),
                ("Z4 Gait Speed (Comfortable)", FormatZ(performance.Z4_GaitComfortable)),
                ("Z5 Gait Speed (Max)", FormatZ(performance.Z5_GaitMax)),
                ("Z6 Power", FormatZ(performance.Z6_Power)),
                ("Z7 Balance", FormatZ(performance.Z7_Balance)),
                ("Z8 Chair Rise", FormatZ(performance.Z8_ChairRise))
            ));

            body.Append(Spacer());

            // ---- Brain Health ----
            body.Append(SectionHeader("Brain Health"));
            body.Append(ParagraphOf($"Brain Health Score: {brain.TotalScore:F1}", bold: true));
            body.Append(ParagraphOf($"Brain Health Level: {brain.Level}", bold: true));

            body.Append(Spacer());

            body.Append(SubHeader("Brain Health Breakdown (points)"));
            body.Append(BuildTwoColumnTable(
                ("Cognitive", brain.CognitivePoints.ToString("F1")),
                ("Depression", brain.DepressionPoints.ToString("F1")),
                ("Anxiety", brain.AnxietyPoints.ToString("F1")),
                ("Resilience", brain.ResiliencePoints.ToString("F1")),
                ("Optimism", brain.OptimismPoints.ToString("F1")),
                ("Meaning", brain.MeaningPoints.ToString("F1")),
                ("Flourishing", brain.FlourishingPoints.ToString("F1")),
                ("Sleep", brain.SleepPoints?.ToString("F1") ?? "N/A"),
                ("Stress", brain.StressPoints?.ToString("F1") ?? "N/A")
            ));

            body.Append(Spacer());

            // Notes
            body.Append(SectionHeader("Notes"));
            body.Append(ParagraphOf("This report is generated from the provided inputs and scoring algorithms implemented in ReportAPI."));

            mainPart.Document.Save();
        }

        return ms.ToArray();
    }

    private static string FormatZ(object? zDetail)
    {
        if (zDetail is null) return "N/A";

        // Both HealthAge.ZDetail and PerformanceAge.ZDetail have:
        // PercentOfAge, ContributionYears
        // We'll use reflection to avoid duplicating formatters.
        var t = zDetail.GetType();
        var pctProp = t.GetProperty("PercentOfAge");
        var yrsProp = t.GetProperty("ContributionYears");
        if (pctProp is null || yrsProp is null) return "N/A";

        var pct = (double)(pctProp.GetValue(zDetail) ?? 0.0);
        var yrs = (double)(yrsProp.GetValue(zDetail) ?? 0.0);

        return $"{pct:P1} ({yrs:F2} yrs)";
    }

    private static Paragraph SectionHeader(string text)
        => ParagraphOf(text, bold: true, fontSizeHalfPoints: 32);

    private static Paragraph SubHeader(string text)
        => ParagraphOf(text, bold: true, fontSizeHalfPoints: 28);

    private static Paragraph Spacer()
        => new Paragraph(new Run(new Break()));

    private static Paragraph ParagraphOf(string text, bool bold = false, bool italic = false, int fontSizeHalfPoints = 24)
    {
        var runProps = new RunProperties();
        if (bold) runProps.Append(new Bold());
        if (italic) runProps.Append(new Italic());
        runProps.Append(new FontSize { Val = fontSizeHalfPoints.ToString() });

        var run = new Run(runProps, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return new Paragraph(run);
    }

    private static Table BuildTwoColumnTable(params (string label, string value)[] rows)
    {
        var table = new Table();

        table.AppendChild(new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 6 },
                new BottomBorder { Val = BorderValues.Single, Size = 6 },
                new LeftBorder { Val = BorderValues.Single, Size = 6 },
                new RightBorder { Val = BorderValues.Single, Size = 6 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 6 },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 6 }
            )
        ));

        foreach (var (label, value) in rows)
        {
            var tr = new TableRow();
            tr.Append(Cell(label, bold: true), Cell(value));
            table.Append(tr);
        }

        return table;
    }

    private static TableCell Cell(string text, bool bold = false)
    {
        var runProps = new RunProperties();
        if (bold) runProps.Append(new Bold());

        var p = new Paragraph(new Run(runProps, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        return new TableCell(p);
    }
}
