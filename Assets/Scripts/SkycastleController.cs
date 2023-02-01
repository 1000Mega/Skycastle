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

    [SerializeField, Range(0f, 90f)]
    float maxGroundAngle = 25f;

    [SerializeField, Range(0f, 100f)]
    float maxSnapSpeed = 100f;

    [SerializeField, Min(0f)]
    float probeDistance = 1f;

    [SerializeField]
    LayerMask probeMask = -1;

    Vector3 vel, desiredVel;
    Vector3 contactNormal;

    Rigidbody body;

    bool tryJump;

    int groundContactCount;
    int stepsSinceLastGrounded, stepsSinceLastJump;
    bool OnGround => groundContactCount > 0; 

    int jumpPhase;

    float minGroundDotProduct;

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

        /*
        float accel = onGround ? maxAccel : maxAirAccel;    //This means we could climb many slopes if our air acceleration was high - may need another approach
        float maxSpeedChange = accel * Time.deltaTime;
        
        vel.x =
            Mathf.MoveTowards(vel.x, desiredVel.x, maxSpeedChange);
        vel.z =
            Mathf.MoveTowards(vel.z, desiredVel.z, maxSpeedChange); 
        */

        if (tryJump) {
            tryJump = false;
            Jump();
        }
        body.velocity = vel;

        ClearState(); 
    }

    void Jump()
    {
        //Probably want to adjust jump speed/gravity later
        if (OnGround || jumpPhase < extraJumps) {
            stepsSinceLastJump = 0;
            jumpPhase += 1;
            float jumpSpeed = Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);
            //Check for speed aligned with the contact or ground normal
            float alignedSpeed = Vector3.Dot(vel, contactNormal);

            //Making sure we don't gain unintended jump speed or lose speed from the double jump
            if (alignedSpeed > 0f)
            {
                jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f); 
            }
            vel += contactNormal * jumpSpeed;
        }
    }

    void UpdateState()
    {
        stepsSinceLastGrounded += 1;
        stepsSinceLastJump += 1;
        //Collisions affect velocity so we must grab the body's velocity
        vel = body.velocity;
        if (OnGround || SnapToGround() )
        {
            stepsSinceLastGrounded = 0;
            jumpPhase = 0;
            if (groundContactCount > 1) {
                contactNormal.Normalize();
            }
            
        }
        else //Use up vector in case we double jump
        {
            contactNormal = Vector3.up;
        }
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

    void EvaluateCollision (Collision collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;
            //onGround |= normal.y >= minGroundDotProduct;

            //If normal does not exceed slope max we are grounded
            if (normal.y >= minGroundDotProduct)
            {
                //onGround = true;
                groundContactCount += 1;
                contactNormal += normal; //Collect our contact normals (if there are multiple)
            }
        }
    }

    void ClearState ()
    {
        groundContactCount = 0; contactNormal = Vector3.zero;
    }

    void OnValidate()
    {
        minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
    }

    //Projecting velocity along the ground so we can move along slopes properly
    void AdjustVelocity ()
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

    Vector3 ProjectOnContactPlane (Vector3 vector)
    {
        return vector - contactNormal * Vector3.Dot(vector, contactNormal);
    }

    bool SnapToGround ()
    {
        //Only try snapping to ground once after losing contact
        if (stepsSinceLastGrounded > 1 || stepsSinceLastJump <=2) {
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
        if (hit.normal.y < minGroundDotProduct){
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
}
