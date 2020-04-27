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
    public float maximumDownstep;

    private BeastController parentController;
    private List<PointPair> leftPoints;
    private List<PointPair> rightPoints;
    private List<PointPair> allPoints; // This list is just the two combined for easy iteration
    
    private int nameCounter;
    
    void Start()
    {
        parentController = transform.parent.GetComponent<BeastController>();

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

            var anchor = Instantiate(targetPointPrefab, transform.position + thisLegPos*legDistanceFromBody, transform.rotation);
            anchor.name = "Anchor" + nameCounter;
            anchor.GetComponent<Renderer>().material.SetColor("_Color", Color.green);
            // Add anchors as children, because they need to always follow the creature
            anchor.transform.parent = transform;

            var futurePoint = Instantiate(targetPointPrefab, anchor.transform.position, transform.rotation);
            futurePoint.name = "FuturePoint" + nameCounter;
            futurePoint.GetComponent<Renderer>().material.SetColor("_Color", Color.blue);
            
            setOfPoints.Add(new PointPair(this, targetPoint, futurePoint, anchor));
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
        // Make each future point sit on the ground, within the limit of maxClimbHeight
        foreach(PointPair pair in allPoints){
            pair.UpdateFuturePoints();

            // Maybe do a check in a small radius to see if there is anything higher, and default to that if possible?
        }

        var averageHeight = 0.0f;
        // Move the targetPoints towards futurePoints if certain conditions are met
        foreach(PointPair pair in allPoints){
            pair.UpdateTargetPoints();
            averageHeight += pair.targetPoint.transform.position.y; // While we are looping, track an average of the feet positions
        }

        // Update the body position to an average of the feet positions
        averageHeight /= (numberOfLegsPerSide*2);
        parentController.UpdateHeight(averageHeight);

        // Update body angle based on difference between leg heights
        SetBodyRotation();
    }

    private Vector3 Rotate(Vector3 vector, float angle){
        return Quaternion.AngleAxis(angle, Vector3.up)*vector;
    }

    // Don't see an obvious way to reuse code here unfortunately
    private void SetBodyRotation(){
        // Find Z difference, used to Pitch forward or backward
        var averageOfFrontLegs = 0.0f;
        averageOfFrontLegs += leftPoints[0].targetPoint.transform.position.y;
        averageOfFrontLegs += rightPoints[0].targetPoint.transform.position.y;
        averageOfFrontLegs /= 2;

        var averageOfRearLegs = 0.0f;
        averageOfRearLegs += leftPoints[leftPoints.Count-1].targetPoint.transform.position.y;
        averageOfRearLegs += rightPoints[leftPoints.Count-1].targetPoint.transform.position.y;
        averageOfRearLegs /= 2;

        var zDifference = averageOfFrontLegs - averageOfRearLegs;

        // Find X difference, used to Roll side to side
        var averageOfLeftLegs = 0.0f;
        for(var i = 0; i < numberOfLegsPerSide; i++){
            averageOfLeftLegs += leftPoints[i].targetPoint.transform.position.y;
        }
        averageOfLeftLegs /= numberOfLegsPerSide;

        var averageOfRightLegs = 0.0f;
        for(var i = 0; i < numberOfLegsPerSide; i++){
            averageOfRightLegs += rightPoints[i].targetPoint.transform.position.y;
        }
        averageOfRightLegs /= numberOfLegsPerSide;

        var xDifference = averageOfLeftLegs - averageOfRightLegs;

        // Now use the calculate differences to update the body rotation
        parentController.Rotate(zDifference, xDifference);
    }

    private bool CorrespondingPairIsNearFuturePoint(PointPair pair){
        // Get index of that pair from leftPoints, if it is in there
        var index = leftPoints.IndexOf(pair);
        if(index < 0){
            index = rightPoints.IndexOf(pair);
            if(index < 0) throw new UnityException("Pair not found in leftPoints nor rightPoints");
            // The pair is in rightPoints, so look for the corresponding pair in leftPoints
            var corresponding = leftPoints[index];
            return corresponding.WithinRangeOfForecast();
        }
        else{
            // The pair is in leftPoints, so look for the corresponding pair in rightPoints
            var corresponding = rightPoints[index];
            return corresponding.WithinRangeOfForecast();
        }
    }



    // ==================================================================================
    /// <summary>
    /// A target point and its associated future point
    /// </summary>
    class PointPair{
        private TargetPointsController controller;
        public GameObject targetPoint {get; private set;}
        public GameObject futurePoint {get; private set;}
        private GameObject anchor; // This is the initial position of the futurePoint, and should always remain fixed in the same position relative to the spider body

        private float initialDistanceToForecast = 0;

        public PointPair(TargetPointsController controller, GameObject targetPoint, GameObject futurePoint, GameObject anchor){
            this.controller = controller;
            this.targetPoint = targetPoint;
            this.futurePoint = futurePoint;
            this.anchor = anchor;

            initialDistanceToForecast = 0;
        }

        public void UpdateFuturePoints(){
            var anchorPos = anchor.transform.position;
            // anchor.y should be the y position of the controller, because we instantiate the futurepoint at the controller's y, and the anchor is derived from the futurepoint's inital position
            Vector3 groundCastOrigin = new Vector3(anchorPos.x, anchorPos.y + controller.maxClimbHeight, anchorPos.z);

            int layerMask = 1 << 8; // Layer 8 is the targetPoints layer
            layerMask = ~layerMask; // Invert it to exlude that layer - we don't want to collide with the targetPoint objects

            var down = new Vector3(0,-1,0);

            RaycastHit hit;
            if(Physics.Raycast(groundCastOrigin, down, out hit, Mathf.Infinity, layerMask)){
                futurePoint.transform.position = new Vector3(anchor.transform.position.x, hit.point.y, anchor.transform.position.z);
            }
        }

        // Need to fix sliding when forecastdistance is set to any significant amount
        public void UpdateTargetPoints(){
            // Only move this leg if the matching leg on the other side of the body is close to grounded
            // But doesn't stop all 4 legs on one side from moveing at once, which looks weird
            if(controller.CorrespondingPairIsNearFuturePoint(this))return;

            // Remake this check to only look backwards, there'll be issues when the forecast is too big and it thinks the leg is lagging behind but is actually too far ahead
            if(TooFarFromFuturePoint()){
                initialDistanceToForecast = GetDistanceToForecast();
            }

            if(initialDistanceToForecast > 0){
                var currentPos = targetPoint.transform.position;
                var futurePos = GetForecast();
                var peakOfTheArc = futurePos + Vector3.up*controller.stepHeight;
                var stepDistance = controller.legSpeed*Time.deltaTime;

                // Have to add stepDistance here to prevent the MoveTowards function thinking it is already at target and refusing to move the foot
                if(GetDistanceToForecast() > (initialDistanceToForecast/2) + stepDistance){
                    // This makes the leg move upwards for the first half of the step, resulting in an arcing effect
                    targetPoint.transform.position = Vector3.MoveTowards(currentPos, peakOfTheArc, stepDistance);
                }
                else{
                    targetPoint.transform.position = Vector3.MoveTowards(currentPos, futurePos, stepDistance);
                }
                if(WithinRangeOfForecast()){
                    initialDistanceToForecast = 0;
                }
            }
        }

        public float GetDistanceToFuturePoint(){
            return (futurePoint.transform.position - targetPoint.transform.position).magnitude;
        }

        private Vector3 GetForecast(){
            var futurePos = futurePoint.transform.position;
            var initial = futurePoint.transform.position + controller.transform.forward * controller.forecastDistance;
            // if base is a lot lower than futurepoint just return futurepoint
            if(initial.y + controller.maximumDownstep < futurePos.y) return futurePos;
            // otherwise check above base pos to find the highest y for that x and z
            // to be written
            return initial;
        }

        // This should not take into account vertical distance, so we can have the spider take high steps?
        // Would run into issues when climbing sloped though?
        public float GetDistanceToForecast(){
            return (GetForecast() - targetPoint.transform.position).magnitude;
        }

        public bool WithinRangeOfForecast(){
            return GetDistanceToForecast() <= controller.maxDistForLegToBeInRange;
        }

        public bool TooFarFromFuturePoint(){
            return GetDistanceToFuturePoint() >= controller.maxLegLag;
        }

        public override string ToString(){
            return "Target[" + targetPoint.name + "], Future[" + futurePoint.name + "]";
        }
    }
}