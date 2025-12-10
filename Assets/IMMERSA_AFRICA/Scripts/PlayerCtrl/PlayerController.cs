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

    [Header("XR Hands Poke")]
    [Tooltip("Right-hand index fingertip transform (from XR Hands rig).")]
    [SerializeField] private Transform rightIndexTip;

    [Tooltip("Left-hand index fingertip transform (optional).")]
    [SerializeField] private Transform leftIndexTip;

    [Tooltip("Enable hand poke navigation using XR Hands.")]
    [SerializeField] private bool useHandPokeForVR = true;

    [Tooltip("Length of the ray cast downward from the fingertip.")]
    [SerializeField] private float handRayLength = 0.20f;

    [Tooltip("Max distance from fingertip to ground to count as a valid 'touch'.")]
    [SerializeField] private float maxHandPokeDistance = 0.05f;

    [Tooltip("Minimum downward speed (m/s) to register a 'tap' when touching the ground.")]
    [SerializeField] private float minDownwardSpeedForPoke = 0.2f;

    [Tooltip("Cooldown between pokes, to avoid spam (seconds).")]
    [SerializeField] private float pokeCooldown = 0.3f;

    [Header("Input Settings (New Input System)")]
    [Tooltip("Use mouse left-click to move in Editor/Desktop (New Input System only).")]
    [SerializeField] private bool enableMouseClick = true;

    [Tooltip("Input Action used for VR trigger with controllers (NOT used for hand poke).")]
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

    // Hand poke state
    private Vector3 _lastRightTipPos;
    private Vector3 _lastLeftTipPos;
    private bool _rightWasTouchingLastFrame;
    private bool _leftWasTouchingLastFrame;
    private float _lastPokeTime;

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

        // Initialize last hand positions so velocity doesn't spike on first frame
        if (rightIndexTip != null) _lastRightTipPos = rightIndexTip.position;
        if (leftIndexTip != null)  _lastLeftTipPos  = leftIndexTip.position;
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
            Debug.LogWarning("[PlayerController] vrClickAction.action is NULL. Assign an InputAction in the inspector if you use controllers.");
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
        UpdateHandPoke();      // NEW: handle finger tapping ground
        UpdateAimVisuals();    // marker follows aim (mouse / finger / VR)
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

    // This is now only for controller-based click.
    private void OnVrClickPerformed(InputAction.CallbackContext ctx)
    {
        if (useHandPokeForVR)
        {
            // When using hand poke, we don't need this for movement.
            // You can either ignore it, or keep it as a fallback for controllers.
            if (debugLogs)
                Debug.Log("[PlayerController] VR click performed, but hand poke is primary. Controller click fallback only.");
        }

        if (!TryGetVrRay(out Ray ray))
        {
            if (debugLogs)
                Debug.LogWarning("[PlayerController] VR click performed but vrController is NULL.");
            return;
        }

        if (debugLogs && vrController != null)
        {
            Debug.Log("[PlayerController] VR trigger performed. Casting ray from controller.");
            Debug.DrawRay(vrController.position, vrController.forward * maxRayDistance, Color.green, 1f);
        }

        TryMoveAgentWithRay(ray);
    }

    // ---------------------- XR HAND POKE ----------------------

    private void UpdateHandPoke()
    {
        if (!useHandPokeForVR || agent == null)
            return;

        if (Time.deltaTime <= 0f)
            return;

        // Process each hand; whichever successfully pokes first wins.
        bool rightPoked = ProcessFingerPoke(
            rightIndexTip,
            ref _lastRightTipPos,
            ref _rightWasTouchingLastFrame
        );

        if (rightPoked) return;

        bool leftPoked = ProcessFingerPoke(
            leftIndexTip,
            ref _lastLeftTipPos,
            ref _leftWasTouchingLastFrame
        );
    }

    /// <summary>
    /// Handles poke detection for a single fingertip:
    ///  - Raycast down from fingertip
    ///  - If just started touching & moving downward fast enough & cooldown passed -> move agent.
    /// </summary>
    private bool ProcessFingerPoke(Transform fingerTip, ref Vector3 lastPos, ref bool wasTouchingLastFrame)
    {
        if (fingerTip == null)
            return false;

        Vector3 currentPos = fingerTip.position;
        Vector3 velocity = (currentPos - lastPos) / Time.deltaTime;
        lastPos = currentPos;

        Vector3 origin = currentPos;
        Vector3 direction = Vector3.down;

        if (debugLogs)
            Debug.DrawRay(origin, direction * handRayLength, Color.cyan, 0.1f);

        bool isTouching = false;
        Vector3 hitPoint = default;
        float hitDistance = 0f;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, handRayLength, groundLayers, QueryTriggerInteraction.Ignore))
        {
            hitDistance = hit.distance;

            if (hit.distance <= maxHandPokeDistance)
            {
                isTouching = true;
                hitPoint = hit.point;

                if (debugLogs)
                    Debug.Log($"[PlayerController] Finger touching {hit.collider.name} at {hit.point} (dist={hit.distance})");
            }
        }

        bool justTouched = isTouching && !wasTouchingLastFrame;
        wasTouchingLastFrame = isTouching;

        if (!justTouched)
            return false;

        // Cooldown so every tiny tap isn't multiple moves
        if (Time.time - _lastPokeTime < pokeCooldown)
            return false;

        // Require downward motion to feel like an intentional tap
        if (velocity.y >= -minDownwardSpeedForPoke)
        {
            if (debugLogs)
                Debug.Log($"[PlayerController] Touch ignored (downward speed too low: {velocity.y}).");
            return false;
        }

        if (debugLogs)
            Debug.Log($"[PlayerController] POKE! Moving to {hitPoint}, velocityY={velocity.y}");

        _lastPokeTime = Time.time;
        TryMoveAgentToPoint(hitPoint);
        return true;
    }

    /// <summary>
    /// For marker hover: get the closest ground point under either fingertip.
    /// </summary>
    private bool TryGetHandHoverGroundPoint(out Vector3 groundPoint)
    {
        groundPoint = default;

        bool found = false;
        float bestDist = float.MaxValue;
        Vector3 bestPoint = default;

        CheckFingerHover(rightIndexTip, ref found, ref bestDist, ref bestPoint);
        CheckFingerHover(leftIndexTip, ref found, ref bestDist, ref bestPoint);

        if (found)
        {
            groundPoint = bestPoint;
            return true;
        }

        return false;
    }

    private void CheckFingerHover(Transform fingerTip, ref bool found, ref float bestDist, ref Vector3 bestPoint)
    {
        if (fingerTip == null)
            return;

        Vector3 origin = fingerTip.position;
        Vector3 direction = Vector3.down;

        if (debugLogs)
            Debug.DrawRay(origin, direction * handRayLength, Color.yellow, 0.1f);

        if (Physics.Raycast(origin, direction, out RaycastHit hit, handRayLength, groundLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.distance < bestDist)
            {
                found = true;
                bestDist = hit.distance;
                bestPoint = hit.point;
            }
        }
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
        // 1) XR Hands hover under fingertip
        if (useHandPokeForVR && TryGetHandHoverGroundPoint(out Vector3 handHoverPoint))
        {
            SetMarkerActive(true, handHoverPoint);
            return;
        }

        // 2) Fallback: mouse / VR controller ray
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

            TryMoveAgentToPoint(hit.point);
        }
        else if (debugLogs)
        {
            Debug.LogWarning("[PlayerController] Raycast from click/VR did not hit anything on groundLayers.");
        }
    }

    private void TryMoveAgentToPoint(Vector3 worldPoint)
    {
        if (agent == null) return;

        if (NavMesh.SamplePosition(worldPoint, out NavMeshHit navHit, navMeshSampleRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(navHit.position);

            if (debugLogs)
                Debug.Log($"[PlayerController] Moving to NavMesh position {navHit.position}");

            // Optionally keep marker snapped on final destination
            SetMarkerActive(true, navHit.position);
        }
        else if (debugLogs)
        {
            Debug.LogWarning("[PlayerController] Hit point is not near any NavMesh.");
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