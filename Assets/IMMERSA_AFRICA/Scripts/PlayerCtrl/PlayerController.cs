using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;          // New Input System
using UnityEngine.InputSystem.XR;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class PlayerController : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private CharacterAnimationController animController;

    [Header("Ray Sources")]
    [Tooltip("Camera used for mouse clicks (for testing in Editor).")]
    [SerializeField] private Camera mainCamera;

    [Tooltip("Transform of the VR controller that will cast the ray (e.g. right-hand controller).")]
    [SerializeField] private Transform vrController;

    [Header("Input Settings (New Input System)")]
    [Tooltip("Use mouse left-click to move in Editor/Desktop (New Input System only).")]
    [SerializeField] private bool enableMouseClick = true;

    [Tooltip("Input Action used for the VR trigger (Button action bound to your controller trigger).")]
    [SerializeField] private InputActionProperty vrClickAction;

    [Header("Raycast Settings")]
    [Tooltip("Max distance the ray will check for the ground.")]
    [SerializeField] private float maxRayDistance = 100f;

    [Tooltip("Layers that represent the walkable ground / geometry above the NavMesh.")]
    [SerializeField] private LayerMask groundLayers = ~0;

    [Tooltip("How far from the hit point we are allowed to search for a valid NavMesh position.")]
    [SerializeField] private float navMeshSampleRadius = 1.0f;

    [Header("Rotation")]
    [Tooltip("How fast the character rotates toward movement direction (degrees per second).")]
    [SerializeField] private float rotationSpeed = 720f;

    [Tooltip("Only rotate when the character is actually moving.")]
    [SerializeField] private bool rotateOnlyWhenMoving = true;

    private void Reset()
    {
        agent = GetComponent<NavMeshAgent>();
        animController = GetComponent<CharacterAnimationController>();
        mainCamera = Camera.main;
    }

    private void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (animController == null)
            animController = GetComponent<CharacterAnimationController>();

        if (mainCamera == null)
            mainCamera = Camera.main;

        // We handle rotation manually for snappier control
        agent.updateRotation = false;
    }

    private void OnEnable()
    {
        // Enable the VR trigger action if assigned
        if (vrClickAction.action != null)
            vrClickAction.action.Enable();
    }

    private void OnDisable()
    {
        if (vrClickAction.action != null)
            vrClickAction.action.Disable();
    }

    private void Update()
    {
        HandleMouseClick();
        HandleVRClick();
        UpdateMovementAnimation();
        UpdateRotation();
    }

    // ---------------------- INPUT ----------------------

    private void HandleMouseClick()
    {
        if (!enableMouseClick) return;
        if (mainCamera == null) return;

        // New Input System mouse
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 screenPos = Mouse.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(screenPos);
            TryMoveAgentWithRay(ray);
        }
    }

    private void HandleVRClick()
    {
        if (vrController == null) return;
        if (vrClickAction.action == null) return;

        // For a Button-type action, `triggered` is true on the frame it performs.
        if (vrClickAction.action.triggered)
        {
            Ray ray = new Ray(vrController.position, vrController.forward);
            TryMoveAgentWithRay(ray);
        }
    }

    // ---------------------- MOVEMENT ----------------------

    private void TryMoveAgentWithRay(Ray ray)
    {
        if (agent == null) return;

        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, groundLayers, QueryTriggerInteraction.Ignore))
        {
            // Find closest point on NavMesh near the hit point
            if (NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, navMeshSampleRadius, NavMesh.AllAreas))
            {
                agent.SetDestination(navHit.position);
            }
            else
            {
                Debug.Log("[PlayerController] No NavMesh near hit point.");
            }
        }
    }

    private void UpdateMovementAnimation()
    {
        if (agent == null || animController == null) return;

        if (agent.pathPending)
        {
            animController.SetMoving(false, 0f);
            return;
        }

        // Planar speed
        Vector2 planarVel = new Vector2(agent.velocity.x, agent.velocity.z);
        float rawSpeed = planarVel.magnitude;

        const float startMoveSpeed = 0.05f;
        const float stopMoveSpeed  = 0.02f;
        const float stopBuffer     = 0.05f;

        bool hasFarDestination = agent.remainingDistance > agent.stoppingDistance + stopBuffer;

        bool isMoving = hasFarDestination && rawSpeed > startMoveSpeed;

        if (!hasFarDestination && rawSpeed < stopMoveSpeed)
        {
            isMoving = false;
        }

        // Normalize speed 0–1 based on agent.speed (max desired speed)
        float normalizedSpeed = 0f;
        if (agent.speed > 0.01f)
            normalizedSpeed = Mathf.Clamp01(rawSpeed / agent.speed);

        animController.SetMoving(isMoving, normalizedSpeed);
    }

    private void UpdateRotation()
    {
        if (agent == null) return;

        // Prefer actual velocity, fall back to desiredVelocity if needed
        Vector3 moveDir = agent.velocity;
        moveDir.y = 0f;

        if (moveDir.sqrMagnitude < 0.0001f && agent.hasPath)
        {
            Vector3 desired = agent.desiredVelocity;
            desired.y = 0f;
            if (desired.sqrMagnitude > 0.0001f)
                moveDir = desired;
        }

        bool hasDirection = moveDir.sqrMagnitude > 0.0001f;
        if (!hasDirection) return;

        if (rotateOnlyWhenMoving && agent.velocity.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRot = Quaternion.LookRotation(moveDir.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRot,
            rotationSpeed * Time.deltaTime
        );
    }
}