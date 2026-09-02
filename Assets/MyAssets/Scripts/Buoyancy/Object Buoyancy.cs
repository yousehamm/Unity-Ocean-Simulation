using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public class ObjectBuoyancy : MonoBehaviour
{
    public List<int> PointArrayIndeces = new();
    public Vector3[] FloatPointOffsets;

    [Header("Physical Properties")]
    public float objectVolumeInCubicMeters = 5.0f;
    public float waterDensity = 1000.0f;
    public float probeHeightExtent = 0.5f;

    [Header("Drag")]
    public float waterDrag = 1.0f;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void ApplyBuoyancyForces(NativeArray<MasterBuoyancyScript.OceanData> results, List<Vector3> AllBuoyantPointsWorldPos)
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
            
        if (PointArrayIndeces.Count == 0)
        {
            return;
        }

        float gravity = Mathf.Abs(Physics.gravity.y);
        float volumePerPoint = objectVolumeInCubicMeters / PointArrayIndeces.Count;

        for (int i = 0; i < PointArrayIndeces.Count; i++)
        {
            int globalIndex = PointArrayIndeces[i];

            Vector3 pointWorldPos = AllBuoyantPointsWorldPos[globalIndex];
            float waterHeight = results[globalIndex].waterHeight;

            if (float.IsNaN(waterHeight))
            {
                continue;
            }

            float depth = waterHeight - pointWorldPos.y;

            if (depth > 0f)
            {
                //Calculate submergence ratio
                float subRatio = Mathf.Clamp01(depth / probeHeightExtent);

                //Upward buoyant force proportional to submerged depth
                Vector3 buoyantForce = Vector3.up * (waterDensity * volumePerPoint * gravity * subRatio);

                //Apply simple counter velocity drag to stabilize motion
                Vector3 pointVelocity = rb.GetPointVelocity(pointWorldPos);
                Vector3 dragForce = -pointVelocity * waterDrag;

                //Apply combined force at the probe position
                rb.AddForceAtPosition(buoyantForce + dragForce, pointWorldPos, ForceMode.Force);
            }
        }
    }
}