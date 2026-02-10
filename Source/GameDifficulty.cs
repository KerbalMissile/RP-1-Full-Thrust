using System;
using System.Reflection;
using UnityEngine;

// Beginner-friendly GameDifficulty wrapper for Windy.
// Keeps settings in the Difficulty screen so users can tweak wind behavior.
// Simple, well-commented, old-compiler friendly C#.

namespace Windy
{
    public class GameDifficulty : GameParameters.CustomParameterNode
    {
        public override string Title { get { return "Windy"; } }
        public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }
        public override string Section { get { return "Windy"; } }
        public override int SectionOrder { get { return 1; } }
        public override string DisplaySection { get { return Section; } }
        public override bool HasPresets { get { return false; } }

        // --- User-facing settings ---

        // Turn all wind effects on/off
        [GameParameters.CustomParameterUI("Enable Wind", toolTip = "Turn all Windy effects on or off.", autoPersistance = true)]
        public bool windEnabled = true;

        // Max wind speed in m/s
        [GameParameters.CustomIntParameterUI("Max Wind Speed (m/s)", toolTip = "Maximum wind speed.", minValue = 0, maxValue = 100, stepSize = 5, autoPersistance = true)]
        public int maxWindSpeed = 25;

        // Allow headwind to add lift to wings
        [GameParameters.CustomParameterUI("Enable Headwind Lift", toolTip = "Headwinds increase lift on wings.", autoPersistance = true)]
        public bool enableHeadwindLift = true;

        // How much extra lift (percent)
        [GameParameters.CustomIntParameterUI("Headwind Lift (%)", toolTip = "Multiplier for extra lift.", minValue = 0, maxValue = 500, stepSize = 10, autoPersistance = true)]
        public int headwindLiftPercent = 150;

        // Toggle for jet streams (work-in-progress)
        [GameParameters.CustomParameterUI("Enable Jet Streams (WIP)", toolTip = "Work in progress.", autoPersistance = true)]
        public bool enableJetStreams = true;

        // Wind shear toggle
        [GameParameters.CustomParameterUI("Enable Wind Shear", toolTip = "Apply vertical wind shear effects.", autoPersistance = true)]
        public bool enableWindShear = true;

        // Gusts toggle
        [GameParameters.CustomParameterUI("Enable Gusts", toolTip = "Enable stochastic gusts (short-timescale turbulence).", autoPersistance = true)]
        public bool enableGusts = true;

        // Hide other settings when the main wind toggle is off.
        public override bool Enabled(MemberInfo member, GameParameters parameters)
        {
            if (member.Name == "windEnabled") return true;
            return windEnabled;
        }

        // Helper to fetch the settings object from the current save.
        public static GameDifficulty GetSettings()
        {
            if (HighLogic.CurrentGame == null) return null;
            return HighLogic.CurrentGame.Parameters.CustomParams<GameDifficulty>();
        }

        // Public helpers used by other classes (old-compiler friendly)
        public static bool IsWindEnabled()
        {
            GameDifficulty settings = GetSettings();
            if (settings == null) return false;
            return settings.windEnabled;
        }

        public static float GetMaxWindSpeed()
        {
            GameDifficulty settings = GetSettings();
            if (settings == null) return 25f;
            return (float)settings.maxWindSpeed;
        }

        // Do we apply headwind-based extra lift?
        public static bool AreHeadwindLiftEnabled()
        {
            GameDifficulty settings = GetSettings();
            return settings != null && settings.windEnabled && settings.enableHeadwindLift;
        }

        // Multiplier for headwind lift (1.0 = 100%)
        public static float GetHeadwindLiftMultiplier()
        {
            GameDifficulty settings = GetSettings();
            if (settings == null) return 1.0f;
            return (float)settings.headwindLiftPercent / 100f;
        }

        // Feature toggles
        public static bool AreJetStreamsEnabled()
        {
            GameDifficulty settings = GetSettings();
            return settings != null && settings.windEnabled && settings.enableJetStreams;
        }

        public static bool IsWindShearEnabled()
        {
            GameDifficulty settings = GetSettings();
            return settings != null && settings.windEnabled && settings.enableWindShear;
        }

        public static bool AreGustsEnabled()
        {
            GameDifficulty settings = GetSettings();
            return settings != null && settings.windEnabled && settings.enableGusts;
        }
    }
}