using UnityEngine;

public enum UniStateType
{
    IDLE,
    PERCEPTS,
    SIGIL
}

public enum SigilCycleState
{
    NONE,
    IN,
    OUT,
    HOLD
}

public delegate void StateChangedDelegate(UniStateType newState);
public delegate void SigilStartDelegate();
public delegate void PerceptDelegate(PerceptData percept);

[System.Serializable]
public struct PerceptData
{
    public string sigilPhrase;
    public string drawCalls;
    public string sessionId;
    public string type;
    public string timestamp;
    public Texture2D pngTexture;
}

public class UniState : MonoBehaviour
{
    public float IdleT = 0f;
    public float PerceptT = 0f;
    public float SigilT = 0f;

    [HideInInspector]
    public float SigilInT = 0f;
    [HideInInspector]
    public float SigilOutT = 0f;

    [HideInInspector]
    public int CurSigil = 0;
    
    public SigilDataSO currentSigilData;
    public SigilDataSO nextSigilData;

    public float SigilInDuration = 0.6f;
    public float SigilOutDuration = 2f;

    private bool loadAfterFadeOut = false;

    [HideInInspector]
    public SigilCycleState SigilCycleState = SigilCycleState.NONE;
    [HideInInspector]
    public float SigilCycleDuration = 10f;
    
    private float sigilHideTimer = 0f;
    private const float SIGIL_HIDE_DELAY = 20f;

    [SerializeField] private float idleTrDuration = 2f;
    [SerializeField] private float perceptTrDuration = 2f;
    [SerializeField] private float sigilTrDuration = 3f;

    private static UniState _instance;
    public static UniState Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<UniState>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("UniState");
                    _instance = go.AddComponent<UniState>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    [SerializeField] public UniStateType currentState = UniStateType.IDLE;

    public event StateChangedDelegate OnStateChanged;
    public event SigilStartDelegate OnSigilStart;
    public event PerceptDelegate OnPercept;
    
    // Percept storage with circular buffer (GC-friendly)
    private const int MAX_PERCEPTS = 20;
    private PerceptData[] percepts = new PerceptData[MAX_PERCEPTS];
    private int perceptCount = 0;
    private int perceptIndex = 0; // Circular buffer index

    public UniStateType CurrentState
    {
        get { return currentState; }
        private set
        {
            if (currentState != value)
            {
                currentState = value;
                OnStateChanged?.Invoke(currentState);
            }
        }
    }

    private void Awake()
    {
        //Application.targetFrameRate = 60;
        
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        CurrentState = UniStateType.IDLE;
        SigilCycleState = SigilCycleState.NONE;
        IdleT = 1f;
        PerceptT = 0f;
        SigilT = 0f;
    }


    private Texture2D CopyTexture(Texture2D source)
    {
        if (source == null) return null;
        
        // Create new texture with same dimensions and format, no mipmaps
        Texture2D copy = new Texture2D(source.width, source.height, source.format, false);
        
        // Copy pixel data using GetPixels/SetPixels (only copies base mip level, no mipmaps)
        copy.SetPixels(source.GetPixels());
        
        // Apply changes
        copy.Apply();
        
        return copy;
    }

    public void ShowSigil(SigilDataSO sigilData)
    {
        if(SigilCycleState == SigilCycleState.NONE)
        {
            currentSigilData.sigilPhrase = sigilData.sigilPhrase;
            currentSigilData.sigilCode = sigilData.sigilCode;
            currentSigilData.pngTexture = CopyTexture(sigilData.pngTexture);
            SigilCycleState = SigilCycleState.IN;
            SigilInT = 0f;
            SigilOutT = 0f;
            loadAfterFadeOut = false;
            sigilHideTimer = 0f; // Reset timer
            resetSigil();
        }
        else
        {
            nextSigilData.sigilPhrase = sigilData.sigilPhrase;
            nextSigilData.sigilCode = sigilData.sigilCode;
            nextSigilData.pngTexture = CopyTexture(sigilData.pngTexture);
            SigilCycleState = SigilCycleState.OUT;
            loadAfterFadeOut = true;
            sigilHideTimer = 0f; // Reset timer when transitioning
        }
    }

    public void HideSigil()
    {
        if(SigilCycleState == SigilCycleState.HOLD)
        {
            SigilCycleState = SigilCycleState.OUT;
        }
    }
    
    public void OnSpool()
    {
        Debug.Log("UniState:: OnSpool");
        // Called when SPOOL phase starts (transition buffer before sigil)
    }

