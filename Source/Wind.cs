using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using KSP;

namespace Windy
{
    public struct WindProfile
    {
        public float startAlt;
        public float peakAlt;
        public float endAlt;
        public WindProfile(float s, float p, float e) { startAlt = s; peakAlt = p; endAlt = e; }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class Wind : MonoBehaviour
    {
        public static Wind Instance = null;

        private Vector3 windDirection;
        private float currentWindSpeed;
        private float currentWindHeading;

        // how fast wind is at sea level
        private const float KerbinSeaLevelBase = 7.5f;

        // settings for the random noise patterns
        private const float NoiseSpatialScale = 0.00005f;
        private const float NoiseTimeScale = 0.00025f;

        // settings for checking mountains
        private const double MountainSampleDeg = 0.15;
        private const float MountainHeightDeltaThreshold = 300f;
        private const float MountainBoostMultiplier = 1.35f;

        private Dictionary<string, float> biomeSpeedMultiplier = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, WindProfile> profiles = new Dictionary<string, WindProfile>(StringComparer.OrdinalIgnoreCase);

        // numbers to make sure the wind is the same every time you load the save
        private int deterministicSeed = 1234567;
        private float noiseSeedOffset = 0f;

        // timer to make sure we don't update too fast and lag the game
        private double lastUpdateUT = -9999.0;
        private const double UpdateInterval = 0.25;

        // --- Oceanic drift tunables (can be adjusted)
        // base multiplier applied to drift acceleration
        private const float baseDriftFactor = 0.0025f;
        // mass scaling: heavier vessels are less affected (this value multiplies 1000 / mass)
        private const float driftMassScale = 1000f;

        void Awake()
        {
            Instance = this;
            SetupDefaultBiomeMultipliers();
            LoadDefaultProfiles();
            InitSeed();
            Forecasts.InitializeFromSeed(deterministicSeed);
        }

        void Update()
        {
            // stop if wind is turned off
            if (!GameDifficulty.IsWindEnabled())
            {
                currentWindSpeed = 0f;
                currentWindHeading = 0f;
                windDirection = Vector3.zero;
                return;
            }

            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || v.mainBody == null)
            {
                currentWindSpeed = 0f;
                currentWindHeading = 0f;
                windDirection = Vector3.zero;
                return;
            }

            // stop if we are in space
            if (IsInSpace(v))
            {
                currentWindSpeed = 0f;
                currentWindHeading = 0f;
                windDirection = Vector3.zero;
                return;
            }

            // wait for the next update time
            double ut = Planetarium.GetUniversalTime();
            if ((ut - lastUpdateUT) < UpdateInterval) return;
            lastUpdateUT = ut;

            UpdateWind();

            // Apply oceanic drift if needed (safe, non-invasive)
            TryApplyOceanDrift(v);
        }

        private void InitSeed()
        {
            // get the seed from the save file
            try
            {
                if (HighLogic.CurrentGame != null)
                {
                    deterministicSeed = (int)(HighLogic.CurrentGame.Seed & 0x7FFFF);
                }
            }
            catch { deterministicSeed = 1234567; }

            System.Random rnd = new System.Random(deterministicSeed);
            noiseSeedOffset = (float)rnd.NextDouble() * 10000f;
        }

        private void SetupDefaultBiomeMultipliers()
        {
            // set how windy different biomes are
            biomeSpeedMultiplier["Mountains"] = 1.6f;
            biomeSpeedMultiplier["Highlands"] = 1.25f;
            biomeSpeedMultiplier["Grasslands"] = 1.0f;
            biomeSpeedMultiplier["Deserts"] = 1.2f;
            biomeSpeedMultiplier["Badlands"] = 1.2f;
            biomeSpeedMultiplier["Tundra"] = 1.0f;
            biomeSpeedMultiplier["IceCaps"] = 0.9f;
            biomeSpeedMultiplier["Shores"] = 1.3f;
            biomeSpeedMultiplier["Water"] = 1.4f;
            biomeSpeedMultiplier["default"] = 1.0f;
        }

