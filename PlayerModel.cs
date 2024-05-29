using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerPawn))]
public class PlayerModel : MonoBehaviour
{

    public Transform modelRootPivot;
    public Transform modelEyePivot;

    private PlayerPawn playerPawn;
    private PlayerDriver playerDriver;

    private void Awake()
    {
        playerPawn = GetComponent<PlayerPawn>();
        playerDriver = GetComponent<PlayerDriver>();
    }

    private void Update()
    {
        // Parity the position from the "motor"
        modelRootPivot.localPosition = playerPawn.rootPivot.localPosition;
    }

    private void LateUpdate()
    {
        // Call model smoothing
        ModelSmoothing();
        // Smooth the cameras
        CameraSmoothing();
    }


    private float _cameraYaw = 0.0f;
    private float _cameraPitch = 0.0f;

    public void UpdateLocalLook(Vector2 look, float minPitch, float maxPitch)
    {
        // Update the yaw
        if (look.x != 0.0f) _cameraYaw += look.x;
        // Update the pitch
        if (look.y != 0.0f) _cameraPitch = Mathf.Clamp(_cameraPitch + look.y, minPitch, maxPitch);
    }


    #region SMOOTHING

    [SerializeField] private Transform modelTransform;

    private float _smoothedPositionalTime = 0.75f;
    private float _positionalSmoothingRateMin = 17f;
    private float _positionalSmoothingRateMax = 25f;

    float _movingTime = 0f;
    private void ModelSmoothing()
    {
        if (modelTransform.position != playerPawn.modelPositionTarget)
        {
            // Calculate how far the model has movement
            float distance = Mathf.Max(0.1f, Vector3.Distance(modelTransform.position, playerPawn.modelPositionTarget));
            // If distance is greater than teleport threshold
            if (distance > 10f)
            {
                // Teleport the model
                modelTransform.position = playerPawn.modelPositionTarget;
            }
            else
            {
                // Add deltaTime since we're called by framerate not tick rate
                _movingTime += Time.deltaTime;
                // Calculate the smoothing rate
                float smoothingPercent = _movingTime / _smoothedPositionalTime;
                float smoothingRate = Mathf.Lerp(_positionalSmoothingRateMax, _positionalSmoothingRateMin, smoothingPercent);
                // Move the transform
                modelTransform.position = Vector3.MoveTowards(modelTransform.position, playerPawn.modelPositionTarget, smoothingRate * distance * Time.deltaTime);
            }
        }
        else
        {
            // Reset the moving Time
            _movingTime = 0f;
        }
    }

    #endregion

    #region SPECTATED LOOK SMOOTHING

    [SerializeField] private Vector2 lookVector;
    [SerializeField] private float _cameraSmoothedPositionalTime = 0.75f;
    [SerializeField] private float _cameraPSmoothingRateMin = 17f;
    [SerializeField] private float _cameraPositionalSmoothingRateMax = 25f;
    float _cameraMovingTime = 0.0f;

    private void CameraSmoothing()
    {
        // If we're the owning player
        if (playerDriver.IsOwner)
        {
            // Update model eye and root
            modelRootPivot.localRotation = Quaternion.Euler(0.0f, _cameraYaw, 0.0f);
            modelEyePivot.localRotation = Quaternion.Euler(_cameraPitch, 0.0f, 0.0f);
            // Return
            return;
        }
        // If we're spectating this player
        // Smoothly interpolate
        if (lookVector != playerPawn.modelLookTarget)
        {
            // Calculate how far the model has movement
            float distance = Mathf.Max(0.1f, Vector3.Distance(lookVector, playerPawn.modelLookTarget));

            // If distance is greater than teleport threshold
            if (distance > 60f)
            {
                // Teleport the model
                lookVector = playerPawn.modelLookTarget;
            }
            else
            {
                // Add deltaTime since we're called by framerate not tick rate
                _cameraMovingTime += Time.deltaTime;
                // Calculate the smoothing rate
                float smoothingPercent = _cameraMovingTime / _cameraSmoothedPositionalTime;
                float smoothingRate = Mathf.Lerp(_cameraPositionalSmoothingRateMax, _cameraPSmoothingRateMin, smoothingPercent);
                // Move the transform
                lookVector = Vector3.MoveTowards(lookVector, playerPawn.modelLookTarget, smoothingRate * distance * Time.deltaTime);
            }
        }
        else
        {
            // Reset the moving Time
            _cameraMovingTime = 0f;
        }
        // Apply look rotation
        modelRootPivot.localRotation = Quaternion.Euler(0.0f, lookVector.y, 0.0f);
        modelEyePivot.localRotation = Quaternion.Euler(lookVector.x, 0.0f, 0.0f);
    }

    #endregion

}
