using UnityEngine;

public class PlayerMovementAnimation
{
    private const string IDLE = "idle";
    private const string RUNNING = "running";

    private readonly PlayerMovement owner;

    public PlayerMovementAnimation(PlayerMovement owner)
    {
        this.owner = owner;
    }

    public void Tick()
    {
        FaceMovement();
        UpdateCarryParams();
        SetNormalAnimation();
    }

    private void FaceMovement()
    {
        if (!owner.RotateToMovement) return;

        Vector3 velocity = owner.Agent.velocity;
        velocity.y = 0f;

        if (velocity.sqrMagnitude <= 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
        owner.transform.rotation = Quaternion.Slerp(
            owner.transform.rotation,
            targetRotation,
            Time.deltaTime * owner.RotationSpeed
        );
    }

    private void UpdateCarryParams()
    {
        if (owner.Animator == null) return;

        Vector3 velocity = owner.Agent.velocity;
        velocity.y = 0f;

        float speed = velocity.magnitude;
        bool isMoving = speed > 0.1f;
        bool isCarrying = WaiterHands.Instance != null &&
                          (WaiterHands.Instance.HasTray || WaiterHands.Instance.HasBill);

        owner.Animator.SetFloat("Speed", speed);
        owner.Animator.SetBool("IsMoving", isMoving);
        owner.Animator.SetBool("IsCarrying", isCarrying);
    }

    private void SetNormalAnimation()
    {
        if (owner.Animator == null) return;

        bool isCarrying = WaiterHands.Instance != null &&
                          (WaiterHands.Instance.HasTray || WaiterHands.Instance.HasBill);

        if (isCarrying) return;

        if (owner.Agent.velocity.sqrMagnitude <= 0.01f)
            owner.Animator.Play(IDLE);
        else
            owner.Animator.Play(RUNNING);
    }
}