    private void Update()
    {
        // Quit application on Q key press
        if (Input.GetKeyDown(KeyCode.Q))
        {
            Application.Quit();
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #endif
        }
        
        IdleT += Time.deltaTime * (CurrentState == UniStateType.IDLE ? 1f : -1f) / idleTrDuration;
        IdleT = Mathf.Clamp01(IdleT);
        PerceptT += Time.deltaTime * (CurrentState == UniStateType.PERCEPTS ? 1f : -1f) / perceptTrDuration;
        PerceptT = Mathf.Clamp01(PerceptT);
        SigilT += Time.deltaTime * (CurrentState == UniStateType.SIGIL ? 1f : -1f) / sigilTrDuration;
        SigilT = Mathf.Clamp01(SigilT);

        // Update sigil hide timer (only count during HOLD state when sigil is fully visible)
        if(SigilCycleState == SigilCycleState.HOLD)
        {
            sigilHideTimer += Time.deltaTime;
            if(sigilHideTimer >= SIGIL_HIDE_DELAY)
            {
                HideSigil();
                sigilHideTimer = 0f; // Reset timer
            }
        }
        else
        {
            sigilHideTimer = 0f; // Reset timer when not in HOLD state
        }
        
        if(SigilCycleState == SigilCycleState.IN)
        {
            SigilInT += Time.deltaTime / SigilInDuration;
            SigilInT = Mathf.Clamp01(SigilInT);
            if(SigilInT >= 1f)
            {
                SigilCycleState = SigilCycleState.HOLD;
            }
        }
        else if(SigilCycleState == SigilCycleState.OUT)
        {
            SigilOutT += Time.deltaTime / SigilOutDuration;
            SigilOutT = Mathf.Clamp01(SigilOutT);
            if(SigilOutT >= 1f)
            {
                SigilInT = 0f;
                SigilOutT = 0f;
                if(loadAfterFadeOut)
                {
                    currentSigilData.sigilPhrase = nextSigilData.sigilPhrase;
                    currentSigilData.sigilCode = nextSigilData.sigilCode;
                    currentSigilData.pngTexture = CopyTexture(nextSigilData.pngTexture);
                    loadAfterFadeOut = false;
                    SigilCycleState = SigilCycleState.IN;
                    resetSigil();
                }
                else
                {
                    SigilCycleState = SigilCycleState.NONE;
                    CurrentState = UniStateType.PERCEPTS;
                    sigilHideTimer = 0f; // Reset timer when sigil cycle ends
                }
            }
        }
    }
    

    private void resetSigil()
    {
        Debug.Log("Unistate:: resetSigil");
        CurrentState = UniStateType.SIGIL;
        SigilInT = 0f;
        SigilOutT = 0f;
        SigilCycleState = SigilCycleState.IN;
        OnSigilStart?.Invoke();
    }
    
    public void AddPercept(PerceptData percept)
    {

        if (CurrentState == UniStateType.IDLE)
        {
            CurrentState = UniStateType.PERCEPTS;
        }

        if (SigilT > 0f)
        {
            return;
        }

        // Fire event
        OnPercept?.Invoke(percept);
    }
    
    public int PerceptCount => perceptCount;
    
    // Get percept at index (0 = oldest, PerceptCount-1 = newest)
    public PerceptData GetPercept(int index)
    {
        if (index < 0 || index >= perceptCount)
        {
            return default(PerceptData);
        }
        
        // Calculate actual index in circular buffer (oldest first)
        int actualIndex = (perceptIndex - perceptCount + index + MAX_PERCEPTS) % MAX_PERCEPTS;
        return percepts[actualIndex];
    }
    
    // Get all percepts as array (newest first)
    public PerceptData[] GetPercepts()
    {
        PerceptData[] result = new PerceptData[perceptCount];
        for (int i = 0; i < perceptCount; i++)
        {
            // Newest first: newest is at (perceptIndex - 1), then go backwards
            int idx = (perceptIndex - 1 - i + MAX_PERCEPTS) % MAX_PERCEPTS;
            result[i] = percepts[idx];
        }
        return result;
    }

    public void ChangeState(UniStateType newState)
    {
        CurrentState = newState;
    }

    public void SetStateIdle()
    {
        ChangeState(UniStateType.IDLE);
    }

    public void SetStatePercepts()
    {
        ChangeState(UniStateType.PERCEPTS);
    }

    public void SetStateSigil()
    {
        ChangeState(UniStateType.SIGIL);
        SigilCycleState = SigilCycleState.NONE;
    }
}