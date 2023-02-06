using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkycastleController : MonoBehaviour
{
    [SerializeField, Range(0f, 100f)]
    float maxSpeed = 10f;

    [SerializeField, Range(0f, 100f)]
    float maxAccel = 10f, maxAirAccel = 3f;

    [SerializeField, Range(0f, 10f)]
    float jumpHeight = 2f;

    [SerializeField, Range(0f, 5f)]
    int extraJumps = 0;

    [SerializeField, Range(0, 90)]
    float maxGroundAngle = 25f, maxStairsAngle = 50f;

    [SerializeField, Range(0f, 100f)]
    float maxSnapSpeed = 100f;

    [SerializeField, Min(0f)]
    float probeDistance = 1f;

    [SerializeField]
    LayerMask probeMask = -1, stairsMask = -1;

    Vector3 vel, desiredVel;
    Vector3 contactNormal, steepNormal;

    Rigidbody body;

    bool tryJump;

    int groundContactCount, steepContactCount;
    int stepsSinceLastGrounded, stepsSinceLastJump;
    bool OnGround => groundContactCount > 0;
    bool OnSteep => steepContactCount > 0;

    int jumpPhase;

    float minGroundDotProduct, minStairsDotProduct;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        OnValidate();
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 playerInput;
        playerInput.x = Input.GetAxis("Horizontal");
        playerInput.y = Input.GetAxis("Vertical");
        playerInput = Vector2.ClampMagnitude(playerInput, 1f);

        //Use the OR operator here to make sure it doesn't get set back to false unless we set it
        tryJump |= Input.GetButtonDown("Jump");

        desiredVel =
            new Vector3(playerInput.x, 0f, playerInput.y) * maxSpeed;
        //vel += desiredVel * Time.deltaTime;

        //White when in air
        GetComponent<Renderer>().material.SetColor(
            "_Color", OnGround ? Color.black : Color.white
            );

        //Vector3 deltaPos = vel * Time.deltaTime;
        //transform.localPosition += deltaPos;
    }

    void FixedUpdate()
    {
        UpdateState();
        AdjustVelocity();

        if (tryJump) {
            tryJump = false;
            Jump();
        }
        body.velocity = vel;

        ClearState();
    }

    void Jump()
    {
        //For other cases like wall jumps. Could use contactNormal without wall jumps
        Vector3 jumpDirection;
        if(OnGround) {
            jumpDirection = contactNormal;
        }
        else if (OnSteep){ //Wall jumps
            jumpDirection = steepNormal;
            jumpPhase = 0; //Remove this if we don't want to refresh air jumps
        }
        else if (extraJumps > 0 && jumpPhase <= extraJumps) { 
            //Make sure we have the correct amount of air jumps if we fall off instead of jump
            if (jumpPhase == 0) {
                jumpPhase = 1;
            }
            jumpDirection = contactNormal;    
        }
        else {
            return;
        }

        //Probably want to adjust jump speed/gravity later
        if (OnGround || jumpPhase < extraJumps) {
            stepsSinceLastJump = 0;
            jumpPhase += 1;
            float jumpSpeed = Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);
            jumpDirection = (jumpDirection + Vector3.up).normalized;
            //Check for speed aligned with the contact or ground normal
            float alignedSpeed = Vector3.Dot(vel, jumpDirection);
            //float alignedSpeed = Vector3.Dot(vel, contactNormal);

            //Making sure we don't gain unintended jump speed or lose speed from the double jump
            if (alignedSpeed > 0f)
            {
                jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f);
            }
            vel += jumpDirection * jumpSpeed;
        }
    }

    void UpdateState()
    {
        stepsSinceLastGrounded += 1;
        stepsSinceLastJump += 1;
        //Collisions affect velocity so we must grab the body's velocity
        vel = body.velocity;
        if (OnGround || SnapToGround() || CheckSteepContacts())
        {
            stepsSinceLastGrounded = 0;
            if (stepsSinceLastJump > 1) {
                jumpPhase = 0;
            }
            
            if (groundContactCount > 1) {
                contactNormal.Normalize();
            }

        }
        else //Use up vector in case we double jump
        {
            contactNormal = Vector3.up;
        }
    }
    void ClearState()
    {
        groundContactCount = steepContactCount = 0; 
        contactNormal = steepNormal = Vector3.zero;
    }

    void OnCollisionEnter(Collision collision)
    {
        EvaluateCollision(collision);
    }

    //Called each physics step while collision is active
    void OnCollisionStay(Collision collision)
    {
        EvaluateCollision(collision);
    }

    void EvaluateCollision(Collision collision)
    {
        float minDot = GetMinDot(collision.gameObject.layer);
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;

            //If normal does not exceed slope/stair max we are grounded
            if (normal.y >= minDot)
            {
                //onGround = true;
                groundContactCount += 1;
                contactNormal += normal; //Collect our contact normals (if there are multiple)
            }
            //Slight allowance for slightly off vertical walls (which should be 0)
            else if (normal.y > -0.01f)
            {
                steepContactCount += 1;
                steepNormal += normal;
            }
        }
    }

    //Attempt to convert multiple steep contacts into virtual ground (such as a small pit)
    bool CheckSteepContacts()
    {
        if (steepContactCount > 1)
        {
            steepNormal.Normalize();
            if (steepNormal.y >= minGroundDotProduct)
            {
                groundContactCount = 1;
                contactNormal = steepNormal;
                return true;
            }
        }
        return false;
    }

    void OnValidate()
    {
        minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
        minStairsDotProduct = Mathf.Cos(maxStairsAngle * Mathf.Deg2Rad);
    }

    //Projecting velocity along the ground so we can move along slopes properly
    void AdjustVelocity()
    {
        Vector3 xAxis = ProjectOnContactPlane(Vector3.right).normalized;
        Vector3 zAxis = ProjectOnContactPlane(Vector3.forward).normalized;

        float currentX = Vector3.Dot(vel, xAxis);
        float currentZ = Vector3.Dot(vel, zAxis);

        float accel = OnGround ? maxAccel : maxAirAccel;
        float maxSpeedChange = accel * Time.deltaTime;

        float newX =
                Mathf.MoveTowards(currentX, desiredVel.x, maxSpeedChange);
        float newZ =
                Mathf.MoveTowards(currentZ, desiredVel.z, maxSpeedChange);

        vel += xAxis * (newX - currentX) + zAxis * (newZ - currentZ);
    }

    Vector3 ProjectOnContactPlane(Vector3 vector)
    {
        return vector - contactNormal * Vector3.Dot(vector, contactNormal);
    }

    bool SnapToGround()
    {
        //Only try snapping to ground once after losing contact
        if (stepsSinceLastGrounded > 1 || stepsSinceLastJump <= 2) {
            return false;
        }
        float speed = vel.magnitude;
        //Snap speed should be a bit different from the max speed to avoid inconsistent results due to precision limitations
        if (speed > maxSnapSpeed) {
            return false;
        }
        //Send a short raycast looking for ground objects
        if (!Physics.Raycast(body.position, Vector3.down, out RaycastHit hit, probeDistance, probeMask)) {
            return false; //Abort if no ground 
        }
        if (hit.normal.y < GetMinDot(hit.collider.gameObject.layer)) {
            return false; //Surface we hit isn't ground
        }

        //Attempting to snap to ground
        groundContactCount = 1;
        contactNormal = hit.normal;

        float dot = Vector3.Dot(vel, hit.normal);
        if (dot > 0f)
        {
            vel = (vel - hit.normal * dot).normalized * speed;
        }
        return true;
    }

    float GetMinDot(int layer) //Return the minimum for the given layer
    {
        return (stairsMask & (1 << layer)) == 0 ?
            minGroundDotProduct : minStairsDotProduct;
    }
}
