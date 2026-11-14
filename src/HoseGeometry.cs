using UnityEngine;

namespace LiquidManagement;

/// <summary>
/// The hose item (GEAR_LM_Hose, sewn from cured gut): 1.5 m of hose lying loosely coiled on the ground. The start lies
/// out straight, the first turn lies on the ground and at its end climbs over the start, the second turn lies on the
/// first (a little inside it), and the end drapes outwards back down to the ground. Both ends are open (a ring and a dark
/// bore). Pure geometry: y up, ground at y 0, the coil's centre over the origin. The outside carries a strip of the cured
/// gut's texture (GEAR_Gut_Cured_Dif), mirrored back and forth so there is no seam; the bore a dark spot of it. Unity
/// glue in HoseItem. Made for the Generator Mod (with Modelfinder), moved here with the siphoning.
/// </summary>
internal static class HoseGeometry
{
    internal readonly record struct Point(float X, float Y, float Z);

    internal const float Length = 1.5f;
    /// <summary>Outer radius: the hose is 13.6 mm thick.</summary>
    internal const float Radius = 0.0068f;
    /// <summary>Inner radius at the open ends (2.3 mm wall).</summary>
    internal const float Bore = 0.0045f;
    internal const float BoreDepth = 0.02f;
    /// <summary>Mean radius of the first turn's centre line (the coil is about 28 cm across outside).</summary>
    internal const float CoilRadius = 0.13f;
    /// <summary>The turns are this much out of round (fraction of the radius), as a loose coil lies.</summary>
    internal const float Oval = 0.05f;
    internal const float OvalPhase = 0.6f;
    /// <summary>The second turn lies this much inside the first (on average), so it sits in the first's shoulder ...</summary>
    internal const float Inset = 0.004f;
    /// <summary>... wandering this much in and out of that, as a loose coil lies.</summary>
    internal const float InsetWander = 0.003f;
    /// <summary>Air between the turns where they touch (keeps the surfaces apart).</summary>
    internal const float Gap = 0.0005f;
    internal const float StartTail = 0.14f;
    /// <summary>The start's tail bends away from the coil: this far out at its free end.</summary>
    internal const float StartSpread = 0.03f;
    internal const float EndTail = 0.12f;
    /// <summary>The end's tail turns outwards off the coil's direction by this angle, over the first EndTurn of it.</summary>
    internal const float EndOutDegrees = 30f;
    internal const float EndTurn = 0.09f;
    /// <summary>The climb over the start: from this many radians before the first turn closes ...</summary>
    internal const float ClimbFrom = 1.0f;
    /// <summary>... to this many before it (on the second turn's height from there).</summary>
    internal const float ClimbTo = 0.25f;
    /// <summary>
    /// The end's tail is down on the ground this far outside the first turn's centre line. At least 2.45 times the turns'
    /// distance (2 Radius + Gap), so the drape (a smoothstep from the crown) never dips below resting on the first turn.
    /// </summary>
    internal const float DrapeWidth = 0.045f;
    internal const int Sides = 12;
    internal const float Step = 0.008f;
    private const float Fine = 0.002f;

    // texture: the cured gut's strip is u 0..0.387, v 0..1, with its bands running along u; the rest of the atlas is dark
    // grey. Along the hose runs u (so the bands become a grain along it, as in dried gut), round it a narrow band of v.
    internal const float U0 = 0.02f, USpan = 0.35f;
    /// <summary>Hose length per run of the texture along it (forth and back is twice this).</summary>
    internal const float URun = 0.25f;
    internal const float V0 = 0.30f, VSpan = 0.10f;
    internal const float BoreU = 0.75f, BoreV = 0.5f;

    /// <summary>Height of the second turn's centre line above the first's where it lies inset to the inside, resting on it.</summary>
    internal static float StackHeight(float inset) => MathF.Sqrt((2f * Radius + Gap) * (2f * Radius + Gap) - inset * inset);

    /// <summary>The centre line from the free start to the free end, evenly about Step apart; its length is Length.</summary>
    public static List<Point> CentreLine()
    {
        // the coil's angle span is found so the whole line is Length long (its length grows steadily with the span)
        float lo = 2f * MathF.PI, hi = 4f * MathF.PI;
        for (int i = 0; i < 40; i++)
        {
            float mid = 0.5f * (lo + hi);
            if (PathLength(Dense(mid)) < Length)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }
        return Resample(Dense(0.5f * (lo + hi)), Step);
    }

