using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

public class CymaticControl : MonoBehaviour
{

    public Pc4MinisListener midiListener;
    
    public VisualEffect vfx;

    public Vector2 val0Range;
    public Vector2 val1Range;
    public Vector2 val2Range;
    public Vector2 val3Range;

    private float curTime = 0;

    public float interpolationSpeed = 1f;

    private float knobVal0 = 0;
    private float knobVal1 = 0;
    private float knobVal2 = 0;
    private float knobVal3 = 0;

    public CymaticVal[] cymaticVals;

    [System.Serializable]
    public struct CymaticVal
    {
        public string id;
        public float min;
        public float max;
        public int knob;
        [HideInInspector]
        public float curVal;
        [HideInInspector]
        public float knobVal;
    }

    private float[] knobValsSmoothed;

    private void OnEnable()
    {
        if (midiListener != null)
            midiListener.KnobChanged += OnPc4KnobChanged;        
    }
    
    void OnDisable()
    {
        if (midiListener != null)
            midiListener.KnobChanged -= OnPc4KnobChanged;
    }
    
    void Start()
    {

        for (int i = 0; i < cymaticVals.Length; i++)
        {
            cymaticVals[i].curVal = 0;
        }
    }

    void Update()
    {
        curTime += Time.deltaTime;

        for (int i = 0; i < cymaticVals.Length; i++)
        {
            var cVal = cymaticVals[i];
            cymaticVals[i].curVal =
                Mathf.Lerp(cVal.curVal, Mathf.Lerp(cVal.min, cVal.max, cVal.knobVal), Time.smoothDeltaTime * interpolationSpeed);
            vfx.SetFloat(cVal.id, cVal.curVal);
        }
    }
    
    private void OnPc4KnobChanged(int ccNumber, float value)
    {
        for (int i = 0; i < cymaticVals.Length; i++)
        {
            if (cymaticVals[i].knob == ccNumber)
            {
                cymaticVals[i].knobVal = value;
            }
        }
    }    
}
