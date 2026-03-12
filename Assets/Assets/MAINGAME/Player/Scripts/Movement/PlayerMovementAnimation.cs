using UnityEngine;

public class PlayerMovementAnimation
{
    private readonly PlayerMovement owner;

    public PlayerMovementAnimation(PlayerMovement owner)
    {
        this.owner = owner;
    }

    public void Tick()
    {
        if (owner == null || owner.Agent == null) return;

        FaceMovement();
        UpdateAnimator();
    }

    private void FaceMovement()
    {
        if (!owner.RotateToMovement) return;
        if (owner.Agent.isStopped) return;

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

    private void UpdateAnimator()
    {
        if (owner.Animator == null) return;

        Vector3 velocity = owner.Agent.velocity;
        velocity.y = 0f;

        bool isActuallyMoving = !owner.Agent.isStopped && velocity.sqrMagnitude > 0.01f;
        float speed = isActuallyMoving ? velocity.magnitude : 0f;

        owner.Animator.SetFloat("Speed", speed);
        owner.Animator.SetBool("IsMoving", isActuallyMoving);

        bool isCarrying = GetIsCarryingForThisOwner();
        owner.Animator.SetBool(owner.CarryingBoolParam, isCarrying);
    }

    private bool GetIsCarryingForThisOwner()
    {
        var hands = WaiterHands.Instance;
        if (hands == null) return false;

        if (!owner.IsActiveControlledRole())
            return false;

        return hands.HasTray || hands.HasBill || hands.HasMoney;
    }
}