    /// <summary>The hose's mesh: the outside along the centre line, and at both ends the rim and the dark bore.</summary>
    public static UnityEngine.Mesh Build(List<Point> centre, string name)
    {
        var m = new Mesher();
        int n = centre.Count;
        var tangent = new Point[n];
        for (int i = 0; i < n; i++)
        {
            tangent[i] = Normalize(Sub(centre[Math.Min(i + 1, n - 1)], centre[Math.Max(i - 1, 0)]));
        }
        // rotation-minimising frames by double reflection (Wang et al. 2008), as the Generator Mod's MachineLinesGeometry.Tube
        var normal = new Point[n];
        normal[0] = Normalize(Cross(tangent[0], new Point(0f, 1f, 0f)));
        for (int i = 1; i < n; i++)
        {
            Point r = normal[i - 1];
            Point v1 = Sub(centre[i], centre[i - 1]);
            float c1 = Dot(v1, v1);
            if (c1 > 1e-12f)
            {
                Point rL = Sub(r, Scale(v1, 2f / c1 * Dot(v1, r)));
                Point tL = Sub(tangent[i - 1], Scale(v1, 2f / c1 * Dot(v1, tangent[i - 1])));
                Point v2 = Sub(tangent[i], tL);
                float c2 = Dot(v2, v2);
                r = c2 > 1e-12f ? Sub(rL, Scale(v2, 2f / c2 * Dot(v2, rL))) : rL;
            }
            normal[i] = Normalize(Sub(r, Scale(tangent[i], Dot(r, tangent[i]))));
        }

        float s = 0f;
        var rings = new int[n][];
        for (int i = 0; i < n; i++)
        {
            if (i > 0)
            {
                s += Dist(centre[i - 1], centre[i]);
            }
            float u = U0 + USpan * Triangle(s / (2f * URun));
            rings[i] = new int[Sides];
            for (int k = 0; k < Sides; k++)
            {
                Point d = Around(normal[i], tangent[i], k);
                Point p = Add(centre[i], Scale(d, Radius));
                rings[i][k] = m.Vertex(p, d, u, V0 + VSpan * Triangle((float)k / Sides));
            }
        }
        for (int i = 0; i + 1 < n; i++)
        {
            for (int k = 0; k < Sides; k++)
            {
                int k1 = (k + 1) % Sides;
                Point side = Add(Around(normal[i], tangent[i], k), Around(normal[i], tangent[i], k1));
                m.Quad(rings[i][k], rings[i][k1], rings[i + 1][k1], rings[i + 1][k], side);
            }
        }
        End(m, centre[0], Scale(tangent[0], -1f), normal[0], tangent[0], U0);
        End(m, centre[n - 1], tangent[n - 1], normal[n - 1], tangent[n - 1], U0 + USpan * Triangle(s / (2f * URun)));
        return m.ToMesh(name);
    }

    /// <summary>Lowest and highest point of the hose's surface (from the centre line): y range for the item's size.</summary>
    public static (float MinY, float MaxY) Heights(List<Point> centre)
    {
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (Point p in centre)
        {
            lo = MathF.Min(lo, p.Y - Radius);
            hi = MathF.Max(hi, p.Y + Radius);
        }
        return (lo, hi);
    }

    // ---------------------------------------------------------------- the centre line

    /// <summary>The centre line, finely sampled, with the coil running over span radians.</summary>
    private static List<Point> Dense(float span)
    {
        var line = new List<Point>();
        // the start's tail, from its free end to where the coil begins: against the coil's direction, bending out
        Point c0 = Coil(0f), t0 = CoilTangent(0f), out0 = Radial(0f);
        int nt = (int)MathF.Ceiling(StartTail / Fine);
        for (int i = nt; i > 0; i--)
        {
            float t = StartTail * i / nt;
            float spread = StartSpread * (t / StartTail) * (t / StartTail);
            Point p = Add(Sub(c0, Scale(t0, t)), Scale(out0, spread));
            line.Add(p with { Y = Radius });
        }
        // the coil
        int nc = (int)MathF.Ceiling(span * CoilRadius / Fine);
        for (int i = 0; i <= nc; i++)
        {
            line.Add(Coil(span * i / nc));
        }
        // the end's tail: turning outwards off the coil's direction, coming down over the first turn to the ground
        Point q = Coil(span), te = CoilTangent(span), oe = Radial(span);
        float outAngle = EndOutDegrees * MathF.PI / 180f, touch = 2f * Radius + Gap;
        int ne = (int)MathF.Ceiling(EndTail / Fine);
        for (int i = 1; i <= ne; i++)
        {
            float t = EndTail * i / ne;
            float a = outAngle * SmoothStep((t - 0.5f * EndTail / ne) / EndTurn);
            q = Add(q, Scale(Add(Scale(te, MathF.Cos(a)), Scale(oe, MathF.Sin(a))), EndTail / ne));
            // still inside the first turn's crown it rests on it (as the second turn does); past the crown it drapes down to
            // the ground, never lower than resting on the first turn would let it (DrapeWidth)
            float off = OffFirstTurn(q);
            float lift = off <= 0f ? MathF.Sqrt(MathF.Max(touch * touch - off * off, 0f)) : touch * (1f - SmoothStep(off / DrapeWidth));
            line.Add(q with { Y = Radius + lift });
        }
        return line;
    }

