using UnityEngine;

public class MenuCameraFloat : MonoBehaviour
{
    [Header("Walking / Pacing Settings")]
    [Tooltip("How far left and right the person paces.")]
    public float walkSideRange = 1.0f;
    [Tooltip("How far forward and back the person steps.")]
    public float walkForwardRange = 0.15f;
    [Tooltip("How fast the person is pacing back and forth.")]
    public float walkSpeed = 0.4f;

    [Header("Handheld Floating Settings")]
    [Tooltip("Slight breathing/floating movements from holding the camera.")]
    public Vector3 handFloatRange = new Vector3(0.06f, 0.05f, 0.03f);
    [Tooltip("Speed of the breathing movements.")]
    public float handFloatSpeed = 0.8f;

    [Header("Camera Tilt (Rotation)")]
    [Tooltip("How much the camera naturally tilts.")]
    public Vector3 rotationRange = new Vector3(0.8f, 1.2f, 0.5f);
    [Tooltip("Speed of the camera tilting.")]
    public float rotationSpeed = 0.6f;

    [Header("Stabilization (The Fix)")]
    [Range(1f, 15f)]
    [Tooltip("Higher = snappier/jittery. Lower = heavier, smoother, and more cinematic.")]
    public float cameraSmoothness = 3.0f;

    private Vector3 startPosition;
    private Quaternion startRotation;

    private float seedX;
    private float seedY;
    private float seedZ;
    private float walkTimer;

    void Start()
    {
        startPosition = transform.localPosition;
        startRotation = transform.localRotation;

        seedX = Random.value * 100f;
        seedY = Random.value * 100f;
        seedZ = Random.value * 100f;
    }

    void Update()
    {
        // 1. PACING POSITION (Sine waves)
        walkTimer += Time.deltaTime * walkSpeed;
        float walkX = Mathf.Sin(walkTimer) * walkSideRange;
        float walkZ = Mathf.Abs(Mathf.Cos(walkTimer)) * walkForwardRange;
        Vector3 currentWalkPos = new Vector3(walkX, 0f, walkZ);

        // 2. SMOOTH HANDHELD FLOAT (Slowed down Perlin Noise)
        float floatX = (Mathf.PerlinNoise(seedX, Time.time * handFloatSpeed) - 0.5f) * 2f * handFloatRange.x;
        float floatY = (Mathf.PerlinNoise(seedY, Time.time * handFloatSpeed) - 0.5f) * 2f * handFloatRange.y;
        float floatZ = (Mathf.PerlinNoise(seedZ, Time.time * handFloatSpeed) - 0.5f) * 2f * handFloatRange.z;
        Vector3 currentFloatPos = new Vector3(floatX, floatY, floatZ);

        // Calculate raw target position, then smoothly slide towards it
        Vector3 targetPosition = startPosition + currentWalkPos + currentFloatPos;
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, Time.deltaTime * cameraSmoothness);

        // 3. SMOOTH ROTATION
        float rotX = (Mathf.PerlinNoise(seedX + 30f, Time.time * rotationSpeed) - 0.5f) * 2f * rotationRange.x;
        // Subtle automatic pan toward center of the screen based on walk position
        float dynamicPan = -walkX * 1.2f;
        float rotY = ((Mathf.PerlinNoise(seedY + 30f, Time.time * rotationSpeed) - 0.5f) * 2f * rotationRange.y) + dynamicPan;
        float rotZ = (Mathf.PerlinNoise(seedZ + 30f, Time.time * rotationSpeed) - 0.5f) * 2f * rotationRange.z;

        Quaternion targetRotation = startRotation * Quaternion.Euler(rotX, rotY, rotZ);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, Time.deltaTime * cameraSmoothness);
    }
}