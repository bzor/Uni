using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

[System.Serializable]
public class SigilData
{
    public string sigilPhrase;
    public string sigilCode;
}

public class ExtractPointsFromSigilData
{
    struct Segment
    {
        public Vector2 start;
        public Vector2 end;
        public float length;
        public int type; // 0=line, 1=arc, 2=quadratic, 3=bezier
        public Vector2 cp1, cp2; // control points
        public float radius, startAngle, endAngle; // for arcs
    }

    public static Vector3[] ExtractPoints(TextAsset jsonFile, int pointCount, float scale = 1.0f)
    {
        if (jsonFile == null)
        {
            Debug.LogWarning("No JSON file provided to ExtractPointsFromSigilData");
            return new Vector3[pointCount];
        }

        try
        {
            string json = jsonFile.text;
            List<SigilData> allSigils = new List<SigilData>();
            
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
            
            if (allSigils.Count == 0)
            {
                Debug.LogWarning("No sigil data found in JSON file");
                return new Vector3[pointCount];
            }

            // Use the first sigil's code
            string sigilCode = allSigils[0].sigilCode;
            return GeneratePoints(pointCount, sigilCode, scale);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to parse JSON: {e.Message}");
            return new Vector3[pointCount];
        }
    }

    public static Vector3[] ExtractPointsFromCode(string sigilCode, int pointCount, float scale = 1.0f)
    {
        return GeneratePoints(pointCount, sigilCode, scale);
    }

    static Vector3[] GeneratePoints(int count, string code, float scale)
    {
        if (string.IsNullOrEmpty(code))
        {
            Vector3[] pts = new Vector3[count];
            for (int i = 0; i < count; i++)
                pts[i] = Vector3.zero;
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
        float scaleFactor = 0.01f * scale; // canvas is 0-100, scale to Unity space
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 centered = rawPoints[i] - center;
            points[i] = new Vector3(centered.x * scaleFactor, -centered.y * scaleFactor, 0f); // Flip Y
        }

        return points;
    }

    static List<Segment> ParseSegments(string code)
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

    static Vector2 EvaluateSegment(Segment seg, float t)
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

    static Vector2 QuadraticBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    static Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float u = 1f - t;
        return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
    }

    static float EstimateQuadraticLength(Vector2 p0, Vector2 p1, Vector2 p2)
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

    static float EstimateBezierLength(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
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

