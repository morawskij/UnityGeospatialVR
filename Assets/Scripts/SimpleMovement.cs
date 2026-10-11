using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;

public class SimpleMovement : MonoBehaviour
{

    public float moveSensibility = 0.05f;
    public float rotationSensibility = 0.1f;
    public InputAction moveAction;
    public InputAction rotateYAction;
    public InputAction rotateXAction;
    private Vector3 moveInput;
    private float rotateY;
    private float rotateX;
    private float pitch;
    private float yaw;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        pitch = transform.eulerAngles.x;
        yaw = transform.eulerAngles.y;
    }

    void OnEnable()
    {
        moveAction.Enable();
        rotateXAction.Enable();
        rotateYAction.Enable();
    }

    // Update is called once per frame
    void Update()
    {
        moveInput = moveAction.ReadValue<Vector3>();
        transform.Translate(moveInput * moveSensibility, Space.Self);
        rotateX = rotateXAction.ReadValue<float>();
        rotateY = rotateYAction.ReadValue<float>();

        pitch = Mathf.Repeat(pitch + rotateX * rotationSensibility, 360f);
        yaw = Mathf.Repeat(yaw + rotateY * rotationSensibility, 360f);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}
