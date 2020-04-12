using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DitzelGames.FastIK;

public class TargetPointsController : MonoBehaviour
{
    public GameObject targetPointPrefab;
    public int numberOfLegsPerSide;
    public float legDistanceFromBody;
    public float angleBetweenLegs;
    public float maxClimbHeight;
    public float distanceBeforeLegUpdate;
    public float stepHeight;
    private List<PointPair> leftPoints; // The order is <point, futurepoint>
    private List<PointPair> rightPoints;
    
    
    void Start()
    {
        leftPoints = new List<PointPair>();
        rightPoints = new List<PointPair>();

        // Offset to take into account odd/even numbers of legs
        float offset;
        if(numberOfLegsPerSide % 2 == 0) offset = (angleBetweenLegs/2);
        else                             offset = 0;

        Transform leftLegs = transform.Find("LeftLegs");
        Transform rightLegs = transform.Find("RightLegs");

        SetupLegsOneSide(leftPoints, leftLegs, offset, false);
        SetupLegsOneSide(rightPoints, rightLegs, offset, true);
    }

    void Update()
    {
        List<PointPair> allPoints = new List<PointPair>();
        allPoints.AddRange(leftPoints);
        allPoints.AddRange(rightPoints);
        // Make each future point sit on the ground, within the limit of maxClimbHeight
        foreach(PointPair pair in allPoints){
            GameObject point = pair.futurePoint;
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

        foreach(PointPair pair in allPoints){
            GameObject point = pair.targetPoint;
            GameObject futurePoint = pair.futurePoint;
            
            // Get distance between the two points
            Vector3 distanceVector = futurePoint.transform.position - point.transform.position;


            if(pair.tooFarApart){
                // Move targetPoint toweards futurePoint in a stepping motion
                // Adjust this coefficient, it's picked arbitrarily for now
                pair.stepTimeElapsed += Time.deltaTime * 1;
                Vector3 nextPos = Vector3.Lerp(point.transform.position, futurePoint.transform.position, pair.stepTimeElapsed);
                // Add some height to the step
                nextPos.y += stepHeight * Mathf.Sin(Mathf.Clamp01(pair.stepTimeElapsed) * Mathf.PI);
                // Actually update the position
                point.transform.position = nextPos;
            }
            else if(distanceVector.magnitude > distanceBeforeLegUpdate){
                pair.tooFarApart = true;
                pair.stepTimeElapsed = 0;
            }

            // The 0.1 here is to allow for some imprecision, but adjust after testing
            if(distanceVector.magnitude < 0.01){
                pair.tooFarApart = false;
            }

        }
    }

    private void SetupLegsOneSide(List<PointPair> setOfPoints, Transform legsContainer, float offset, bool rightSide){
        
        var rotationDir = -1; // If we are dealing with the left side we want to rotate some things in the opposiute direction
        if(rightSide) rotationDir = 1;

        // A vector pointing out from the side of the creature at a right angle
        Vector3 midPoint = Rotate(transform.forward, 90*rotationDir);

        // Create the points, starting from the front and moving backwards in increments of the given angle
        var frontMostLegPoint = Rotate(midPoint, -1*rotationDir*angleBetweenLegs*(numberOfLegsPerSide/2)+(offset*rotationDir));
        for(int i = 0; i < numberOfLegsPerSide; i++){
            var thisLegPos = Rotate(frontMostLegPoint, rotationDir*i*angleBetweenLegs);
            var point = Instantiate(targetPointPrefab, transform.position + thisLegPos*legDistanceFromBody, transform.rotation);

            var futurePoint = Instantiate(targetPointPrefab, transform.position + thisLegPos*legDistanceFromBody, transform.rotation);
            futurePoint.GetComponent<Renderer>().material.SetColor("_Color", Color.blue);
            // Add future points as children, because they need to always follow the creature
            futurePoint.transform.parent = transform;
            
            setOfPoints.Add(new PointPair(point, futurePoint));
        }
        Debug.Log(setOfPoints.Count);

        // Find the legs of this object and assign our targets to those legs
        var IKScripts = legsContainer.GetComponentsInChildren<FastIKFabric>();
        int count = 0;
        foreach(FastIKFabric legScript in IKScripts){
            Debug.Log(count);
            legScript.Target = setOfPoints[count].targetPoint.transform;
            count++;
            if(count >= setOfPoints.Count) break;
        }
    }

    private Vector3 Rotate(Vector3 vector, float angle){
        return Quaternion.AngleAxis(angle, Vector3.up)*vector;
    }
}

/// <summary>
/// A target point and its associated future point
/// </summary>
class PointPair{
    public GameObject targetPoint {get; private set;}
    public GameObject futurePoint {get; private set;}
    public bool tooFarApart;
    public float stepTimeElapsed;

    public PointPair(GameObject targetPoint, GameObject futurePoint){
        this.targetPoint = targetPoint;
        this.futurePoint = futurePoint;
    }
}