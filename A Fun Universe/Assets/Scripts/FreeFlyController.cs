using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FreeFlyController : MonoBehaviour
{
    /* -------- flight tuning ----------------------------------- */
    [Header("Flight")]
    public float maxSpeed = 240f;
    public float accelRate = 60f;
    public float decelRate = 120f;
    public float mouseSense = 2f;

    /* -------- private ----------------------------------------- */
    Rigidbody rb;
    Camera cam;
    float yaw, pitch;
    Vector3 lastPos;                 // world-space pos previous Update

    /* ----------------------------------------------------------- */
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        cam = GetComponentInChildren<Camera>();

        rb.useGravity = false;
        rb.freezeRotation = true;

        /* ── reset view so free-flight always starts level ───────── */

        // keep whatever yaw we had, but zero-out pitch & roll
        float startYaw = transform.eulerAngles.y;
        transform.rotation = Quaternion.Euler(0f, startYaw, 0f);

        yaw = startYaw;   // initialise accumulators
        pitch = 0f;

        if (cam) cam.transform.localEulerAngles = Vector3.zero;   // camera straight ahead

        lastPos = transform.position;
    }

    /* ---------------- view & capture -------------------------- */
    void Update()
    {
        /* 0. start-of-frame pos                                    */
        Vector3 startPos = transform.position;

        /* 1. look rotation                                         */
        yaw += Input.GetAxis("Mouse X") * mouseSense;
        pitch -= Input.GetAxis("Mouse Y") * mouseSense;
        pitch = Mathf.Clamp(pitch, -89f, 89f);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0);

        /* 2. capture test using segment (lastPos → startPos)       */
        TryCapturePlanet(lastPos, startPos);

        /* 3. remember for next frame                               */
        lastPos = startPos;
    }

    /* ---------------- thrust physics --------------------------- */
    void FixedUpdate()
    {
        Vector3 keyDir = new Vector3(Input.GetAxisRaw("Horizontal"), 0,
                                      Input.GetAxisRaw("Vertical")).normalized;
        Vector3 desired = transform.forward * keyDir.z + transform.right * keyDir.x;

        if (Input.GetKey(KeyCode.E)) desired += transform.up;
        if (Input.GetKey(KeyCode.Q)) desired -= transform.up;

        Vector3 v = rb.linearVelocity;
        float dt = Time.fixedDeltaTime;

        if (desired != Vector3.zero)
        {
            v += desired.normalized * accelRate * dt;
            v = Vector3.ClampMagnitude(v, maxSpeed);
        }
        else
        {
            float speed = Mathf.Max(0, v.magnitude - decelRate * dt);
            v = v.normalized * speed;
        }
        rb.linearVelocity = v;
    }

    /* ---------------- planet capture --------------------------- */
    void TryCapturePlanet(Vector3 a, Vector3 b)
    {
        Collider[] hits = Physics.OverlapSphere(b, 10000f,
                                                LayerMask.GetMask("Planet"));
        if (hits.Length == 0) return;

        /* nearest planet this frame                                */
        Collider nearest = hits[0];
        float best = (nearest.transform.position - b).sqrMagnitude;
        foreach (Collider c in hits)
        {
            float d = (c.transform.position - b).sqrMagnitude;
            if (d < best) { nearest = c; best = d; }
        }

        PlanetGenerator pg = nearest.GetComponent<PlanetGenerator>();
        if (!pg) { Debug.LogWarning("Planet without PlanetGenerator"); return; }

        /* dynamic margins ----------------------------------------- */
        int captureMargin = Mathf.CeilToInt(pg.OuterRadius - 5f);
        float shell = pg.OuterRadius + captureMargin;      // distance to hit
        float releaseMargin = captureMargin + 10f;                  // pass to walker

        /* segment A: player (a→b), segment B: planet centre (prev→curr) */
        Vector3 cCurr = nearest.transform.position;
        Vector3 cPrev = cCurr - nearest.attachedRigidbody.linearVelocity * Time.deltaTime;

        /* ×2 because DistanceSegToSeg ≈ half real closest approach */
        if (DistanceSegToSeg(a, b, cPrev, cCurr) < shell * 2f)
        {
            float g = pg.surfaceGravity;
            var walker = gameObject.AddComponent<PlanetRelative>();
            walker.SetPlanet(nearest.transform, pg.OuterRadius, releaseMargin, g);
            Destroy(this);                                         // switch mode
        }
    }

    /* segment-to-segment distance (unchanged) ------------------- */
    static float DistanceSegToSeg(Vector3 p1, Vector3 q1,
                                  Vector3 p2, Vector3 q2)
    {
        Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
        float a = Vector3.Dot(d1, d1);
        float e = Vector3.Dot(d2, d2);
        float f = Vector3.Dot(d2, r);

        float s = 0, t = 0;
        if (a <= 1e-6f && e <= 1e-6f) return r.magnitude;          // both points

        if (a > 1e-6f)
        {
            float c = Vector3.Dot(d1, r);
            if (e > 1e-6f)
            {
                float b = Vector3.Dot(d1, d2);
                float denom = a * e - b * b;
                if (denom != 0) s = Mathf.Clamp01((b * f - c * e) / denom);
            }
            else s = Mathf.Clamp01(-c / a);
        }

        t = (Vector3.Dot(d1 * s - r, d2) / e);
        t = Mathf.Clamp01(t);

        s = (b: Vector3.Dot(d1, d2),
             c: Vector3.Dot(d1, r),
             denom: a * e - Vector3.Dot(d1, d2) * Vector3.Dot(d1, d2))
            .denom != 0 ? Mathf.Clamp01((Vector3.Dot(d1, d2) * t - Vector3.Dot(d1, r)) / a) : s;

        return (p1 + d1 * s - (p2 + d2 * t)).magnitude;
    }
}
