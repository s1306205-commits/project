using UnityEngine;

[RequireComponent(typeof(CharacterController))]

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]

    public float moveSpeed = 5f;

    public float runSpeed = 10f;

    public float gravity = -9.81f;


    [Header("Camera")]

    public Transform playerCamera;


    private CharacterController controller;

    private float verticalVelocity;


    void Start()
    {
        controller = GetComponent<CharacterController>();
    }


    void Update()
    {
        float horizontal = Input.GetAxis("Horizontal");

        float vertical = Input.GetAxis("Vertical");


        // 取得鏡頭的前方及右方

        Vector3 forward = playerCamera.forward;

        Vector3 right = playerCamera.right;


        // 移除上下方向

        forward.y = 0f;

        right.y = 0f;


        forward.Normalize();

        right.Normalize();


        // 計算移動方向

        Vector3 move =
            right * horizontal +
            forward * vertical;


        move = Vector3.ClampMagnitude(move, 1f);


        // 一般走路速度

        float currentSpeed = moveSpeed;


        // Shift 奔跑

        if (Input.GetKey(KeyCode.LeftShift))
        {
            currentSpeed = runSpeed;
        }


        // 重力

        if (controller.isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += gravity * Time.deltaTime;


        // 水平移動與重力

        Vector3 velocity =
            move * currentSpeed +
            Vector3.up * verticalVelocity;
        controller.Move(
            velocity * Time.deltaTime
        );
    }
}