        private void LoadDefaultProfiles()
        {
            // set where the atmosphere starts and ends for planets
            profiles.Clear();
            profiles["Kerbin"] = new WindProfile(0f, 40000f, 70000f);
            profiles["Duna"] = new WindProfile(0f, 12000f, 30000f);
            profiles["Eve"] = new WindProfile(0f, 30000f, 120000f);
            profiles["Jool"] = new WindProfile(0f, 80000f, 20000f);
            profiles["Laythe"] = new WindProfile(0f, 20000f, 50000f);
        }

        private bool IsInSpace(Vessel v)
        {
            if (v == null || v.mainBody == null) return true;
            if (v.altitude >= v.mainBody.atmosphereDepth) return true;
            if (v.situation == Vessel.Situations.ORBITING || v.situation == Vessel.Situations.ESCAPING) return true;
            return false;
        }

        private void UpdateWind()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            CelestialBody body = v.mainBody;

            float bodyScale = GetBodyWindScale(body);
            if (bodyScale <= 0f)
            {
                currentWindSpeed = 0f;
                currentWindHeading = 0f;
                windDirection = Vector3.zero;
                return;
            }

            // get the smooth wind from the forecast file
            Forecasts.ForecastData bg = Forecasts.GetCurrentWind(v.altitude, Planetarium.GetUniversalTime());

            // calculate the final wind vector
            Vector3 totalWind = ComputeWindVector(v, body, bg);

            currentWindSpeed = totalWind.magnitude;

            if (currentWindSpeed <= 0.0001f)
            {
                currentWindHeading = 0f;
                windDirection = Vector3.zero;
            }
            else
            {
                // turn the 3D vector into a compass heading
                Vector3 horiz = new Vector3(totalWind.x, 0f, totalWind.z);
                horiz.Normalize();
                float rad = Mathf.Atan2(-horiz.x, -horiz.z);
                float deg = rad * Mathf.Rad2Deg;
                currentWindHeading = NormalizeHeading(deg);
                windDirection = horiz;
            }
        }

        private Vector3 ComputeWindVector(Vessel v, CelestialBody body, Forecasts.ForecastData background)
        {
            float alt = (float)v.altitude;

            // turn the forecast heading into a vector
            float baseSpeed = background.windSpeed;
            float baseHeading = background.windDirection;
            float baseRad = baseHeading * Mathf.Deg2Rad;
            Vector3 baseVec = new Vector3(-Mathf.Sin(baseRad), 0f, -Mathf.Cos(baseRad)) * baseSpeed;

            // add some random variation based on where the ship is in the world
            Vector3 pos = v.GetWorldPos3D();
            float regionalScale = 0.45f * GetRegionalStrengthForAltitude(alt);
            float fbmX = NoiseUtil.FBM(new Vector3(pos.x * NoiseSpatialScale, pos.y * NoiseSpatialScale, pos.z * NoiseSpatialScale), (float)(Planetarium.GetUniversalTime() * 0.0005 + noiseSeedOffset), 4, 2f, 0.5f);
            float fbmDir = NoiseUtil.FBM(new Vector3((pos.x + 314.159f) * NoiseSpatialScale, (pos.y + 271.828f) * NoiseSpatialScale, (pos.z + 141.421f) * NoiseSpatialScale), (float)(Planetarium.GetUniversalTime() * 0.0004 + noiseSeedOffset + 100f), 3);
            float regionalMagnitudeFactor = 1f + regionalScale * fbmX;
            float regionalHeadingOffset = fbmDir * 22f;

            Quaternion regionalRot = Quaternion.Euler(0f, regionalHeadingOffset, 0f);
            Vector3 regionalVec = regionalRot * baseVec * regionalMagnitudeFactor;

            // make the wind slower when you are close to the ground
            float vertScale = VerticalWindScale(alt, body);
            Vector3 verticalAdjusted = regionalVec * vertScale;

            // adjust speed based on biome and mountains
            float terrainFactor = 1f;
            string biome = GetBiomeAtVessel(v);
            float biomeMult = 1f;
            if (!string.IsNullOrEmpty(biome))
            {
                if (!biomeSpeedMultiplier.TryGetValue(biome, out biomeMult)) biomeMult = biomeSpeedMultiplier["default"];
            }
            terrainFactor *= biomeMult;

            bool mountainsNearby = IsMountainNearby(v, body);
            if (mountainsNearby) terrainFactor *= MountainBoostMultiplier;

            Vector3 terrainAdjusted = verticalAdjusted * terrainFactor;

            // add a tiny bit of curve because the planet is spinning
            Vector3 coriolisAdjusted = ApplyCoriolis(terrainAdjusted, v.latitude, body);

            // THERMAL WIND: apply day/night multiplier based on local solar time
            float thermalMultiplier = GetThermalMultiplier(body, v, Planetarium.GetUniversalTime());
            Vector3 thermallyAdjusted = coriolisAdjusted * thermalMultiplier;

            // make sure the wind doesn't go over the max speed setting
            float userMax = GameDifficulty.GetMaxWindSpeed();
            if (userMax <= 1f) userMax = 25f;
            float finalSpeed = Mathf.Clamp(thermallyAdjusted.magnitude, 0f, userMax);

            if (thermallyAdjusted.magnitude > 0.0001f)
            {
                return thermallyAdjusted.normalized * finalSpeed;
            }
            return Vector3.zero;
        }

