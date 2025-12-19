public sealed record ReportRequest(
    PhenoAge.Inputs PhenoAge,
    HealthAge.Inputs HealthAge,
    PerformanceAge.Inputs PerformanceAge,
    BrainHealth.Inputs BrainHealth
);
