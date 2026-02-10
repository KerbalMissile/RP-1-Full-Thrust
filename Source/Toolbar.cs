using System;
using UnityEngine;
using KSP.UI.Screens;

namespace Windy
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class Toolbar : MonoBehaviour
    {
        private Rect windowRect = new Rect(150, 150, 320, 380); // Made slightly taller for forecasts
        private bool showWindow = false;
        private ApplicationLauncherButton appButton;
        private bool showWindArrows = false;

        void Start()
        {
            GameEvents.onGUIApplicationLauncherReady.Add(OnAppLauncherReady);
        }

        private void OnAppLauncherReady()
        {
            if (appButton != null) return;
            Texture2D icon = GameDatabase.Instance.GetTexture("Windy/Assets/Textures/Windy", false);
            if (icon == null) icon = new Texture2D(32, 32);

            appButton = ApplicationLauncher.Instance.AddModApplication(
                OnButtonOn, OnButtonOff,
                null, null, null, null, ApplicationLauncher.AppScenes.FLIGHT, icon);
        }

        private void OnButtonOn() { showWindow = true; }
        private void OnButtonOff() { showWindow = false; }

        void OnGUI()
        {
            if (showWindow)
            {
                GUI.skin = HighLogic.Skin;
                windowRect = GUILayout.Window(9912, windowRect, DrawWindow, "Windy Monitor");
            }
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();
            
            // --- CURRENT WIND SECTION ---
            GUIStyle sectionHeader = new GUIStyle(GUI.skin.label);
            sectionHeader.fontStyle = FontStyle.Bold;
            sectionHeader.normal.textColor = Color.yellow;
            
            GUILayout.Label("Current Conditions", sectionHeader);
            if (Wind.Instance != null)
            {
                GUILayout.Label(string.Format("Speed: {0:F1} m/s", Wind.Instance.CurrentWindSpeed));
                GUILayout.Label(string.Format("Heading: {0:F0}°", Wind.Instance.CurrentWindHeading));
            }
            else
            {
                GUILayout.Label("Wind System Offline...");
            }

            GUILayout.Space(10);

            // --- FORECAST SECTION ---
            GUILayout.Label("Forecast (Surface)", sectionHeader);
            double ut = Planetarium.GetUniversalTime();
            
            // 5 Minute Forecast
            var f5 = Forecasts.GetForecast(0, ut, 5f);
            GUILayout.Label(string.Format("In 5m: {0:F1} m/s @ {1:F0}°", f5.windSpeed, f5.windDirection));

            // 15 Minute Forecast
            var f15 = Forecasts.GetForecast(0, ut, 15f);
            GUILayout.Label(string.Format("In 15m: {0:F1} m/s @ {1:F0}°", f15.windSpeed, f15.windDirection));

            GUILayout.Space(15);

            // --- VISUALIZER SECTION ---
            GUILayout.Label("Visualizer", sectionHeader);
            bool toggleRequest = GUILayout.Toggle(showWindArrows, " Enable Wind Arrows");
            if (toggleRequest != showWindArrows)
            {
                showWindArrows = toggleRequest;
                UpdateVisualizer();
            }

            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            GUI.contentColor = Color.green;
            GUILayout.Label("█", GUILayout.Width(20));
            GUI.contentColor = Color.white;
            GUILayout.Label("Coming From");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.contentColor = Color.red;
            GUILayout.Label("█", GUILayout.Width(20));
            GUI.contentColor = Color.white;
            GUILayout.Label("Going To");
            GUILayout.EndHorizontal();

            GUILayout.Space(15);

            if (GUILayout.Button("Close"))
            {
                showWindow = false;
                if (appButton != null) appButton.SetFalse();
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void UpdateVisualizer()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;

            var existing = v.gameObject.GetComponent<WindDirection3D>();
            if (showWindArrows)
            {
                if (existing == null) v.gameObject.AddComponent<WindDirection3D>();
            }
            else
            {
                if (existing != null) Destroy(existing);
            }
        }

        void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(OnAppLauncherReady);
            if (appButton != null) ApplicationLauncher.Instance.RemoveModApplication(appButton);
        }
    }
}