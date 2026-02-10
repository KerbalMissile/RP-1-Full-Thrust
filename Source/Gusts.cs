using System;
using UnityEngine;
using KSP;

namespace Windy
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class Gusts : MonoBehaviour
    {
        private const float GustFrequency = 0.15f; // Chance per second
        private const float GustDurationMin = 2.0f;
        private const float GustDurationMax = 6.0f;
        private const float GustStrengthMult = 1.8f;

        private bool isGusting = false;
        private float gustTimer = 0f;
        private float currentGustFactor = 1.0f;

        void FixedUpdate()
        {
            if (!GameDifficulty.AreGustsEnabled()) return;

            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || v.mainBody == null || v.altitude > v.mainBody.atmosphereDepth) return;

            if (!isGusting)
            {
                if (UnityEngine.Random.value < GustFrequency * Time.fixedDeltaTime)
                {
                    isGusting = true;
                    gustTimer = UnityEngine.Random.Range(GustDurationMin, GustDurationMax);
                }
            }
            else
            {
                gustTimer -= Time.fixedDeltaTime;
                if (gustTimer <= 0)
                {
                    isGusting = false;
                    currentGustFactor = 1.0f;
                }
                else
                {
                    // Smooth ramp up and down
                    currentGustFactor = 1.0f + Mathf.Sin(Mathf.PI * (gustTimer / 4.0f)) * (GustStrengthMult - 1.0f);
                    ApplyGustForce(v);
                }
            }
        }

        private void ApplyGustForce(Vessel v)
        {
            if (Wind.Instance == null || v.rootPart == null || v.rootPart.rb == null) return;
            
            float speed = Wind.Instance.CurrentWindSpeed * (currentGustFactor - 1.0f);
            if (speed < 0.1f) return;

            float heading = Wind.Instance.CurrentWindHeading;
            float rad = heading * Mathf.Deg2Rad;
            Vector3 gustDir = new Vector3(-Mathf.Sin(rad), 0f, -Mathf.Cos(rad)).normalized;

            float mass = v.GetTotalMass() * 1000f;
            float force = 0.5f * (float)v.atmDensity * speed * speed * v.vesselSize.magnitude * 2.0f;
            
            v.rootPart.rb.AddForce(gustDir * (force / mass), ForceMode.Acceleration);
        }
    }
}