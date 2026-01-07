public static class ClinicalData
{
    public sealed record Inputs(
        bool? PregnancyPotential,
        CancerScreeningInputs? CancerScreening,
        ThyroidInputs? Thyroid,
        SexHormoneInputs? SexHormoneHealth,
        KidneyInputs? Kidney,
        LiverGiInputs? LiverGi,
        BloodHealthInputs? BloodHealth,
        BoneHealthInputs? BoneHealth,
        VaccinationInputs? Vaccinations,
        SupplementsInputs? Supplements
    );

    public sealed record CancerScreeningInputs(
        ScreeningEntry? BreastMammography,
        ScreeningEntry? CervicalPapHpv,
        ScreeningEntry? ColorectalColonoscopy,
        ScreeningEntry? ColorectalFit,
        ScreeningEntry? ColorectalCologuard,
        ScreeningEntry? ProstatePsa,
        ScreeningEntry? LungLowDoseCt,
        ScreeningEntry? SkinDermExam,
        AdvancedScreeningStatus? TotalBodyMri,
        AdvancedScreeningStatus? GeneticTesting,
        AdvancedScreeningStatus? McedBloodTest,
        bool? WantsAdvancedScreening,
        bool? DiscussAdvancedOptions
    );

    public sealed record ScreeningEntry(
        string? LastCompletedDate,
        string? ResultStatus,
        string? NextDueDate,
        string[]? EligibilityFlags
    );

    public sealed record AdvancedScreeningStatus(
        string? Status,
        string? LastDiscussedDate
    );

    public sealed record ThyroidInputs(
        double? TshValue,
        string? TshDate,
        double? FreeT4Value,
        string? FreeT4Date,
        double? FreeT3Value,
        string? FreeT3Date,
        string? ThyroidMedicationStatus
    );

    public sealed record SexHormoneInputs(
        string? MenopausalStatus,
        double? TotalTestosterone,
        string? TotalTestosteroneDate,
        double? FreeTestosterone,
        string? FreeTestosteroneDate,
        double? Shbg,
        string? ShbgDate,
        double? Estradiol,
        string? EstradiolDate,
        double? Progesterone,
        string? ProgesteroneDate,
        string[]? SymptomFlags,
        string? HormoneTherapyStatus
    );

    public sealed record KidneyInputs(
        double? EgfrValue,
        string? EgfrDate,
        double? UacrValueMgG,
        string? UacrDate,
        double? CystatinCValue,
        string? CystatinCDate
    );

    public sealed record LiverGiInputs(
        string? HepatitisScreeningStatus
    );

    public sealed record BloodHealthInputs(
        double? HemoglobinValue,
        string? HemoglobinDate,
        double? FerritinValue,
        string? FerritinDate,
        double? B12Value,
        string? B12Date,
        double? FolateValue,
        string? FolateDate
    );

    public sealed record BoneHealthInputs(
        double? DEXATScore,
        string? DEXADate,
        string? DEXASite,
        bool? FractureHistory,
        string? MenopauseStatus
    );

    public sealed record VaccinationInputs(
        string? VaccinationStatus,
        string[]? MissingVaccines,
        string[]? NextDueVaccines
    );

    public sealed record SupplementsInputs(
        bool? TakesSupplements,
        bool? SupplementsThirdPartyTested,
        string? SupplementsRecommendedBy
    );
}
