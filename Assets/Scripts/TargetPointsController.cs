using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TargetPointsController : MonoBehaviour
{
    public GameObject targetPointPrefab;
    public int numberOfLegsPerSide;
    public float legDistanceFromBody;
    public float angleBetweenLegs;
    public float maxClimbHeight;
    public List<GameObject> leftPoints {get; private set;}

    private GameObject point;
    private GameObject point1;
    private GameObject point2;
    
    void Start()
    {
        leftPoints = new List<GameObject>();
        
        // A vector pointing out from the left of the game object at a right angle
        Vector3 midpoint = Quaternion.AngleAxis(-90, Vector3.up) * transform.forward;

        // Offset to take into account odd/even numbers of legs
        float offset;
        if(numberOfLegsPerSide % 2 == 0) offset = (angleBetweenLegs/2);
        else                             offset = 0;

        // Create the left-hand points, starting from the front and moving backwards in increments of the given angle
        var frontMostLegPoint = Rotate(midpoint, angleBetweenLegs*(numberOfLegsPerSide/2)-offset);
        for(int i = 0; i < numberOfLegsPerSide; i++){
            var thisLegPos = Rotate(frontMostLegPoint, -1*i*angleBetweenLegs);
            var point = Instantiate(targetPointPrefab, transform.position + thisLegPos*legDistanceFromBody, transform.rotation);
            point.transform.parent = transform;
            leftPoints.Add(point);
        }
    }

    void Update()
    {
        // Make each point sit on the ground, within the limit of maxClimbHeight
        foreach(GameObject point in leftPoints){
            // The point in space we want to look downwards from
            Vector3 groundCastOrigin = new Vector3(point.transform.position.x, transform.position.y + maxClimbHeight, point.transform.position.z);

            int layerMask = 1 << 8; // Layer 8 is the targetPoints layer
            layerMask = ~layerMask; // Invert it to exlude that layer - we don't want to collide with the targetPoint objects

            var down = new Vector3(0,-1,0);

            RaycastHit hit;
            if(Physics.Raycast(groundCastOrigin, down, out hit, Mathf.Infinity, layerMask)){
                point.transform.position = new Vector3(point.transform.position.x, hit.point.y, point.transform.position.z);
            }
            else{
                Debug.Log("No object below " + gameObject.name);
            }
        }
    }

    private Vector3 Rotate(Vector3 vector, float angle){
        return Quaternion.AngleAxis(angle, Vector3.up)*vector;
    }
}
