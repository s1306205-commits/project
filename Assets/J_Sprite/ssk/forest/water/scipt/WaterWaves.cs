using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class WaterWaves : MonoBehaviour
{
    [Header("Main Wave")]
    public float waveHeight = 0.015f;
    public float waveSpeed = 0.8f;
    public float waveFrequency = 1.5f;

    [Header("Small Wave")]
    public float smallWaveHeight = 0.005f;
    public float smallWaveFrequency = 4f;
    public float smallWaveSpeed = 1.2f;

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
        float time = Time.time;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertex = originalVertices[i];

            // 第一層：主要水波
            float wave1 = Mathf.Sin(
                vertex.x * waveFrequency +
                time * waveSpeed
            ) * waveHeight;

            // 第二層：不同方向的水波
            float wave2 = Mathf.Cos(
                vertex.z * waveFrequency * 0.8f +
                time * waveSpeed * 0.7f
            ) * waveHeight * 0.5f;

            // 第三層：細小水波
            float wave3 = Mathf.Sin(
                (vertex.x + vertex.z) *
                smallWaveFrequency +
                time * smallWaveSpeed
            ) * smallWaveHeight;

            vertex.y =
                originalVertices[i].y +
                wave1 +
                wave2 +
                wave3;

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