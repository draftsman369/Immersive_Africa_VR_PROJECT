using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
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

    [Header("Target Visuals")]
    [Tooltip("Prefab to show the current target point (e.g. a circle decal).")]
    [SerializeField] private GameObject destinationMarkerPrefab;

    [Tooltip("Vertical offset for the marker to avoid z-fighting.")]
    [SerializeField] private float markerYOffset = 0.02f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private GameObject destinationMarkerInstance;

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

        // Create marker instance if prefab is assigned
        if (destinationMarkerPrefab != null && destinationMarkerInstance == null)
        {
            destinationMarkerInstance = Instantiate(destinationMarkerPrefab);
            destinationMarkerInstance.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (vrClickAction.action != null)
        {
            vrClickAction.action.Enable();
            vrClickAction.action.performed += OnVrClickPerformed;
        }
        else if (debugLogs)
        {
            Debug.LogWarning("[PlayerController] vrClickAction.action is NULL. Assign an InputAction in the inspector.");
        }
    }

    private void OnDisable()
    {
        if (vrClickAction.action != null)
        {
            vrClickAction.action.performed -= OnVrClickPerformed;
            vrClickAction.action.Disable();
        }
    }

    private void Update()
    {
        HandleMouseClick();
        UpdateAimVisuals();    // marker follows aim (mouse or VR)
        UpdateMovementAnimation();
        UpdateRotation();
    }

    // ---------------------- INPUT ----------------------

    private void HandleMouseClick()
    {
        if (!enableMouseClick) return;
        if (mainCamera == null) return;
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (!TryGetMouseRay(out Ray ray))
                return;

            if (debugLogs)
                Debug.Log("[PlayerController] Mouse click raycast.");

            TryMoveAgentWithRay(ray);
        }
    }

    private void OnVrClickPerformed(InputAction.CallbackContext ctx)
    {
        if (!TryGetVrRay(out Ray ray))
        {
            if (debugLogs)
                Debug.LogWarning("[PlayerController] VR click performed but vrController is NULL.");
            return;
        }

        if (debugLogs)
        {
            Debug.Log("[PlayerController] VR trigger performed. Casting ray from controller.");
            Debug.DrawRay(vrController.position, vrController.forward * maxRayDistance, Color.green, 1f);
        }

        TryMoveAgentWithRay(ray);
    }

    // ---------------------- RAY HELPERS ----------------------

    private bool TryGetVrRay(out Ray ray)
    {
        if (vrController == null)
        {
            ray = default;
            return false;
        }

        ray = new Ray(vrController.position, vrController.forward);
        return true;
    }

    private bool TryGetMouseRay(out Ray ray)
    {
        if (mainCamera == null || Mouse.current == null)
        {
            ray = default;
            return false;
        }

        Vector2 screenPos = Mouse.current.position.ReadValue();
        ray = mainCamera.ScreenPointToRay(screenPos);
        return true;
    }

    // ---------------------- AIM VISUALS (HOVER MARKER) ----------------------

    private void UpdateAimVisuals()
    {
        Ray ray;
        bool hasRay = false;

        // EDITOR / DESKTOP: prefer mouse if enabled
        if (enableMouseClick && TryGetMouseRay(out ray))
        {
            hasRay = true;
        }
        // VR: fallback to controller ray
        else if (TryGetVrRay(out ray))
        {
            hasRay = true;
        }

        if (!hasRay)
        {
            SetMarkerActive(false);
            return;
        }

        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, groundLayers, QueryTriggerInteraction.Ignore))
        {
            SetMarkerActive(true, hit.point);
        }
        else
        {
            SetMarkerActive(false);
        }
    }

    private void SetMarkerActive(bool active, Vector3 hitPoint = default)
    {
        if (destinationMarkerInstance == null)
            return;

        destinationMarkerInstance.SetActive(active);

        if (!active)
            return;

        Vector3 pos = hitPoint;
        pos.y += markerYOffset;
        destinationMarkerInstance.transform.position = pos;
    }

    // ---------------------- MOVEMENT & ANIM ----------------------

    private void TryMoveAgentWithRay(Ray ray)
    {
        if (agent == null) return;

        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, groundLayers, QueryTriggerInteraction.Ignore))
        {
            if (debugLogs)
                Debug.Log($"[PlayerController] Ray hit {hit.collider.name} at {hit.point}");

            if (NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, navMeshSampleRadius, NavMesh.AllAreas))
            {
                agent.SetDestination(navHit.position);

                if (debugLogs)
                    Debug.Log($"[PlayerController] Moving to NavMesh position {navHit.position}");
            }
            else if (debugLogs)
            {
                Debug.LogWarning("[PlayerController] Ray hit, but no NavMesh near hit point.");
            }
        }
        else if (debugLogs)
        {
            Debug.LogWarning("[PlayerController] Raycast from click/VR did not hit anything on groundLayers.");
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

        float normalizedSpeed = 0f;
        if (agent.speed > 0.01f)
            normalizedSpeed = Mathf.Clamp01(rawSpeed / agent.speed);

        animController.SetMoving(isMoving, normalizedSpeed);
    }

    private void UpdateRotation()
    {
        if (agent == null) return;

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