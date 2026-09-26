using UnityEngine;

public class ML : MonoBehaviour
{
    public float mouseSensitivity = 200f;

    public Transform playerBody;

    float xRotation = 0f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;

        Cursor.visible = true;
    }

    void Update()
    {
        // 只有按住滑鼠左鍵才可以轉動鏡頭
        if (Input.GetMouseButton(0))
        {
            float mouseX =
                Input.GetAxis("Mouse X")
                * mouseSensitivity
                * Time.deltaTime;

            float mouseY =
                Input.GetAxis("Mouse Y")
                * mouseSensitivity
                * Time.deltaTime;

            // 上下觀看
            xRotation -= mouseY;

            // 限制上下觀看角度
            xRotation = Mathf.Clamp(
                xRotation,
                -90f,
                90f
            );

            // 攝影機只負責上下旋轉
            transform.localRotation =
                Quaternion.Euler(xRotation, 0f, 0f);

            // 人物負責左右旋轉
            if (playerBody != null)
            {
                playerBody.Rotate(
                    Vector3.up * mouseX
                );
            }
        }
    }
}