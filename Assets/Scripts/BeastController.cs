using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BeastController : MonoBehaviour
{
    CharacterController characterController;

    public float speed = 6.0f;
    public float jumpSpeed = 8.0f;
    public float gravity = 20.0f;
    public float rotationSpeed;
    public float verticalRepositionSpeed;

    private Vector3 moveDirection = Vector3.zero;
    private Vector3 initialEulers;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        initialEulers = transform.eulerAngles;
    }

    void Update()
    {
        moveDirection = new Vector3(Input.GetAxis("Horizontal"), 0.0f, Input.GetAxis("Vertical"));
        moveDirection *= speed;

        if (moveDirection != Vector3.zero) {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(moveDirection),
                Time.deltaTime * rotationSpeed
            );
        }

        // Move the controller
        characterController.Move(moveDirection * Time.deltaTime);
    }

    public void UpdatePos(Vector3 newPos){
        var difference = newPos - transform.position;
        characterController.Move(difference * Time.deltaTime * verticalRepositionSpeed);
    }

    public void Rotate(float xDiff, float zDiff){
        // var nextRotation = Quaternion.Euler(initialEulers.x - xDiff*11, transform.eulerAngles.y, initialEulers.z - zDiff*11);
        // transform.rotation = Quaternion.Lerp(transform.rotation, nextRotation, Time.deltaTime * rotationSpeed);
    }
}