    /// <summary>The coil's centre line at angle phi (radians from its start, counter-clockwise seen from above).</summary>
    private static Point Coil(float phi)
    {
        float climb = SmoothStep((phi - (2f * MathF.PI - ClimbFrom)) / (ClimbFrom - ClimbTo));
        float inset = Inset + InsetWander * MathF.Sin(1.3f * phi + 1f);
        float r = FirstTurnRadius(phi) - inset * climb;
        return new Point(r * MathF.Cos(phi), Radius + StackHeight(inset) * climb, r * MathF.Sin(phi));
    }

    private static float FirstTurnRadius(float phi) => CoilRadius * (1f + Oval * MathF.Sin(2f * phi + OvalPhase));

    private static Point CoilTangent(float phi)
    {
        const float h = 1e-3f;
        Point d = Sub(Coil(phi + h), Coil(phi - h));
        return Normalize(d with { Y = 0f });
    }

    private static Point Radial(float phi) => new(MathF.Cos(phi), 0f, MathF.Sin(phi));

    /// <summary>How far p lies outside the first turn's centre line, horizontally (radially at p's angle; inside: negative).</summary>
    private static float OffFirstTurn(Point p)
    {
        float phi = MathF.Atan2(p.Z, p.X);
        return MathF.Sqrt(p.X * p.X + p.Z * p.Z) - FirstTurnRadius(phi);
    }

    // ---------------------------------------------------------------- the open ends

