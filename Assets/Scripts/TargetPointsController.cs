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
    public float maximumLegLag;
    public float legSpeed;
    public float forecastDistance;
    public float stepHeight;
    private List<PointPair> leftPoints; // The order is <point, futurepoint>
    private List<PointPair> rightPoints;
    private List<PointPair> allPoints; // This list is just the two combined for easy iteration
    
    private int nameCounter;
    
    void Start()
    {
        leftPoints = new List<PointPair>();
        rightPoints = new List<PointPair>();
        allPoints = new List<PointPair>();
        nameCounter = 0;

        if(maximumLegLag < forecastDistance) throw new UnityException("Maximum Leg lag should be >= forecastDistance");

        // Offset to take into account odd/even numbers of legs
        float offset;
        if(numberOfLegsPerSide % 2 == 0) offset = (angleBetweenLegs/2);
        else                             offset = 0;

        Transform leftLegs = transform.Find("LeftLegs");
        Transform rightLegs = transform.Find("RightLegs");

        SetupLegsOneSide(leftPoints, leftLegs, offset, false);
        SetupLegsOneSide(rightPoints, rightLegs, offset, true);

        allPoints.AddRange(leftPoints);
        allPoints.AddRange(rightPoints);
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
            var targetPoint = Instantiate(targetPointPrefab, transform.position + thisLegPos*legDistanceFromBody, transform.rotation);
            targetPoint.name = "TargetPoint" + nameCounter;

            var futurePoint = Instantiate(targetPointPrefab, transform.position + thisLegPos*legDistanceFromBody, transform.rotation);
            futurePoint.name = "FuturePoint" + nameCounter;
            futurePoint.GetComponent<Renderer>().material.SetColor("_Color", Color.blue);
            // Add future points as children, because they need to always follow the creature
            futurePoint.transform.parent = transform;
            
            setOfPoints.Add(new PointPair(targetPoint, futurePoint));
            nameCounter++;
        }

        // Shift every other leg to get a zigzag pattern
        var counter = 0;
        if(rightSide) counter = 1;
        foreach(PointPair pair in setOfPoints){
            var target = pair.futurePoint;
            if(counter % 2 == 0)target.transform.position = target.transform.position + target.transform.forward * 0.8f;
            counter++;
        }

        // Find the legs of this object and assign our targets to those legs
        var IKScripts = legsContainer.GetComponentsInChildren<FastIKFabric>();
        int count = 0;
        foreach(FastIKFabric legScript in IKScripts){
            legScript.Target = setOfPoints[count].targetPoint.transform;
            count++;
            if(count >= setOfPoints.Count) break;
        }
    }

    void Update()
    {
        foreach(PointPair p in allPoints){
        }
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
                Debug.Log("No object below " + point.name);
            }
        }

        // Move the targetPoints towards futurePoints if certain conditions are met
        Debug.Log("========================");
        foreach(PointPair pair in allPoints){
            Debug.Log("Pointpair " + pair + ", tooFarApart: " + pair.tooFarApart);

            if(CorrespondingPairIsGrounded(pair)){
                pair.TryMove(legSpeed, stepHeight);
            }
            // Sets this pointPair to start reuniting next update
            if(!pair.tooFarApart && pair.GetDistanceToFuturePoint() > maximumLegLag){
                pair.StartMove(forecastDistance);
            }

        }
    }

    private bool CorrespondingPairIsGrounded(PointPair pair){
        // Get index of that pair from leftPoints, if it is in there
        var index = leftPoints.IndexOf(pair);
        if(index < 0){
            index = rightPoints.IndexOf(pair);
            if(index < 0) throw new UnityException("Pair not found in leftPoints nor rightPoints");
            // The pair is in rightPoints, so look for the corresponding pair in leftPoints
            var corresponding = leftPoints[index];
            return !corresponding.onDescent; // Will be close enough to grounded if the pair is not too far apart
        }
        else{
            // The pair is in leftPoints, so look for the corresponding pair in rightPoints
            var corresponding = rightPoints[index];
            return !corresponding.onDescent; // Will be close enough to grounded if the pair is not too far apart
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
    public bool onDescent;
    public Vector3 forecastTarget {get; private set;}

    public float initialDistanceToForecast;

    public PointPair(GameObject targetPoint, GameObject futurePoint){
        this.targetPoint = targetPoint;
        this.futurePoint = futurePoint;
        tooFarApart = false;
        initialDistanceToForecast = (futurePoint.transform.position - targetPoint.transform.position).magnitude;
    }

    public void StartMove(float forecastDistance){
        Debug.Log("start move");
        tooFarApart = true;
        Debug.DrawRay(futurePoint.transform.position, futurePoint.transform.forward * forecastDistance, Color.yellow, 5);
        forecastTarget = futurePoint.transform.position + futurePoint.transform.forward * forecastDistance;
        initialDistanceToForecast = GetDistanceToForecast();
    }

    public void TryMove(float speed, float stepHeight){
        Debug.Log("try move");
        if(!tooFarApart) return;
        Debug.Log("try move reaches past first return");
        var remainingDistanceToForecast = GetDistanceToForecast();
        if(remainingDistanceToForecast > initialDistanceToForecast/1.5){
            // This makes the leg move upwards for the first half of the step, resulting in an arcing effect
            Debug.DrawLine(targetPoint.transform.position, forecastTarget + Vector3.up*stepHeight, Color.red, 0.5f);
            targetPoint.transform.position = Vector3.MoveTowards(targetPoint.transform.position, forecastTarget + Vector3.up*stepHeight, speed);
            onDescent = false;
        }
        else{
            targetPoint.transform.position = Vector3.MoveTowards(targetPoint.transform.position, forecastTarget, speed);
            onDescent = true;
        }
        Debug.Log("Initial distance to forecast/10: " + initialDistanceToForecast/10);
        if(GetDistanceToForecast() < initialDistanceToForecast/10){// The 0.1 here is to allow for some imprecision, but adjust after testing
            tooFarApart = false;
            onDescent = false;
        }
    }

    public float GetDistanceToFuturePoint(){
        return (futurePoint.transform.position - targetPoint.transform.position).magnitude;
    }

    public float GetDistanceToForecast(){
        return (forecastTarget - this.targetPoint.transform.position).magnitude;
    }

    public override string ToString(){
        return "Target[" + targetPoint.name + "], Future[" + futurePoint.name + "]";
    }
}