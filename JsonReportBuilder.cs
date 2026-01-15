using System;
using System.Text;
using System.Text.Json;

/// <summary>
/// Builds the full report as a single JSON document (UTF-8 bytes).
/// Intended to be the canonical "source of truth" for any downstream renderer (web, PDF, etc.).
/// </summary>
public static class JsonReportBuilder
{
    private static object BuildTest(string test, object? result, string? reference = null, string? status = null)
    {
        return new
        {
            test,
            result,
            reference,
            status
        };
    }

    public static byte[] BuildFullReportJson(
        ReportRequest req,
        PhenoAge.Result pheno,
        HealthAge.Result health,
        PerformanceAge.Result performance,
        BrainHealth.Result brain,
        Cardiology.Result? cardio,
        string improvementParagraph,
        string cardiologyInterpretationParagraph,
        string fitnessMobilityAssessmentParagraph,
        string strengthStabilityAssessmentParagraph,
        AiInsights.MetabolicHealthAiResult? metabolicAi,
        object? metabolicAiInput = null,
        AiInsights.ClinicalPreventiveChecklistResult? clinicalPreventiveChecklist = null,
        object? clinicalPreventiveChecklistInput = null,
        AiInsights.ProtectYourBrainResult? protectYourBrain = null,
        object? protectYourBrainInput = null,
        AiInsights.MentallyEmotionallyWellResult? mentallyEmotionallyWell = null,
        object? mentallyEmotionallyWellInput = null,
        BeConnected.Result? beConnected = null,
        LongevityMindset.Result? longevityMindset = null
    )
    {
        // Keep output stable + frontend-friendly.
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var toxinsAssessment = ToxinsLifestyle.Evaluate(req.ToxinsLifestyle, req.BrainHealth.PerceivedStressScore);

        var cardiologyTests = new[]
        {
            BuildTest("CAC Score", req.Cardiology?.CacScore),
            BuildTest("CAC Percentile", req.Cardiology?.CacPercentile),
            BuildTest("Carotid Plaque Severity", req.Cardiology?.CarotidPlaqueSeverity),
            BuildTest("Coronary Plaque Severity", req.Cardiology?.CoronaryPlaqueSeverity),
            BuildTest("CTA Max Stenosis Percent", req.Cardiology?.CtaMaxStenosisPercent),
            BuildTest("CTA Overall Result", req.Cardiology?.CtaOverallResult),
            BuildTest("Treadmill Overall Result", req.Cardiology?.TreadmillOverallResult),
            BuildTest("Echo Overall Result", req.Cardiology?.EchoOverallResult),
            BuildTest("Echo Details", req.Cardiology?.EchoDetails),
            BuildTest("Ejection Fraction Percent", req.Cardiology?.EjectionFractionPercent),
            BuildTest("Heart Structure Severity", req.Cardiology?.HeartStructureSeverity),
            BuildTest("Duke Treadmill Score", req.Cardiology?.DukeTreadmillScore),
            BuildTest("ECG Severity", req.Cardiology?.EcgSeverity),
            BuildTest("ECG Details", req.Cardiology?.EcgDetails),
            BuildTest("Abdominal Aorta Screening", req.Cardiology?.AbdominalAortaScreening),
            BuildTest("ETT Interpretation", req.Cardiology?.EttInterpretation),
            BuildTest("ETT FAC", req.Cardiology?.EttFac),
            BuildTest("CTA Plaque Quantification", req.Cardiology?.CtaPlaqueQuantification),
            BuildTest("CTA Plaqu Quantification", req.Cardiology?.CtaPlaquQuantification),
            BuildTest("CTA Soft Plaque", req.Cardiology?.CtaSoftPlaque),
            BuildTest("CTA Calcified Plaque", req.Cardiology?.CtaCalcifiedPlaque),
            BuildTest("Hard Soft Plaque Ratio", req.Cardiology?.HardSoftPlaqueRatio),
            BuildTest("Has Clinical ASCVD History", req.Cardiology?.HasClinicalAscVDHistory),
            BuildTest("Clinical ASCVD History Details", req.Cardiology?.ClinicalAscVDHistoryDetails),
            BuildTest("Has Family History Premature ASCVD", req.Cardiology?.HasFamilyHistoryPrematureAscVD),
            BuildTest("Family History Premature ASCVD Details", req.Cardiology?.FamilyHistoryPrematureAscVDDetails),
            BuildTest("Specific Cardiology Instructions", req.Cardiology?.SpecificCardiologyInstructions),
            BuildTest("Lipoprotein A", req.Cardiology?.Lipoproteina),
            BuildTest("ApoB", req.Cardiology?.ApoB),
            BuildTest("Modifiable Heart Health Score", req.Cardiology?.ModifiableHeartHealthScore),
            BuildTest("Blood Pressure Systolic", req.HealthAge.SystolicBP),
            BuildTest("Blood Pressure Diastolic", req.HealthAge.DiastolicBP),
            BuildTest("Non HDL", req.HealthAge.NonHdlMgDl),
            BuildTest("Non HDL Risk Group", req.HealthAge.NonHdlRiskGroup),
            BuildTest("Total Cholesterol", req.HealthAge.TotalCholesterol),
            BuildTest("Triglycerides", req.HealthAge.Triglycerides_mg_dL),
            BuildTest("HDL", req.HealthAge.Hdl_mg_dL),
            BuildTest("hs CRP", req.PhenoAge.CRP_mg_L)
        };

        double? leanToFatMassRatio =
            (req.HealthAge.TotalLeanMass is not null &&
             req.HealthAge.TotalFatMass is not null &&
             req.HealthAge.TotalFatMass != 0)
                ? req.HealthAge.TotalLeanMass / req.HealthAge.TotalFatMass
                : (double?)null;

        var metabolicTests = new[]
        {
            BuildTest("Sex", req.HealthAge.Sex),
            BuildTest("Body Fat Percentile", req.HealthAge.BodyFatPercentile),
            BuildTest("Visceral Fat Percentile", req.HealthAge.VisceralFatPercentile),
            BuildTest("Appendicular Muscle Percentile", req.HealthAge.AppendicularMusclePercentile),
            BuildTest("Lean To Fat Mass Ratio", leanToFatMassRatio),
            BuildTest("Fasting Insulin", req.HealthAge.FastingInsulin_uIU_mL),
            BuildTest("Fasting Glucose", req.HealthAge.FastingGlucose_mg_dL),
            BuildTest("Hemoglobin A1c", req.HealthAge.HemoglobinA1c),
            BuildTest("Triglycerides", req.HealthAge.Triglycerides_mg_dL),
            BuildTest("HDL", req.HealthAge.Hdl_mg_dL),
            BuildTest("Triglycerides HDL Ratio", req.HealthAge.TriglyceridesHdlRatio),
            BuildTest("HOMA IR", req.HealthAge.HomaIr),
            BuildTest("FIB 4 Score", req.HealthAge.Fib4Score),
            BuildTest("AST", req.HealthAge.Ast),
            BuildTest("ALT", req.HealthAge.Alt),
            BuildTest("Platelets", req.HealthAge.Platelets),
            BuildTest("Body Fat Percentage", req.HealthAge.BodyFatPercentage),
            BuildTest("Total Fat Mass", req.HealthAge.TotalFatMass),
            BuildTest("Total Fat Mass Per Height", req.HealthAge.TotalFatMassPerHeight),
            BuildTest("Visceral Fat Mass", req.HealthAge.VisceralFatMass),
            BuildTest("Total Lean Mass", req.HealthAge.TotalLeanMass),
            BuildTest("Total Lean Mass Per Height", req.HealthAge.TotalLeanMassPerHeight),
            BuildTest("Appendicular Lean Mass", req.HealthAge.AppendicularLeanMass)
        };

        var clinicalTests = new[]
        {
            BuildTest("Albumin", req.PhenoAge.Albumin_g_dL),
            BuildTest("Creatinine", req.PhenoAge.Creatinine_mg_dL),
            BuildTest("Glucose", req.PhenoAge.Glucose_mg_dL),
            BuildTest("CRP", req.PhenoAge.CRP_mg_L),
            BuildTest("Lymphocyte Percent", req.PhenoAge.LymphocytePercent),
            BuildTest("MCV", req.PhenoAge.MCV_fL),
            BuildTest("RDW Percent", req.PhenoAge.RDW_percent),
            BuildTest("Alkaline Phosphatase", req.PhenoAge.AlkalinePhosphatase_U_L),
            BuildTest("WBC", req.PhenoAge.WBC_10e3_per_uL),
            BuildTest("Pregnancy Potential", req.ClinicalData.PregnancyPotential),
            BuildTest("Cancer Screening Breast Mammography", req.ClinicalData.CancerScreening?.BreastMammography),
            BuildTest("Cancer Screening Cervical Pap HPV", req.ClinicalData.CancerScreening?.CervicalPapHpv),
            BuildTest("Cancer Screening Colorectal Colonoscopy", req.ClinicalData.CancerScreening?.ColorectalColonoscopy),
            BuildTest("Cancer Screening Colorectal FIT", req.ClinicalData.CancerScreening?.ColorectalFit),
            BuildTest("Cancer Screening Colorectal Cologuard", req.ClinicalData.CancerScreening?.ColorectalCologuard),
            BuildTest("Cancer Screening Prostate PSA", req.ClinicalData.CancerScreening?.ProstatePsa),
            BuildTest("Cancer Screening Lung Low Dose CT", req.ClinicalData.CancerScreening?.LungLowDoseCt),
            BuildTest("Cancer Screening Skin Derm Exam", req.ClinicalData.CancerScreening?.SkinDermExam),
            BuildTest("Cancer Screening Total Body MRI", req.ClinicalData.CancerScreening?.TotalBodyMri),
            BuildTest("Cancer Screening Genetic Testing", req.ClinicalData.CancerScreening?.GeneticTesting),
            BuildTest("Cancer Screening MCED Blood Test", req.ClinicalData.CancerScreening?.McedBloodTest),
            BuildTest("Cancer Screening Wants Advanced Screening", req.ClinicalData.CancerScreening?.WantsAdvancedScreening),
            BuildTest("Cancer Screening Discuss Advanced Options", req.ClinicalData.CancerScreening?.DiscussAdvancedOptions),
            BuildTest("Thyroid TSH Value", req.ClinicalData.Thyroid?.TshValue),
            BuildTest("Thyroid TSH Date", req.ClinicalData.Thyroid?.TshDate),
            BuildTest("Thyroid Free T4 Value", req.ClinicalData.Thyroid?.FreeT4Value),
            BuildTest("Thyroid Free T4 Date", req.ClinicalData.Thyroid?.FreeT4Date),
            BuildTest("Thyroid Free T3 Value", req.ClinicalData.Thyroid?.FreeT3Value),
            BuildTest("Thyroid Free T3 Date", req.ClinicalData.Thyroid?.FreeT3Date),
            BuildTest("Thyroid Medication Status", req.ClinicalData.Thyroid?.ThyroidMedicationStatus),
            BuildTest("Sex Hormone Menopausal Status", req.ClinicalData.SexHormoneHealth?.MenopausalStatus),
            BuildTest("Sex Hormone Total Testosterone", req.ClinicalData.SexHormoneHealth?.TotalTestosterone),
            BuildTest("Sex Hormone Total Testosterone Date", req.ClinicalData.SexHormoneHealth?.TotalTestosteroneDate),
            BuildTest("Sex Hormone Free Testosterone", req.ClinicalData.SexHormoneHealth?.FreeTestosterone),
            BuildTest("Sex Hormone Free Testosterone Date", req.ClinicalData.SexHormoneHealth?.FreeTestosteroneDate),
            BuildTest("Sex Hormone SHBG", req.ClinicalData.SexHormoneHealth?.Shbg),
            BuildTest("Sex Hormone SHBG Date", req.ClinicalData.SexHormoneHealth?.ShbgDate),
            BuildTest("Sex Hormone Estradiol", req.ClinicalData.SexHormoneHealth?.Estradiol),
            BuildTest("Sex Hormone Estradiol Date", req.ClinicalData.SexHormoneHealth?.EstradiolDate),
            BuildTest("Sex Hormone Progesterone", req.ClinicalData.SexHormoneHealth?.Progesterone),
            BuildTest("Sex Hormone Progesterone Date", req.ClinicalData.SexHormoneHealth?.ProgesteroneDate),
            BuildTest("Sex Hormone Symptom Flags", req.ClinicalData.SexHormoneHealth?.SymptomFlags),
            BuildTest("Sex Hormone Therapy Status", req.ClinicalData.SexHormoneHealth?.HormoneTherapyStatus),
            BuildTest("Kidney eGFR Value", req.ClinicalData.Kidney?.EgfrValue),
            BuildTest("Kidney eGFR Date", req.ClinicalData.Kidney?.EgfrDate),
            BuildTest("Kidney UACR Value", req.ClinicalData.Kidney?.UacrValueMgG),
            BuildTest("Kidney UACR Date", req.ClinicalData.Kidney?.UacrDate),
            BuildTest("Kidney Cystatin C Value", req.ClinicalData.Kidney?.CystatinCValue),
            BuildTest("Kidney Cystatin C Date", req.ClinicalData.Kidney?.CystatinCDate),
            BuildTest("Liver GI Hepatitis Screening Status", req.ClinicalData.LiverGi?.HepatitisScreeningStatus),
            BuildTest("Blood Health Hemoglobin Value", req.ClinicalData.BloodHealth?.HemoglobinValue),
            BuildTest("Blood Health Hemoglobin Date", req.ClinicalData.BloodHealth?.HemoglobinDate),
            BuildTest("Blood Health Ferritin Value", req.ClinicalData.BloodHealth?.FerritinValue),
            BuildTest("Blood Health Ferritin Date", req.ClinicalData.BloodHealth?.FerritinDate),
            BuildTest("Blood Health B12 Value", req.ClinicalData.BloodHealth?.B12Value),
            BuildTest("Blood Health B12 Date", req.ClinicalData.BloodHealth?.B12Date),
            BuildTest("Blood Health Folate Value", req.ClinicalData.BloodHealth?.FolateValue),
            BuildTest("Blood Health Folate Date", req.ClinicalData.BloodHealth?.FolateDate),
            BuildTest("Bone Health DEXA T Score", req.ClinicalData.BoneHealth?.DEXATScore),
            BuildTest("Bone Health DEXA Date", req.ClinicalData.BoneHealth?.DEXADate),
            BuildTest("Bone Health DEXA Site", req.ClinicalData.BoneHealth?.DEXASite),
            BuildTest("Bone Health Fracture History", req.ClinicalData.BoneHealth?.FractureHistory),
            BuildTest("Bone Health Menopause Status", req.ClinicalData.BoneHealth?.MenopauseStatus),
            BuildTest("Vaccination Status", req.ClinicalData.Vaccinations?.VaccinationStatus),
            BuildTest("Vaccination Missing Vaccines", req.ClinicalData.Vaccinations?.MissingVaccines),
            BuildTest("Vaccination Next Due Vaccines", req.ClinicalData.Vaccinations?.NextDueVaccines),
            BuildTest("Supplements Takes Supplements", req.ClinicalData.Supplements?.TakesSupplements),
            BuildTest("Supplements Third Party Tested", req.ClinicalData.Supplements?.SupplementsThirdPartyTested),
            BuildTest("Supplements Recommended By", req.ClinicalData.Supplements?.SupplementsRecommendedBy)
        };

        var toxinsTests = new[]
        {
            BuildTest("Alcohol Intake", req.ToxinsLifestyle.AlcoholIntake),
            BuildTest("Alcohol Drinks Per Week", req.ToxinsLifestyle.AlcoholDrinksPerWeek),
            BuildTest("Smoking", req.ToxinsLifestyle.Smoking),
            BuildTest("Chewing Tobacco", req.ToxinsLifestyle.ChewingTobacco),
            BuildTest("Vaping", req.ToxinsLifestyle.Vaping),
            BuildTest("Other Nicotine Use", req.ToxinsLifestyle.OtherNicotineUse),
            BuildTest("Cannabis Use", req.ToxinsLifestyle.CannabisUse),
            BuildTest("Screen Time", req.ToxinsLifestyle.ScreenTime),
            BuildTest("Ultra Processed Food Intake", req.ToxinsLifestyle.UltraProcessedFoodIntake),
            BuildTest("Medications Or Supplements Impact", req.ToxinsLifestyle.MedicationsOrSupplementsImpact),
            BuildTest("Physical Environment Impact", req.ToxinsLifestyle.PhysicalEnvironmentImpact),
            BuildTest("Media Exposure Impact", req.ToxinsLifestyle.MediaExposureImpact),
            BuildTest("Stressful Environments Or Relationships Impact", req.ToxinsLifestyle.StressfulEnvironmentsOrRelationshipsImpact),
            BuildTest("Blood Lead Level", req.ToxinsLifestyle.BloodLeadLevel),
            BuildTest("Blood Mercury", req.ToxinsLifestyle.BloodMercury)
        };

        var fitnessTests = new[]
        {
            BuildTest("VO2 Max Percentile", req.PerformanceAge.Vo2MaxPercentile),
            BuildTest("Heart Rate Recovery", performance.HeartRateRecovery),
            BuildTest("Gait Speed Comfortable Percentile", req.PerformanceAge.GaitSpeedComfortablePercentile),
            BuildTest("Gait Speed Max Percentile", req.PerformanceAge.GaitSpeedMaxPercentile),
            BuildTest("Trunk Endurance Percentile", performance.TrunkEndurance),
            BuildTest("Posture Tragus To Wall", performance.PostureAssessment),
            BuildTest("Floor To Stand Score", performance.FloorToStandTest),
            BuildTest("Mobility ROM Flags", performance.MobilityRom)
        };

        var strengthTests = new[]
        {
            BuildTest("Quadriceps Strength Percentile", req.PerformanceAge.QuadricepsStrengthPercentile),
            BuildTest("Hip Strength Percentile", performance.HipStrength),
            BuildTest("Calf Strength Percentile", performance.CalfStrength),
            BuildTest("Rotator Cuff Percentile", performance.RotatorCuffIntegrity),
            BuildTest("IMTP Percentile", performance.IsometricThighPullPercentile),
            BuildTest("IMTP Force", performance.IsometricThighPull),
            BuildTest("Grip Strength Percentile", req.PerformanceAge.GripStrengthPercentile),
            BuildTest("Power Percentile", req.PerformanceAge.PowerPercentile),
            BuildTest("Balance Percentile", req.PerformanceAge.BalancePercentile),
            BuildTest("Chair Rise Percentile", req.PerformanceAge.ChairRisePercentile),
            BuildTest("5x Sit-to-Stand", performance.ChairRiseFiveTimes),
            BuildTest("30-Second Sit-to-Stand Count", performance.ChairRiseThirtySeconds)
        };

        var brainTests = new[]
        {
            BuildTest("Cognitive Function", req.BrainHealth.CognitiveFunction),
            BuildTest("Cognitive Function Prior", req.BrainHealth.CognitiveFunctionPrior),
            BuildTest("ApoE4 Status", req.BrainHealth.ApoE4Status),
            BuildTest("Family History Dementia", req.BrainHealth.FamilyHistoryDementia),
            BuildTest("Dementia Onset Age", req.BrainHealth.DementiaOnsetAge)
        };

        var mentalEmotionalTests = new[]
        {
            BuildTest("PROMIS Depression", req.BrainHealth.PromisDepression_8a),
            BuildTest("PROMIS Anxiety", req.BrainHealth.PromisAnxiety_8a),
            BuildTest("PROMIS Sleep Disturbance", req.BrainHealth.PromisSleepDisturbance),
            BuildTest("Perceived Stress Score", req.BrainHealth.PerceivedStressScore),
            BuildTest("Assessment Date", req.BrainHealth.AssessmentDate),
            BuildTest("PROMIS Depression Prior", req.BrainHealth.PromisDepressionPrior),
            BuildTest("PROMIS Anxiety Prior", req.BrainHealth.PromisAnxietyPrior),
            BuildTest("Perceived Stress Score Prior", req.BrainHealth.PerceivedStressScorePrior)
        };

        var socialConnectionTests = new[]
        {
            BuildTest("Flourishing Scale", req.BrainHealth.FlourishingScale),
            BuildTest("Flourishing Scale Prior", req.BrainHealth.FlourishingScalePrior)
        };

        var longevityMindsetTests = new[]
        {
            BuildTest("Brief Resilience Scale", req.BrainHealth.BriefResilienceScale),
            BuildTest("Brief Resilience Scale Prior", req.BrainHealth.BriefResilienceScalePrior),
            BuildTest("Life Orientation Test", req.BrainHealth.LifeOrientationTest_R),
            BuildTest("Life Orientation Test Prior", req.BrainHealth.LifeOrientationTestPrior),
            BuildTest("Meaning In Life Questionnaire", req.BrainHealth.MeaningInLifeQuestionnaire),
            BuildTest("Meaning In Life Presence", req.BrainHealth.MeaningInLifePresence),
            BuildTest("Meaning In Life Search", req.BrainHealth.MeaningInLifeSearch),
            BuildTest("Meaning In Life Presence Prior", req.BrainHealth.MeaningInLifePresencePrior),
            BuildTest("Meaning In Life Search Prior", req.BrainHealth.MeaningInLifeSearchPrior)
        };

        var payload = new
        {
            meta = new
            {
                generatedAtUtc = DateTime.UtcNow,
                schemaVersion = "report-json-v3"
            },
            chronologicalAgeYears = req.PhenoAge.ChronologicalAgeYears,
            healthScores = new
            {
                healthAge = health.HealthAgeFinal,
                brainScore = brain.TotalScore,
                physicalPerformanceScore = performance.PerformanceAge,
                heartScore = cardio?.HeartHealthScore,
                
            },
            pillars = new object[]
            {
                new
                {
                    pillar = "avoiddisease",
                    domains = new object[]
                    {
                        new
                        {
                            domain = "cardiology",
                            tests = cardiologyTests,
                            assessment = new
                            {
                                riskCategory = cardio?.RiskCategory,
                                interpretation = cardiologyInterpretationParagraph ?? string.Empty
                            }
                        },
                        new
                        {
                            domain = "metabolichealth",
                            tests = metabolicTests,
                            assessment = metabolicAi is null
                                ? null
                                : new
                                {
                                    metabolicHealthCategory = metabolicAi.MetabolicHealthCategory,
                                    biggestContributors = metabolicAi.BiggestContributors,
                                    opportunityParagraphs = metabolicAi.OpportunityParagraphs,
                                    notes = metabolicAi.Notes
                                }
                        },
                        new
                        {
                            domain = "clinical",
                            tests = clinicalTests,
                            assessment = clinicalPreventiveChecklist
                        },
                        new
                        {
                            domain = "toxinsandlifestyle",
                            tests = toxinsTests,
                            assessment = new
                            {
                                overallStatus = toxinsAssessment.OverallStatus,
                                summary = toxinsAssessment.Summary,
                                exposures = toxinsAssessment.Exposures,
                                opportunities = toxinsAssessment.Opportunities,
                                stressAmplified = toxinsAssessment.StressAmplified
                            }
                        }
                    }
                },
                new
                {
                    pillar = "strongandindepedent",
                    domains = new object[]
                    {
                        new
                        {
                            domain = "fitnessandmobility",
                            tests = fitnessTests,
                            assessment = fitnessMobilityAssessmentParagraph ?? string.Empty
                        },
                        new
                        {
                            domain = "strengthandstability",
                            tests = strengthTests,
                            assessment = strengthStabilityAssessmentParagraph ?? string.Empty
                        }
                    }
                },
                new
                {
                    pillar = "mentallysharp",
                    domains = new object[]
                    {
                        new
                        {
                            domain = "brainhealth",
                            tests = brainTests,
                            assessment = protectYourBrain
                        },
                        new
                        {
                            domain = "mentalemotionalwellness",
                            tests = mentalEmotionalTests,
                            assessment = mentallyEmotionallyWell
                        },
                        new
                        {
                            domain = "socialconnection",
                            tests = socialConnectionTests,
                            assessment = beConnected
                        },
                        new
                        {
                            domain = "longevitymindset",
                            tests = longevityMindsetTests,
                            assessment = longevityMindset
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(payload, options);
        return Encoding.UTF8.GetBytes(json);
    }
}
