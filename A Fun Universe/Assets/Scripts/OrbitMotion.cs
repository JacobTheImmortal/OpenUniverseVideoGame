using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Rotates this body around `origin` at a fixed radius & speed.
///
/// • `orbitAxis`  : normal of the orbital plane (will be normalised in Start)
/// • `orbitSpeed` : ° / sec  (negative = retrograde)
/// • `selfSpin`   : ° / sec  body’s own rotation
/// • `startPhase` : initial true-anomaly in degrees
public class OrbitMotion : MonoBehaviour
{
    public Transform origin;        // body we orbit (usually the star)

    public float orbitRadius;      // metres
    public float orbitSpeed;       // deg / sec  (-ve → opposite direction)
    public float selfSpin;         // deg / sec
    public float startPhase;       // deg
    public Vector3 orbitAxis = Vector3.up;   // ← NEW (defaults to old behaviour)

    float angle;                    // internal tracker

    void Start()
    {
        orbitAxis = orbitAxis.normalized;        // make sure it’s unit length

        angle = startPhase;
        Vector3 offset =
            Quaternion.AngleAxis(angle, orbitAxis) * (Vector3.right * orbitRadius);

        transform.position = origin.position + offset;
    }

    void Update()
    {
        float delta = orbitSpeed * Time.deltaTime;

        transform.RotateAround(origin.position, orbitAxis, delta);

        if (selfSpin != 0f)
            transform.Rotate(Vector3.up, selfSpin * Time.deltaTime, Space.Self);
    }
}
