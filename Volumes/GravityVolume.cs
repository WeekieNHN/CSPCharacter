using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GravityVolume : PredictedVolume
{

    [Header("Gravity")]
    public Vector3 gravityDirection = Vector3.down * 9.81f;

    void Awake()
    {
        // make sure that we have the appropriate volume type
        base.volumeType = PlayerVolumeTrigger.VolumeType.gravity;
    }
}
