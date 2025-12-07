using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.Rendering;
using Unity.Mathematics;

[RequireComponent(typeof(VisualEffect))]
public class SigilDraw : MonoBehaviour
{
    public string meshPropertyName = "PointCacheMesh";
    public string pointCountPropertyName = "PointCount";

    [SerializeField] int pointCount = 10000;
    [SerializeField] float radius = 5f;
    [SerializeField] float updateInterval = 20f;

    [SerializeField] float disperseTimeStart = 0f;
    [SerializeField] float disperseTimeEnd = 0.1f;
    [SerializeField] float formtTimeStart = 0.3f;
    [SerializeField] float formtTimeEnd = 0.3f;

    [Header("Sigil Data")]
    public TextAsset jsonFile;
    [Range(0.5f, 10f)] public float sigilScale = 1f;
    
    [SerializeField] string sigilPhrase;
    [SerializeField, HideInInspector] string sigilCode;
    
    [Header("VFX Parameters")]
    [SerializeField, HideInInspector] float springStrength;
    [SerializeField] Vector2 springStrengthRange = new Vector2(0f, 1f);
    
    [SerializeField, HideInInspector] float damping;
    [SerializeField] Vector2 dampingRange = new Vector2(0f, 1f);
    
    [SerializeField, HideInInspector] float turbulence;
    [SerializeField] Vector2 turbulenceRange = new Vector2(0f, 1f);
    
    [SerializeField, HideInInspector] float turbulenceFreq;
    [SerializeField] Vector2 turbulenceFreqRange = new Vector2(0f, 1f);
    
    [SerializeField, HideInInspector] float turbulenceRoughness;
    [SerializeField] Vector2 turbulenceRoughnessRange = new Vector2(0f, 1f);
    
    [SerializeField, HideInInspector] float turbulenceLacunarity;
    [SerializeField] Vector2 turbulenceLacunarityRange = new Vector2(0f, 1f);

    VisualEffect vfx;
    Mesh pointMesh;
    float tick;
    int currentSigilIndex = 0;
    List<SigilData> allSigils = new List<SigilData>();
    bool transitionedThisCycle = false;
    Vector3[] oldPoints;
    Vector3[] newPoints;
    bool isTransitioning = false;

    void Awake()
    {
        vfx = GetComponent<VisualEffect>();

        pointMesh = new Mesh();
        pointMesh.indexFormat = IndexFormat.UInt32; // allow >65k

        LoadSigilData();

        // Initialize values to min
        springStrength = springStrengthRange.x;
        damping = dampingRange.x;
        turbulence = turbulenceRange.x;
        turbulenceFreq = turbulenceFreqRange.x;
        turbulenceRoughness = turbulenceRoughnessRange.x;
        turbulenceLacunarity = turbulenceLacunarityRange.x;

        RebuildPoints();
        
        // Initialize oldPoints for transition
        oldPoints = new Vector3[pointMesh.vertexCount];
        pointMesh.vertices.CopyTo(oldPoints, 0);
        
        vfx.SetMesh(meshPropertyName, pointMesh);
        vfx.SetInt(pointCountPropertyName, pointCount);

        vfx.Play();
    }

