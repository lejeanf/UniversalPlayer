using UnityEngine;

[RequireComponent(typeof(Animator))]
public class Hand : MonoBehaviour
{
    private Animator animator;
    private float gripTarget;
    private float triggerTarget;
    private float gripCurrent;
    private float triggerCurrent;
    [SerializeField] private float speed = 10.0f;
    
    private string animatorGripParam = "Grip";
    private string animatorTriggerParam = "Trigger";
    
    private void Start()
    {
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        AnimateHand();
    }

    public void SetGrip(float value)
    {
        gripTarget = value;
    }

    public void SetTrigger(float readValue)
    {
        triggerTarget = readValue;
    }

    private void AnimateHand()
    {
        if (triggerCurrent != triggerTarget)
        {
            triggerCurrent = Mathf.MoveTowards(triggerCurrent, triggerTarget, Time.deltaTime * speed);
            animator.SetFloat(animatorTriggerParam, triggerCurrent);
        }
        
        if (gripCurrent != gripTarget)
        {
            gripCurrent = Mathf.MoveTowards(gripCurrent, gripTarget, Time.deltaTime * speed);
            animator.SetFloat(animatorGripParam, gripCurrent);
        }
    }
}
