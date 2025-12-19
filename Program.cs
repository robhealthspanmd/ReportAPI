using System;
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

app.MapPost("/api/phenoage/report.docx", (PhenoAge.Inputs input) =>
{
    var result = PhenoAge.Calculate(input);
    var bytes = WordReportBuilder.BuildPhenoAgeReport(input, result);

    var filename = $"PhenoAge_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.docx";

    return Results.File(
        fileContents: bytes,
        contentType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        fileDownloadName: filename
    );
});

app.Run();