    void LoadSigilData()
    {
        if (jsonFile == null)
        {
            Debug.LogWarning("No JSON file assigned to SigilDraw");
            return;
        }

        try
        {
            string json = jsonFile.text;
            allSigils.Clear();
            
            // Find all sigilPhrase matches
            var phraseMatches = Regex.Matches(json, @"""sigilPhrase""\s*:\s*""([^""]+)""");
            
            // Try both sigilCode and drawCalls formats
            var codeMatches = Regex.Matches(json, @"""sigilCode""\s*:\s*""((?:[^""\\]|\\.|\\n)*)""", RegexOptions.Singleline);
            var drawCallsMatches = Regex.Matches(json, @"""drawCalls""\s*:\s*""((?:[^""\\]|\\.|\\n)*)""", RegexOptions.Singleline);
            
            // Use whichever format is found
            MatchCollection codeCollection = codeMatches.Count > 0 ? codeMatches : drawCallsMatches;
            
            int count = Mathf.Min(phraseMatches.Count, codeCollection.Count);
            for (int i = 0; i < count; i++)
            {
                string phrase = phraseMatches[i].Groups[1].Value;
                string code = codeCollection[i].Groups[1].Value.Replace("\\n", "\n").Replace("\\\"", "\"");
                allSigils.Add(new SigilData { sigilPhrase = phrase, sigilCode = code });
            }
            
            if (allSigils.Count > 0)
            {
                LoadSigilByIndex(0);
                Debug.Log($"Loaded {allSigils.Count} sigils");
            }
            else
            {
                Debug.LogWarning("No sigil data found in JSON file");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to parse JSON: {e.Message}");
        }
    }

    void LoadSigilByIndex(int index)
    {
        if (allSigils.Count == 0) return;
        
        currentSigilIndex = index % allSigils.Count;
        if (currentSigilIndex < 0) currentSigilIndex += allSigils.Count;
        
        sigilPhrase = allSigils[currentSigilIndex].sigilPhrase;
        sigilCode = allSigils[currentSigilIndex].sigilCode;
    }

    void Update()
    {
        tick += Time.deltaTime;
        float t = Mathf.Clamp01(tick / updateInterval);
        
        // Reset transition flag at start of cycle
        if (t < 0.01f)
        {
            transitionedThisCycle = false;
            isTransitioning = false;
        }
        
        float valueT = 0;
        valueT = math.smoothstep(disperseTimeStart, disperseTimeEnd, t);
        valueT *= (1f - math.smoothstep(formtTimeStart, formtTimeEnd, t));
        
        // Update values
        springStrength = Mathf.Lerp(springStrengthRange.x, springStrengthRange.y, valueT);
        damping = Mathf.Lerp(dampingRange.x, dampingRange.y, valueT);
        turbulence = Mathf.Lerp(turbulenceRange.x, turbulenceRange.y, valueT);
        turbulenceFreq = Mathf.Lerp(turbulenceFreqRange.x, turbulenceFreqRange.y, valueT);
        turbulenceRoughness = Mathf.Lerp(turbulenceRoughnessRange.x, turbulenceRoughnessRange.y, valueT);
        turbulenceLacunarity = Mathf.Lerp(turbulenceLacunarityRange.x, turbulenceLacunarityRange.y, valueT);
        
        // Set VFX properties
        vfx.SetFloat("SpringStrength", springStrength);
        vfx.SetFloat("Damping", damping);
        vfx.SetFloat("Turbulence", turbulence);
        vfx.SetFloat("TurbulenceFreq", turbulenceFreq);
        vfx.SetFloat("TurbulenceRoughness", turbulenceRoughness);
        vfx.SetFloat("TurbulenceLacunarity", turbulenceLacunarity);
        
        // Start transition at disperseTimeEnd
        if (t >= disperseTimeEnd && !transitionedThisCycle)
        {
            transitionedThisCycle = true;
            isTransitioning = true;
            
            // Store current points as old
            oldPoints = new Vector3[pointMesh.vertexCount];
            pointMesh.vertices.CopyTo(oldPoints, 0);
            
            // Generate new sigil points
            LoadSigilByIndex(currentSigilIndex + 1);
            newPoints = GeneratePoints(pointCount, sigilCode);
        }
        
        // Blend points during transition period
        if (isTransitioning && t >= disperseTimeEnd && t <= formtTimeStart)
        {
            float transitionProgress = Mathf.InverseLerp(disperseTimeEnd, formtTimeStart, t);
            BlendPoints(transitionProgress);
            pointMesh.UploadMeshData(false);
        }
        else if (isTransitioning && t > formtTimeStart)
        {
            // Transition complete, use new points
            isTransitioning = false;
            pointMesh.vertices = newPoints;
            pointMesh.UploadMeshData(false);
        }
        
        if (tick >= updateInterval)
        {
            tick = 0f;
        }
    }

    void RebuildPoints()
    {
        Vector3[] points = GeneratePoints(pointCount, sigilCode);

        pointMesh.vertices = points;

        if (pointMesh.GetIndexCount(0) == 0)
        {
            int[] indices = Enumerable.Range(0, pointCount).ToArray();
            pointMesh.SetIndices(indices, MeshTopology.Points, 0, false);
        }
    }

    void BlendPoints(float globalProgress)
    {
        if (oldPoints == null || newPoints == null || oldPoints.Length != newPoints.Length) return;
        
        Vector3[] blended = new Vector3[oldPoints.Length];
        int count = oldPoints.Length;
        
        if (count == 1)
        {
            blended[0] = Vector3.Lerp(oldPoints[0], newPoints[0], globalProgress);
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                // Point i transitions when globalProgress reaches i/(count-1)
                // First point (i=0) transitions immediately, last point (i=count-1) transitions at the end
                float pointProgress = Mathf.Clamp01((globalProgress * (count - 1) - i + 1f) / 1f);
                blended[i] = Vector3.Lerp(oldPoints[i], newPoints[i], pointProgress);
            }
        }
        
        pointMesh.vertices = blended;
    }

