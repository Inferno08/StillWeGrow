using System.Collections.Generic;
using UnityEngine;

public class CameraOccluderFade : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private LayerMask occluderMask;
    [SerializeField] private float fadedAlpha = 0.3f;
    [SerializeField] private float fadeSpeed = 6f;

    [Header("Detection region around player")]
    [Tooltip("Radius of the area around the player that gets 'cleared' of occluders.")]
    [SerializeField] private float clearRadius = 1.5f;
    [Tooltip("How high above the player's feet to check (catches roofs overhead).")]
    [SerializeField] private float clearHeight = 3f;
    [Tooltip("How far past the player an occluder can be before it stops fading.")]
    [SerializeField] private float behindTolerance = 1f;

    [Header("Debug")]
    [Tooltip("Log every occluder the rays hit. Turn off once it's working.")]
    [SerializeField] private bool debugLogHits = true;
    [Tooltip("Draw the sample rays in the Scene view (Play mode, gizmos on).")]
    [SerializeField] private bool debugDrawRays = true;

    private readonly Dictionary<Renderer, float> tracked = new();
    private readonly HashSet<Renderer> hitThisFrame = new();

    private void LateUpdate()
    {
        if (player == null || cameraTransform == null)
        {
            if (debugLogHits) Debug.LogWarning("CameraOccluderFade: player or cameraTransform not assigned.");
            return;
        }

        hitThisFrame.Clear();

        Vector3 origin = cameraTransform.position;

        // Horizontal camera->player direction & distance (used to reject geometry
        // that sits BEYOND the player, which can't be blocking the view).
        Vector3 camToPlayer = player.position - origin;
        camToPlayer.y = 0f;
        float playerHoriz = camToPlayer.magnitude;
        Vector3 camToPlayerDir = camToPlayer.sqrMagnitude > 0.001f
            ? camToPlayer.normalized
            : Vector3.forward;

        // Sample points in a volume around the player.
        Vector3 basePos = player.position;
        Vector3[] samples =
        {
            basePos,
            basePos + Vector3.up * clearHeight,
            basePos + player.right * clearRadius,
            basePos - player.right * clearRadius,
            basePos + Vector3.up * clearHeight + player.right * clearRadius,
            basePos + Vector3.up * clearHeight - player.right * clearRadius,
        };

        foreach (var target in samples)
        {
            Vector3 dir = target - origin;
            float dist = dir.magnitude;

            // Visualize the ray in the Scene view
            if (debugDrawRays)
                Debug.DrawLine(origin, origin + dir.normalized * dist, Color.cyan);

            var hits = Physics.RaycastAll(origin, dir.normalized, dist,
                                          occluderMask, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (debugLogHits)
                    Debug.Log($"Fade ray hit: {h.collider.name} (layer {LayerMask.LayerToName(h.collider.gameObject.layer)})");

                // Skip occluders beyond the player (behind, relative to camera).
                Vector3 flatHit = h.point - origin;
                flatHit.y = 0f;
                float hitAlong = Vector3.Dot(flatHit, camToPlayerDir);
                if (hitAlong > playerHoriz + behindTolerance)
                {
                    if (debugLogHits)
                        Debug.Log($"  -> rejected as 'behind player' ({hitAlong:0.00} > {playerHoriz + behindTolerance:0.00})");
                    continue;
                }

                var r = h.collider.GetComponent<Renderer>();
                if (r == null)
                {
                    if (debugLogHits)
                        Debug.Log($"  -> {h.collider.name} has no Renderer on the collider's GameObject");
                    continue;
                }

                hitThisFrame.Add(r);
                if (!tracked.ContainsKey(r)) tracked[r] = 1f;
            }
        }

        // Fade caught renderers down toward fadedAlpha, restore the rest to 1.
        var keys = new List<Renderer>(tracked.Keys);
        foreach (var r in keys)
        {
            if (r == null) { tracked.Remove(r); continue; }
            float target = hitThisFrame.Contains(r) ? fadedAlpha : 1f;
            float a = Mathf.MoveTowards(tracked[r], target, fadeSpeed * Time.deltaTime);
            tracked[r] = a;
            SetAlpha(r, a);
            if (a >= 0.999f && !hitThisFrame.Contains(r)) tracked.Remove(r);
        }
    }

    private void SetAlpha(Renderer r, float a)
    {
        var mpb = new MaterialPropertyBlock();
        r.GetPropertyBlock(mpb);
        Color c = r.sharedMaterial.color;
        c.a = a;
        mpb.SetColor("_BaseColor", c);
        r.SetPropertyBlock(mpb);
    }
}