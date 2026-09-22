using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class WaterWaves : MonoBehaviour
{
    [Header("Wave Settings")]

    public float waveHeight = 0.15f;

    public float waveSpeed = 1.5f;

    public float waveFrequency = 1.0f;

    private Mesh mesh;

    private Vector3[] originalVertices;

    private Vector3[] vertices;

    void Start()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();

        mesh = Instantiate(meshFilter.sharedMesh);

        meshFilter.mesh = mesh;

        originalVertices = mesh.vertices;

        vertices = new Vector3[originalVertices.Length];
    }

    void Update()
    {
        float time = Time.time * waveSpeed;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertex = originalVertices[i];

            float wave1 = Mathf.Sin(
                vertex.x * waveFrequency + time
            );

            float wave2 = Mathf.Cos(
                vertex.z * waveFrequency + time * 0.7f
            );

            vertex.y =
                originalVertices[i].y +
                (wave1 + wave2) * waveHeight;

            vertices[i] = vertex;
        }

        mesh.vertices = vertices;

        mesh.RecalculateNormals();

        mesh.RecalculateBounds();
    }

    void OnDestroy()
    {
        if (mesh != null)
        {
            Destroy(mesh);
        }
    }
}
