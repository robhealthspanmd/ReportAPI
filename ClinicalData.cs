public static class ClinicalData
{
    public sealed record Inputs(
        string? CancerScreeningRecommendations,
        string? VaccinationRecommendations,
        string? ThyroidResults,
        string? HormoneResults,
        string? Albuminuria,
        string? Cbc,
        string? BoneDensity,
        string? VisionScreen,
        string? HearingScreen,
        string? DentalHygieneStatus
    );
}
