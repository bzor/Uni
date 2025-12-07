using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.EnhancedTouch;
#endif

public class TouchDebug : MonoBehaviour
{
    public List<GameObject> touchSprites;

    void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        for (int i = 0; i < touchSprites.Count; i++)
        {
            touchSprites[i].SetActive(false);
        }
        for (int i = 0; i < Input.touchCount; i++)
        {
            touchSprites[i].SetActive(true);
            var touch = Input.GetTouch(i);
            // Debug.Log($"touch {i} ({touch.fingerId} pos {touch.position} {touch.rawPosition})");
            touchSprites[i].transform.localScale = Vector3.one;
            touchSprites[i].transform.localEulerAngles = Vector3.zero;
            if (touch.phase == TouchPhase.Began)
            {
                touchSprites[i].transform.localScale = Vector3.one * 1.5f;
            }
            if (touch.phase == TouchPhase.Moved)
            {
                touchSprites[i].transform.localEulerAngles = Vector3.forward * 45f;
            }
            touchSprites[i].GetComponent<RectTransform>().anchoredPosition = touch.position;
        }
#else
        Debug.Log("TouchDebug Update called");

        // Enable EnhancedTouch if not already enabled
        if (!EnhancedTouchSupport.enabled)
            EnhancedTouchSupport.Enable();

        // New Input System
        var touchscreen = Touchscreen.current;
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

        // Hide all sprites initially
        for (int i = 0; i < touchSprites.Count; i++)
        {
            touchSprites[i].SetActive(false);
        }

        int count = Mathf.Min(touches.Count, touchSprites.Count);

        for (int i = 0; i < count; i++)
        {
            touchSprites[i].SetActive(true);
            var touch = touches[i];
            touchSprites[i].transform.localScale = Vector3.one;
            touchSprites[i].transform.localEulerAngles = Vector3.zero;
            if (touch.phase.value == UnityEngine.InputSystem.TouchPhase.Began)
            {
                touchSprites[i].transform.localScale = Vector3.one * 1.5f;
            }
            if (touch.phase.value == UnityEngine.InputSystem.TouchPhase.Moved)
            {
                touchSprites[i].transform.localEulerAngles = Vector3.forward * 45f;
            }
            // Convert screen position to anchored position if needed
            touchSprites[i].GetComponent<RectTransform>().anchoredPosition = touch.position.ReadValue();
        }
#endif
    }
}
