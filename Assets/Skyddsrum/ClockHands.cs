using System;
using UnityEngine;

/// <summary>Väggklocka som visar riktig tid.</summary>
public class ClockHands : MonoBehaviour
{
    public Transform hourHand;
    public Transform minuteHand;
    public Transform secondHand;

    void Update()
    {
        var t = DateTime.Now.TimeOfDay;
        float sec = (float)t.TotalSeconds;
        if (secondHand != null) secondHand.localRotation = Quaternion.Euler(0f, 0f, -(sec % 60f) * 6f);
        if (minuteHand != null) minuteHand.localRotation = Quaternion.Euler(0f, 0f, -(sec / 60f % 60f) * 6f);
        if (hourHand != null) hourHand.localRotation = Quaternion.Euler(0f, 0f, -(sec / 3600f % 12f) * 30f);
    }
}
