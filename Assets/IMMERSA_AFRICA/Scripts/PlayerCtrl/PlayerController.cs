using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;          // NEW INPUT SYSTEM
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

        bool hasPath = agent.hasPath && agent.remainingDistance > agent.stoppingDistance + 0.05f;
        bool isMoving = hasPath && agent.velocity.sqrMagnitude > 0.001f;
        float speed = agent.velocity.magnitude;

        animController.SetMoving(isMoving, speed);
    }
}