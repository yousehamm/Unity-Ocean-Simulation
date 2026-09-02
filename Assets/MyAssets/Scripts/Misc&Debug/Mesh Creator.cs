using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MeshCreator : MonoBehaviour
{
    [Header("Ocean Settings")]
    public Camera mainCam;

    [Tooltip("The physical width (and length) in meters of the central high-fidelity patch.")]
    public float centerPatchWidth = 64f;

    [Tooltip("The number of quads along one axis in the center patch. Higher = more density!")]
    public int innerGridResolution = 64;

    [Tooltip("How many outer concentric rings to generate. Each ring doubles the cell size.")]
    public int ringLevels = 4;

    public Material waterMaterial;
    private Mesh oceanMesh;

    void Start()
    {
        GenerateSingleMeshClipmap();
    }

    void LateUpdate()
    {
        UpdatePositions();
    }

    public void GenerateSingleMeshClipmap()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

        oceanMesh = new Mesh();
        oceanMesh.name = "ConcentricClipmapOcean";
        oceanMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        List<Vector3> verticesList = new List<Vector3>();
        List<Vector2> uvsList = new List<Vector2>();
        List<int> trianglesList = new List<int>();

        //Ensure resolution is an even number to keep symmetry clean
        if (innerGridResolution % 2 != 0)
        {
            innerGridResolution++;
        }

        //Build the core center grid vertices
        float cellSize = centerPatchWidth / innerGridResolution;
        int res = innerGridResolution;
        float halfSize = centerPatchWidth * 0.5f;

        int vertexOffset = 0;

        for (int z = 0; z <= res; z++)
        {
            float posZ = -halfSize + (z * cellSize);
            for (int x = 0; x <= res; x++)
            {
                float posX = -halfSize + (x * cellSize);
                verticesList.Add(new Vector3(posX, 0f, posZ));
                uvsList.Add(new Vector2(posX, posZ));
            }
        }

        //Connect center grid vertices into triangles
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                int rowStart = z * (res + 1);
                int nextRowStart = (z + 1) * (res + 1);

                int bl = vertexOffset + rowStart + x;
                int br = bl + 1;
                int tl = vertexOffset + nextRowStart + x;
                int tr = tl + 1;

                trianglesList.Add(bl);
                trianglesList.Add(tl);
                trianglesList.Add(tr);
                trianglesList.Add(bl);
                trianglesList.Add(tr);
                trianglesList.Add(br);
            }
        }

        vertexOffset += (res + 1) * (res + 1);

        //Build outer concentric shell rings around the center grid
        for (int level = 1; level <= ringLevels; level++)
        {
            float prevCellSize = cellSize;
            cellSize *= 2f;

            float innerHalfSize = (res * prevCellSize) * 0.5f;
            float outerHalfSize = innerHalfSize * 2f;

            int halfRes = res / 2;
            int levelStartVertex = verticesList.Count;

            //Generate vertices for the current ring layer
            for (int z = 0; z <= res; z++)
            {
                float posZ = -outerHalfSize + (z * cellSize);
                bool inHoleZ = (z > halfRes / 2 && z < res - halfRes / 2);

                for (int x = 0; x <= res; x++)
                {
                    float posX = -outerHalfSize + (x * cellSize);
                    bool inHoleX = (x > halfRes / 2 && x < res - halfRes / 2);

                    //Skip vertices falling inside the inner hole
                    if (inHoleZ && inHoleX)
                    {
                        continue;
                    }

                    float finalX = posX;
                    float finalZ = posZ;

                    bool onInnerZBoundary = (z == halfRes / 2 || z == res - halfRes / 2);
                    bool onInnerXBoundary = (x == halfRes / 2 || x == res - halfRes / 2);

                    if (onInnerZBoundary && (posX >= -innerHalfSize && posX <= innerHalfSize))
                    {
                        finalX = Mathf.Round(posX / prevCellSize) * prevCellSize;
                    }
                    if (onInnerXBoundary && (posZ >= -innerHalfSize && posZ <= innerHalfSize))
                    {
                        finalZ = Mathf.Round(posZ / prevCellSize) * prevCellSize;
                    }

                    verticesList.Add(new Vector3(finalX, 0f, finalZ));
                    uvsList.Add(new Vector2(finalX, finalZ));
                }
            }

            //Map grid coordinates to vertex indices for triangle stitching
            Dictionary<string, int> coordToIdx = new Dictionary<string, int>();
            int vCounter = levelStartVertex;

            for (int z = 0; z <= res; z++)
            {
                bool inHoleZ = (z > halfRes / 2 && z < res - halfRes / 2);
                for (int x = 0; x <= res; x++)
                {
                    bool inHoleX = (x > halfRes / 2 && x < res - halfRes / 2);
                    if (!(inHoleZ && inHoleX))
                    {
                        coordToIdx.Add($"{x},{z}", vCounter++);
                    }
                }
            }

            //Stitch ring triangles while skipping the inner hole area
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    bool qZ = (z >= halfRes / 2 && z < res - halfRes / 2);
                    bool qX = (x >= halfRes / 2 && x < res - halfRes / 2);
                    if (qZ && qX)
                    {
                        continue;
                    }

                    string kBL = $"{x},{z}";
                    string kBR = $"{x + 1},{z}";
                    string kTL = $"{x},{z + 1}";
                    string kTR = $"{x + 1},{z + 1}";

                    if (coordToIdx.ContainsKey(kBL) && coordToIdx.ContainsKey(kBR) &&
                        coordToIdx.ContainsKey(kTL) && coordToIdx.ContainsKey(kTR))
                    {
                        int bl = coordToIdx[kBL];
                        int br = coordToIdx[kBR];
                        int tl = coordToIdx[kTL];
                        int tr = coordToIdx[kTR];

                        trianglesList.Add(bl);
                        trianglesList.Add(tl);
                        trianglesList.Add(tr);
                        trianglesList.Add(bl);
                        trianglesList.Add(tr);
                        trianglesList.Add(br);
                    }
                }
            }

            vertexOffset = verticesList.Count;
        }

        oceanMesh.vertices = verticesList.ToArray();
        oceanMesh.uv = uvsList.ToArray();
        oceanMesh.triangles = trianglesList.ToArray();

        float totalExtent = halfSize * Mathf.Pow(2, ringLevels);
        oceanMesh.bounds = new Bounds(Vector3.zero, new Vector3(totalExtent * 2f, 500f, totalExtent * 2f));
        oceanMesh.RecalculateNormals();

        meshFilter.mesh = oceanMesh;
        meshRenderer.material = waterMaterial;
    }

    void UpdatePositions()
    {
        if (mainCam != null)
        {
            transform.position = new Vector3(Mathf.CeilToInt(mainCam.transform.position.x), 0f, Mathf.CeilToInt(mainCam.transform.position.z));
        }
    }
}