public static class ToxinsLifestyle
{
    public sealed record Inputs(
        string? AlcoholIntake,
        string? Smoking,
        string? ChewingTobacco,
        string? Vaping,
        string? CannabisUse,
        string? ScreenTime,
        string? UltraProcessedFoodIntake
    );
}
