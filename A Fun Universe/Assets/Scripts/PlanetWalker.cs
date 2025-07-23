using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Attach to the capsule player.
/// – Finds the planet you’re closest to (Layer = Planet)
/// – Pulls you toward its centre
/// – Keeps you pinned to the surface
/// – Lets WASD walk on the tangent plane
[RequireComponent(typeof(Rigidbody))]
public class PlanetWalker : MonoBehaviour
{
    public float gravity = 15f;
    public float walkSpeed = 6f;
    public float mouseSense = 2f;

    Rigidbody rb;
    Camera cam;
    Transform planet;
    float planetRadius;      // real radius from PlanetGenerator

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        cam = GetComponentInChildren<Camera>();
        rb.useGravity = false;
        rb.freezeRotation = false;
    }

    public void SetTargetPlanet(Transform t)
    {
        planet = t;
        planetRadius = t.GetComponent<PlanetGenerator>().RealRadius;

        /* current radial direction & distance */
        Vector3 toCentre = planet.position - transform.position; // IN
        float currentDist = toCentre.magnitude;
        Vector3 upDir = -toCentre.normalized;               // OUT (feet ↓)

        /* target distance is exactly one unit above the tallest surface */
        float desiredDist = planetRadius + 1.0f;                  // eye height
        float offset = desiredDist - currentDist;            // +out / –in

        /* apply the full offset once */
        transform.position += upDir * offset;
        transform.up = upDir;

        /* stop any drift so gravity starts clean */
        rb = GetComponent<Rigidbody>();
        if (rb != null) rb.linearVelocity = Vector3.zero;
    }

    IEnumerator Start()
    {
        // wait one physics step so planets & colliders are ready
        yield return new WaitForFixedUpdate();

        // shoot ray inward to snap exactly to the surface
        if (Physics.Raycast(transform.position + transform.up * 20f,
                            -transform.up, out RaycastHit hit, 40f,
                            LayerMask.GetMask("Planet")))
        {
            transform.position = hit.point + hit.normal * 1.0f; // stand-height
            transform.up = hit.normal;
        }
    }

    void FixedUpdate()
    {
        /* not locked to a planet yet */
        if (planet == null) return;

        /* ---------- 1. gravity vector ---------- */
        Vector3 toCentre = planet.position - transform.position;   // points IN
        float currentDist = toCentre.magnitude;
        Vector3 gravityDir = toCentre.normalized;                    // unit IN
        Vector3 surfaceUp = -gravityDir;                            // unit OUT

        /* apply gravity */
        rb.AddForce(gravityDir * gravity, ForceMode.Acceleration);

        /* ---------- 2. surface snap ---------- */
        float desiredDist = planetRadius + 1.0f;                     // eye height
        float offset = desiredDist - currentDist;               // +out / –in

        if (Mathf.Abs(offset) > 0.01f)
        {
            rb.MovePosition(rb.position + surfaceUp * offset);       // push OUT/IN
        }

        /* ---------- 3. upright alignment ---------- */
        Quaternion targetRot = Quaternion.FromToRotation(transform.up, surfaceUp)
                               * transform.rotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
                                              10f * Time.fixedDeltaTime);

        /* ---------- 4. WASD walking (tangent plane) ---------- */
        Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0,
                                    Input.GetAxisRaw("Vertical")).normalized;

        Vector3 move = (transform.forward * input.z +
                        transform.right * input.x) * walkSpeed * Time.fixedDeltaTime;

        rb.MovePosition(rb.position + move);

        /* ---------- 5. yaw / pitch look ---------- */
        if (cam == null) cam = GetComponentInChildren<Camera>();     // lazy fetch
        if (cam != null)
        {
            float yaw = Input.GetAxis("Mouse X") * mouseSense;
            float pitch = Mathf.Clamp(cam.transform.localEulerAngles.x +
                                      Input.GetAxis("Mouse Y") * -mouseSense,
                                      -89f, 89f);

            transform.Rotate(0, yaw, 0, Space.Self);
            cam.transform.localEulerAngles = new Vector3(pitch, 0, 0);
        }
    }
}