    struct Segment
    {
        public Vector2 start;
        public Vector2 end;
        public float length;
        public int type; // 0=line, 1=arc, 2=quadratic, 3=bezier
        public Vector2 cp1, cp2; // control points
        public float radius, startAngle, endAngle; // for arcs
    }

    Vector3[] GeneratePoints(int count, string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            Vector3[] pts = new Vector3[count];
            for (int i = 0; i < count; i++)
                pts[i] = UnityEngine.Random.insideUnitSphere * radius;
            return pts;
        }

        List<Segment> segments = ParseSegments(code);
        if (segments.Count == 0) return new Vector3[count];

        float totalLength = segments.Sum(s => s.length);
        if (totalLength == 0) return new Vector3[count];

        List<Vector2> rawPoints = new List<Vector2>(count);

        foreach (var seg in segments)
        {
            int segPointCount = Mathf.Max(1, Mathf.RoundToInt(count * seg.length / totalLength));
            for (int i = 0; i < segPointCount; i++)
            {
                float t = segPointCount > 1 ? (float)i / (segPointCount - 1) : 0f;
                rawPoints.Add(EvaluateSegment(seg, t));
            }
        }

        int baseCount = rawPoints.Count;
        while (rawPoints.Count < count && baseCount > 0)
            rawPoints.Add(rawPoints[rawPoints.Count % baseCount]);

        // Calculate bounding box and center
        Vector2 min = rawPoints[0];
        Vector2 max = rawPoints[0];
        for (int i = 1; i < rawPoints.Count; i++)
        {
            min = Vector2.Min(min, rawPoints[i]);
            max = Vector2.Max(max, rawPoints[i]);
        }
        Vector2 center = (min + max) * 0.5f;

