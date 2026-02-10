using System;
using UnityEngine;

namespace Windy
{
    // Simple procedural wind generator used by the Wind class.
    // Beginner-friendly, deterministic if a seed is supplied.
    public class Forecasts
    {
        public struct ForecastData
        {
            public float windSpeed;
            public float windDirection;
            public float altitude;
            public string description;
        }

        // Internal seeds
        private static float seedTime;
        private static float seedAlt;
        private static float seedDir;

        // Initialize with a random seed (called by older code paths)
        public static void Initialize()
        {
            // pick random seeds so runs are not identical
            seedTime = UnityEngine.Random.Range(0f, 10000f);
            seedAlt  = UnityEngine.Random.Range(0f, 10000f);
            seedDir  = UnityEngine.Random.Range(0f, 10000f);
        }

        // Initialize deterministically from an integer seed (for save-file consistency)
        public static void InitializeFromSeed(int seed)
        {
            System.Random rnd = new System.Random(seed);
            seedTime = (float)(rnd.NextDouble() * 10000.0);
            seedAlt  = (float)(rnd.NextDouble() * 10000.0);
            seedDir  = (float)(rnd.NextDouble() * 10000.0);
        }

        public static ForecastData GetCurrentWind(double altitude, double currentTime)
        {
            return CalculateWind(altitude, currentTime);
        }

        public static ForecastData GetForecast(double altitude, double currentTime, float minutesAhead)
        {
            double futureTime = currentTime + (minutesAhead * 60.0);
            return CalculateWind(altitude, futureTime);
        }

        // Simple fBm using Perlin noise to make wind feel natural
        private static float FBmNoise(float x, float y, int octaves)
        {
            float amp = 1f;
            float freq = 1f;
            float sum = 0f;
            float max = 0f;

            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Mathf.PerlinNoise(x * freq, y * freq);
                max += amp;
                amp *= 0.5f;
                freq *= 2f;
            }

            if (max == 0f) return 0f;
            return sum / max;
        }

        private static ForecastData CalculateWind(double altitude, double time)
        {
            // Respect the user's max wind setting
            float userMax = GameDifficulty.GetMaxWindSpeed();
            if (userMax <= 1f) userMax = 25f;

            // Tunable constants
            const float BaseMean = 3.0f;
            const float BaseVar = 10.0f;
            const float TimeScale = 0.008f;
            const float AltScale = 0.0006f;
            const int Octaves = 4;

            float t = (float)time;
            float altF = (float)altitude;

            float noise = FBmNoise(seedTime + t * TimeScale, seedAlt + altF * AltScale, Octaves);
            float rawSpeed = BaseMean + (noise * BaseVar);

            float shearFactor = 1f + (altF / 5000f);
            float speedAfterShear = rawSpeed * shearFactor;

            float finalSpeed = Mathf.Clamp(speedAfterShear, 0.0f, userMax);

            float dirNoise = FBmNoise(seedDir + t * (TimeScale * 0.9f), seedAlt + altF * (AltScale * 0.5f), 3);
            float direction = Mathf.Repeat(dirNoise * 360f, 360f);

            string desc = "Stable";
            if (finalSpeed > userMax * 0.8f) desc = "Strong";
            else if (finalSpeed > userMax * 0.45f) desc = "Breezy";

            ForecastData d;
            d.windSpeed = finalSpeed;
            d.windDirection = direction;
            d.altitude = altF;
            d.description = desc;
            return d;
        }
    }
}