using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PCDebugMovement : MonoBehaviour
{
    // =========================================================
    // References
    // =========================================================

    [Header("References")]

    // XR Main Camera
    public Transform playerCamera;


    // =========================================================
    // Movement
    // =========================================================

    [Header("Movement")]

    public bool enableKeyboardMovement = true;

    public float moveSpeed = 2f;

    public float runSpeed = 4f;

    public float turnSpeed = 90f;


    // =========================================================
    // Private
    // =========================================================

    private CharacterController controller;


    // =========================================================
    // Start
    // =========================================================

    private void Start()
    {
        controller =
            GetComponent<CharacterController>();
    }


    // =========================================================
    // Update
    // =========================================================

    private void Update()
    {
        if (!enableKeyboardMovement)
        {
            return;
        }


        if (
            controller == null ||
            playerCamera == null
        )
        {
            return;
        }


        // =====================================================
        // WASD
        // =====================================================

        float horizontal =
            Input.GetAxisRaw("Horizontal");

        float vertical =
            Input.GetAxisRaw("Vertical");


        // =====================================================
        // 以玩家目前視角方向前進
        // =====================================================

        Vector3 forward =
            playerCamera.forward;

        Vector3 right =
            playerCamera.right;


        forward.y = 0f;

        right.y = 0f;


        forward.Normalize();

        right.Normalize();


        Vector3 move =
            forward * vertical +
            right * horizontal;


        move =
            Vector3.ClampMagnitude(
                move,
                1f
            );


        // =====================================================
        // Shift 加速
        // =====================================================

        float speed =
            Input.GetKey(KeyCode.LeftShift)
            ? runSpeed
            : moveSpeed;


        controller.Move(
            move *
            speed *
            Time.deltaTime
        );


        // =====================================================
        // Q / E 轉向
        // =====================================================

        float turn = 0f;


        if (Input.GetKey(KeyCode.Q))
        {
            turn -= 1f;
        }


        if (Input.GetKey(KeyCode.E))
        {
            turn += 1f;
        }


        transform.Rotate(
            Vector3.up,
            turn *
            turnSpeed *
            Time.deltaTime
        );
    }
}