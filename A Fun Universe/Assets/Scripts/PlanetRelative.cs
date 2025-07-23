using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlanetRelative : MonoBehaviour
{
    /* ───────── Inspector Tunables ─────────────────────────── */
    [Header("Speeds")]
    public float walkSpeed = 6f;
    public float hoverSpeed = 12f;
    public float radialSpeed = 25f;   // up/down in Flight
    public float jumpSpeed = 8f;    // vertical take-off in Walk
    public float planetGravity = 12f;   // m/s² toward centre
    public float mouseSense = 2f;

    [Header("Surface / Release")]
    public float surfaceClearance = 0.3f;  // height above mesh
    public float releaseMargin = 20f;   // added above radius to drop to free-fly

    [Header("Traversal limits")]
    [Range(0f, 89f)] public float maxWalkSlope = 50f;   // deg. > 50° = too steep
    [Range(0f, 3f)] public float wallCheckAhead = 2f; // metres to probe fwd

    /* ───────── Runtime state ───────────────────────────────── */
    enum Mode { Flight, Walk }
    Mode mode = Mode.Flight;

    Transform planet;
    float planetRadius;          // NOTE: half radius ⇒ we use *2f in maths
    Vector3 localOffset;
    float yaw, pitch;
    float verticalVel;           // used in Walk
    int planetMask;            // will be set in Awake()
    float groundSnapSpeed = 10f;   // metres per second that we can auto-rise
    Rigidbody rb;
    Camera cam;

    /* called by FreeFlyController */
    public void SetPlanet(Transform p, float outerRadius, float relMargin, float grav)
    {
        planet = p;
        planetRadius = outerRadius;
        releaseMargin = relMargin;
        planetGravity = grav;           // NEW
        localOffset = planet.InverseTransformPoint(transform.position);
        transform.up = (transform.position - planet.position).normalized;
    }
    /* ───────── Setup ───────────────────────────────────────── */
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        cam = GetComponentInChildren<Camera>();

        planetMask = LayerMask.GetMask("Planet");

        if (TryGetComponent(out MeshRenderer mr)) mr.enabled = false;

        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }
    void Start()
    {
        float h = localOffset.magnitude - planetRadius;
        Debug.Log($"[Capture]  {planet.name}  radius={planetRadius:F1}  height={h:F2} m");
    }
    /* ───────── Per-frame update ────────────────────────────── */
    void Update()
    {
        if (!planet) return;

        /* 1 ─ follow planet motion */
        transform.position = planet.TransformPoint(localOffset);
        Vector3 radialUp = (transform.position - planet.position).normalized;

        /* 2 ─ mouse look */
        float mx = Input.GetAxis("Mouse X") * mouseSense;
        float my = Input.GetAxis("Mouse Y") * mouseSense;
        yaw += mx;
        pitch = Mathf.Clamp(pitch - my, -89f, 89f);

        transform.RotateAround(transform.position, radialUp, mx);
        if (cam) cam.transform.localEulerAngles = new Vector3(pitch, 0, 0);
        transform.rotation =
            Quaternion.FromToRotation(transform.up, radialUp) * transform.rotation;

        /* 3 ─ movement by mode */
        if (mode == Mode.Flight) DoFlight(radialUp);
        else DoWalk(radialUp);

        /* 4 ─ keep localOffset current */
        localOffset = planet.InverseTransformPoint(transform.position);

        /* 5 ─ auto-release to free-fly */
        if (localOffset.magnitude > planetRadius + releaseMargin)
        {
            gameObject.AddComponent<FreeFlyController>();
            Destroy(this);
        }
    }
    /* ───────── Flight behaviour ────────────────────────────── */
    void DoFlight(Vector3 radialUp)
    {
        /* tangent WASD */
        Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0,
                                    Input.GetAxisRaw("Vertical")).normalized;
        Vector3 move = (transform.forward * input.z + transform.right * input.x)
                        * hoverSpeed * Time.deltaTime;

        /* vertical */
        if (Input.GetKey(KeyCode.LeftShift))
            move += -radialUp * radialSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.Space))
            move += radialUp * radialSpeed * Time.deltaTime;

        transform.position += move;

        /* terrain clamp – push up only (✱ sphere-cast ✱) */
        const float rayExtra = 5f;
        const float footRadius = 0.7f;        // roughly capsule radius
        Vector3 castStart = transform.position + radialUp * rayExtra;

        if (Physics.SphereCast(castStart, footRadius, -radialUp,
                           out RaycastHit hit, planetRadius * 2f, planetMask))
        {
            float groundDist = hit.distance - rayExtra;
            float offset = surfaceClearance - groundDist;  // + = too low
            if (offset > 0.01f)
            {
                float maxStep = groundSnapSpeed * Time.deltaTime;
                transform.position += radialUp * Mathf.Min(offset, maxStep);
                /* touched ground → switch to Walk */
                SwitchMode(Mode.Walk);
                verticalVel = 0f;
            }
        }
    }
    /* ───────── Walk / Jump behaviour ───────────────────────── */
    void DoWalk(Vector3 radialUp)
    {
        /* tangent WASD */
        Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0,
                                    Input.GetAxisRaw("Vertical")).normalized;

        Vector3 desiredMove = (transform.forward * input.z +
                           transform.right * input.x).normalized;

        bool wallTooSteep = false;
        if (desiredMove != Vector3.zero)
        {
            Vector3 castStart = transform.position + radialUp * 0.5f;
            if (Physics.SphereCast(castStart, 0.5f, desiredMove,
                                   out RaycastHit wh, wallCheckAhead, planetMask))
            {
                float wallSlope = Vector3.Angle(wh.normal, radialUp);
                if (wallSlope > maxWalkSlope)        // cliff or wall
                    wallTooSteep = true;
            }
        }

        if (wallTooSteep)               // cancel uphill portion
            desiredMove = Vector3.zero;

        Vector3 move = desiredMove * walkSpeed * Time.deltaTime;


        /* ---------- jump + Shift + Space to fly ------------------- */
        bool grounded = false;
        bool spaceTap = Input.GetKeyDown(KeyCode.Space);
        bool shiftHold = Input.GetKey(KeyCode.LeftShift);

        verticalVel -= planetGravity * Time.deltaTime;

        /* short ground-check *sphere* (handles steep walls) */
        const float footRadius = 0.7f;
        float probeLen = surfaceClearance + 0.6f;           // just past feet
        if (Physics.SphereCast(transform.position + radialUp * probeLen,
                                footRadius, -radialUp, out RaycastHit gh, probeLen, planetMask))
        {
            float gSlope = Vector3.Angle(gh.normal, radialUp);

            if (gSlope > maxWalkSlope)      // standing on too-steep ground?
            {
                // Project motion so you slide sideways / down only
                Vector3 downhill = Vector3.ProjectOnPlane(desiredMove, gh.normal);
                move = downhill * walkSpeed * Time.deltaTime;
            }

            float groundDist = gh.distance - surfaceClearance;
            float offset = surfaceClearance - groundDist;  // + = too low
            if (groundDist < surfaceClearance + 0.01f)
            {
                grounded = true;
                if (verticalVel < 0) verticalVel = 0f;
                float maxStep = groundSnapSpeed * Time.deltaTime;
                transform.position += radialUp * Mathf.Min(offset, maxStep);
            }
        }

        /* 1 ─ Jump if grounded (no Shift needed) */
        if (spaceTap && !shiftHold)
        {
            verticalVel = jumpSpeed;
        }

        /* 2 ─ While airborne, press Shift + Space together to re-enter Flight */
        else if (!grounded && spaceTap && shiftHold)
        {
            SwitchMode(Mode.Flight);
            return;                          // skip rest of Walk logic this frame
        }

        /* apply gravity & move */

        move += radialUp * verticalVel * Time.deltaTime;
        transform.position += move;
    }

    /* ───────── OnDestroy log ───────────────────────────────── */
    void OnDestroy()
    {
        if (!planet) return;
        float h = localOffset.magnitude - planetRadius;
        Debug.Log($"[Release]  {planet.name}  radius={planetRadius:F1}  height={h:F2} m");
    }

    void SwitchMode(Mode newMode)
    {
        if (mode == newMode) return;        // already in that state
        mode = newMode;
        Debug.Log($"[Mode]  →  {mode}");
    }
}
