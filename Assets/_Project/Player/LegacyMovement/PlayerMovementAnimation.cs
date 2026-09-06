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

        // Animation can tick before navigation attaches, or while a role is disabled.
        // Do not read isStopped until the agent is active and on the baked mesh.
        if (!owner.Agent.isActiveAndEnabled || !owner.Agent.isOnNavMesh)
        {
            if (owner.Animator != null)
            {
                owner.Animator.SetFloat("Speed", 0f);
                owner.Animator.SetBool("IsMoving", false);
            }
            return;
        }

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
        if (owner.GetComponent<ManagerPlayer>() == null && !owner.IsActiveControlledRole())
            return false;

        var hands = WaiterHands.For(owner);
        if (hands == null) return false;

        // Bills and money are small hand props. Only trays use the dedicated
        // carry idle/walk animation set.
        if (hands.HasTray)
            return true;

        BusserHands busserHands = BusserHands.For(owner);
        return busserHands != null && busserHands.HasTray;
    }
}
