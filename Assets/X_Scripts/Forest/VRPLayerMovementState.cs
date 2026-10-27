using UnityEngine;

public class VRPlayerMovementState : MonoBehaviour
{
    // =========================================================
    // Settings
    // =========================================================

    [Header("Movement Detection")]

    // 每秒移動超過多少公尺才算真的在移動
    public float movementThreshold = 0.03f;


    // =========================================================
    // Private
    // =========================================================

    private Vector3 lastPosition;

    private Vector3 currentMoveDirection;

    private bool isMoving;


    // =========================================================
    // Public
    // =========================================================

    public bool IsMoving
    {
        get
        {
            return isMoving;
        }
    }


    public Vector3 MoveDirection
    {
        get
        {
            return currentMoveDirection;
        }
    }


    // =========================================================
    // Start
    // =========================================================

    private void Start()
    {
        lastPosition = transform.position;
    }


    // =========================================================
    // Late Update
    // =========================================================

    private void LateUpdate()
    {
        Vector3 currentPosition =
            transform.position;


        Vector3 movement =
            currentPosition -
            lastPosition;


        // 不把上下高度變化當成玩家走路
        movement.y = 0f;


        float speed =
            movement.magnitude /
            Mathf.Max(
                Time.deltaTime,
                0.0001f
            );


        isMoving =
            speed >
            movementThreshold;


        if (
            movement.sqrMagnitude >
            0.000001f
        )
        {
            currentMoveDirection =
                movement.normalized;
        }


        lastPosition =
            currentPosition;
    }
}