using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using UnityEditor.ShaderGraph;
using Unity.VisualScripting;

public class MasterBuoyancyScript : MonoBehaviour
{
    public List<Vector3> AllBuoyantPointsLocalPos = new();
    public List<Vector3> AllBuoyantPointsWorldPos = new();
    public List<Transform> CorrespondingChildTransform = new();
    
    public List<OceanData> AllBuoyantPointsWaterInfo = new();
    public struct OceanData
    {
        public float waterHeight;
        public Vector3 normal;
    }

    //Compute Stuff
    public ComputeShader computeShader;
    int kernelIndex;
    ComputeBuffer InBuffer;
    ComputeBuffer OutBuffer;
    
    //Ocean Gameobject
    public Transform Ocean;
    void InitializeStuff()
    {
        int p = 0;
        for(int i = 0; i < transform.childCount; i++)
        {
            Transform floatingObject = transform.GetChild(i);
            Mesh objectMesh = floatingObject.GetComponent<MeshFilter>().sharedMesh;

            Vector3[] rawOffsets = floatingObject.GetComponent<ObjectBuoyancy>().FloatPointOffsets;
            Vector3[] transformedPositions = new Vector3[rawOffsets.Length];
            int[] arrayIndeces = new int[transformedPositions.Length];

            for(int x = 0; x < transformedPositions.Length; x++)
            {
                transformedPositions[x] = Vector3.Scale(rawOffsets[x], objectMesh.bounds.extents) + objectMesh.bounds.center;
                
                arrayIndeces[x] = p + x;
                
                CorrespondingChildTransform.Add(floatingObject.transform);
            }

            AllBuoyantPointsLocalPos.AddRange(transformedPositions);

            floatingObject.GetComponent<ObjectBuoyancy>().PointArrayIndeces.AddRange(arrayIndeces);

            p += transformedPositions.Length;
        }

        AllBuoyantPointsWorldPos = new List<Vector3>(new Vector3[AllBuoyantPointsLocalPos.Count]);

        //Setting Kernel
        kernelIndex = computeShader.FindKernel("SampleWaterHeight");

        //Initialzing Buffer
        int totalPoints = AllBuoyantPointsLocalPos.Count;

        //Avoid initializing buffers if there are no points
        if (totalPoints > 0)
        {
            InBuffer = new ComputeBuffer(totalPoints, sizeof(float) * 3, ComputeBufferType.Structured);

            OutBuffer = new ComputeBuffer(totalPoints, sizeof(float) * 4, ComputeBufferType.Structured);
        }
    }

    void FixedUpdate()
    {
        if(Time.frameCount == 2)
        {
            InitializeStuff();
        }
        else if(Time.frameCount < 2)
        {
            return;
        }

        //Calcualting World Posititons From Local Pos
        for(int i = 0; i < AllBuoyantPointsLocalPos.Count; i++)
        {
            Transform floatingObjectTransform = CorrespondingChildTransform[i];
            AllBuoyantPointsWorldPos[i] = floatingObjectTransform.TransformPoint(AllBuoyantPointsLocalPos[i]);
        }
        
        //Setting Buffers
        InBuffer.SetData(AllBuoyantPointsWorldPos);

        computeShader.SetBuffer(kernelIndex, "_InputPositions", InBuffer);
        computeShader.SetBuffer(kernelIndex, "_OutputResults", OutBuffer);

        //Setting Variables
        computeShader.SetTexture(kernelIndex, "_DisplacementCascades_Height", Ocean.GetComponent<OceanGenerator>().displacementTexHeightArray);
        computeShader.SetTexture(kernelIndex, "_DisplacementCascades_x", Ocean.GetComponent<OceanGenerator>().displacementTexXArray);
        computeShader.SetTexture(kernelIndex, "_DisplacementCascades_Y", Ocean.GetComponent<OceanGenerator>().displacementTexYArray);
        computeShader.SetTexture(kernelIndex, "_SlopeCascades_X", Ocean.GetComponent<OceanGenerator>().slopeTexXArray);
        computeShader.SetTexture(kernelIndex, "_SlopeCascades_Z", Ocean.GetComponent<OceanGenerator>().slopeTexZArray);

        computeShader.SetInt("_ProbeCount", AllBuoyantPointsWorldPos.Count);

        //Calculating threadgroups
        int threadGroups = Mathf.CeilToInt(AllBuoyantPointsWorldPos.Count / 64f);

        //Dispatching Shader
        computeShader.Dispatch(kernelIndex, threadGroups, 1, 1);

        //Async Readback
        AsyncGPUReadback.Request(OutBuffer, OnReadbackComplete);
    }
    void OnReadbackComplete(AsyncGPUReadbackRequest request)
    {
        if (request.hasError)
        {
            Debug.LogError("GPU Readback Error!");
            return;
        }

        //Getting Data From GPU Output
        Unity.Collections.NativeArray<OceanData> results = request.GetData<OceanData>();

        //Distributing Water Data To Child Objects
        for (int i = 0; i < transform.childCount; i++)
        {
            transform.GetChild(i).GetComponent<ObjectBuoyancy>().ApplyBuoyancyForces(results, AllBuoyantPointsWorldPos);
        }
    }
    void OnDestroy()
    {
        if (InBuffer != null) InBuffer.Dispose();
        if (OutBuffer != null) OutBuffer.Dispose();
    }
}
