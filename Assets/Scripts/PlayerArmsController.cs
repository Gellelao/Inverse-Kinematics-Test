using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DitzelGames.FastIK;

public class PlayerArmsController : MonoBehaviour
{
    public GameObject targetPointPrefab;
    public float armSpeed;
    public float narutoThreshold;
    private GameObject leftArm;
    private GameObject rightArm;
    private GameObject leftTarget;
    private GameObject rightTarget;
    private Vector3 leftTargetResting;
    private Vector3 rightTargetResting;
    private Vector3 leftTargetMoving;
    private Vector3 rightTargetMoving;
    // Start is called before the first frame update
    void Start()
    {
        // Find arms
        leftArm = transform.Find("Arms/LeftArm").gameObject;
        rightArm = transform.Find("Arms/RightArm").gameObject;

        // Create targets
        leftTarget = Instantiate(targetPointPrefab, transform.position - transform.right, transform.rotation);
        leftTarget.name = "PlayerLeftHandTarget";
        leftTarget.transform.parent = transform;
        rightTarget = Instantiate(targetPointPrefab, transform.position + transform.right, transform.rotation);
        rightTarget.name = "PlayerRightHandTarget";
        rightTarget.transform.parent = transform;

        // Set up hand positions
        leftTargetResting = leftTarget.transform.localPosition;
        leftTargetMoving = leftTarget.transform.localPosition - transform.forward;
        rightTargetResting = rightTarget.transform.localPosition;
        rightTargetMoving = rightTarget.transform.localPosition - transform.forward;

        // Assign targets to arms (Assumes that arms will only contain 1 IK script each)
        leftArm.GetComponentsInChildren<FastIKFabric>()[0].Target = leftTarget.transform;
        rightArm.GetComponentsInChildren<FastIKFabric>()[0].Target = rightTarget.transform;
    }

    // Update is called once per frame
    void Update()
    {
        var moveMagnitude = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical")).magnitude;
        Debug.Log(moveMagnitude);
        if (moveMagnitude > narutoThreshold)
        {
            leftTarget.transform.localPosition = Vector3.MoveTowards(leftTarget.transform.localPosition, leftTargetMoving, armSpeed*Time.deltaTime);
            rightTarget.transform.localPosition = Vector3.MoveTowards(rightTarget.transform.localPosition, rightTargetMoving, armSpeed*Time.deltaTime);
        }
        else{
            leftTarget.transform.localPosition = Vector3.MoveTowards(leftTarget.transform.localPosition, leftTargetResting, armSpeed*Time.deltaTime);
            rightTarget.transform.localPosition = Vector3.MoveTowards(rightTarget.transform.localPosition, rightTargetResting, armSpeed*Time.deltaTime);
        }
    }
}
