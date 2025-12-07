using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.EnhancedTouch;
#endif

public class SimpleOrbitControls : MonoBehaviour
{
    float angleX;
    float angleY;
    public Transform yPivot;
    public Transform xPivot;
    public Transform scalingPivot;
    public bool reverse;

    [Serializable]
    public struct OrbitControlSettings
    {
        public float mouseSens;
        public float touchSens;
        public float pinchSens;
        public float mwheelSens;
        public float rotationSpeed;
        public float rotationDrag;
        public float zoomSpeed;
        public float zoomLerpSpeed;
        public float slideAmount;
        public float slideSens;
    }
    public OrbitControlSettings controlSettings;

    public float scale = 0.5f;
    float lerpedScale = 0.5f;
    float startingScale = 1f;
    Vector2 velocity;

    float slide;
    float lerpedSlide;

    const string mwheelAxis = "Mouse ScrollWheel";

    void Start()
    {
        angleX = xPivot.localEulerAngles.x;
        angleY = yPivot.localEulerAngles.y;
        startingScale = scalingPivot.localScale.x;
    }

    int movingFingerId = -1;

    // Update is called once per frame
    void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        // rotate touch
        if (Input.touchCount == 1)
        {
            if (movingFingerId == -1)
            {
                movingFingerId = Input.GetTouch(0).fingerId;
            }
            foreach (var touch in Input.touches)
            {
                if (touch.fingerId == movingFingerId)
                {
                    velocity += (reverse ? -1f : 1f) * touch.deltaPosition * controlSettings.touchSens;
                }
            }
        }
        else
        {
            movingFingerId = -1;
        }

        // zoom touch
        if (Input.touchCount == 2)
        {
            var touch0 = Input.GetTouch(0);
            var touch1 = Input.GetTouch(1);
            // if (touch0.phase == TouchPhase.Moved && touch1.phase == TouchPhase.Moved)
            {
                var pos0 = touch0.position;
                var pos1 = touch1.position;
                var prevPos0 = touch0.position - touch0.deltaPosition;
                var prevPos1 = touch1.position - touch1.deltaPosition;

                var deltaMag = (pos0 - pos1).magnitude;
                var prevDeltaMag = (prevPos0 - prevPos1).magnitude;
                var zoom = prevDeltaMag - deltaMag;

                scale += (reverse ? -1f : 1f) * zoom * controlSettings.zoomSpeed * controlSettings.pinchSens;
                scale = Mathf.Clamp01(scale);
            }
        }

        // don't use mouse if touches
        if (Input.touchCount == 0)
        {
            // rotate mouse
            if (Input.GetMouseButton(0))
            {
                velocity += (reverse ? -1f : 1f) * (Vector2)Input.mousePositionDelta * controlSettings.mouseSens;
            }
            // zoom mouse
            {
                var mouseScroll = Input.GetAxis(mwheelAxis);
                if (mouseScroll != 0f)
                {
                    scale += (reverse ? -1f : 1f) * mouseScroll * controlSettings.zoomSpeed * controlSettings.mwheelSens;
                    scale = Mathf.Clamp01(scale);
                }
            }
        }
#else
        // Enable EnhancedTouch if not already enabled
        if (!EnhancedTouchSupport.enabled)
            EnhancedTouchSupport.Enable();

        // New Input System
        var touchscreen = Touchscreen.current;
        var mouse = Mouse.current;
        var touches = new List<TouchControl>();
        if (touchscreen != null)
        {
            foreach (var touch in Touchscreen.current.touches)
            {
                if (touch.phase.value != UnityEngine.InputSystem.TouchPhase.None &&
                    touch.phase.value != UnityEngine.InputSystem.TouchPhase.Canceled &&
                    touch.phase.value != UnityEngine.InputSystem.TouchPhase.Ended)
                {
                    touches.Add(touch);
                }
            }
        }
        bool hasTouch = touchscreen != null && touches.Count > 0;

        Debug.Log($"Using new Input System, touches: {touches.Count}");

        // rotate touch
        if (touchscreen != null && touches.Count == 1)
        {
            var touch = touches[0];
            if (movingFingerId == -1)
            {
                movingFingerId = touch.touchId.ReadValue();
            }
            foreach (var t in touches)
            {
                if (t.touchId.ReadValue() == movingFingerId)
                {
                    velocity += (reverse ? -1f : 1f) * t.delta.ReadValue() * controlSettings.touchSens;
                }
            }
        }
        else
        {
            movingFingerId = -1;
        }

        // zoom touch
        if (touchscreen != null && touches.Count == 2)
        {
            var t0 = touches[0];
            var t1 = touches[1];

            var pos0 = t0.position.ReadValue();
            var pos1 = t1.position.ReadValue();
            var prevPos0 = pos0 - t0.delta.ReadValue();
            var prevPos1 = pos1 - t1.delta.ReadValue();

            var deltaMag = (pos0 - pos1).magnitude;
            var prevDeltaMag = (prevPos0 - prevPos1).magnitude;
            var zoom = prevDeltaMag - deltaMag;

            scale += (reverse ? -1f : 1f) * zoom * controlSettings.zoomSpeed * controlSettings.pinchSens;
            scale = Mathf.Clamp01(scale);
        }

        // don't use mouse if touches
        if (!hasTouch)
        {
            // rotate mouse
            if (mouse != null && mouse.leftButton.isPressed)
            {
                // Mouse delta is per-frame, so use it directly
                velocity += (reverse ? -1f : 1f) * mouse.delta.ReadValue() * controlSettings.mouseSens;
            }
            // zoom mouse
            if (mouse != null)
            {
                var mouseScroll = mouse.scroll.ReadValue().y;
                if (mouseScroll != 0f)
                {
                    scale += (reverse ? -1f : 1f) * mouseScroll * controlSettings.zoomSpeed * controlSettings.mwheelSens;
                    scale = Mathf.Clamp01(scale);
                }
            }
        }
#endif

        angleX += -velocity.y * controlSettings.rotationSpeed * Time.deltaTime;
        angleY += velocity.x * controlSettings.rotationSpeed * Time.deltaTime;

        velocity = Vector2.Lerp(Vector2.zero, velocity, Mathf.Exp(-controlSettings.rotationDrag * Time.deltaTime));

        angleX = Mathf.Clamp(angleX, -88, 88);

        yPivot.localEulerAngles = Vector3.up * angleY;
        xPivot.localEulerAngles = Vector3.right * angleX;

        // scale
        lerpedScale = Mathf.Lerp(scale, lerpedScale, Mathf.Exp(-controlSettings.zoomLerpSpeed * Time.deltaTime));
        var curvedScale = 2f * (lerpedScale * lerpedScale) + 0.5f;
        scalingPivot.localScale = Vector3.one * startingScale * curvedScale;

        // // slide
        // lerpedSlide = Mathf.Lerp(slide, lerpedSlide, Mathf.Exp(-controlSettings.zoomLerpSpeed * Time.deltaTime));
        // transform.localPosition = Vector3.forward * lerpedSlide;
    }
}