        // Center and scale points
        Vector3[] points = new Vector3[Mathf.Min(count, rawPoints.Count)];
        float scale = 0.01f * sigilScale; // canvas is 0-100, scale to Unity space
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 centered = rawPoints[i] - center;
            points[i] = new Vector3(centered.x * scale, centered.y * scale, 0f);
        }

        return points;
    }

    List<Segment> ParseSegments(string code)
    {
        List<Segment> segments = new List<Segment>();
        Vector2 currentPos = Vector2.zero;
        Vector2 pathStart = Vector2.zero;

        string[] lines = code.Split('\n');
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("ctx.moveTo"))
            {
                var match = Regex.Match(trimmed, @"moveTo\(([\d.]+),\s*([\d.]+)\)");
                if (match.Success)
                {
                    currentPos = new Vector2(float.Parse(match.Groups[1].Value), float.Parse(match.Groups[2].Value));
                    pathStart = currentPos;
                }
            }
            else if (trimmed.StartsWith("ctx.lineTo"))
            {
                var match = Regex.Match(trimmed, @"lineTo\(([\d.]+),\s*([\d.]+)\)");
                if (match.Success)
                {
                    Vector2 end = new Vector2(float.Parse(match.Groups[1].Value), float.Parse(match.Groups[2].Value));
                    segments.Add(new Segment { start = currentPos, end = end, length = Vector2.Distance(currentPos, end), type = 0 });
                    currentPos = end;
                }
            }
            else if (trimmed.StartsWith("ctx.arc"))
            {
                var match = Regex.Match(trimmed, @"arc\(([\d.]+),\s*([\d.]+),\s*([\d.]+),\s*([\d.]+),\s*([\d.]+)\)");
                if (match.Success)
                {
                    float cx = float.Parse(match.Groups[1].Value);
                    float cy = float.Parse(match.Groups[2].Value);
                    float r = float.Parse(match.Groups[3].Value);
                    float sa = float.Parse(match.Groups[4].Value);
                    float ea = float.Parse(match.Groups[5].Value);
                    float arcLength = r * Mathf.Abs(ea - sa);
                    Vector2 startPt = new Vector2(cx + r * Mathf.Cos(sa), cy + r * Mathf.Sin(sa));
                    Vector2 endPt = new Vector2(cx + r * Mathf.Cos(ea), cy + r * Mathf.Sin(ea));
                    segments.Add(new Segment { start = startPt, end = endPt, length = arcLength, type = 1, radius = r, startAngle = sa, endAngle = ea, cp1 = new Vector2(cx, cy) });
                    currentPos = endPt;
                }
            }
            else if (trimmed.StartsWith("ctx.quadraticCurveTo"))
            {
                var match = Regex.Match(trimmed, @"quadraticCurveTo\(([\d.]+),\s*([\d.]+),\s*([\d.]+),\s*([\d.]+)\)");
                if (match.Success)
                {
                    Vector2 cp = new Vector2(float.Parse(match.Groups[1].Value), float.Parse(match.Groups[2].Value));
                    Vector2 end = new Vector2(float.Parse(match.Groups[3].Value), float.Parse(match.Groups[4].Value));
                    float len = EstimateQuadraticLength(currentPos, cp, end);
                    segments.Add(new Segment { start = currentPos, end = end, length = len, type = 2, cp1 = cp });
                    currentPos = end;
                }
            }
            else if (trimmed.StartsWith("ctx.bezierCurveTo"))
            {
                var match = Regex.Match(trimmed, @"bezierCurveTo\(([\d.]+),\s*([\d.]+),\s*([\d.]+),\s*([\d.]+),\s*([\d.]+),\s*([\d.]+)\)");
                if (match.Success)
                {
                    Vector2 cp1 = new Vector2(float.Parse(match.Groups[1].Value), float.Parse(match.Groups[2].Value));
                    Vector2 cp2 = new Vector2(float.Parse(match.Groups[3].Value), float.Parse(match.Groups[4].Value));
                    Vector2 end = new Vector2(float.Parse(match.Groups[5].Value), float.Parse(match.Groups[6].Value));
                    float len = EstimateBezierLength(currentPos, cp1, cp2, end);
                    segments.Add(new Segment { start = currentPos, end = end, length = len, type = 3, cp1 = cp1, cp2 = cp2 });
                    currentPos = end;
                }
            }
            else if (trimmed.StartsWith("ctx.closePath"))
            {
                if (Vector2.Distance(currentPos, pathStart) > 0.001f)
                {
                    segments.Add(new Segment { start = currentPos, end = pathStart, length = Vector2.Distance(currentPos, pathStart), type = 0 });
                    currentPos = pathStart;
                }
            }
        }

        return segments;
    }

    Vector2 EvaluateSegment(Segment seg, float t)
    {
        switch (seg.type)
        {
            case 0: return Vector2.Lerp(seg.start, seg.end, t);
            case 1:
                float angle = Mathf.Lerp(seg.startAngle, seg.endAngle, t);
                return new Vector2(seg.cp1.x + seg.radius * Mathf.Cos(angle), seg.cp1.y + seg.radius * Mathf.Sin(angle));
            case 2: return QuadraticBezier(seg.start, seg.cp1, seg.end, t);
            case 3: return CubicBezier(seg.start, seg.cp1, seg.cp2, seg.end, t);
            default: return seg.start;
        }
    }

    Vector2 QuadraticBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float u = 1f - t;
        return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
    }

    float EstimateQuadraticLength(Vector2 p0, Vector2 p1, Vector2 p2)
    {
        float len = 0f;
        Vector2 prev = p0;
        for (int i = 1; i <= 10; i++)
        {
            float t = i / 10f;
            Vector2 curr = QuadraticBezier(p0, p1, p2, t);
            len += Vector2.Distance(prev, curr);
            prev = curr;
        }
        return len;
    }

    float EstimateBezierLength(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
    {
        float len = 0f;
        Vector2 prev = p0;
        for (int i = 1; i <= 10; i++)
        {
            float t = i / 10f;
            Vector2 curr = CubicBezier(p0, p1, p2, p3, t);
            len += Vector2.Distance(prev, curr);
            prev = curr;
        }
        return len;
    }
}
