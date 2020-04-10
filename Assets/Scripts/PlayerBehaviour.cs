using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerBehaviour : MonoBehaviour
{
    public float speed;
    public GameObject bulletPrefab;
    private Rigidbody ourRigidbody;

    void Start()
    {
        ourRigidbody = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // Movement
        Vector3 inputVector = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
        if(inputVector.magnitude > 0){
            ourRigidbody.velocity = inputVector * speed;
        }

        Vector3 newPosition = transform.position + inputVector;

        transform.LookAt(newPosition);

        // Shooting
        if(Input.GetButton("Fire1")){
            Instantiate(bulletPrefab, transform.position + transform.forward, transform.rotation);
        }
    }
}
