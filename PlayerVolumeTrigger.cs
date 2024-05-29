using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Component.Prediction;
using UnityEngine.Events;
using System;

[RequireComponent(typeof(NetworkCollision))]
public class PlayerVolumeTrigger : NetworkBehaviour
{
    public enum VolumeType
    {
        swim = 0,
        fly = 1,
        gravity = 2,
        planet = 3
    }


    private NetworkCollision _networkCollision;

    private void Awake()
    {
        //Get the NetworkCollision component placed on this object.
        //You can place the component anywhere you would normally
        //use Unity collider callbacks!
        _networkCollision = GetComponent<NetworkCollision>();
        // Subscribe to the desired collision event
        _networkCollision.OnEnter += NetworkCollisionEnter;
        _networkCollision.OnStay += NetworkCollisionStay;
        _networkCollision.OnExit += NetworkCollisionExit;
    }

    private void OnDestroy()
    {
        //Since the NetworkCollider is placed on the same object as
        //this script we do not need to unsubscribe; the callbacks
        //will be destroyed with the object.
        //
        //But if your NetworkCollider resides on another object you
        //likely will want to unsubscribe to your events as well as shown.
        if (_networkCollision != null)
        {
            _networkCollision.OnEnter -= NetworkCollisionEnter;
            _networkCollision.OnStay -= NetworkCollisionStay;
            _networkCollision.OnExit -= NetworkCollisionExit;
        }
    }

    private void NetworkCollisionEnter(Collider other)
    {
        // Only run when not currently reconciling
        if (!base.PredictionManager.IsReconciling)
        {
            // Check for volumes
            PredictedVolume volume = other.GetComponent<PredictedVolume>();
            // Return of we did not get a volume
            if (volume == null) return;
            // Check for a swim volume
            SwimVolume swimVolume = volume.GetComponent<SwimVolume>();
            if (swimVolume != null)
            {
                // Get the gravity
                Debug.Log("Swim Volume");
            }
            // Check for a gravity volume
            GravityVolume gravityVolume = volume.GetComponent<GravityVolume>();
            if (gravityVolume != null)
            {
                // Get the gravity
                Debug.Log("In Gravity Volume => " + gravityVolume.gravityDirection);
                GetComponent<PlayerPawn>().SetGravity(gravityVolume.gravityDirection);
            }
        }

        /*
        //Always apply velocity to this player on enter, even if reconciling.
        PlayerMover pm = GetComponent<PlayerMover>();
        //For this example we are pushing away from the other object.
        Vector3 dir = (transform.position - other.gameObject.transform).normalized;
        pm.PredictionRigidbody.AddForce(dir * 50f, ForceMode.Impulse);
        */

    }

    private void NetworkCollisionStay(Collider other)
    {
        // Handle collision stay logic
    }

    private void NetworkCollisionExit(Collider other)
    {
        // Only run when not currently reconciling
        if (!base.PredictionManager.IsReconciling)
        {
            // Check for volumes
            PredictedVolume volume = other.GetComponent<PredictedVolume>();
            // Return of we did not get a volume
            if (volume == null) return;
            // Check for a swim volume
            SwimVolume swimVolume = volume.GetComponent<SwimVolume>();
            if (swimVolume != null)
            {
                // Get the gravity
                Debug.Log("Swim Volume");
            }
            // Check for a gravity volume
            GravityVolume gravityVolume = volume.GetComponent<GravityVolume>();
            if (gravityVolume != null)
            {
                // Get the gravity
                Debug.Log("Out Gravity Volume => " + GetComponent<PlayerPawn>().gravity);
                GetComponent<PlayerPawn>().SetGravity(GetComponent<PlayerPawn>().gravity);
            }
        }
    }
}
