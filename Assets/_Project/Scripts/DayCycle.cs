using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Drives in-game time. Fires OnDayEnd once per day; the seed shop, crops,
/// and events all hook into it. For now a day is just a timer, with a debug
/// key to skip ahead.
/// </summary>
public class DayCycle : MonoBehaviour
{
    public static DayCycle Instance { get; private set; }

    [Header("Time")]
    [Tooltip("Real seconds per in-game day.")]
    [SerializeField] private float dayLength = 120f;
    [SerializeField] private int startDay = 1;

    [Header("Debug")]
    [Tooltip("Press this to jump to the next day instantly.")]
    [SerializeField] private Key skipDayKey = Key.N;

    public int CurrentDay { get; private set; }
    /// <summary>0 at dawn, 1 at end of day.</summary>
    public float DayProgress => Mathf.Clamp01(timer / dayLength);

    /// <summary>Fires as each day ends, passing the day number that just finished.</summary>
    public event Action<int> OnDayEnd;
    public event Action<int> OnDayStart;

    private float timer;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        CurrentDay = startDay;
    }

    private void Start()
    {
        OnDayStart?.Invoke(CurrentDay);
    }

    private void Update()
    {
        if (Keyboard.current != null && skipDayKey != Key.None &&
            Keyboard.current[skipDayKey].wasPressedThisFrame)
        {
            EndDay();
            return;
        }

        timer += Time.deltaTime;
        if (timer >= dayLength) EndDay();
    }

    private void EndDay()
    {
        int finished = CurrentDay;
        OnDayEnd?.Invoke(finished);

        CurrentDay++;
        timer = 0f;

        OnDayStart?.Invoke(CurrentDay);
        Debug.Log($"Day {finished} ended. Now day {CurrentDay}.");
    }
}