using System;
using UnityEngine;

namespace Windy
{
    public class WindDirection3D : MonoBehaviour
    {
        private GameObject arrowRoot;
        private LineRenderer lineComing;
        private LineRenderer lineGoing;
        private Material lineMaterial;

        private float arrowYOffset = 4.0f; 
        private float arrowMinLen = 3.0f;
        private float arrowMaxLen = 15.0f;
        private float arrowWidth = 0.15f;

        // Store which vessel we belong to
        private Vessel myVessel;

        void Start()
        {
            myVessel = GetComponent<Vessel>();
            CreateWindArrow();
        }

        void OnDestroy()
        {
            if (arrowRoot != null) Destroy(arrowRoot);
            if (lineMaterial != null) Destroy(lineMaterial);
        }

        void LateUpdate()
        {
            // FIX: If we are no longer the active vessel (switched or crashed), destroy this visualizer
            if (FlightGlobals.ActiveVessel != myVessel || myVessel == null)
            {
                Destroy(this);
                return;
            }

            if (Wind.Instance == null || arrowRoot == null) return;
            if (myVessel.rootPart == null) return;

            // Update position to follow the vessel
            arrowRoot.transform.position = myVessel.rootPart.transform.position + (myVessel.upAxis * arrowYOffset);

            // Calculate horizontal direction
            float heading = Wind.Instance.CurrentWindHeading;
            float speed = Wind.Instance.CurrentWindSpeed;
            float rad = heading * Mathf.Deg2Rad;
            
            Vector3 north = Vector3.ProjectOnPlane(myVessel.mainBody.transform.up, myVessel.upAxis).normalized;
            Vector3 east = Vector3.Cross(myVessel.upAxis, north).normalized;
            
            Vector3 toVec = (north * -Mathf.Cos(rad) + east * -Mathf.Sin(rad)).normalized;
            Vector3 fromVec = -toVec;

            float len = Mathf.Lerp(arrowMinLen, arrowMaxLen, Mathf.Clamp01(speed / 50f));
            Vector3 start = arrowRoot.transform.position;

            lineComing.SetPosition(0, start);
            lineComing.SetPosition(1, start + fromVec * len);

            lineGoing.SetPosition(0, start);
            lineGoing.SetPosition(1, start + toVec * len);
        }

        private void CreateWindArrow()
        {
            arrowRoot = new GameObject("WindyArrowRoot");
            lineMaterial = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));

            lineComing = CreateLine("Coming", Color.green);
            lineGoing = CreateLine("Going", Color.red);
        }

        private LineRenderer CreateLine(string name, Color col)
        {
            GameObject go = new GameObject(name);
            go.transform.parent = arrowRoot.transform;
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.material = lineMaterial;
            lr.positionCount = 2;
            lr.startWidth = arrowWidth;
            lr.endWidth = arrowWidth;
            lr.useWorldSpace = true;
            lr.startColor = col;
            lr.endColor = col;
            return lr;
        }
    }
}