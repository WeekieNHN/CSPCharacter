using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwimVolume : PredictedVolume
{
    void Awake()
    {
        // make sure that we have the appropriate volume type
        base.volumeType = PlayerVolumeTrigger.VolumeType.swim;
    }
}
