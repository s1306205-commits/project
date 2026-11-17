
using UnityEngine;

public class DogAnimationController : MonoBehaviour
{
    public enum DogState
    {
        Idle = 0,
        Run = 1
    }

    [Header("References")]
    [SerializeField] private Animator animator;

    [SerializeField]
    private VRPlayerMovementState playerMovement;

    [Header("Rotation")]
    [SerializeField] private Transform dogVisual;

    [SerializeField]
    private Transform playerCamera;

    [SerializeField]
    private float rotationSpeed = 5f;

    [SerializeField]
    private float rotationOffset = 0f;

    private DogState currentState = DogState.Idle;

    private void Start()
    {
        ForceAnimationState(DogState.Idle);
    }

    private void Update()
    {
        bool isMoving =
            playerMovement != null &&
            playerMovement.IsMoving;

        if (isMoving)
        {
            SetAnimationState(DogState.Run);
        }
        else
        {
            SetAnimationState(DogState.Idle);
        }
    }

    private void LateUpdate()
    {
        if (dogVisual == null || playerCamera == null)
            return;

        Vector3 direction;

        bool isMoving =
            playerMovement != null &&
            playerMovement.IsMoving;

        if (isMoving)
        {
            // 移動時朝玩家的水平前方
            direction = playerCamera.forward;
        }
        else
        {
            // 待機時面向玩家
            direction =
                playerCamera.position -
                dogVisual.position;
        }

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(direction.normalized);

        targetRotation *= Quaternion.Euler(
            0f, rotationOffset, 0f
        );

        dogVisual.rotation = Quaternion.Slerp(
            dogVisual.rotation,
            targetRotation,
            Mathf.Clamp01(rotationSpeed * Time.deltaTime)
        );
    }

    private void SetAnimationState(DogState newState)
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

        Debug.Log("🐶 Dog Animation → " + newState);
    }

    private void ForceAnimationState(DogState newState)
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
