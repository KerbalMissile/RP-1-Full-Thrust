using System;
using UnityEngine;
using KSP;

namespace Windy
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class WindShear : MonoBehaviour
    {
        // tiny tunables you can change
        private const float SampleAlt = 200f;      // meters
        private const float ShearCoeff = 0.6f;     // strength
        private const float MaxShearAccel = 4.0f;  // max m/s^2
        private const float TorqueMult = 0.45f;    // roll/pitch effect
        private const float MinWindThreshold = 2.0f; // ignore tiny winds
        private const float Gustiness = 0.25f;     // random jitter

        void FixedUpdate()
        {
            try
            {
                // only run if enabled
                var settings = GameDifficulty.GetSettings();
                if (settings == null || !settings.windEnabled) return;
                if (!settings.enableWindShear) return;
                if (Wind.Instance == null) return;

                Vessel vessel = FlightGlobals.ActiveVessel;
                if (vessel == null) return;
                if (vessel.mainBody == null) return;
                if (vessel.altitude >= vessel.mainBody.atmosphereDepth) return;

                Part root = vessel.rootPart;
                if (root == null || root.rb == null) return;

                float currentWind = Wind.Instance.CurrentWindSpeed;
                if (currentWind < MinWindThreshold) return; // too weak

                double ut = Planetarium.GetUniversalTime();
                
                // Get forecast data (structs are never null)
                Forecasts.ForecastData baseWind = Forecasts.GetCurrentWind(vessel.altitude, ut);
                Forecasts.ForecastData upperWind = Forecasts.GetCurrentWind(vessel.altitude + SampleAlt, ut);

                float deltaRaw = (float)(upperWind.windSpeed - baseWind.windSpeed);
                if (Mathf.Abs(deltaRaw) < 0.25f) return; // tiny shear

                float vesselScale = vessel.vesselSize.magnitude;
                float massKg = vessel.GetTotalMass() * 1000f;
                if (massKg <= 0f) return;

                // basic accel calc
                float accel = deltaRaw * ShearCoeff * vesselScale / Mathf.Max(1f, massKg);

                // add some jitter
                float rand = (UnityEngine.Random.value - 0.5f) * 2f * Gustiness;
                accel = accel * (1f + rand);

                // clamp
                accel = Mathf.Clamp(accel, -MaxShearAccel, MaxShearAccel);

                // wind direction (heading is FROM)
                float heading = Wind.Instance.CurrentWindHeading;
                float rad = heading * Mathf.Deg2Rad;
                Vector3 windDir = new Vector3(-Mathf.Sin(rad), 0f, -Mathf.Cos(rad));
                windDir = windDir.normalized;

                // apply lateral accel
                root.rb.AddForce(windDir * accel, ForceMode.Acceleration);

                // small random torque
                Vector3 torque = new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f));
                torque = torque.normalized * Mathf.Abs(accel) * TorqueMult;
                root.rb.AddTorque(torque, ForceMode.Acceleration);
            }
            catch (Exception ex)
            {
                Debug.Log("[Windy] WindShear error: " + ex.Message);
            }
        }
    }
}