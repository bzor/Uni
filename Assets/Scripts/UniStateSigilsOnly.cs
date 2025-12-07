using UnityEngine;

public enum UniStateSigilsOnlyType
{
    IDLE,
    THINKING,
    LISTENING,
    SIGIL
}

public delegate void SigilsOnlyStateChangedDelegate(UniStateSigilsOnlyType newState);

public class UniStateSigilsOnly : MonoBehaviour
{
    [HideInInspector]
    public float IdleT = 0f;
    [HideInInspector]
    public float ListeningT = 0f;
    [HideInInspector]
    public float ThinkingT = 0f;
    [HideInInspector]
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

    [SerializeField] private float idleTrDuration = 2.2f;
    [SerializeField] private float listeningTrDuration = 2.2f;
    [SerializeField] private float thinkingTrDuration = 2.2f;
    [SerializeField] private float sigilTrDuration = 3f;

    private static UniStateSigilsOnly _instance;
    public static UniStateSigilsOnly Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<UniStateSigilsOnly>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("UniStateSigilsOnlyType");
                    _instance = go.AddComponent<UniStateSigilsOnly>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    [SerializeField] public UniStateSigilsOnlyType currentState = UniStateSigilsOnlyType.IDLE;

    public event SigilsOnlyStateChangedDelegate OnStateChanged;
    public event SigilStartDelegate OnSigilStart;

    public UniStateSigilsOnlyType CurrentState
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
        Application.targetFrameRate = 60;
        
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
        CurrentState = UniStateSigilsOnlyType.IDLE;
        SigilCycleState = SigilCycleState.NONE;
        IdleT = 1f;
        ListeningT = 0f;
        ThinkingT = 0f;
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
            resetSigil();
        }
        else
        {
            nextSigilData.sigilPhrase = sigilData.sigilPhrase;
            nextSigilData.sigilCode = sigilData.sigilCode;
            nextSigilData.pngTexture = CopyTexture(sigilData.pngTexture);
            SigilCycleState = SigilCycleState.OUT;
            loadAfterFadeOut = true;
        }
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
        
        IdleT += Time.deltaTime * (CurrentState == UniStateSigilsOnlyType.IDLE ? 1f : -1f) / idleTrDuration;
        IdleT = Mathf.Clamp01(IdleT);
        ListeningT += Time.deltaTime * (CurrentState == UniStateSigilsOnlyType.LISTENING ? 1f : -1f) / listeningTrDuration;
        ListeningT = Mathf.Clamp01(ListeningT);
        ThinkingT += Time.deltaTime * (CurrentState == UniStateSigilsOnlyType.THINKING ? 1f : -1f) / thinkingTrDuration;
        ThinkingT = Mathf.Clamp01(ThinkingT);
        SigilT += Time.deltaTime * (CurrentState == UniStateSigilsOnlyType.SIGIL ? 1f : -1f) / sigilTrDuration;
        SigilT = Mathf.Clamp01(SigilT);

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
                SigilCycleState = SigilCycleState.NONE;
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
            }
        }
    }
    

    private void resetSigil()
    {
        Debug.Log("Unistate:: resetSigil");
        OnSigilStart?.Invoke();
    }

    public void ChangeState(UniStateSigilsOnlyType newState)
    {
        CurrentState = newState;
    }

    public void SetStateIdle()
    {
        ChangeState(UniStateSigilsOnlyType.IDLE);
    }

    public void SetStateThinking()
    {
        ChangeState(UniStateSigilsOnlyType.THINKING);
    }

    public void SetStateSigil()
    {
        ChangeState(UniStateSigilsOnlyType.SIGIL);
        SigilCycleState = SigilCycleState.NONE;
    }

    public void SetStateListening()
    {
        ChangeState(UniStateSigilsOnlyType.LISTENING);
    }
}

