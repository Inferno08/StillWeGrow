using UnityEngine;
using Unity.Cinemachine;

public class CameraEdgeAdjust : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private CinemachineCamera vcam;
    [SerializeField] private CinemachinePositionComposer composer;

    [Header("Basin bounds")]
    [Tooltip("Centre of the basin. Usually the generator's transform position.")]
    [SerializeField] private Transform basinCentre;
    [Tooltip("Radius of the basin floor — match the generator's baseRadius.")]
    [SerializeField] private float basinRadius = 30f;
    [Tooltip("How close to the rim before the camera starts lifting.")]
    [SerializeField] private float edgeStartDistance = 8f;

    [Header("Camera angle")]
    [SerializeField] private float normalPitch = 50f;
    [SerializeField] private float edgePitch = 65f;

    [Header("Camera distance")]
    [SerializeField] private float normalDistance = 22f;
    [SerializeField] private float edgeDistance = 26f;

    [Header("Smoothing")]
    [SerializeField] private float smoothSpeed = 4f;

    private float currentPitch;
    private float currentDistance;

    private void Start()
    {
        currentPitch = normalPitch;
        currentDistance = normalDistance;
    }

    private void Update()
    {
        if (player == null || vcam == null) return;

        Vector3 centre = basinCentre != null ? basinCentre.position : Vector3.zero;

        // Radial distance from the basin centre, flattened
        Vector3 flat = player.position - centre;
        flat.y = 0f;
        float distFromCentre = flat.magnitude;

        // How far from the rim the player is
        float distToRim = basinRadius - distFromCentre;

        // 0 out in the open, 1 right at the rim
        float t = Mathf.InverseLerp(edgeStartDistance, 0f, distToRim);

        float targetPitch = Mathf.Lerp(normalPitch, edgePitch, t);
        float targetDistance = Mathf.Lerp(normalDistance, edgeDistance, t);

        currentPitch = Mathf.Lerp(currentPitch, targetPitch, smoothSpeed * Time.deltaTime);
        currentDistance = Mathf.Lerp(currentDistance, targetDistance, smoothSpeed * Time.deltaTime);

        vcam.transform.rotation = Quaternion.Euler(currentPitch, vcam.transform.eulerAngles.y, 0f);
        if (composer != null) composer.CameraDistance = currentDistance;
    }
}