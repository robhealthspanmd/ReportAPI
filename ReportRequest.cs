public sealed record ReportRequest(
    PhenoAge.Inputs PhenoAge,
    HealthAge.Inputs HealthAge,
    PerformanceAge.Inputs PerformanceAge,
    BrainHealth.Inputs BrainHealth,
    Cardiology.Inputs? Cardiology,          // ✅ nullable matches Program.cs
    ClinicalData.Inputs ClinicalData,       // ✅ new
    ToxinsLifestyle.Inputs ToxinsLifestyle  // ✅ new
);