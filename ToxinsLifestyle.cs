public static class ToxinsLifestyle
{
    // NOTE: Numeric-only lab inputs for blood lead/mercury (no object wrapper).
    public sealed record Inputs(
        string? AlcoholIntake,
        string? AlcoholDrinksPerWeek,
        string? Smoking,
        string? ChewingTobacco,
        string? Vaping,
        string? OtherNicotineUse,
        string? CannabisUse,
        string? ScreenTime,
        string? UltraProcessedFoodIntake,
        string? MedicationsOrSupplementsImpact,
        string? PhysicalEnvironmentImpact,
        string? MediaExposureImpact,
        string? StressfulEnvironmentsOrRelationshipsImpact,
        double? BloodLeadLevel,
        double? BloodMercury
    );
}
