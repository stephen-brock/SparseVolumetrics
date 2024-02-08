using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public abstract class RaymarcherPass : CustomPass
{
    public abstract void SetVolumetricParams(VolumetricParams volumeParams);

    public abstract void SetParameter(string id, float value);
}