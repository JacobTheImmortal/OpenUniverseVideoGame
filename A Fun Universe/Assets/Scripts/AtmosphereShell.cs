using UnityEngine;

/// Holds per-planet numbers the shader needs.
/// Nothing in here alloc-frees every frame – it’s set-and-forget.
[RequireComponent(typeof(Renderer))]
public class AtmosphereShell : MonoBehaviour
{
    
    

    [Tooltip("Radius of the planet surface (inner fade-out).")]
    public float planetRadius = 1f;

    [Tooltip("Thickness of the visible atmosphere shell.")]
    public float shellThickness = 0.04f;

    [ColorUsage(false, true)]
    public Color tint = new Color(0.3f, 0.55f, 1f, 0.5f);



    
    static readonly int _R = Shader.PropertyToID("_PlanetRadius");
    static readonly int _Shell = Shader.PropertyToID("_ShellThickness");

    void Awake()
    {
        // duplicate the material so every planet can have its own numbers
        GetComponent<Renderer>().material =
            Instantiate(GetComponent<Renderer>().sharedMaterial);
        PushToGPU();
    }

    public void Configure(Vector3 centre, float innerR, float thickness)
    {
        
        planetRadius = innerR;
        shellThickness = thickness;
        PushToGPU();
    }

    static readonly int _TintID = Shader.PropertyToID("_Tint");   // add at top

    void PushToGPU()
    {
        var m = GetComponent<Renderer>().material;
        
        m.SetFloat(_R, planetRadius);
        m.SetFloat(_Shell, shellThickness);
        m.SetColor(_TintID, tint);        // <-- NEW
    }
}