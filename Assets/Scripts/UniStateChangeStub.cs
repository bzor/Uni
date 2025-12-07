using UnityEngine;
using System.Collections;

public class UniStateChangeStub : MonoBehaviour
{
    [SerializeField] private float idleToThinkingDelay = 15.0f;
    [SerializeField] private float thinkingToSigilDelay = 10.0f;
    [SerializeField] private float sigilToIdleDelay = 10.0f;

    private UniState uniState;
    // private Coroutine stateChangeCoroutine;

    private void Start()
    {
        uniState = UniState.Instance;
        
        if (uniState != null)
        {
            // Ensure we start at IDLE
            uniState.SetStateIdle();
            
            // Start the state change cycle
            // stateChangeCoroutine = StartCoroutine(StateChangeCycle());
        }
        else
        {
            Debug.LogError("UniState instance not found!");
        }
    }

    private void Update()
    {
        if (uniState == null) return;
        
        // Set states with number keys 1-4
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            uniState.SetStateIdle();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            uniState.SetStatePercepts();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            uniState.SetStateSigil();
        }
    }

    /*
    private IEnumerator StateChangeCycle()
    {
        // Ensure we start at IDLE
        uniState.SetStateIdle();
        
        while (true)
        {
            // IDLE -> THINKING
            yield return new WaitForSeconds(idleToThinkingDelay);
            uniState.SetStateThinking();
            
            // THINKING -> SIGIL
            yield return new WaitForSeconds(thinkingToSigilDelay);
            uniState.SetStateSigil();
            
            // SIGIL -> IDLE
            yield return new WaitForSeconds(sigilToIdleDelay);
            uniState.SetStateIdle();
            
            // Cycle repeats
        }
    }

    private void OnDestroy()
    {
        if (stateChangeCoroutine != null)
        {
            StopCoroutine(stateChangeCoroutine);
        }
    }
    */
}

