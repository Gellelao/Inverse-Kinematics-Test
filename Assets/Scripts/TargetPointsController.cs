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
    public float maxLegLag;
    public float maxDistForLegToBeInRange;
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

        if(maxLegLag < forecastDistance) throw new UnityException("Maximum Leg lag should be >= forecastDistance");

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
            
            setOfPoints.Add(new PointPair(this, targetPoint, futurePoint));
            nameCounter++;
        }

        // Shift every other leg to get a zigzag pattern
        // var counter = 0;
        // if(rightSide) counter = 1;
        // foreach(PointPair pair in setOfPoints){
        //     var target = pair.futurePoint;
        //     if(counter % 2 == 0)target.transform.position = target.transform.position + target.transform.forward * 0.8f;
        //     counter++;
        // }

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

            // Maybe do a check in a small radius to see if there s anything higher, and default to that if possible?
        }

        // Move the targetPoints towards futurePoints if certain conditions are met
        Debug.Log("========================");
        foreach(PointPair pair in allPoints){
            pair.Update();
        }
    }

    private bool CorrespondingPairIsNearFuturePoint(PointPair pair){
        // Get index of that pair from leftPoints, if it is in there
        var index = leftPoints.IndexOf(pair);
        if(index < 0){
            index = rightPoints.IndexOf(pair);
            if(index < 0) throw new UnityException("Pair not found in leftPoints nor rightPoints");
            // The pair is in rightPoints, so look for the corresponding pair in leftPoints
            var corresponding = leftPoints[index];
            return corresponding.WithinRangeOfFuturePoint(); // Will be close enough to grounded if the pair is not too far apart
        }
        else{
            // The pair is in leftPoints, so look for the corresponding pair in rightPoints
            var corresponding = rightPoints[index];
            return corresponding.WithinRangeOfFuturePoint(); // Will be close enough to grounded if the pair is not too far apart
        }
    }

    private Vector3 Rotate(Vector3 vector, float angle){
        return Quaternion.AngleAxis(angle, Vector3.up)*vector;
    }

    

    /// <summary>
    /// A target point and its associated future point
    /// </summary>
    class PointPair{
        private TargetPointsController controller;
        public GameObject targetPoint {get; private set;}
        public GameObject futurePoint {get; private set;}

        private float initialDistanceToFuturePoint = 0;

        public PointPair(TargetPointsController controller, GameObject targetPoint, GameObject futurePoint){
            this.controller = controller;
            this.targetPoint = targetPoint;
            this.futurePoint = futurePoint;

            initialDistanceToFuturePoint = 0;
        }

        public void Update(){
            
            if(TooFarFromFuturePoint()) initialDistanceToFuturePoint = GetDistanceToFuturePoint();

            if(initialDistanceToFuturePoint > 0){
                var currentPos = targetPoint.transform.position;
                // Refactor how th forecast is added here -> needs to be built into surrounding code like the if check below
                var futurePos = futurePoint.transform.position + controller.transform.forward * controller.forecastDistance;
                var peakOfTheArc = futurePos + Vector3.up*controller.stepHeight;
                var stepDistance = controller.legSpeed*Time.deltaTime;

                // Have to add stepDistance here to prevent the MoveTowards function thinking it is already at target and refusing to move the foot
                if(GetDistanceToFuturePoint() > (initialDistanceToFuturePoint/2) + stepDistance){
                    // This makes the leg move upwards for the first half of the step, resulting in an arcing effect
                    targetPoint.transform.position = Vector3.MoveTowards(currentPos, peakOfTheArc, stepDistance);
                }
                else{
                    targetPoint.transform.position = Vector3.MoveTowards(currentPos, futurePos, stepDistance);
                }
                if(WithinRangeOfFuturePoint()) initialDistanceToFuturePoint = 0;
            }
        }

        public float GetDistanceToFuturePoint(){
            return (futurePoint.transform.position - targetPoint.transform.position).magnitude;
        }

        private Vector3 GetForecast(){
            var initial = futurePoint.transform.position + controller.transform.forward * controller.forecastDistance;
            // if base is a lot lower than futurepoint just return futurepoint
            // otherwise check above base pos to find the highest y for that x and z
            return initial;
        }

        public bool WithinRangeOfFuturePoint(){

            return GetDistanceToFuturePoint() <= controller.maxDistForLegToBeInRange;
        }

        public bool TooFarFromFuturePoint(){

            return GetDistanceToFuturePoint() >= controller.maxLegLag;
        }

        public override string ToString(){
            return "Target[" + targetPoint.name + "], Future[" + futurePoint.name + "]";
        }
    }
}