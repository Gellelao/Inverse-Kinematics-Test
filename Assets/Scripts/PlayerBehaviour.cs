using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerBehaviour : MonoBehaviour
{
    CharacterController characterController;

    public float speed;
    public float jumpSpeed;
    public float gravity;
    public float rotationSpeed;
    public float tiltDampening;

    private Vector3 moveDirection = Vector3.zero;
    private Vector3 initialEulers;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        initialEulers = transform.eulerAngles;
    }

    void Update()
    {


        if (characterController.isGrounded)
        {
            moveDirection = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
            moveDirection *= speed;

            if (Input.GetButton("Jump"))
            {
                moveDirection.y = jumpSpeed;
            }
        }
        else{
            moveDirection.x = Input.GetAxis("Horizontal")*speed;
            moveDirection.z = Input.GetAxis("Vertical")*speed;
        }

        if (Input.GetAxis("Horizontal") != 0 || Input.GetAxis("Vertical") != 0)
        {
            var lookPlane = new Vector3(moveDirection.x, 0, moveDirection.z);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                TiltRotationTowardsVelocity(Quaternion.LookRotation(lookPlane), Vector3.up, lookPlane, tiltDampening),
                Time.deltaTime * rotationSpeed
            );
        }

        // Apply gravity. Gravity is multiplied by deltaTime twice (once here, and once below
        // when the moveDirection is multiplied by deltaTime). This is because gravity should be applied
        // as an acceleration (ms^-2)
        moveDirection.y -= gravity * Time.deltaTime;


        // Move the controller
        characterController.Move(moveDirection * Time.deltaTime);
    }

    public void Rotate(float xDiff, float zDiff)
    {
        var nextRotation = Quaternion.Euler(initialEulers.x - xDiff * 13, transform.eulerAngles.y, initialEulers.z - zDiff * 11);
        transform.rotation = Quaternion.Lerp(transform.rotation, nextRotation, Time.deltaTime * rotationSpeed);
    }

     /// <summary>
     /// THANKS TO THIS PERSON: http://answers.unity.com/answers/1498260/view.html
     /// Tilts a rotation towards a velocity relative to referenceUp
     /// Example:
     /// myTransform.rotation = TiltRotationTowardsVelocity( myCleanRotation.rotation, Vector3.up, velocity, 20F );
     /// </summary>
     /// <param name="cleanRotation"        >Target rotation of the transform, maybe your transform is already looking at something, you don't want to loose this alignment</param>
     /// <param name="referenceUp"          >The up Vector, mostly, this will be Vector3.up, if your gravity is pointing down</param>
     /// <param name="vel"                  >The velocity vector that is meant to cause the tilt</param>
     /// <param name="velMagFor45Degree"    >A velocity with a magnitude of velMagFor45Degree will yield a 45degree tilt</param>
     /// <returns>returns currentRotation modified by a tilt</returns>
     public static Quaternion TiltRotationTowardsVelocity( Quaternion cleanRotation, Vector3 referenceUp, Vector3 vel, float velMagFor45Degree )
     {
        Vector3 rotAxis = Vector3.Cross( referenceUp, vel );
        float tiltAngle = Mathf.Atan( vel.magnitude /velMagFor45Degree) *Mathf.Rad2Deg;
        return Quaternion.AngleAxis( tiltAngle, rotAxis ) *cleanRotation;    //order matters
     }

}
