using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerTargeting : MonoBehaviour
{
    private enum Action { None, Till, Plant, Harvest, Drink, Storage }

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private CharacterController playerCollider;
    [SerializeField] private Transform marker;
    [SerializeField] private EditableTerrain terrain;
    [SerializeField] private DigMeter digMeter;
    [SerializeField] private PlantTamp plantTamp;

    [Header("Crop to plant")]
    [SerializeField] private CropData selectedCrop;
    [SerializeField] private GameObject plantRootPrefab;

    [Header("Layers")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private LayerMask wallMask;

    [Header("Interact layers")]
    [SerializeField] private LayerMask cropMask;
    [SerializeField] private LayerMask storageMask;
    [SerializeField] private LayerMask waterMask;

    [Header("Rules")]
    [SerializeField] private float maxReach = 3f;
    [SerializeField] private float interactReach = 3f;
    [SerializeField] private float wallClearance = 0.75f;
    [SerializeField] private float wallCheckHeight = 0.5f;

    [Header("Height limits")]
    [SerializeField] private float maxHeightAbove = 0.5f;
    [SerializeField] private float maxHeightBelow = 1.5f;

    [Header("Tilling")]
    [SerializeField] private float tillRadius = 1.4f;

    [Header("Planting")]
    [SerializeField] private float defaultPlantSpacing = 0.7f;

    [Header("Drinking")]
    [SerializeField] private float drinkAmount = 40f;
    [SerializeField] private float drinkStandDistance = 1f;
    [Tooltip("Log the water-edge scan to the Console for debugging.")]
    [SerializeField] private bool debugDrink = false;

    [Header("Standing")]
    [SerializeField] private float standDistance = 1.3f;
    [SerializeField] private bool standBehindSite = true;
    [SerializeField] private float standClearance = 0.45f;

    [Header("Marker colors")]
    [SerializeField] private Color tillColor = new Color(0.7f, 0.5f, 0.3f, 0.6f);
    [SerializeField] private Color plantColor = new Color(0.3f, 1f, 0.3f, 0.6f);
    [SerializeField] private Color harvestColor = new Color(1f, 0.85f, 0.2f, 0.7f);
    [SerializeField] private Color drinkColor = new Color(0.3f, 0.7f, 1f, 0.6f);
    [SerializeField] private Color storageColor = new Color(0.8f, 0.6f, 1f, 0.6f);
    [SerializeField] private Color invalidColor = new Color(1f, 0.3f, 0.3f, 0.4f);

    private Camera cam;
    private Renderer markerRenderer;

    private Action currentAction;
    private bool actionValid;
    private Vector3 targetPoint;
    private Vector3 pendingPoint;
    private PlantedCrop targetCrop;
    private StoragePile targetPile;
    private Vector3 waterPoint;
    private bool busy;

    public CropData SelectedCrop { get => selectedCrop; set => selectedCrop = value; }

    private void Awake()
    {
        cam = Camera.main;
        if (marker != null) markerRenderer = marker.GetComponent<Renderer>();
        if (playerCollider == null && player != null)
            playerCollider = player.GetComponent<CharacterController>();
        if (terrain == null) terrain = EditableTerrain.Instance;
    }

    private void OnEnable()
    {
        if (digMeter != null)
        {
            digMeter.OnStrike += HandleStrike;
            digMeter.OnDigComplete += HandleTillComplete;
            digMeter.OnDigCancelled += HandleTillCancelled;
        }
        if (plantTamp != null)
        {
            plantTamp.OnComplete += HandleTampComplete;
            plantTamp.OnCancelled += HandleTampCancelled;
        }
    }

    private void OnDisable()
    {
        if (digMeter != null)
        {
            digMeter.OnStrike -= HandleStrike;
            digMeter.OnDigComplete -= HandleTillComplete;
            digMeter.OnDigCancelled -= HandleTillCancelled;
        }
        if (plantTamp != null)
        {
            plantTamp.OnComplete -= HandleTampComplete;
            plantTamp.OnCancelled -= HandleTampCancelled;
        }
    }

    private void Update()
    {
        ResolveTarget();
        HandleClick();
        UpdateTillGrowth();
    }

    private float PlayerFeetY()
    {
        if (playerCollider != null) return playerCollider.bounds.min.y;
        return player.position.y - 1f;
    }

    private bool WithinHeightRange(Vector3 point)
    {
        float delta = point.y - PlayerFeetY();
        return delta <= maxHeightAbove && delta >= -maxHeightBelow;
    }

    private bool InReach(Vector3 point, float reach)
    {
        Vector3 flat = point - player.position;
        flat.y = 0f;
        return flat.magnitude <= reach;
    }

    private float SpacingFor(CropData crop)
    {
        if (crop != null && crop.rootRadius > 0f) return crop.rootRadius;
        return defaultPlantSpacing;
    }

    // ---------------------------------------------------------------
    // Context resolution — decide what a click here would do
    // ---------------------------------------------------------------

    private void ResolveTarget()
    {
        currentAction = Action.None;
        actionValid = false;
        targetCrop = null;
        targetPile = null;

        if (cam == null || Mouse.current == null || busy)
        {
            if (marker != null) marker.gameObject.SetActive(false);
            return;
        }

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = cam.ScreenPointToRay(mousePos);

        // 1. Water → drink (real, exposed water only)
        if (Physics.Raycast(ray, out RaycastHit wHit, 200f, waterMask))
        {
            float wl = terrain != null ? terrain.WaterLevel : -0.5f;
            float gy = terrain != null ? terrain.SampleHeight(wHit.point.x, wHit.point.z) : 0f;
            if (gy < wl)
            {
                currentAction = Action.Drink;
                waterPoint = wHit.point;
                targetPoint = wHit.point;
                actionValid = true;
                ShowMarker(false);
                return;
            }
        }

        // 2. Mature crop → harvest (immature crops don't grab the cursor)
        var cropHits = Physics.RaycastAll(ray, 200f, cropMask, QueryTriggerInteraction.Collide);
        if (cropHits.Length > 0)
        {
            PlantedCrop best = null; float bestScore = float.MaxValue;
            foreach (var h in cropHits)
            {
                var crop = h.collider.GetComponentInParent<PlantedCrop>();
                if (crop == null || !crop.IsMature) continue;   // only mature crops
                Vector3 toCrop = crop.transform.position - ray.origin;
                float along = Vector3.Dot(toCrop, ray.direction);
                Vector3 onRay = ray.origin + ray.direction * along;
                float off = (crop.transform.position - onRay).sqrMagnitude;
                if (off < bestScore) { bestScore = off; best = crop; }
            }
            if (best != null)
            {
                currentAction = Action.Harvest;
                targetCrop = best;
                targetPoint = best.transform.position;
                actionValid = InReach(targetPoint, interactReach);
                ShowMarker(false);
                return;
            }
        }

        // 3. Storage → deposit/eat
        if (Physics.Raycast(ray, out RaycastHit sHit, 200f, storageMask))
        {
            var pile = sHit.collider.GetComponentInParent<StoragePile>();
            if (pile != null)
            {
                currentAction = Action.Storage;
                targetPile = pile;
                targetPoint = sHit.point;
                actionValid = InReach(sHit.point, interactReach);
                ShowMarker(false);
                return;
            }
        }

        // 4/5. Ground → plant (on ridge) or till (bare)
        if (Physics.Raycast(ray, out RaycastHit gHit, 200f, groundMask))
        {
            targetPoint = gHit.point;
            bool reach = InReach(targetPoint, maxReach);
            bool heightOk = WithinHeightRange(targetPoint);

            bool onCrest = terrain != null && terrain.IsOnRidgeCrest(targetPoint);
            if (onCrest)
            {
                bool spaced = IsSpacedFromCrops(targetPoint, selectedCrop);
                currentAction = Action.Plant;
                actionValid = spaced && reach && heightOk && selectedCrop != null;
                ShowMarker(true, actionValid ? plantColor : invalidColor);
                return;
            }
            else
            {
                Vector3 wallCheck = targetPoint + Vector3.up * wallCheckHeight;
                bool clearWalls = !Physics.CheckSphere(wallCheck, wallClearance, wallMask,
                                                       QueryTriggerInteraction.Ignore);
                currentAction = Action.Till;
                actionValid = reach && clearWalls && heightOk;
                ShowMarker(true, actionValid ? tillColor : invalidColor);
                return;
            }
        }

        ShowMarker(false);
    }

    /// <summary>
    /// True if a new crop of type `crop` can be planted at `point` without
    /// crowding existing crops. Uses each crop's own rootRadius so big crops
    /// (tomatoes) keep more distance than small ones (carrots).
    /// </summary>
    private bool IsSpacedFromCrops(Vector3 point, CropData crop)
    {
        float newRadius = SpacingFor(crop);

        // Check a generous area, then test true distance against combined radii.
        float searchRadius = newRadius + 2f;   // + max expected existing radius
        var hits = Physics.OverlapSphere(point, searchRadius, cropMask,
                                         QueryTriggerInteraction.Collide);
        foreach (var h in hits)
        {
            var other = h.GetComponentInParent<PlantedCrop>();
            if (other == null) continue;

            float otherRadius = other.Data != null && other.Data.rootRadius > 0f
                ? other.Data.rootRadius : defaultPlantSpacing;

            float needed = newRadius + otherRadius;   // both crops' space combined
            Vector3 flat = other.transform.position - point;
            flat.y = 0f;
            if (flat.magnitude < needed) return false;   // too close
        }
        return true;
    }

    private void ShowMarker(bool show, Color? color = null)
    {
        if (marker == null) return;
        marker.gameObject.SetActive(show);
        if (show)
        {
            marker.position = targetPoint + Vector3.up * 0.02f;
            if (markerRenderer != null && color.HasValue)
            {
                var mpb = new MaterialPropertyBlock();
                markerRenderer.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", color.Value);
                markerRenderer.SetPropertyBlock(mpb);
            }
        }
    }

    // ---------------------------------------------------------------

    private void HandleClick()
    {
        if (Mouse.current == null || busy) return;
        if (digMeter != null && Time.frameCount == digMeter.LastEndFrame) return;
        if (plantTamp != null && Time.frameCount == plantTamp.LastEndFrame) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (!actionValid) return;

        switch (currentAction)
        {
            case Action.Till: StartTill(); break;
            case Action.Plant: StartPlant(); break;
            case Action.Harvest: DoHarvest(); break;
            case Action.Drink: StartDrink(waterPoint); break;
            case Action.Storage: DoStorage(); break;
        }
    }

    private void DoHarvest()
    {
        if (targetCrop == null || !targetCrop.IsMature) return;
        if (CarryInventory.Instance != null && CarryInventory.Instance.IsFull)
        {
            Debug.Log("Hands are full — take these to storage first.");
            return;
        }
        targetCrop.TryHarvestToHands();
    }

    private void DoStorage()
    {
        if (targetPile == null) return;
        var hands = CarryInventory.Instance;
        if (hands != null && hands.Count > 0)
        {
            int moved = targetPile.DepositAll(hands);
            Debug.Log($"Deposited {moved}. Pile now {targetPile.Total}.");
        }
        else
        {
            if (targetPile.EatOne()) Debug.Log($"Ate one. Pile now {targetPile.Total}.");
            else Debug.Log("Storage is empty.");
        }
    }

    private Vector3 GetStandPosition()
    {
        Vector3 preferred;
        if (standBehindSite && cam != null) { preferred = pendingPoint - cam.transform.position; preferred.y = 0f; }
        else { preferred = player.position - pendingPoint; preferred.y = 0f; }
        if (preferred.sqrMagnitude < 0.01f) preferred = -player.forward;
        preferred.Normalize();

        float[] angles = { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f };
        foreach (float a in angles)
        {
            Vector3 dir = Quaternion.Euler(0f, a, 0f) * preferred;
            Vector3 candidate = pendingPoint + dir * standDistance;
            Vector3 checkPos = candidate + Vector3.up * wallCheckHeight;
            if (Physics.CheckSphere(checkPos, standClearance, wallMask, QueryTriggerInteraction.Ignore))
                continue;
            return candidate;
        }
        return player.position;
    }

    // --- Tilling ---------------------------------------------------

    private void StartTill()
    {
        if (digMeter == null || playerController == null || terrain == null) return;
        pendingPoint = targetPoint;
        busy = true;
        playerController.MovementLocked = true;
        playerController.FacePoint(pendingPoint);
        playerController.WalkTo(GetStandPosition(), () =>
        {
            if (!digMeter.Begin(pendingPoint)) { ReleasePlayer(); return; }
        });
    }

    private void UpdateTillGrowth()
    {
        if (busy && digMeter != null && digMeter.IsActive && terrain != null)
        {
            float r = tillRadius * Mathf.Clamp01(digMeter.Progress);
            terrain.TillFurrow(pendingPoint, r, SpacingFor(selectedCrop), 0.12f);
        }
    }

    private void HandleStrike(ActionQuality quality) { }

    private void HandleTillComplete()
    {
        ReleasePlayer();
        if (terrain != null)
            terrain.TillFurrow(pendingPoint, tillRadius, SpacingFor(selectedCrop), 1f);
    }

    private void HandleTillCancelled() { ReleasePlayer(); }

    // --- Planting --------------------------------------------------

    private void StartPlant()
    {
        if (plantTamp == null || playerController == null) return;
        if (selectedCrop == null || plantRootPrefab == null)
        {
            Debug.LogWarning("Can't plant: no crop selected.");
            return;
        }
        pendingPoint = targetPoint;
        busy = true;
        playerController.MovementLocked = true;
        playerController.FacePoint(pendingPoint);
        playerController.WalkTo(GetStandPosition(), () => { plantTamp.Begin(pendingPoint); });
    }

    private void HandleTampComplete(ActionQuality quality, float soil)
    {
        ReleasePlayer();
        if (selectedCrop == null || plantRootPrefab == null) return;

        GameObject go = Instantiate(plantRootPrefab, pendingPoint, Quaternion.identity);
        var crop = go.GetComponent<PlantedCrop>();
        if (crop == null) crop = go.AddComponent<PlantedCrop>();
        crop.Initialise(selectedCrop, soil);

        Debug.Log($"Planted {selectedCrop.cropName} — soil {soil:0.00}");
    }

    private void HandleTampCancelled() { ReleasePlayer(); }

    // --- Drinking (walk to the water's edge, then drink) -----------

    private void StartDrink(Vector3 water)
    {
        if (playerController == null) return;
        var stats = SurvivalStats.Instance;
        if (stats != null && stats.ThirstIsFull) { Debug.Log("Not thirsty."); return; }

        pendingPoint = water;
        busy = true;
        playerController.MovementLocked = true;
        playerController.FacePoint(pendingPoint);

        Vector3 standPos = GetDrinkStandPosition(water);
        if (debugDrink) Debug.Log($"[Drink] stand target {standPos}, player at {player.position}");

        playerController.WalkTo(standPos, () =>
        {
            var s = SurvivalStats.Instance;
            if (s != null && !s.ThirstIsFull) { s.Drink(drinkAmount); Debug.Log("Drank."); }
            ReleasePlayer();
        });
    }

    private Vector3 GetDrinkStandPosition(Vector3 water)
    {
        Vector3 from = player.position; from.y = 0f;
        Vector3 to = water; to.y = 0f;
        Vector3 dir = (to - from);
        float total = dir.magnitude;
        if (total < 0.01f) return player.position;
        dir /= total;

        float wl = terrain != null ? terrain.WaterLevel : -0.5f;
        float stepSize = 0.4f;
        Vector3 lastLand = from;

        for (float dist = 0f; dist <= total + stepSize; dist += stepSize)
        {
            Vector3 sample = from + dir * dist;
            float gy = terrain != null ? terrain.SampleHeight(sample.x, sample.z) : 0f;

            if (gy < wl)
            {
                if (debugDrink) Debug.Log($"[Drink] found water edge at dist {dist:0.0}, terrainY {gy:0.00} < waterLevel {wl:0.00}");
                Vector3 stand = lastLand;
                stand.y = terrain != null ? terrain.SampleHeight(stand.x, stand.z) : 0f;
                return stand;
            }
            lastLand = sample;
        }

        if (debugDrink) Debug.Log("[Drink] no water found along path — using fallback near click");
        Vector3 fb = from + dir * Mathf.Max(0f, total - drinkStandDistance);
        fb.y = terrain != null ? terrain.SampleHeight(fb.x, fb.z) : 0f;
        return fb;
    }

    private void ReleasePlayer()
    {
        busy = false;
        if (playerController == null) return;
        playerController.CancelWalk();
        playerController.FacePoint(null);
        playerController.MovementLocked = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (player == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(player.position, maxReach);
    }
}