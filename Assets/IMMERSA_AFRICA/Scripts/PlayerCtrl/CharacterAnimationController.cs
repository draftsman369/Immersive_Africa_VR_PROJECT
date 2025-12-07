using UnityEngine;

[DisallowMultipleComponent]
public class CharacterAnimationController : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Header("Movement States")]
    [Tooltip("State name for idle (e.g. 'Idle' or 'Base Layer.Idle').")]
    [SerializeField] private string idleStateName = "Idle";

    [Tooltip("State name for walking (e.g. 'Walk' or 'Base Layer.Walk').")]
    [SerializeField] private string walkStateName = "Walk";

    [Tooltip("Optional float parameter to drive blend trees (e.g. 'Speed'). Leave empty if not used.")]
    [SerializeField] private string speedParameter = "";

    [Tooltip("Crossfade time for switching between states.")]
    [SerializeField] private float crossFadeDuration = 0.1f;

    private string currentState;
    private int speedParamHash = -1;
    private bool hasSpeedParam;

    private void Reset()
    {
        animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        CacheParameters();
    }

    private void CacheParameters()
    {
        if (animator == null) return;

        if (!string.IsNullOrEmpty(speedParameter))
        {
            speedParamHash = Animator.StringToHash(speedParameter);
            hasSpeedParam = AnimatorHasParameter(speedParamHash, AnimatorControllerParameterType.Float);
        }
        else
        {
            hasSpeedParam = false;
        }
    }

    private bool AnimatorHasParameter(int hash, AnimatorControllerParameterType type)
    {
        foreach (var p in animator.parameters)
        {
            if (p.type == type && p.nameHash == hash)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Call this every frame to update movement animation.
    /// </summary>
    public void SetMoving(bool isMoving, float speed = 0f)
    {
        if (animator == null) return;

        string targetState = isMoving ? walkStateName : idleStateName;
        if (string.IsNullOrEmpty(targetState)) return;

        // Update speed param for blend tree, if configured.
        if (hasSpeedParam)
        {
            animator.SetFloat(speedParamHash, speed);
        }

        if (currentState == targetState) return;

        animator.CrossFade(targetState, crossFadeDuration);
        currentState = targetState;
    }

    /// <summary>
    /// Generic helper if you want to play other animations later (attacks, emotes, etc).
    /// </summary>
    public void PlayState(string stateName, float customCrossFade = -1f)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return;

        float duration = customCrossFade >= 0f ? customCrossFade : crossFadeDuration;
        animator.CrossFade(stateName, duration);
        currentState = stateName;
    }
}