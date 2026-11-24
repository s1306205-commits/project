
using UnityEngine;

public class PandaAnimationController : MonoBehaviour
{
    public enum PandaState
    {
        Idle = 0,
        Run = 1
    }

    // =========================================================
    // References
    // =========================================================

    [Header("References")]

    [SerializeField]
    private Animator animator;

    [SerializeField]
    private VRPlayerMovementState playerMovement;


    // =========================================================
    // Rotation
    // =========================================================

    [Header("Rotation")]

    [SerializeField]
    private Transform pandaVisual;

    [SerializeField]
    private Transform playerCamera;

    [SerializeField]
    private float rotationSpeed = 5f;

    [SerializeField]
    private float rotationOffset = 0f;


    // =========================================================
    // Private
    // =========================================================

    private PandaState currentState = PandaState.Idle;


    // =========================================================
    // Start
    // =========================================================

    private void Start()
    {
        ForceAnimationState(PandaState.Idle);
    }


    // =========================================================
    // Update
    // =========================================================

    private void Update()
    {
        bool isMoving =
            playerMovement != null &&
            playerMovement.IsMoving;

        if (isMoving)
        {
            SetAnimationState(PandaState.Run);
        }
        else
        {
            SetAnimationState(PandaState.Idle);
        }
    }


    // =========================================================
    // Late Update - Rotation
    // =========================================================

    private void LateUpdate()
    {
        if (pandaVisual == null || playerCamera == null)
            return;

        Vector3 direction;

        bool isMoving =
            playerMovement != null &&
            playerMovement.IsMoving;

        if (isMoving)
        {
            // 玩家移動時，熊貓朝玩家前方
            direction = playerCamera.forward;
        }
        else
        {
            // 玩家停止時，熊貓轉回面向玩家
            direction =
                playerCamera.position -
                pandaVisual.position;
        }

        // 只允許水平旋轉
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(
                direction.normalized,
                Vector3.up
            );

        // 修正模型原本的正面方向
        targetRotation *= Quaternion.Euler(
            0f,
            rotationOffset,
            0f
        );

        // 平滑旋轉
        pandaVisual.rotation = Quaternion.Slerp(
            pandaVisual.rotation,
            targetRotation,
            Mathf.Clamp01(
                rotationSpeed * Time.deltaTime
            )
        );
    }


    // =========================================================
    // Set Animation
    // =========================================================

    private void SetAnimationState(PandaState newState)
    {
        if (currentState == newState)
            return;

        currentState = newState;

        if (animator != null)
        {
            animator.SetInteger(
                "State",
                (int)newState
            );
        }

        Debug.Log(
            "🐼 Panda Animation → " + newState
        );
    }


    // =========================================================
    // Force Animation
    // =========================================================

    private void ForceAnimationState(PandaState newState)
    {
        currentState = newState;

        if (animator != null)
        {
            animator.SetInteger(
                "State",
                (int)newState
            );
        }
    }
}
