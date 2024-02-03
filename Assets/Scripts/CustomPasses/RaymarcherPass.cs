using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public abstract class RaymarcherPass : CustomPass
{
    public abstract void SetVolumetricParams(VolumetricParams volumeParams);
}