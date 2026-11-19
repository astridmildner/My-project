using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class CrankRotator : MonoBehaviour
{
    public Transform pivot;
    public Vector3 localAxis = Vector3.forward;
    public float releaseDrag = 2f;
    public bool keepSpinning = true;

    public float TotalAngle { get; private set; }

    Transform _hand;
    Vector3 _prevDir;
    bool _grabbing;
    float _velocity;

    public void OnGrab(SelectEnterEventArgs args)
    {
        var interactable = args.interactableObject;
        _hand = args.interactorObject.GetAttachTransform(interactable);
        _prevDir = GetHandDirection();
        _grabbing = true;
        _velocity = 0f;
        Debug.Log("Grab!");
    }

    public void OnRelease(SelectExitEventArgs args)
    {
        _grabbing = false;
        _hand = null;
    }

    void Update()
    {
        if (pivot == null) return;

        if (_grabbing && _hand != null)
        {
            Vector3 axis = pivot.TransformDirection(localAxis).normalized;
            Vector3 currentDir = GetHandDirection();
            float delta = Vector3.SignedAngle(_prevDir, currentDir, axis);
            pivot.Rotate(axis, delta, Space.World);
            TotalAngle += delta;
            _velocity = delta / Mathf.Max(Time.deltaTime, 0.0001f);
            _prevDir = currentDir;
        }
        else if (keepSpinning && Mathf.Abs(_velocity) > 1f)
        {
            Vector3 axis = pivot.TransformDirection(localAxis).normalized;
            pivot.Rotate(axis, _velocity * Time.deltaTime, Space.World);
            _velocity = Mathf.Lerp(_velocity, 0f, releaseDrag * Time.deltaTime);
        }
    }

    Vector3 GetHandDirection()
    {
        Vector3 axis = pivot.TransformDirection(localAxis).normalized;
        Vector3 toHand = _hand.position - pivot.position;
        return Vector3.ProjectOnPlane(toHand, axis).normalized;
    }
}
