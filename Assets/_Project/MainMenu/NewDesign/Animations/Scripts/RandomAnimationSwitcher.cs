using UnityEngine;

public class RandomAnimationSwitcher : MonoBehaviour
{
    private Animator animator;
    
    
    public int totalAnimations = 3; 
    public float switchInterval = 5f;

    private void Start()
    {
        animator = GetComponent<Animator>();
        InvokeRepeating(nameof(ChangeAnimation), 0f, switchInterval);
    }

    private void ChangeAnimation()
    {
        // Random.Range(0, 3) will return 0, 1, or 2
        int randomIndex = Random.Range(0, totalAnimations);
        
        animator.SetInteger("AnimationIndex", randomIndex);
    }
}