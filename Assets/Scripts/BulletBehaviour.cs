using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletBehaviour : MonoBehaviour
{
    public float bulletSpeed;
    private Rigidbody ourRigidbody;
    
    void Start()
    {
        ourRigidbody = GetComponent<Rigidbody>();
        ourRigidbody.velocity = transform.forward * bulletSpeed;
    }

    void Update()
    {
    }
}
