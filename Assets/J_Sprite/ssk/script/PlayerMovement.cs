using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float runSpeed = 10f;

    public Transform playerCamera;

    void Update()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // 取得鏡頭的前方及右方
        Vector3 forward = playerCamera.forward;
        Vector3 right = playerCamera.right;

        // 移除上下方向，避免人物飛起來
        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        // 計算移動方向
        Vector3 move =
            right * horizontal +
            forward * vertical;

        move = Vector3.ClampMagnitude(move, 1f);

        float currentSpeed = moveSpeed;

        if (Input.GetKey(KeyCode.LeftShift))
        {
            currentSpeed = runSpeed;
        }

        transform.position +=
            move * currentSpeed * Time.deltaTime;
    }
}