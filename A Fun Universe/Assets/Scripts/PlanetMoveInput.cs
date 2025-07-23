using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlanetMoveInput : MonoBehaviour
{
    public float moveSpeed = 6f;
    public float mouseSense = 2f;

    Rigidbody rb;
    float yaw;

    void Awake() { rb = GetComponent<Rigidbody>(); }

    void Update()
    {
        // yaw from mouse
        yaw += Input.GetAxis("Mouse X") * mouseSense;
        transform.rotation = Quaternion.Euler(0, yaw, 0);

        // translation (relative to body’s forward/right)
        Vector3 dir = new Vector3(Input.GetAxis("Horizontal"), 0,
                                  Input.GetAxis("Vertical")).normalized;
        rb.MovePosition(rb.position + transform.TransformDirection(dir) * moveSpeed * Time.deltaTime);
    }
}
