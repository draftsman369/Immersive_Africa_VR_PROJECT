using UnityEngine;

[DisallowMultipleComponent]
public class CharacterAnimationController : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Header("Mode")]
    [Tooltip("If true, use a single locomotion state with a blend tree driven by Speed.")]
    [SerializeField] private bool useBlendTreeLocomotion = true;

    [Tooltip("Name of the locomotion state that contains the blend tree (e.g. 'Locomotion' or 'Base Layer.Locomotion').")]
    [SerializeField] private string locomotionStateName = "Locomotion";

    [Header("Movement States (non-blend-tree mode)")]
    [Tooltip("State name for idle (e.g. 'Idle' or 'Base Layer.Idle'). Only used if blend tree mode is OFF.")]
    [SerializeField] private string idleStateName = "Idle";

    [Tooltip("State name for walking (e.g. 'Walk' or 'Base Layer.Walk'). Only used if blend tree mode is OFF.")]
    [SerializeField] private string walkStateName = "Walk";

    [Header("Parameters")]
    [Tooltip("Float parameter to drive blend trees (e.g. 'Speed'). Leave empty if not used.")]
    [SerializeField] private string speedParameter = "Speed";

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
    /// speed is typically normalized (0–1) if using a blend tree.
    /// </summary>
    public void SetMoving(bool isMoving, float speed = 0f)
    {
        if (animator == null) return;

        // Always update Speed if available
        if (hasSpeedParam)
        {
            animator.SetFloat(speedParamHash, speed);
        }

        // ---------------- BLEND TREE LOCOMOTION MODE ----------------
        if (useBlendTreeLocomotion)
        {
            if (string.IsNullOrEmpty(locomotionStateName))
                return;

            // We only need to ensure we're in the locomotion state.
            // Idle/Walk/Run are handled inside the blend tree via Speed.
            if (currentState == locomotionStateName)
                return;

            animator.CrossFade(locomotionStateName, crossFadeDuration);
            currentState = locomotionStateName;
            return;
        }

        // ---------------- SIMPLE IDLE/WALK MODE ----------------
        string targetState = isMoving ? walkStateName : idleStateName;
        if (string.IsNullOrEmpty(targetState)) return;

        if (currentState == targetState) return;

        animator.CrossFade(targetState, crossFadeDuration);
        currentState = targetState;
    }

    /// <summary>
    /// Generic helper if you want to play other animations later (attacks, emotes, etc).
    /// This will override locomotion temporarily.
    /// </summary>
    public void PlayState(string stateName, float customCrossFade = -1f)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return;

        float duration = customCrossFade >= 0f ? customCrossFade : crossFadeDuration;
        animator.CrossFade(stateName, duration);
        currentState = stateName;
    }
}