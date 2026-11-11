using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Hands;

public class HandCrank : MonoBehaviour
{
    [Header("Referenser")]
    public Transform xrOrigin;      // XR Origin (XR Rig), handlederna är relativa till den
    public Transform crank;         // objektet som roterar (Vev, pivot i axeln)
    public Transform handle;        // handtaget (där man greppar)
    public Vector3 localAxis = Vector3.forward;

    [Header("Grepp")]
    public float grabRadius = 0.12f;      // hur nära handflatan måste vara handtaget
    public float fistCloseDistance = 0.07f; // under detta räknas det som knuten näve
    public float fistOpenDistance = 0.10f;  // över detta släpps greppet

    [Header("Händelser")]
    public UnityEvent<float> onCranked;
    public UnityEvent onFullTurn;

    XRHandSubsystem hands;
    int grabbingHand = -1;   // -1 = ingen, 0 = vänster, 1 = höger
    Vector3 lastDir;
    bool hasLast;
    float totalAngle;

    static readonly XRHandJointID[] tips =
    {
        XRHandJointID.IndexTip, XRHandJointID.MiddleTip,
        XRHandJointID.RingTip,  XRHandJointID.LittleTip
    };

    void Update()
    {
        if (hands == null && !TryGetSubsystem()) return;

        if (grabbingHand >= 0)
        {
            if (!GetHandData(grabbingHand, out Vector3 palm, out float curl) || curl > fistOpenDistance)
            {
                Release();
                return;
            }
            Rotate(palm);
        }
        else
        {
            for (int i = 0; i < 2; i++)
            {
                if (GetHandData(i, out Vector3 palm, out float curl)
                    && curl < fistCloseDistance
                    && Vector3.Distance(palm, handle.position) < grabRadius)
                {
                    grabbingHand = i;
                    hasLast = false;
                    break;
                }
            }
        }
    }

    bool TryGetSubsystem()
    {
        var list = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(list);
        if (list.Count > 0) hands = list[0];
        return hands != null;
    }

    // Ger handflatans världsposition och medelavstånd från fingertoppar till handflata
    bool GetHandData(int index, out Vector3 palmWorld, out float avgCurl)
    {
        palmWorld = Vector3.zero;
        avgCurl = 999f;

        XRHand hand = index == 0 ? hands.leftHand : hands.rightHand;
        if (!hand.isTracked) return false;

        if (!hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palmPose)) return false;
        palmWorld = xrOrigin.TransformPoint(palmPose.position);

        float sum = 0f;
        foreach (var id in tips)
        {
            if (!hand.GetJoint(id).TryGetPose(out Pose tipPose)) return false;
            Vector3 tipWorld = xrOrigin.TransformPoint(tipPose.position);
            sum += Vector3.Distance(tipWorld, palmWorld);
        }
        avgCurl = sum / tips.Length;
        return true;
    }

    void Rotate(Vector3 handPos)
    {
        Vector3 axis = crank.TransformDirection(localAxis).normalized;
        Vector3 dir = Vector3.ProjectOnPlane(handPos - crank.position, axis);
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        if (hasLast)
        {
            float delta = Vector3.SignedAngle(lastDir, dir, axis);
            crank.Rotate(axis, delta, Space.World);
            onCranked?.Invoke(delta);

            float before = totalAngle;
            totalAngle += Mathf.Abs(delta);
            if (Mathf.FloorToInt(totalAngle / 360f) > Mathf.FloorToInt(before / 360f))
                onFullTurn?.Invoke();
        }
        lastDir = dir;
        hasLast = true;
    }

    void Release()
    {
        grabbingHand = -1;
        hasLast = false;
    }
}