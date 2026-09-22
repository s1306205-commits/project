using UnityEngine;

public class WaterMovement : MonoBehaviour
{
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

        waterMaterial.SetTextureOffset("_BumpMap", offset);
    }
}