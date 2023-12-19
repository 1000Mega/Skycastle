using UnityEngine;

[RequireComponent(typeof(Camera))]
public class OrbitCamera : MonoBehaviour
{
    [SerializeField]
    Transform focus = default;

    [SerializeField, Range(1f, 20f)]
    float distance = 5f;

    [SerializeField, Min(0f)]
    float focusRadius = 1f;

    [SerializeField, Range(0f, 1f)]
    float focusCentering = 0.5f;

    [SerializeField, Range(0f, 360f)]
    float rotationSpeed = 90f;

    [SerializeField, Range(-89f, 89f)]
    float minVerticalAngle = -30f, maxVerticalAngle = 60f;

    [SerializeField, Min(0f)]
    float alignDelay = 5f;

    [SerializeField, Range(0f, 90f)]
    float alignSmoothRange = 45f;

    //For performance reasons or stability, we can use layer masks to ignore small detailed geometry
    [SerializeField]
    LayerMask obstructionMask = -1;

    Camera regularCamera;

    Vector3 focusPoint, previousFocusPoint;

    Vector2 orbitAngles = new Vector2(45f, 0f);

    //https://catlikecoding.com/unity/tutorials/movement/orbit-camera/ 4.2
    //3D vector for half extends to use for box cast (half dimensions)
    Vector3 CameraHalfExtends
    {
        get {
            Vector3 halfExtends;
            halfExtends.y =
                regularCamera.nearClipPlane *
                Mathf.Tan(0.5f * Mathf.Deg2Rad * regularCamera.fieldOfView);
            halfExtends.x = halfExtends.y * regularCamera.aspect;
            halfExtends.z = 0f;
            return halfExtends;
        }
    }

    //Finished Orbit Camera

    float lastManualRotationTime;

    void Awake()
    {
        regularCamera = GetComponent<Camera>();
        focusPoint = focus.position;
        transform.localRotation = Quaternion.Euler(orbitAngles);
    }

    void OnValidate()
    {
        if (maxVerticalAngle < minVerticalAngle) {
            maxVerticalAngle = minVerticalAngle;
        }
    }

    void LateUpdate()
    {
        UpdateFocusPoint();
        Quaternion lookRotation;
        //Only recalculate rotation if there's a change, check manual first
        if (ManualRotation() || AutomaticRotation())
        {
            ConstrainAngles();
            //Converts orbitAngles Vec2 to Vector3, with Z rotation set to zero
            lookRotation = Quaternion.Euler(orbitAngles);
        }
        else {
            lookRotation = transform.localRotation;
        }
        
        Vector3 lookDirection = transform.forward;
        Vector3 lookPosition = focusPoint - lookDirection * distance;

        Vector3 rectOffset = lookDirection * regularCamera.nearClipPlane;
        Vector3 rectPosition = lookPosition + rectOffset;
        Vector3 castFrom = focus.position;
        Vector3 castLine = rectPosition - castFrom;
        float castDistance = castLine.magnitude;
        Vector3 castDirection = castLine / castDistance;


        //Use cast hit distance to relocate camera if we're hitting something that will block view
        //Use box cast to make sure camera near plane isn't cutting through geometry
        //Have a solution for minimal distance?
        //Half extends = half the box's width, height, and depth
        if (Physics.BoxCast(
            castFrom, CameraHalfExtends, castDirection, out RaycastHit hit, 
            lookRotation, castDistance, obstructionMask)) 
        {
            rectPosition = castFrom + castDirection * hit.distance;
            lookPosition = rectPosition - rectOffset;
        }

        transform.SetPositionAndRotation(lookPosition, lookRotation);
    }

    void UpdateFocusPoint()
    {
        previousFocusPoint = focusPoint;
        Vector3 targetPoint = focus.position;
        if (focusRadius > 0f) 
        {
            float distance = Vector3.Distance(targetPoint, focusPoint);
            //Returning focus gradually such that it continues if the focus stops
            float t = 1f;
            if (distance > 0.01f && focusCentering > 0f) {
                t = Mathf.Pow(1f - focusCentering, Time.unscaledDeltaTime);
            }
            if (distance > focusRadius) {
                //focusPoint = Vector3.Lerp(
                   // targetPoint, focusPoint, focusRadius/distance
                  //  );
                  t = Mathf.Min(t, focusRadius / distance);
            }
            focusPoint = Vector3.Lerp(targetPoint, focusPoint, t);
        }
        else {
            focusPoint = targetPoint;
        }
        
    }

    bool ManualRotation()
    {
        Vector2 input = new Vector2(
            Input.GetAxis("Vertical Camera"),
            Input.GetAxis("Horizontal Camera")
        );
        const float e = 0.001f;
        if (input.x < -e || input.x > e || input.y < -e || input.y > e) {
            orbitAngles += rotationSpeed * Time.unscaledDeltaTime * input;
            lastManualRotationTime = Time.unscaledTime;
            return true;
        }
        return false;
        //Return whether or not we've changed the angle
    }

    bool AutomaticRotation()
    {
        if (Time.unscaledTime - lastManualRotationTime < alignDelay) {
            return false;
        }

        Vector2 movement = new Vector2(
            focusPoint.x - previousFocusPoint.x,
            focusPoint.z - previousFocusPoint.z
            );
        float movementDeltaSqr = movement.sqrMagnitude;
        if (movementDeltaSqr < 0.0001f) {
            return false;
        }
        //We already have the squared magnitude so it's more efficient to normalize ourselves
        float headingAngle = GetAngle(movement / Mathf.Sqrt(movementDeltaSqr));
        float deltaAbs = Mathf.Abs(Mathf.DeltaAngle(orbitAngles.y, headingAngle));
        float rotationChange = 
            rotationSpeed * Mathf.Min(Time.unscaledDeltaTime, movementDeltaSqr);
        //Within a certain range we do not adjust at full rotation speed
        if (deltaAbs < alignSmoothRange) { 
            rotationChange *= deltaAbs / alignSmoothRange;
        }
        else if (180f - deltaAbs < alignSmoothRange) {
            rotationChange *= (180f - deltaAbs) / alignSmoothRange;
        }
        orbitAngles.y =
            Mathf.MoveTowardsAngle(orbitAngles.y, headingAngle, rotationChange);
        return true;
    }

    static float GetAngle(Vector2 direction)
    {
        float angle = Mathf.Acos(direction.y) * Mathf.Rad2Deg;
        //If x is negative, angle is counterclockwise and we subtract from 360
        return direction.x < 0f ? 360f - angle : angle;
    }

    void ConstrainAngles()
    {
        orbitAngles.x = 
            Mathf.Clamp(orbitAngles.x, minVerticalAngle, maxVerticalAngle);

        if (orbitAngles.y <0f) {
            orbitAngles.y += 360f;
        }
        else if (orbitAngles.y >= 360f) {
            orbitAngles.y -= 360f;
        }
    }
}
