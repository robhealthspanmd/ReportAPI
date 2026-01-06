public static class ToxinsLifestyle
{
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
