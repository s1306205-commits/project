using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class WaterMovement : MonoBehaviour
{
    [Header("Water Flow")]

    public float speedX = 0.02f;

    public float speedY = 0.01f;

    private Material waterMaterial;

    private Vector2 offset;

    void Start()
    {
        Renderer rend = GetComponent<Renderer>();

        waterMaterial = rend.material;

        offset = waterMaterial.GetTextureOffset("_BumpMap");
    }

    void Update()
    {
        offset.x += speedX * Time.deltaTime;

        offset.y += speedY * Time.deltaTime;

        // 移動水面 Normal Map
        waterMaterial.SetTextureOffset(
            "_BumpMap",
            offset
        );

        // 移動主要水面貼圖
        waterMaterial.SetTextureOffset(
            "_MainTex",
            offset
        );
    }
}