        private float GetRegionalStrengthForAltitude(float altitude)
        {
            // wind is smoother the higher up you go
            float decay = Mathf.Exp(-altitude / 20000f);
            return Mathf.Clamp01(decay);
        }

        private float VerticalWindScale(float altitude, CelestialBody body, float mixingHeight = 2000f, float z0 = 0.1f)
        {
            // math to calculate how much the ground slows down the wind
            altitude = Mathf.Max(1e-3f, altitude);
            float zRef = 100f;
            float denom = Mathf.Log(Mathf.Max(zRef, z0 + 1e-6f) / z0);
            float nearSurface = 1f;
            if (denom > 0f)
            {
                nearSurface = Mathf.Log(Mathf.Max(altitude, z0) / z0) / denom;
            }
            nearSurface = Mathf.Clamp(nearSurface, 0.2f, 6f);
            if (altitude >= mixingHeight) return Mathf.Lerp(nearSurface, 1.0f, 0.8f);
            float t = altitude / mixingHeight;
            return Mathf.Lerp(nearSurface, 1.0f, t);
        }

        private Vector3 ApplyCoriolis(Vector3 wind, double latitude, CelestialBody body)
        {
            // math to curve the wind based on planet rotation
            if (wind.magnitude <= 0.0001f || body == null) return wind;
            double omega = 0.0;
            try
            {
                Type t = body.GetType();
                FieldInfo rf = t.GetField("rotationPeriod", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (rf != null)
                {
                    object val = rf.GetValue(body);
                    double rotationPeriod = Convert.ToDouble(val);
                    if (rotationPeriod > 1e-6) omega = 2.0 * Math.PI / rotationPeriod;
                }
                else
                {
                    PropertyInfo rp = t.GetProperty("rotationPeriod", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (rp != null)
                    {
                    object val = rp.GetValue(body, null);
                    double rotationPeriod = Convert.ToDouble(val);
                    if (rotationPeriod > 1e-6) omega = 2.0 * Math.PI / rotationPeriod;
                    }
                }
            }
            catch { omega = 0.0; }

            if (omega <= 0.0) return wind;

            float f = 2f * (float)omega * Mathf.Sin((float)(latitude * Mathf.Deg2Rad));
            Vector3 lateral = Vector3.Cross(Vector3.up, wind).normalized;
            Vector3 deflection = lateral * (f * wind.magnitude * 0.08f);
            return wind + deflection;
        }

        private float GetThermalMultiplier(CelestialBody body, Vessel v, double ut)
        {
            // Return a multiplier ~ (1 +/- amplitude) based on local solar time (simple sinusoid).
            // amplitude is scaled by the planet wind scale so thick-atmosphere worlds can have stronger diurnal effects.
            double rotationPeriod = GetBodyRotationPeriod(body);
            if (rotationPeriod <= 1.0) return 1f;

            // compute a simple local-phase: offset by longitude so local noon varies with longitude
            double localOffset = (v.longitude / 360.0) * rotationPeriod;
            double localPhase = (ut + localOffset) % rotationPeriod;

            double phaseFrac = localPhase / rotationPeriod; // 0..1
            double sinVal = Math.Sin(2.0 * Math.PI * phaseFrac); // -1..1, noon ~ 0->sin(0)=0 but depends on offset; OK for smooth cycle

            // thermal amplitude: tune this to taste. scale with body wind scale so Eve/Kerbin differ.
            float amplitude = 0.35f * GetBodyWindScale(body); // +/- amplitude
            float mult = 1f + (float)sinVal * amplitude;

            // clamp to avoid negative or extreme values
            return Mathf.Clamp(mult, 0.4f, 2.5f);
        }

        private double GetBodyRotationPeriod(CelestialBody body)
        {
            if (body == null) return 0.0;
            try
            {
                Type t = body.GetType();
                FieldInfo rf = t.GetField("rotationPeriod", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (rf != null)
                {
                    object val = rf.GetValue(body);
                    return Convert.ToDouble(val);
                }
                PropertyInfo rp = t.GetProperty("rotationPeriod", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (rp != null)
                {
                    object val = rp.GetValue(body, null);
                    return Convert.ToDouble(val);
                }
            }
            catch { }
            return 0.0;
        }

        private string GetBiomeAtVessel(Vessel v)
        {
            // find out what biome the ship is currently in
            try
            {
                if (v == null || v.mainBody == null) return null;
                double lat = v.latitude;
                double lon = v.longitude;
                CelestialBody body = v.mainBody;
                Type cbType = body.GetType();

                MethodInfo[] methods = cbType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (MethodInfo mi in methods)
                {
                    if (mi.Name.IndexOf("biome", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                    ParameterInfo[] pars = mi.GetParameters();
                    if (pars.Length == 2)
                    {
                    object p0 = ConvertParameter(lat, pars[0].ParameterType);
                    object p1 = ConvertParameter(lon, pars[1].ParameterType);
                    try
                    {
                    object res = mi.Invoke(body, new object[] { p0, p1 });
                    if (res != null) return res.ToString();
                    }
                    catch { }
                    }
                    }
                }
                return null;
            }
            catch { return null; }
        }

        private object ConvertParameter(double value, Type targetType)
        {
            if (targetType == typeof(double)) return value;
            if (targetType == typeof(float)) return (float)value;
            if (targetType == typeof(int)) return (int)Math.Round(value);
            return Convert.ChangeType(value, targetType);
        }

        private bool IsMountainNearby(Vessel v, CelestialBody body)
        {
            // check if there are big hills or mountains around the ship
            try
            {
                if (v == null || body == null) return false;
                double lat = v.latitude;
                double lon = v.longitude;

                Func<double, double, double> getHeight = null;
                Type cbType = body.GetType();
                MethodInfo[] methods = cbType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (MethodInfo mi in methods)
                {
                    string n = mi.Name.ToLowerInvariant();
                    if (n.Contains("height") || n.Contains("surface"))
                    {
                    ParameterInfo[] ps = mi.GetParameters();
                    if (ps.Length == 2)
                    {
                    getHeight = (dlat, dlon) =>
                    {
                    object p0 = ConvertParameter(dlat, ps[0].ParameterType);
                    object p1 = ConvertParameter(dlon, ps[1].ParameterType);
                    try
                    {
                    object res = mi.Invoke(body, new object[] { p0, p1 });
                    return Convert.ToDouble(res);
                    }
                    catch { return double.NaN; }
                    };
                    break;
                    }
                    }
                }

                if (getHeight == null) return false;

                double centerH = getHeight(lat, lon);
                if (double.IsNaN(centerH)) return false;

                double maxH = centerH;
                double[] offs = new double[] { -MountainSampleDeg, 0.0, MountainSampleDeg };
                foreach (double dlat in offs)
                {
                    foreach (double dlon in offs)
                    {
                    if (dlat == 0.0 && dlon == 0.0) continue;
                    double h = getHeight(lat + dlat, lon + dlon);
                    if (!double.IsNaN(h) && h > maxH) maxH = h;
                    }
                }

                if ((maxH - centerH) >= MountainHeightDeltaThreshold) return true;
                return false;
            }
            catch { return false; }
        }

        private float GetBodyWindScale(CelestialBody body)
        {
            // scale wind based on how thick the atmosphere is
            if (body == null) return 0f;
            float kerbinDepth = 70000f;
            float depth = (float)body.atmosphereDepth;
            if (depth <= 0f) return 0f;
            return Mathf.Clamp(depth / kerbinDepth, 0.01f, 5.0f);
        }

        private float NormalizeHeading(float h)
        {
            while (h < 0f) h += 360f;
            while (h >= 360f) h -= 360f;
            return h;
        }

        public float CurrentWindSpeed { get { return currentWindSpeed; } }
        public float CurrentWindHeading { get { return currentWindHeading; } }
        public Vector3 CurrentWindDirection { get { return windDirection; } }

        private static class NoiseUtil
        {
            // math to make smooth random noise
            public static float FBM(Vector3 pos, float time, int octaves = 4, float lacunarity = 2f, float gain = 0.5f)
            {
                float frequency = 1f;
                float amplitude = 1f;
                float sum = 0f;
                float max = 0f;

                for (int i = 0; i < octaves; i++)
                {
                    float n1 = Mathf.PerlinNoise(pos.x * frequency + time, pos.y * frequency);
                    float n2 = Mathf.PerlinNoise(pos.z * frequency - time, pos.x * frequency);
                    float n = (n1 + n2) * 0.5f;
                    sum += (n * 2f - 1f) * amplitude;
                    max += amplitude;
                    amplitude *= gain;
                    frequency *= lacunarity;
                }
                if (max <= 0f) return 0f;
                return sum / max;
            }
        }

        // ----
        // Ocean drift helper methods
        // ----
        private void TryApplyOceanDrift(Vessel v)
        {
            try
            {
                if (v == null) return;
                if (!v.Splashed) return; // only apply to splashed vessels
                if (v.rootPart == null) return;

                // get the world-space horizontal wind vector
                if (windDirection == Vector3.zero || currentWindSpeed <= 0.0001f) return;
                Vector3 windVec = windDirection.normalized * currentWindSpeed;

                // ensure there is a rigidbody to apply force to
                Rigidbody rb = null;

                // 1) Preferred modern approach: GetComponent<Rigidbody>()
                try
                {
                    rb = v.rootPart.GetComponent<Rigidbody>();
                }
                catch { rb = null; }

                // 2) Fallback: older 'rigidbody' field (obsolete) via reflection if present
                if (rb == null)
                {
                    try
                    {
#pragma warning disable 0618
                        var oldRbField = v.rootPart.GetType().GetField("rigidbody", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
#pragma warning restore 0618
                        if (oldRbField != null)
                        {
                            object rv = oldRbField.GetValue(v.rootPart);
                            rb = rv as Rigidbody;
                        }
                    }
                    catch { rb = null; }
                }

                // 3) Final fallback: common 'rb' field
                if (rb == null)
                {
                    var rbField = v.rootPart.GetType().GetField("rb", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (rbField != null)
                    {
                        object rv = rbField.GetValue(v.rootPart);
                        rb = rv as Rigidbody;
                    }
                }

                if (rb == null) return;

                // compute a small acceleration to nudge the vessel downwind
                float mass = (float)Math.Max(1.0, v.GetTotalMass());
                float accel = baseDriftFactor * currentWindSpeed * (driftMassScale / Mathf.Max(1f, mass));
                accel = Mathf.Clamp(accel, 0.0001f, 0.5f); // safety clamp

                Vector3 accelVec = windVec.normalized * accel;

                // apply acceleration via Rigidbody (ForceMode.Acceleration)
                rb.AddForce(accelVec, ForceMode.Acceleration);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Windy] OceanDrift error: " + ex.Message);
            }
        }
    }
}