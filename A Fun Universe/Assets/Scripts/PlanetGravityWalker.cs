using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlanetGravityWalker : MonoBehaviour
{
    public float gravityStrength = 9.8f;
    public float alignSpeed = 6f;      // how snappy the body upright-aligns
    public LayerMask groundMask;            // assign “Planet” layer

    Rigidbody rb;
    Transform cam;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;           // we’ll handle orientation
    }

    void FixedUpdate()
    {
        if (cam == null) cam = GetComponentInChildren<Camera>().transform;
        // 1) find nearest planet by collider
        Collider[] hits = Physics.OverlapSphere(transform.position, 1_000f, groundMask);
        if (hits.Length == 0) return;
        Transform planet = hits[0].transform;          // nearest

        Vector3 toCentre = (planet.position - transform.position).normalized;

        // 2) gravity
        rb.AddForce(toCentre * gravityStrength, ForceMode.Acceleration);

        // 3) upright alignment
        Quaternion targetRot = Quaternion.FromToRotation(transform.up, -toCentre) * transform.rotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, alignSpeed * Time.fixedDeltaTime);

        // keep camera pitch independent (simple)
        float yaw = transform.eulerAngles.y;
        float pitch = cam.localEulerAngles.x + Input.GetAxis("Mouse Y") * -2f;
        cam.localEulerAngles = new Vector3(pitch, 0, 0);
    }
}