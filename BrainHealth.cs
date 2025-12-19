using System;

public static class BrainHealth
{
    public sealed record Inputs(
        double CognitiveFunction,          // B4
        double PromisDepression_8a,         // B5 (range 8–40)
        double PromisAnxiety_8a,            // B6 (range 8–40)
        double BriefResilienceScale,        // B7 (range 6–30)
        double LifeOrientationTest_R,       // B8 (range 0–24)
        double MeaningInLifeQuestionnaire,  // B9 (range 10–70)
        double FlourishingScale,            // B10 (range 8–56)
        double? PromisSleepDisturbance,     // B11 (T-score style buckets)
        double? PerceivedStressScore        // B12 (0–40 typical)
    );

    public sealed record Result(
        double CognitivePoints,
        double DepressionPoints,
        double AnxietyPoints,
        double ResiliencePoints,
        double OptimismPoints,
        double MeaningPoints,
        double FlourishingPoints,
        double? SleepPoints,
        double? StressPoints,
        double TotalScore,
        string Level
    );

    public static Result Calculate(Inputs x)
    {
        // Clamp helper matches Excel MAX(0, MIN(100, ...))
        static double Clamp01(double v) => v < 0 ? 0 : (v > 100 ? 100 : v);

        // Exact Excel formulas
        double cognitive = Clamp01(x.CognitiveFunction * 0.3);

        double depression = Clamp01(((40.0 - x.PromisDepression_8a) / 32.0) * 100.0 * 0.1);
        double anxiety    = Clamp01(((40.0 - x.PromisAnxiety_8a)    / 32.0) * 100.0 * 0.075);

        double resilience = Clamp01(((x.BriefResilienceScale - 6.0) / 24.0) * 100.0 * 0.075);
        double optimism   = Clamp01((x.LifeOrientationTest_R / 24.0)        * 100.0 * 0.075);

        double meaning     = Clamp01(((x.MeaningInLifeQuestionnaire - 10.0) / 60.0) * 100.0 * 0.075);
        double flourishing = Clamp01(((x.FlourishingScale - 8.0)            / 48.0) * 100.0 * 0.1);

        double? sleep = x.PromisSleepDisturbance is null ? null :
            (x.PromisSleepDisturbance <= 55 ? 10 :
             x.PromisSleepDisturbance < 60  ? 9  :
             x.PromisSleepDisturbance < 70  ? 4  : 2);

        double? stress = x.PerceivedStressScore is null ? null :
            (x.PerceivedStressScore <= 13 ? 10 :
             x.PerceivedStressScore <= 26 ? 7  : 3);

        // Excel SUM ignores blanks; in C# treat null as 0
        double total =
            cognitive + depression + anxiety + resilience + optimism + meaning + flourishing +
            (sleep ?? 0) + (stress ?? 0);

        string level =
            total >= 85 ? "Optimal" :
            total >= 70 ? "Healthy" :
            "Needs Attention";

        return new Result(
            CognitivePoints: cognitive,
            DepressionPoints: depression,
            AnxietyPoints: anxiety,
            ResiliencePoints: resilience,
            OptimismPoints: optimism,
            MeaningPoints: meaning,
            FlourishingPoints: flourishing,
            SleepPoints: sleep,
            StressPoints: stress,
            TotalScore: total,
            Level: level
        );
    }
}
