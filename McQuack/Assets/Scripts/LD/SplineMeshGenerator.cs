using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(SplinePath))]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class SplineMeshGenerator : MonoBehaviour
{
    [Header("Fence Pieces")]

    [SerializeField]
    private Mesh startMesh;

    [SerializeField]
    private Mesh[] interiorMeshes;

    [SerializeField]
    private Mesh endMesh;

    [Header("Source Scale")]

    [SerializeField]
    private float meshScale = 1f;

    [Header("Materials")]

    [SerializeField]
    private Material[] materials;

    [Header("Section Spacing")]

    [Tooltip("Distance from the start mesh to the first interior mesh.")]
    [SerializeField]
    private float endSpacing = 0f;

    [Tooltip("Distance along the spline between consecutive interior meshes.")]
    [SerializeField]
    private float interiorStep = 0f;

    [Header("Randomization")]

    [SerializeField]
    private int randomSeed = 12345;

    [SerializeField]
    private bool avoidImmediateRepeats = true;

    [Header("Generation")]

    [Tooltip(
        "If the spline is not an exact multiple of the source mesh length, " +
        "stretch the final section to fill the remaining distance.")]
    [SerializeField]
    private bool stretchLastSection = true;

    [Header("Source Mesh Axis")]
    [Tooltip("The source mesh's local X axis represents its length.")]
    [SerializeField]
    private bool sourceMeshIsCenteredOnX = false;

    [Header("Output")]
    [SerializeField]
    private bool generateCollider = false;

    [SerializeField]
    private bool recalculateNormals = false;

    [SerializeField]
    private bool recalculateTangents = false;

    private SplinePath spline;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    private Mesh generatedMesh;

    private bool dirty = true;

    private readonly List<Vector3> vertices = new();
    private readonly List<Vector3> normals = new();
    private readonly List<Vector4> tangents = new();
    private readonly List<Vector2> uvs = new();

    private readonly List<List<int>> submeshTriangles = new List<List<int>>();

    private void Awake()
    {
        Initialize();
        Rebuild();
    }

    private void OnEnable()
    {
        Initialize();
        dirty = true;
    }

    private void Update()
    {
        if (!dirty)
            return;

        Rebuild();
    }

    private void Initialize()
    {
        if (spline == null)
            spline = GetComponent<SplinePath>();

        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();

        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();
    }

    public void MarkDirty()
    {
        dirty = true;
    }

    private float GetInteriorSpacing(Mesh mesh)
    {
        if (interiorStep > 0f)
            return interiorStep;

        return GetMeshLength(mesh);
    }

    [ContextMenu("Rebuild Mesh")]
    public void Rebuild()
    {
        Initialize();

        if (spline == null ||
            spline.PointCount < 2)
        {
            ClearGeneratedMesh();
            dirty = false;
            return;
        }

        ClearLists();

        float splineLength = spline.Length;

        if (splineLength <= Mathf.Epsilon)
        {
            ClearGeneratedMesh();
            dirty = false;
            return;
        }

        // -------------------------------------------------------------
        // Start piece
        // -------------------------------------------------------------

        float startDistance = 0f;
        float startLength = 0f;

        if (startMesh != null)
        {
            startLength =
                GetMeshLength(startMesh);

            AddMeshSection(
                startMesh,
                0f,
                startLength);

            startDistance = startLength;
        }

        // -------------------------------------------------------------
        // End piece
        //
        // We reserve space for both the end piece AND the requested
        // spacing between the interior and end pieces.
        // -------------------------------------------------------------

        float endLength = 0f;

        if (endMesh != null)
        {
            endLength =
                GetMeshLength(endMesh);
        }

        float interiorStart =
            startDistance + endSpacing;

        float interiorEnd =
            splineLength -
            endLength -
            endSpacing;

        // Make sure the interior region is valid.
        interiorEnd =
            Mathf.Max(
                interiorStart,
                interiorEnd);

        // -------------------------------------------------------------
        // Interior pieces
        // -------------------------------------------------------------

        if (interiorMeshes != null &&
            interiorMeshes.Length > 0 &&
            interiorEnd > interiorStart)
        {
            System.Random random =
                new System.Random(randomSeed);

            int previousMeshIndex = -1;

            float currentDistance =
                interiorStart;

            while (currentDistance < interiorEnd)
            {
                int meshIndex =
                    ChooseRandomMesh(
                        random,
                        previousMeshIndex);

                Mesh mesh =
                    interiorMeshes[meshIndex];

                if (mesh == null)
                {
                    previousMeshIndex = meshIndex;
                    continue;
                }

                float meshLength =
                    GetMeshLength(mesh) * meshScale;

                if (meshLength <= Mathf.Epsilon)
                {
                    previousMeshIndex = meshIndex;
                    continue;
                }

                float remaining =
                    interiorEnd - currentDistance;

                bool finalSection =
                    meshLength > remaining;

                float sectionLength =
                    finalSection && stretchLastSection
                        ? remaining
                        : meshLength;

                // If this mesh fits, or if we're allowed to stretch
                // the final mesh to the available space, generate it.
                if (!finalSection ||
                    stretchLastSection)
                {
                    AddMeshSection(
                        mesh,
                        currentDistance,
                        sectionLength);

                    // -------------------------------------------------
                    // IMPORTANT:
                    //
                    // Advance by interiorStep rather than by the
                    // physical mesh length.
                    // -------------------------------------------------

                    float step =
                        interiorStep > Mathf.Epsilon
                            ? interiorStep
                            : meshLength;

                    currentDistance += step;
                }
                else
                {
                    // The final mesh doesn't fit and we're not
                    // stretching it, so stop.
                    break;
                }

                previousMeshIndex =
                    meshIndex;
            }
        }

        // -------------------------------------------------------------
        // End piece
        // -------------------------------------------------------------

        if (endMesh != null &&
            endLength > Mathf.Epsilon)
        {
            float endStart =
                splineLength - endLength;

            AddMeshSection(
                endMesh,
                endStart,
                endLength);
        }

        FinishMesh();

        dirty = false;
    }

    private void AddMeshSection(
        Mesh mesh,
        float startDistance,
        float sectionLength)
    {
        if (mesh == null)
            return;

        Vector3[] sourceVertices =
            mesh.vertices;

        Vector3[] sourceNormals =
            mesh.normals;

        Vector4[] sourceTangents =
            mesh.tangents;

        Vector2[] sourceUVs =
            mesh.uv;

        int submeshCount =
            mesh.subMeshCount;

        EnsureSubmeshLists(submeshCount);

        int vertexOffset =
            vertices.Count;

        float sourceMinX =
            mesh.bounds.min.x;

        float sourceMaxX =
            mesh.bounds.max.x;

        float sourceLength =
            sourceMaxX - sourceMinX;

        if (sourceLength <= Mathf.Epsilon)
            return;

        for (int i = 0;
             i < sourceVertices.Length;
             i++)
        {
            Vector3 sourceVertex =
                sourceVertices[i] * meshScale;

            float normalizedX =
                Mathf.InverseLerp(
                    sourceMinX,
                    sourceMaxX,
                    sourceVertex.x);

            normalizedX =
                Mathf.Clamp01(normalizedX);

            float distance =
                startDistance +
                normalizedX * sectionLength;

            SplinePath.SplineSample sample =
                spline.EvaluateAtDistance(
                    distance);

            Vector3 tangent =
                sample.Tangent.normalized;

            Vector3 up =
                sample.Up.normalized;

            Vector3 right =
                Vector3.Cross(
                    up,
                    tangent).normalized;

            up =
                Vector3.Cross(
                    tangent,
                    right).normalized;

            Vector3 worldPosition =
                    sample.Position
                    + up * sourceVertex.z
                    + right * sourceVertex.y;

            vertices.Add(
                transform.InverseTransformPoint(
                    worldPosition));

            // ---------------------------------------------------------
            // Normal
            // ---------------------------------------------------------

            if (sourceNormals.Length ==
                sourceVertices.Length)
            {
                Vector3 sourceNormal =
                    sourceNormals[i];

                Vector3 worldNormal =
                    tangent * sourceNormal.x +
                    up * sourceNormal.z +
                    right * sourceNormal.y;

                normals.Add(
                    transform.InverseTransformDirection(
                        worldNormal).normalized);
            }
            else
            {
                normals.Add(Vector3.up);
            }

            // ---------------------------------------------------------
            // Tangent
            // ---------------------------------------------------------

            if (sourceTangents.Length ==
                sourceVertices.Length)
            {
                Vector4 sourceTangent =
                    sourceTangents[i];

                Vector3 worldTangent =
                    tangent * sourceTangent.x +
                    up * sourceTangent.z +
                    right * sourceTangent.y;

                Vector3 localTangent =
                    transform.InverseTransformDirection(
                        worldTangent).normalized;

                tangents.Add(
                    new Vector4(
                        localTangent.x,
                        localTangent.y,
                        localTangent.z,
                        sourceTangent.w));
            }
            else
            {
                tangents.Add(
                    new Vector4(
                        tangent.x,
                        tangent.y,
                        tangent.z,
                        1f));
            }

            // ---------------------------------------------------------
            // UV
            // ---------------------------------------------------------

            if (sourceUVs.Length ==
                sourceVertices.Length)
            {
                uvs.Add(sourceUVs[i]);
            }
            else
            {
                uvs.Add(Vector2.zero);
            }
        }

        // -------------------------------------------------------------
        // Triangles
        // -------------------------------------------------------------

        for (int submesh = 0;
             submesh < submeshCount;
             submesh++)
        {
            int[] sourceTriangles =
                mesh.GetTriangles(submesh);

            List<int> destination =
                submeshTriangles[submesh];

            for (int i = 0;
                 i < sourceTriangles.Length;
                 i++)
            {
                destination.Add(
                    vertexOffset +
                    sourceTriangles[i]);
            }
        }
    }

    private float GetMeshLength(Mesh mesh)
    {
        if (mesh == null)
            return 0f;

        return mesh.bounds.max.x -
               mesh.bounds.min.x;
    }

    private int ChooseRandomMesh(
    System.Random random,
    int previousIndex)
    {
        if (interiorMeshes == null ||
            interiorMeshes.Length == 0)
        {
            return -1;
        }

        if (!avoidImmediateRepeats ||
            interiorMeshes.Length == 1)
        {
            return random.Next(
                0,
                interiorMeshes.Length);
        }

        int result;

        do
        {
            result =
                random.Next(
                    0,
                    interiorMeshes.Length);

        } while (result == previousIndex);

        return result;
    }

    private void FinishMesh()
    {
        if (generatedMesh == null)
        {
            generatedMesh =
                new Mesh
                {
                    name = $"{name}_Generated"
                };
        }
        else
        {
            generatedMesh.Clear();
        }

        generatedMesh.indexFormat =
            vertices.Count > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

        generatedMesh.SetVertices(vertices);

        generatedMesh.SetUVs(0, uvs);

        if (recalculateNormals)
        {
            generatedMesh.RecalculateNormals();
        }
        else
        {
            generatedMesh.SetNormals(normals);
        }

        if (recalculateTangents)
        {
            generatedMesh.RecalculateTangents();
        }
        else
        {
            generatedMesh.SetTangents(tangents);
        }

        generatedMesh.subMeshCount =
            submeshTriangles.Count;

        for (int i = 0;
             i < submeshTriangles.Count;
             i++)
        {
            generatedMesh.SetTriangles(
                submeshTriangles[i],
                i);
        }

        generatedMesh.RecalculateBounds();

        meshFilter.sharedMesh =
            generatedMesh;

        meshRenderer.sharedMaterials =
            materials;

        UpdateCollider();
    }

    private void EnsureSubmeshLists(int count)
    {
        while (submeshTriangles.Count < count)
        {
            submeshTriangles.Add(
                new List<int>());
        }
    }

    private void ClearLists()
    {
        vertices.Clear();
        normals.Clear();
        tangents.Clear();
        uvs.Clear();

        submeshTriangles.Clear();
    }

    private void ClearGeneratedMesh()
    {
        if (generatedMesh != null)
            generatedMesh.Clear();

        if (meshFilter != null)
            meshFilter.sharedMesh =
                generatedMesh;

        MeshCollider collider =
            GetComponent<MeshCollider>();

        if (collider != null)
            collider.sharedMesh = null;
    }

    private void UpdateCollider()
    {
        if (!generateCollider)
            return;

        MeshCollider collider =
            GetComponent<MeshCollider>();

        if (collider == null)
        {
            collider =
                gameObject.AddComponent<MeshCollider>();
        }

        collider.sharedMesh = null;
        collider.sharedMesh = generatedMesh;
    }

    private void OnValidate()
    {
        dirty = true;
    }

    private void OnDestroy()
    {
        if (generatedMesh == null)
            return;

        if (Application.isPlaying)
            Destroy(generatedMesh);
        else
            DestroyImmediate(generatedMesh);
    }
}
