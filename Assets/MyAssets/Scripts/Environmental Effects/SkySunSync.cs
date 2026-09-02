using UnityEngine;

public class SkyUpdater : MonoBehaviour
{
    public CustomRenderTexture skyCubemap;
    public Material skyMaterial;
    private Vector3 lastSunDir;

    void Start()
    {
        UpdateSky();
    }

    void Update()
    {
        //Only re-bake when the sun has actually moved meaningfully
        if (Vector3.Dot(transform.forward, lastSunDir) < 0.9998f)
        {
            UpdateSky();
            Shader.SetGlobalTexture("_SkyCubemap", skyCubemap);
        }
    }

    void UpdateSky()
    {
        //Light forward points away from the sun
        Vector3 sunDir = -transform.forward;
        skyMaterial.SetVector("_SunDirection", sunDir);

        //Triggers a single re-render of the CRT, since UpdateMode is OnDemand
        skyCubemap.Update();
        lastSunDir = transform.forward;

        Shader.SetGlobalTexture("_PhysicalSkyCubemap", skyCubemap);
    }
}