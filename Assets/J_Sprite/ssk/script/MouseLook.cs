using UnityEngine;

public class MouseLook : MonoBehaviour
{
    public float mouseSensitivity = 200f;

    float xRotation = 0f;
    float yRotation = 0f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        xRotation = transform.localEulerAngles.x;
        yRotation = transform.localEulerAngles.y;
    }

    void Update()
    {
        // 只有按住滑鼠左鍵才可以轉動鏡頭
        if (Input.GetMouseButton(0))
        {
            float mouseX = Input.GetAxis("Mouse X")
                           * mouseSensitivity
                           * Time.deltaTime;

            float mouseY = Input.GetAxis("Mouse Y")
                           * mouseSensitivity
                           * Time.deltaTime;

            // 上下觀看
            xRotation -= mouseY;

            // 左右觀看
            yRotation += mouseX;

            // 限制上下觀看角度
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);

            // 只旋轉 Camera，不旋轉 Player
            transform.localRotation =
                Quaternion.Euler(xRotation, yRotation, 0f);
        }
    }
}