    // the rim (a ring from the outside in to the bore, facing out along the hose) and the bore: a short dark tube inwards,
    // closed at its bottom
    private static void End(Mesher m, Point centre, Point outward, Point normal, Point tangent, float u)
    {
        var inner = new int[Sides];
        var rim = new int[Sides];
        var outerRim = new int[Sides];
        for (int k = 0; k < Sides; k++)
        {
            Point d = Around(normal, tangent, k);
            float v = V0 + VSpan * Triangle((float)k / Sides);
            outerRim[k] = m.Vertex(Add(centre, Scale(d, Radius)), outward, u, v);
            rim[k] = m.Vertex(Add(centre, Scale(d, Bore)), outward, u, v);
        }
        Point bottom = Sub(centre, Scale(outward, BoreDepth));
        var deep = new int[Sides];
        for (int k = 0; k < Sides; k++)
        {
            Point d = Around(normal, tangent, k);
            Point inwards = Scale(d, -1f);
            inner[k] = m.Vertex(Add(centre, Scale(d, Bore)), inwards, BoreU, BoreV);
            deep[k] = m.Vertex(Add(bottom, Scale(d, Bore)), inwards, BoreU, BoreV);
        }
        int middle = m.Vertex(bottom, outward, BoreU, BoreV);
        for (int k = 0; k < Sides; k++)
        {
            int k1 = (k + 1) % Sides;
            m.Quad(outerRim[k], outerRim[k1], rim[k1], rim[k], outward);
            Point side = Scale(Add(Around(normal, tangent, k), Around(normal, tangent, k1)), -1f);
            m.Quad(inner[k], inner[k1], deep[k1], deep[k], side);
            int b0 = m.Vertex(Add(bottom, Scale(Around(normal, tangent, k), Bore)), outward, BoreU, BoreV);
            int b1 = m.Vertex(Add(bottom, Scale(Around(normal, tangent, k1), Bore)), outward, BoreU, BoreV);
            m.Triangle(middle, b0, b1, outward);
        }
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>Direction of side k round the tube in the frame (normal, tangent x normal).</summary>
    private static Point Around(Point normal, Point tangent, int k)
    {
        float a = k * 2f * MathF.PI / Sides;
        return Add(Scale(normal, MathF.Cos(a)), Scale(Cross(tangent, normal), MathF.Sin(a)));
    }

    /// <summary>0 at 0, 1 at 0.5, 0 again at 1, repeating: the texture runs forth and back without a seam.</summary>
    private static float Triangle(float x)
    {
        float f = x - MathF.Floor(x);
        return 1f - MathF.Abs(2f * f - 1f);
    }

    private static float SmoothStep(float x)
    {
        x = Math.Clamp(x, 0f, 1f);
        return x * x * (3f - 2f * x);
    }

    private static float PathLength(List<Point> line)
    {
        float l = 0f;
        for (int i = 1; i < line.Count; i++)
        {
            l += Dist(line[i - 1], line[i]);
        }
        return l;
    }

    /// <summary>The line resampled evenly about step apart (first and last point kept).</summary>
    private static List<Point> Resample(List<Point> line, float step)
    {
        var length = new float[line.Count];
        for (int i = 1; i < line.Count; i++)
        {
            length[i] = length[i - 1] + Dist(line[i - 1], line[i]);
        }
        float total = length[^1];
        int n = Math.Max(1, (int)MathF.Ceiling(total / step));
        var result = new List<Point>(n + 1) { line[0] };
        int j = 1;
        for (int k = 1; k < n; k++)
        {
            float s = total * k / n;
            while (j < line.Count - 1 && length[j] < s)
            {
                j++;
            }
            float span = length[j] - length[j - 1];
            result.Add(Lerp(line[j - 1], line[j], span > 0f ? (s - length[j - 1]) / span : 0f));
        }
        result.Add(line[^1]);
        return result;
    }

    private static Point Lerp(Point a, Point b, float t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);

    private static Point Add(Point a, Point b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    private static Point Sub(Point a, Point b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    private static Point Scale(Point a, float s) => new(a.X * s, a.Y * s, a.Z * s);
    private static float Dot(Point a, Point b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    private static Point Cross(Point a, Point b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    private static float Dist(Point a, Point b) => MathF.Sqrt(Dot(Sub(a, b), Sub(a, b)));

    private static Point Normalize(Point a)
    {
        float l = MathF.Sqrt(Dot(a, a));
        return l > 1e-9f ? Scale(a, 1f / l) : new Point(1f, 0f, 0f);
    }

    /// <summary>Vertices with their own uv; triangles turned so their front faces the given normal (as the Generator Mod's CableLeadGeometry.Builder).</summary>
    private sealed class Mesher
    {
        private readonly List<float> m_P = new(), m_N = new(), m_Uv = new();
        private readonly List<int> m_T = new();

        public int Vertex(Point p, Point n, float u, float v)
        {
            m_P.Add(p.X); m_P.Add(p.Y); m_P.Add(p.Z);
            m_N.Add(n.X); m_N.Add(n.Y); m_N.Add(n.Z);
            m_Uv.Add(u); m_Uv.Add(v);
            return m_P.Count / 3 - 1;
        }

        // Unity's front face: Cross(b - a, c - a) points out of it
        public void Triangle(int a, int b, int c, Point n)
        {
            Point pa = At(a);
            Point cr = Cross(Sub(At(b), pa), Sub(At(c), pa));
            if (Dot(cr, n) >= 0f)
            {
                m_T.Add(a); m_T.Add(b); m_T.Add(c);
            }
            else
            {
                m_T.Add(a); m_T.Add(c); m_T.Add(b);
            }
        }

        public void Quad(int a, int b, int c, int d, Point n)
        {
            Triangle(a, b, c, n);
            Triangle(a, c, d, n);
        }

        public UnityEngine.Mesh ToMesh(string name)
        {
            int count = m_P.Count / 3;
            var positions = new Vector3[count];
            var normals = new Vector3[count];
            var uv = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                positions[i] = new Vector3(m_P[i * 3], m_P[i * 3 + 1], m_P[i * 3 + 2]);
                normals[i] = new Vector3(m_N[i * 3], m_N[i * 3 + 1], m_N[i * 3 + 2]);
                uv[i] = new Vector2(m_Uv[i * 2], m_Uv[i * 2 + 1]);
            }
            var mesh = new UnityEngine.Mesh { name = name };
            mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = m_T.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        private Point At(int i) => new(m_P[i * 3], m_P[i * 3 + 1], m_P[i * 3 + 2]);
    }
}
