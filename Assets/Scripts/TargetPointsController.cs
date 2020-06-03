using UnityEngine.Assertions;
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
    public int maxLegsRaisedAtOnce;

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
        }

        // Move the targetPoints towards forecasts if certain conditions are met
        foreach(PointPair pair in allPoints){
            pair.UpdateTargetPoints();
        }

        // Update the body position to an average of the feet positions
        var averageHeight = leftPoints[0].targetPoint.transform.position.y;
        averageHeight += rightPoints[0].targetPoint.transform.position.y;
        averageHeight /= 2;
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

    private bool AllSurroundingLegsGrounded(PointPair pair){
        List<PointPair> relevantPairs = new List<PointPair>();
        
        // Get index of that pair from leftPoints, if it is in there
        var index = leftPoints.IndexOf(pair);
        if(index < 0){
            // If we look for it in leftPoints and get -1, try rightPoints
            index = rightPoints.IndexOf(pair);
            if(index < 0) throw new UnityException("Pair not found in leftPoints nor rightPoints");

            relevantPairs.Add(leftPoints[index]); // Matching leg on other side
            if(index > 0) relevantPairs.Add(rightPoints[index-1]); // Leg in front
            if(index < numberOfLegsPerSide-1) relevantPairs.Add(rightPoints[index+1]); // Leg in behind
        }
        else{
            // The pair is in leftPoints
            relevantPairs.Add(rightPoints[index]); // Matching leg on other side
            if(index > 0) relevantPairs.Add(leftPoints[index-1]); // Leg in front
            if(index < numberOfLegsPerSide-1) relevantPairs.Add(leftPoints[index+1]); // Leg in behind
        }
        Assert.IsTrue(relevantPairs.Count > 0 && relevantPairs.Count <= 3);
        var raisedLegs = 0;
        foreach(PointPair p in relevantPairs){
            if(p.movingTowardForecast())raisedLegs++;
        }
        return raisedLegs == 0;
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

        public bool movingTowardForecast() => initialDistanceToForecast != 0;

        public PointPair(TargetPointsController controller, GameObject targetPoint, GameObject futurePoint, GameObject anchor){
            this.controller = controller;
            this.targetPoint = targetPoint;
            this.futurePoint = futurePoint;
            this.anchor = anchor;

            initialDistanceToForecast = GetDistanceToForecast();
        }

        public void UpdateFuturePoints(){
            var anchorPos = anchor.transform.position;
            Vector3 groundCastOrigin = anchor.transform.position + anchor.transform.up*controller.maxClimbHeight;

            int layerMask = 1 << 8; // Layer 8 is the targetPoints layer
            layerMask = ~layerMask; // Invert it to exlude that layer - we don't want to collide with the targetPoint objects

            var down = -anchor.transform.up;

            RaycastHit hit;
            if(Physics.Raycast(groundCastOrigin, down, out hit, Mathf.Infinity, layerMask)){
                futurePoint.transform.position = new Vector3(hit.point.x, hit.point.y, hit.point.z);
            }
        }

        public void UpdateTargetPoints(){
            // Remake this check to only look backwards, there'll be issues when the forecast is too big and it thinks the leg is lagging behind but is actually too far ahead
            // This is saying: if this leg has lagged behind, and the surrounding legs are not also moving, then start moving toward the forecast
            if(TooFarFromFuturePoint() && controller.AllSurroundingLegsGrounded(this)){
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

        private Vector3 GetForecast(){
            var futurePos = futurePoint.transform.position;
            // Calcluate an ideal forecast position, then if that is not valid revert to either just the FuturePoint or potentially do a closest point check
            var ideal = futurePos + controller.transform.forward * controller.forecastDistance;
            // if ideal is a lot lower than futurepoint just return futurepoint
            if(ideal.y + controller.maximumDownstep < futurePos.y) return futurePos;

            // do a raycast to see what collider the ideal might be inside
            RaycastHit hit;
            if(Physics.Raycast(futurePos, controller.transform.forward, out hit, controller.forecastDistance)){
                // POTENTIAL BUG LOCATION: not using a layer mask here, but in testing didn't have any problems colliding with self. Keep an eye on this...

                // Get closest point on surface of collider.
                // If that is further than forecast distance from futuerpoint, return futurepoint

                var collidee = hit.collider;
                if(IsPointWithinCollider(collidee, ideal)){
                    // ideal is within a collider, so first lets check if we can put the foot on the top of that collider
                    // For now just use Vector3.down but it might be a good idea to update this to use a relative down direction (like spider.down?)
                    var castOrigin = new Vector3(ideal.x, ideal.y + controller.maxClimbHeight, ideal.z);
                    RaycastHit hit2;
                    if(Physics.Raycast(castOrigin, Vector3.down, out hit2, controller.maxClimbHeight)){
                        var surfacePoint = hit2.point;
                        return surfacePoint;
                    }

                    // can't do that, so lets put the foot on the closest point on the surface of the collider
                    var nearest = collidee.ClosestPoint(ideal);
                    if((futurePos - nearest).magnitude > controller.forecastDistance) return futurePos;
                    return nearest;
                }
            }

            // ideal has passed all checks so it is ok to move the foot towards that
            return ideal;
        }

        public float GetDistanceToFuturePoint(){
            return (futurePoint.transform.position - targetPoint.transform.position).magnitude;
        }

        // Made this a bit complicated because the spider leg height when stepping was interfering
        public float GetDistanceToForecast(){
            // Make a vertical plane at the position of the forecast, pointing backwards towards where we
            // expect the current position of the targetPoint to be.
            Plane plane = new Plane();
            plane.SetNormalAndPosition(controller.gameObject.transform.forward*-1, GetForecast());
            // GetDistanceToPoint will use closest point on the plane. Since our plane is vertical, this
            // effectively ignores the vertical difference between the fgorecast and target point
            return Mathf.Abs(plane.GetDistanceToPoint(targetPoint.transform.position));
        }

        public bool WithinRangeOfForecast(){
            return GetDistanceToForecast() <= controller.maxDistForLegToBeInRange;
        }

        public bool TooFarFromFuturePoint(){
            return GetDistanceToFuturePoint() >= controller.maxLegLag;
        }

        public static bool IsPointWithinCollider(Collider collider, Vector3 point)
        {
            return (collider.ClosestPoint(point) - point).sqrMagnitude < Mathf.Epsilon * Mathf.Epsilon;
        }

        public override string ToString(){
            return "Target[" + targetPoint.name + "], Future[" + futurePoint.name + "]";
        }
    }
}