using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

// Fauna brasileira low-poly: poucos animais por fase, só visuais (sem colisão).
// Fica num objeto raiz "Fauna", separado do cenário; pode ser gerada sozinha (AddFauna)
// sem alterar o resto da cena.
public static partial class ThemedLevelBuilder
{
    enum F { Brown, DarkBrown, Orange, Gray, Black, White, Blue, Yellow, Red, Beak, Gator, PinkRed, DolphinGray, TurtleGreen, Shell, Morpho }
    static readonly string[] FaunaHex =
    {
        "#7A5A3C", "#4A3626", "#D4752E", "#8A8A88", "#222226", "#F2F0EA", "#2F6FD6", "#F2C230",
        "#D6332E", "#F28C1E", "#55623E", "#E8909E", "#8C9AA8", "#5E7A4A", "#8A6A3A", "#3FA9F5",
    };

    static Vector2 FU(F f, int shade = 4) => UV((R)(int)f, shade);
    static void Ell(MB mb, Vector3 c, Vector3 s, F f, int sub = 0) => Blob(mb, c, s, 0.05f, sub, n => FU(f, Mathf.Clamp(Mathf.RoundToInt(3.5f + n.y * 1.5f), 1, 6)));

    static void Rod(MB mb, Vector3 a, Vector3 b, float r0, float r1, F f, int sides = 5)
    {
        var dir = (b - a).normalized;
        var side = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
        var up = Vector3.Cross(side, dir);
        Vector3[] Ring(Vector3 c, float r) { var ring = new Vector3[sides]; for (int j = 0; j < sides; j++) { float t = j * Mathf.PI * 2 / sides; ring[j] = c + (side * Mathf.Cos(t) + up * Mathf.Sin(t)) * r; } return ring; }
        var ra = Ring(a, r0); var rb = Ring(b, Mathf.Max(0.001f, r1));
        Loft(mb, new List<Vector3[]> { ra, rb }, (i, j, n) => FU(f, 3 + (j % 2)), a, b);
        Fan(mb, b, rb, () => FU(f, 4), dir);
    }

    static void ConeTo(MB mb, Vector3 a, Vector3 tip, float r, F f) => Rod(mb, a, tip, r, 0.001f, f);

    static MB Wing(float len, float width, F f, F tip, bool right)
    {
        var mb = new MB();
        float sx = right ? 1f : -1f;
        Vector3 P(float x, float z) => new Vector3(x * sx, 0f, z);
        var pts = new[] { P(0, width * 0.5f), P(len * 0.45f, width * 0.55f), P(len, width * 0.1f), P(len * 0.85f, -width * 0.35f), P(len * 0.35f, -width * 0.5f), P(0, -width * 0.35f) };
        for (int i = 0; i < pts.Length - 1; i++)
            mb.TriRaw(Vector3.zero, pts[i], pts[i + 1], FU(i == 1 || i == 2 ? tip : f, 4));
        return mb;
    }

    class Species
    {
        public string name;
        public Mesh body, head, wingL, wingR;
        public Vector3 headPivot, wingPivotL, wingPivotR;
    }

    struct BirdSpec
    {
        public float L, beakLen, tailLen, wingLen, neckLen, legLen;
        public F body, belly, headF, beak, tail, wing, wingTip, leg;
        public bool folded;
    }

    static Species MakeBird(string name, BirdSpec s)
    {
        float L = s.L;
        var b = new MB();
        Ell(b, Vector3.zero, new Vector3(0.28f, 0.26f, 0.5f) * L, s.body, 1);
        Ell(b, new Vector3(0, -0.05f, 0.03f) * L, new Vector3(0.24f, 0.2f, 0.42f) * L, s.belly, 0);
        var neckBase = new Vector3(0, 0.1f, 0.38f) * L;
        var hc = new Vector3(0, 0.14f, 0.5f) * L;
        if (s.neckLen > 0f)
        {
            hc = neckBase + new Vector3(0, s.neckLen * 0.75f, s.neckLen * 0.45f);
            Rod(b, neckBase, hc, 0.07f * L, 0.05f * L, s.headF);
        }
        Ell(b, Vector3.zero + new Vector3(0, -0.02f, -0.5f - s.tailLen * 0.45f / Mathf.Max(0.01f, L)) * L, new Vector3(0.12f * L, 0.03f * L, s.tailLen * 0.5f), s.tail, 0);
        var head = new MB();
        Ell(head, Vector3.zero, Vector3.one * 0.17f * L, s.headF, 0);
        float beakR = s.beakLen > 0.25f * L ? 0.075f * L : 0.045f * L;
        ConeTo(head, new Vector3(0, -0.01f, 0.12f) * L, new Vector3(0, -0.02f * L - s.beakLen * 0.15f, 0.12f * L + s.beakLen), beakR, s.beak);
        foreach (float sx in new[] { -1f, 1f }) Ell(head, new Vector3(sx * 0.13f, 0.04f, 0.06f) * L, Vector3.one * 0.035f * L, F.Black, 0);
        if (s.legLen > 0f)
            foreach (float sx in new[] { -1f, 1f }) Rod(b, new Vector3(sx * 0.07f, -0.18f, 0f) * L, new Vector3(sx * 0.07f * L, -0.18f * L - s.legLen, s.folded ? 0.02f * L : -s.legLen * 0.6f), 0.025f * L, 0.018f * L, s.leg, 4);
        var sp = new Species { name = name, headPivot = hc };
        sp.body = Reg(b.Build($"Fauna_{name}_Body"));
        sp.head = Reg(head.Build($"Fauna_{name}_Head"));
        if (s.wingLen > 0f)
        {
            sp.wingL = Reg(Wing(s.wingLen, 0.45f * L, s.wing, s.wingTip, false).Build($"Fauna_{name}_WingL"));
            sp.wingR = Reg(Wing(s.wingLen, 0.45f * L, s.wing, s.wingTip, true).Build($"Fauna_{name}_WingR"));
            sp.wingPivotL = new Vector3(-0.12f, 0.08f, 0.05f) * L;
            sp.wingPivotR = new Vector3(0.12f, 0.08f, 0.05f) * L;
        }
        return sp;
    }

    // quadrúpedes / bípedes (com os pés em y = 0)
    static Species MakeWalker(string name)
    {
        var b = new MB(); var h = new MB();
        Vector3 hp;
        switch (name)
        {
            case "Capivara":
            {
                float L = 1.1f, y = 0.38f;
                Ell(b, new Vector3(0, y, 0), new Vector3(0.32f, 0.3f, 0.52f) * L, F.Brown, 1);
                foreach (var (x, z) in new[] { (-0.18f, 0.32f), (0.18f, 0.32f), (-0.18f, -0.3f), (0.18f, -0.3f) }) Rod(b, new Vector3(x * L, y - 0.1f, z * L), new Vector3(x * L, 0f, z * L), 0.06f, 0.05f, F.DarkBrown, 4);
                hp = new Vector3(0, y + 0.12f, 0.5f * L);
                Ell(h, new Vector3(0, 0.02f, 0.14f), new Vector3(0.17f, 0.19f, 0.26f), F.Brown, 0);
                Ell(h, new Vector3(0, -0.02f, 0.36f), new Vector3(0.12f, 0.12f, 0.08f), F.DarkBrown, 0);
                foreach (float sx in new[] { -1f, 1f }) { Ell(h, new Vector3(sx * 0.11f, 0.15f, 0.02f), Vector3.one * 0.045f, F.DarkBrown, 0); Ell(h, new Vector3(sx * 0.12f, 0.07f, 0.2f), Vector3.one * 0.025f, F.Black, 0); }
                break;
            }
            case "LoboGuara":
            {
                float y = 0.62f;
                Ell(b, new Vector3(0, y, 0), new Vector3(0.17f, 0.2f, 0.45f), F.Orange, 1);
                Ell(b, new Vector3(0, y + 0.12f, 0.28f), new Vector3(0.11f, 0.12f, 0.16f), F.Black, 0); // crina
                foreach (var (x, z) in new[] { (-0.09f, 0.3f), (0.09f, 0.3f), (-0.09f, -0.3f), (0.09f, -0.3f) }) Rod(b, new Vector3(x, y - 0.1f, z), new Vector3(x, 0f, z), 0.04f, 0.03f, F.Black, 4);
                Rod(b, new Vector3(0, y + 0.05f, -0.42f), new Vector3(0, y - 0.25f, -0.72f), 0.07f, 0.03f, F.Orange);
                Ell(b, new Vector3(0, y - 0.27f, -0.74f), Vector3.one * 0.05f, F.White, 0);
                hp = new Vector3(0, y + 0.2f, 0.45f);
                Ell(h, new Vector3(0, 0.02f, 0.06f), new Vector3(0.1f, 0.1f, 0.12f), F.Orange, 0);
                ConeTo(h, new Vector3(0, -0.01f, 0.14f), new Vector3(0, -0.04f, 0.3f), 0.05f, F.Black);
                foreach (float sx in new[] { -1f, 1f }) { ConeTo(h, new Vector3(sx * 0.06f, 0.08f, 0.02f), new Vector3(sx * 0.1f, 0.26f, 0f), 0.045f, F.Orange); Ell(h, new Vector3(sx * 0.07f, 0.05f, 0.13f), Vector3.one * 0.018f, F.Black, 0); }
                break;
            }
            case "Tatu":
            {
                float y = 0.16f;
                Ell(b, new Vector3(0, y, 0), new Vector3(0.17f, 0.15f, 0.26f), F.Gray, 1);
                for (int i = -2; i <= 2; i++) Ell(b, new Vector3(0, y + 0.02f, i * 0.07f), new Vector3(0.175f, 0.135f, 0.02f), F.DarkBrown, 0);
                Rod(b, new Vector3(0, y - 0.02f, -0.24f), new Vector3(0, 0.03f, -0.42f), 0.035f, 0.01f, F.Gray);
                foreach (var (x, z) in new[] { (-0.09f, 0.12f), (0.09f, 0.12f), (-0.09f, -0.12f), (0.09f, -0.12f) }) Rod(b, new Vector3(x, y - 0.06f, z), new Vector3(x, 0f, z), 0.025f, 0.02f, F.Gray, 4);
                hp = new Vector3(0, y, 0.24f);
                ConeTo(h, new Vector3(0, 0, 0), new Vector3(0, -0.06f, 0.16f), 0.05f, F.Gray);
                foreach (float sx in new[] { -1f, 1f }) ConeTo(h, new Vector3(sx * 0.03f, 0.03f, -0.01f), new Vector3(sx * 0.05f, 0.1f, -0.02f), 0.02f, F.Gray);
                break;
            }
            case "Ema":
            {
                float y = 0.95f;
                Ell(b, new Vector3(0, y, 0), new Vector3(0.36f, 0.32f, 0.48f), F.Gray, 1);
                foreach (float sx in new[] { -1f, 1f }) Rod(b, new Vector3(sx * 0.1f, y - 0.2f, 0f), new Vector3(sx * 0.1f, 0f, 0.05f), 0.05f, 0.03f, F.Gray, 4);
                Rod(b, new Vector3(0, y + 0.15f, 0.32f), new Vector3(0, y + 0.75f, 0.5f), 0.08f, 0.05f, F.Gray);
                hp = new Vector3(0, y + 0.78f, 0.52f);
                Ell(h, Vector3.zero, Vector3.one * 0.08f, F.Gray, 0);
                ConeTo(h, new Vector3(0, -0.01f, 0.06f), new Vector3(0, -0.02f, 0.2f), 0.03f, F.Beak);
                foreach (float sx in new[] { -1f, 1f }) Ell(h, new Vector3(sx * 0.06f, 0.02f, 0.03f), Vector3.one * 0.016f, F.Black, 0);
                break;
            }
            case "Pinguim":
            {
                Ell(b, new Vector3(0, 0.32f, 0), new Vector3(0.18f, 0.3f, 0.17f), F.Black, 1);
                Ell(b, new Vector3(0, 0.3f, 0.1f), new Vector3(0.13f, 0.23f, 0.085f), F.White, 0);
                foreach (float sx in new[] { -1f, 1f }) Ell(b, new Vector3(sx * 0.06f, 0.02f, 0.06f), new Vector3(0.05f, 0.02f, 0.08f), F.PinkRed, 0);
                hp = new Vector3(0, 0.62f, 0f);
                Ell(h, Vector3.zero, Vector3.one * 0.11f, F.Black, 0);
                Ell(h, new Vector3(0, -0.04f, 0.06f), new Vector3(0.09f, 0.04f, 0.05f), F.White, 0);
                ConeTo(h, new Vector3(0, -0.01f, 0.08f), new Vector3(0, -0.03f, 0.2f), 0.03f, F.Black);
                foreach (float sx in new[] { -1f, 1f }) Ell(h, new Vector3(sx * 0.07f, 0.02f, 0.06f), Vector3.one * 0.018f, F.White, 0);
                break;
            }
            case "Calango":
            {
                Ell(b, new Vector3(0, 0.05f, 0), new Vector3(0.06f, 0.04f, 0.17f), F.Gator, 0);
                Rod(b, new Vector3(0, 0.05f, -0.15f), new Vector3(0, 0.02f, -0.48f), 0.035f, 0.005f, F.Gator);
                foreach (var (x, z) in new[] { (-0.07f, 0.09f), (0.07f, 0.09f), (-0.07f, -0.08f), (0.07f, -0.08f) }) Rod(b, new Vector3(x * 0.5f, 0.04f, z), new Vector3(x, 0f, z + 0.02f), 0.012f, 0.008f, F.Gator, 4);
                hp = new Vector3(0, 0.06f, 0.16f);
                ConeTo(h, Vector3.zero, new Vector3(0, -0.01f, 0.1f), 0.04f, F.Orange);
                break;
            }
            case "Macaco":
            {
                // macaco-prego sentado (origem nos pés), cauda longa pendurada
                Ell(b, new Vector3(0, 0.3f, 0), new Vector3(0.17f, 0.24f, 0.15f), F.Brown, 1);
                Ell(b, new Vector3(0, 0.26f, 0.08f), new Vector3(0.12f, 0.16f, 0.07f), F.Shell, 0);
                foreach (float sx in new[] { -1f, 1f })
                {
                    Rod(b, new Vector3(sx * 0.13f, 0.42f, 0.02f), new Vector3(sx * 0.16f, 0.12f, 0.14f), 0.04f, 0.03f, F.DarkBrown, 4);
                    Rod(b, new Vector3(sx * 0.09f, 0.12f, 0.02f), new Vector3(sx * 0.12f, 0.02f, 0.16f), 0.05f, 0.035f, F.DarkBrown, 4);
                }
                Rod(b, new Vector3(0, 0.12f, -0.12f), new Vector3(0.05f, -0.25f, -0.2f), 0.035f, 0.03f, F.DarkBrown, 4);
                Rod(b, new Vector3(0.05f, -0.25f, -0.2f), new Vector3(0.12f, -0.55f, -0.08f), 0.03f, 0.015f, F.DarkBrown, 4);
                hp = new Vector3(0, 0.58f, 0.03f);
                Ell(h, Vector3.zero, new Vector3(0.12f, 0.12f, 0.11f), F.DarkBrown, 0);
                Ell(h, new Vector3(0, -0.02f, 0.07f), new Vector3(0.08f, 0.08f, 0.05f), F.Shell, 0);
                Ell(h, new Vector3(0, 0.09f, 0.02f), new Vector3(0.09f, 0.04f, 0.08f), F.Black, 0);
                foreach (float sx in new[] { -1f, 1f }) { Ell(h, new Vector3(sx * 0.035f, 0.01f, 0.1f), Vector3.one * 0.018f, F.Black, 0); Ell(h, new Vector3(sx * 0.11f, 0.02f, 0f), Vector3.one * 0.03f, F.Shell, 0); }
                break;
            }
            case "Preguica":
            {
                // preguiça pendurada (origem nas mãos, no galho); corpo para baixo
                foreach (float sx in new[] { -1f, 1f })
                {
                    Rod(b, new Vector3(sx * 0.08f, 0f, 0.12f), new Vector3(sx * 0.13f, -0.32f, 0.04f), 0.035f, 0.045f, F.Gray, 4);
                    Rod(b, new Vector3(sx * 0.08f, 0f, -0.18f), new Vector3(sx * 0.12f, -0.62f, -0.06f), 0.035f, 0.05f, F.Gray, 4);
                    ConeTo(b, new Vector3(sx * 0.08f, 0f, 0.12f), new Vector3(sx * 0.08f, 0.04f, 0.2f), 0.02f, F.DarkBrown);
                }
                Ell(b, new Vector3(0, -0.5f, 0f), new Vector3(0.2f, 0.28f, 0.18f), F.Gray, 1);
                Ell(b, new Vector3(0, -0.48f, 0.08f), new Vector3(0.14f, 0.2f, 0.1f), F.Shell, 0);
                hp = new Vector3(0, -0.3f, 0.16f);
                Ell(h, Vector3.zero, new Vector3(0.11f, 0.1f, 0.1f), F.Shell, 0);
                foreach (float sx in new[] { -1f, 1f }) { Ell(h, new Vector3(sx * 0.045f, 0.01f, 0.07f), new Vector3(0.035f, 0.022f, 0.02f), F.DarkBrown, 0); Ell(h, new Vector3(sx * 0.045f, 0.012f, 0.085f), Vector3.one * 0.012f, F.Black, 0); }
                Ell(h, new Vector3(0, -0.03f, 0.095f), Vector3.one * 0.018f, F.Black, 0);
                break;
            }
            default: throw new Exception("animal " + name);
        }
        var sp = new Species { name = name, headPivot = hp };
        sp.body = Reg(b.Build($"Fauna_{name}_Body"));
        sp.head = Reg(h.Build($"Fauna_{name}_Head"));
        if (name == "Pinguim")
        {
            sp.wingL = Reg(Wing(0.22f, 0.1f, F.Black, F.Black, false).Build("Fauna_Pinguim_WingL"));
            sp.wingR = Reg(Wing(0.22f, 0.1f, F.Black, F.Black, true).Build("Fauna_Pinguim_WingR"));
            sp.wingPivotL = new Vector3(-0.16f, 0.45f, 0f); sp.wingPivotR = new Vector3(0.16f, 0.45f, 0f);
        }
        return sp;
    }

    // animais da água (centro do corpo em y = 0)
    static Species MakeSwimmer(string name)
    {
        var b = new MB();
        switch (name)
        {
            case "Jacare":
                Ell(b, Vector3.zero, new Vector3(0.36f, 0.16f, 0.75f), F.Gator, 1);
                Ell(b, new Vector3(0, -0.02f, 0.95f), new Vector3(0.16f, 0.08f, 0.42f), F.Gator, 0);
                Rod(b, new Vector3(0, 0, -0.65f), new Vector3(0, -0.02f, -2.0f), 0.2f, 0.02f, F.Gator);
                for (int i = 0; i < 6; i++) Ell(b, new Vector3(0, 0.13f, 0.5f - i * 0.22f), new Vector3(0.12f, 0.05f, 0.08f), F.Gator, 0);
                foreach (float sx in new[] { -1f, 1f }) { Ell(b, new Vector3(sx * 0.11f, 0.09f, 0.62f), Vector3.one * 0.06f, F.Yellow, 0); Ell(b, new Vector3(sx * 0.13f, 0.1f, 0.65f), Vector3.one * 0.025f, F.Black, 0); }
                break;
            case "BotoRosa":
                // boto-cor-de-rosa: bico longo e fino, testa arredondada (melão), corpo rosado
                Ell(b, Vector3.zero, new Vector3(0.3f, 0.3f, 0.9f), F.PinkRed, 1);
                Ell(b, new Vector3(0, -0.06f, 0.1f), new Vector3(0.25f, 0.2f, 0.72f), F.White, 0);
                Ell(b, new Vector3(0, 0.08f, 0.72f), new Vector3(0.2f, 0.2f, 0.2f), F.PinkRed, 0);
                ConeTo(b, new Vector3(0, -0.05f, 0.85f), new Vector3(0, -0.1f, 1.45f), 0.06f, F.PinkRed);
                Ell(b, new Vector3(0, 0.22f, -0.1f), new Vector3(0.06f, 0.06f, 0.3f), F.PinkRed, 0);
                Ell(b, new Vector3(0, 0, -1.0f), new Vector3(0.4f, 0.04f, 0.14f), F.PinkRed, 0);
                foreach (float sx in new[] { -1f, 1f }) Ell(b, new Vector3(sx * 0.3f, -0.12f, 0.35f), new Vector3(0.2f, 0.03f, 0.12f), F.PinkRed, 0);
                foreach (float sx in new[] { -1f, 1f }) Ell(b, new Vector3(sx * 0.12f, 0.02f, 0.68f), Vector3.one * 0.025f, F.Black, 0);
                break;
            case "Boto":
                Ell(b, Vector3.zero, new Vector3(0.3f, 0.32f, 0.95f), F.DolphinGray, 1);
                Ell(b, new Vector3(0, -0.08f, 0.1f), new Vector3(0.24f, 0.2f, 0.75f), F.White, 0);
                ConeTo(b, new Vector3(0, -0.05f, 0.85f), new Vector3(0, -0.08f, 1.25f), 0.09f, F.DolphinGray);
                ConeTo(b, new Vector3(0, 0.25f, -0.05f), new Vector3(0, 0.6f, -0.35f), 0.12f, F.DolphinGray);
                Ell(b, new Vector3(0, 0, -1.05f), new Vector3(0.42f, 0.04f, 0.14f), F.DolphinGray, 0);
                foreach (float sx in new[] { -1f, 1f }) Ell(b, new Vector3(sx * 0.3f, -0.12f, 0.35f), new Vector3(0.16f, 0.03f, 0.1f), F.DolphinGray, 0);
                foreach (float sx in new[] { -1f, 1f }) Ell(b, new Vector3(sx * 0.14f, 0.02f, 0.72f), Vector3.one * 0.03f, F.Black, 0);
                break;
            case "Tartaruga":
                Ell(b, new Vector3(0, 0.05f, 0), new Vector3(0.42f, 0.16f, 0.52f), F.Shell, 1);
                Ell(b, new Vector3(0, -0.04f, 0), new Vector3(0.38f, 0.08f, 0.48f), F.TurtleGreen, 0);
                Ell(b, new Vector3(0, 0.02f, 0.6f), new Vector3(0.13f, 0.11f, 0.16f), F.TurtleGreen, 0);
                foreach (float sx in new[] { -1f, 1f }) { Ell(b, new Vector3(sx * 0.45f, -0.02f, 0.25f), new Vector3(0.3f, 0.03f, 0.12f), F.TurtleGreen, 0); Ell(b, new Vector3(sx * 0.3f, -0.02f, -0.42f), new Vector3(0.14f, 0.03f, 0.09f), F.TurtleGreen, 0); }
                break;
            case "Peixe":
                Ell(b, Vector3.zero, new Vector3(0.08f, 0.12f, 0.24f), F.Orange, 0);
                Ell(b, new Vector3(0, 0, -0.26f), new Vector3(0.02f, 0.12f, 0.08f), F.Yellow, 0);
                break;
            default: throw new Exception("animal " + name);
        }
        return new Species { name = name, body = Reg(b.Build($"Fauna_{name}_Body")) };
    }

    static Species MakeButterfly()
    {
        var b = new MB();
        Rod(b, new Vector3(0, 0, 0.06f), new Vector3(0, 0, -0.07f), 0.012f, 0.008f, F.Black, 4);
        MB W(bool right)
        {
            var w = new MB(); float sx = right ? 1f : -1f;
            w.TriRaw(Vector3.zero, new Vector3(0.11f * sx, 0, 0.1f), new Vector3(0.13f * sx, 0, 0.01f), FU(F.Morpho, 5));
            w.TriRaw(Vector3.zero, new Vector3(0.13f * sx, 0, 0.01f), new Vector3(0.09f * sx, 0, -0.08f), FU(F.Morpho, 3));
            w.TriRaw(Vector3.zero, new Vector3(0.09f * sx, 0, -0.08f), new Vector3(0.02f * sx, 0, -0.06f), FU(F.Black, 3));
            return w;
        }
        return new Species
        {
            name = "Borboleta", body = Reg(b.Build("Fauna_Borboleta_Body")),
            wingL = Reg(W(false).Build("Fauna_Borboleta_WingL")), wingR = Reg(W(true).Build("Fauna_Borboleta_WingR")),
        };
    }

    static Species MakeBat()
    {
        var b = new MB();
        Ell(b, Vector3.zero, new Vector3(0.06f, 0.06f, 0.1f), F.DarkBrown, 0);
        Ell(b, new Vector3(0, 0.03f, 0.09f), Vector3.one * 0.045f, F.DarkBrown, 0);
        foreach (float sx in new[] { -1f, 1f }) ConeTo(b, new Vector3(sx * 0.02f, 0.06f, 0.09f), new Vector3(sx * 0.04f, 0.12f, 0.08f), 0.015f, F.DarkBrown);
        return new Species
        {
            name = "Morcego", body = Reg(b.Build("Fauna_Morcego_Body")),
            wingL = Reg(Wing(0.24f, 0.16f, F.DarkBrown, F.Black, false).Build("Fauna_Morcego_WingL")),
            wingR = Reg(Wing(0.24f, 0.16f, F.DarkBrown, F.Black, true).Build("Fauna_Morcego_WingR")),
            wingPivotL = new Vector3(-0.04f, 0.02f, 0f), wingPivotR = new Vector3(0.04f, 0.02f, 0f),
        };
    }

    static Species BirdOf(string kind)
    {
        switch (kind)
        {
            case "AraraAzul": return MakeBird(kind, new BirdSpec { L = 0.55f, body = F.Blue, belly = F.Yellow, headF = F.Blue, beak = F.Black, beakLen = 0.1f, tail = F.Blue, tailLen = 0.6f, wing = F.Blue, wingTip = F.Blue, wingLen = 0.6f });
            case "AraraVermelha": return MakeBird(kind, new BirdSpec { L = 0.55f, body = F.Red, belly = F.Red, headF = F.Red, beak = F.White, beakLen = 0.1f, tail = F.Red, tailLen = 0.6f, wing = F.Red, wingTip = F.Blue, wingLen = 0.6f });
            case "Tucano": return MakeBird(kind, new BirdSpec { L = 0.42f, body = F.Black, belly = F.White, headF = F.Black, beak = F.Beak, beakLen = 0.34f, tail = F.Black, tailLen = 0.22f, wing = F.Black, wingTip = F.Black, wingLen = 0.3f, legLen = 0.12f, leg = F.Gray, folded = true });
            case "Urubu": return MakeBird(kind, new BirdSpec { L = 0.6f, body = F.Black, belly = F.Black, headF = F.Gray, beak = F.Gray, beakLen = 0.07f, tail = F.Black, tailLen = 0.22f, wing = F.Black, wingTip = F.Black, wingLen = 0.95f });
            case "Garca": return MakeBird(kind, new BirdSpec { L = 0.5f, body = F.White, belly = F.White, headF = F.White, beak = F.Yellow, beakLen = 0.2f, tail = F.White, tailLen = 0.15f, wing = F.White, wingTip = F.White, wingLen = 0.75f, neckLen = 0.35f, legLen = 0.4f, leg = F.Black });
            case "Tuiuiu": return MakeBird(kind, new BirdSpec { L = 0.72f, body = F.White, belly = F.White, headF = F.Black, beak = F.Black, beakLen = 0.36f, tail = F.White, tailLen = 0.18f, wing = F.White, wingTip = F.Black, wingLen = 1.05f, neckLen = 0.42f, legLen = 0.5f, leg = F.Black });
            case "Atoba": return MakeBird(kind, new BirdSpec { L = 0.45f, body = F.White, belly = F.White, headF = F.DarkBrown, beak = F.Yellow, beakLen = 0.12f, tail = F.DarkBrown, tailLen = 0.18f, wing = F.DarkBrown, wingTip = F.DarkBrown, wingLen = 0.75f });
            case "Fragata": return MakeBird(kind, new BirdSpec { L = 0.5f, body = F.Black, belly = F.Red, headF = F.Black, beak = F.Gray, beakLen = 0.14f, tail = F.Black, tailLen = 0.4f, wing = F.Black, wingTip = F.Black, wingLen = 1.15f });
            case "Gaivota": return MakeBird(kind, new BirdSpec { L = 0.4f, body = F.White, belly = F.White, headF = F.White, beak = F.Yellow, beakLen = 0.09f, tail = F.White, tailLen = 0.15f, wing = F.Gray, wingTip = F.Black, wingLen = 0.62f });
            case "GralhaAzul": return MakeBird(kind, new BirdSpec { L = 0.36f, body = F.Blue, belly = F.Blue, headF = F.Black, beak = F.Black, beakLen = 0.07f, tail = F.Blue, tailLen = 0.26f, wing = F.Blue, wingTip = F.Black, wingLen = 0.38f });
            case "Harpia": return MakeBird(kind, new BirdSpec { L = 0.85f, body = F.Gray, belly = F.White, headF = F.Gray, beak = F.Black, beakLen = 0.12f, tail = F.Gray, tailLen = 0.4f, wing = F.DarkBrown, wingTip = F.Black, wingLen = 1.25f });
            case "Coruja": return MakeBird(kind, new BirdSpec { L = 0.3f, body = F.Brown, belly = F.Shell, headF = F.Brown, beak = F.Gray, beakLen = 0.05f, tail = F.Brown, tailLen = 0.08f, legLen = 0.1f, leg = F.Gray, folded = true });
        }
        throw new Exception("ave " + kind);
    }

    // ------------------------------------------------------------------ placement
    static Material faunaMat;
    static MeshCollider terrainCollider;
    static List<Rect> faunaRects;
    static readonly Dictionary<string, Species> speciesCache = new Dictionary<string, Species>();

    static Species Get(string kind)
    {
        if (speciesCache.TryGetValue(kind, out var s)) return s;
        switch (kind)
        {
            case "Capivara": case "LoboGuara": case "Tatu": case "Ema": case "Pinguim": case "Calango": case "Macaco": case "Preguica": s = MakeWalker(kind); break;
            case "Jacare": case "Boto": case "BotoRosa": case "Tartaruga": case "Peixe": s = MakeSwimmer(kind); break;
            case "Borboleta": s = MakeButterfly(); break;
            case "Morcego": s = MakeBat(); break;
            default: s = BirdOf(kind); break;
        }
        speciesCache[kind] = s;
        return s;
    }

    static float PlatDist(Vector2 p)
    {
        float best = float.MaxValue;
        foreach (var r in faunaRects)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax), dz = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz));
        }
        return best;
    }

    static bool GroundAt(float x, float z, out Vector3 p, out Vector3 n)
    {
        p = Vector3.zero; n = Vector3.up;
        if (terrainCollider == null) return false;
        if (!terrainCollider.Raycast(new Ray(new Vector3(x, 300f, z), Vector3.down), out var hit, 600f)) return false;
        p = hit.point; n = hit.normal;
        return true;
    }

    static FaunaMotion Spawn(Transform root, string kind, Vector3 pos, float scale, FaunaMotion.Mode mode, Action<SerializedObject> setup)
    {
        var sp = Get(kind);
        var go = new GameObject(kind);
        go.transform.SetParent(root, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0, Rf(0, 360), 0);
        go.transform.localScale = Vector3.one * scale;
        MeshObject("Body", go.transform, sp.body, faunaMat, Vector3.zero, Quaternion.identity, Vector3.one, false, true);
        Transform head = null, wl = null, wr = null;
        if (sp.head != null) head = MeshObject("Head", go.transform, sp.head, faunaMat, sp.headPivot, Quaternion.identity, Vector3.one, false, true).transform;
        if (sp.wingL != null)
        {
            wl = MeshObject("WingL", go.transform, sp.wingL, faunaMat, sp.wingPivotL, Quaternion.identity, Vector3.one, false, true).transform;
            wr = MeshObject("WingR", go.transform, sp.wingR, faunaMat, sp.wingPivotR, Quaternion.identity, Vector3.one, false, true).transform;
        }
        var fm = go.AddComponent<FaunaMotion>();
        var so = new SerializedObject(fm);
        so.FindProperty("mode").enumValueIndex = (int)mode;
        so.FindProperty("phase").floatValue = Rf(0f, 1f);
        so.FindProperty("head").objectReferenceValue = head;
        so.FindProperty("wingL").objectReferenceValue = wl;
        so.FindProperty("wingR").objectReferenceValue = wr;
        setup?.Invoke(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        return fm;
    }

    static void Flyer(Transform root, string kind, Vector3 center, float radius, float speed, float scale, bool glide, float flapFreq = 3f, float flapAngle = 35f)
    {
        Spawn(root, kind, center + Vector3.right * radius, scale, FaunaMotion.Mode.Circle, so =>
        {
            so.FindProperty("center").vector3Value = center;
            so.FindProperty("radius").floatValue = radius;
            so.FindProperty("speed").floatValue = speed;
            so.FindProperty("glide").boolValue = glide;
            so.FindProperty("flapFrequency").floatValue = flapFreq;
            so.FindProperty("flapAngle").floatValue = flapAngle;
        });
    }

    static void Walker(Transform root, string kind, Vector3 a, Vector3 b, float speed, float scale, float pause, float bobAmp, float bobFreq, float waddle = 0f)
    {
        Spawn(root, kind, a, scale, FaunaMotion.Mode.Wander, so =>
        {
            so.FindProperty("pointA").vector3Value = a;
            so.FindProperty("pointB").vector3Value = b;
            so.FindProperty("speed").floatValue = speed;
            so.FindProperty("pause").floatValue = pause;
            so.FindProperty("bob").floatValue = bobAmp;
            so.FindProperty("bobFrequency").floatValue = bobFreq;
            so.FindProperty("waddle").floatValue = waddle;
            so.FindProperty("flapAngle").floatValue = 20f;
            so.FindProperty("flapFrequency").floatValue = 2.5f;
        });
    }

    // ponto no chão a uma distância "visível" do percurso
    static bool FindGround(float minD, float maxD, float minHeight, out Vector3 p, out Vector3 n, Bounds area)
    {
        p = Vector3.zero; n = Vector3.up;
        for (int i = 0; i < 400; i++)
        {
            float x = Rf(area.min.x, area.max.x), z = Rf(area.min.z, area.max.z);
            float d = PlatDist(new Vector2(x, z));
            if (d < minD || d > maxD) continue;
            if (!GroundAt(x, z, out p, out n)) continue;
            if (n.y < 0.85f || p.y < minHeight) continue;
            return true;
        }
        return false;
    }

    static float waterMinDepth = 0.7f;
    static bool FindWater(float minD, float maxD, out Vector3 p, Bounds area)
    {
        p = Vector3.zero;
        for (int i = 0; i < 400; i++)
        {
            float x = Rf(area.min.x, area.max.x), z = Rf(area.min.z, area.max.z);
            float d = PlatDist(new Vector2(x, z));
            if (d < minD || d > maxD) continue;
            if (!GroundAt(x, z, out var g, out _)) continue;
            if (g.y > T.waterY - waterMinDepth) continue;
            p = new Vector3(x, T.waterY, z);
            return true;
        }
        return false;
    }

    static bool SegmentFrom(Vector3 a, float minLen, float maxLen, bool water, out Vector3 b)
    {
        b = a;
        for (int i = 0; i < 30; i++)
        {
            float ang = Rf(0, 6.28f), len = Rf(minLen, maxLen);
            var q = a + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * len;
            if (!GroundAt(q.x, q.z, out var g, out var n)) continue;
            if (water) { if (g.y > T.waterY - waterMinDepth) continue; b = new Vector3(q.x, a.y, q.z); return true; }
            if (n.y < 0.85f || Mathf.Abs(g.y - a.y) > 1.2f || (T.water != WaterMode.None && g.y < T.waterY + 0.2f)) continue;
            b = g; return true;
        }
        return false;
    }

    // ================================================================== FAUNA
    public static string AddFauna(int level)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Saia do Play Mode.");
        T = ThemeFor(level);
        string scenePath = $"Assets/Scenes/Level_{level:00}.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var keepAssets = new List<Mesh>(meshAssets);
        meshAssets.Clear();
        string result = BuildFauna(scene, level);
        string dir = $"{ArtRoot}/{T.key}";
        var faunaPath = $"{dir}/Level{level:00}_Fauna.asset";
        AssetDatabase.DeleteAsset(faunaPath);
        if (meshAssets.Count > 0)
        {
            AssetDatabase.CreateAsset(meshAssets[0], faunaPath);
            foreach (var m in meshAssets.Skip(1)) AssetDatabase.AddObjectToAsset(m, faunaPath);
        }
        AssetDatabase.SaveAssets();
        meshAssets.Clear(); meshAssets.AddRange(keepAssets);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return result;
    }

    static string BuildFauna(UnityEngine.SceneManagement.Scene scene, int level)
    {
        var keepRng = rng;
        rng = new Random(9000 + level * 131);   // RNG próprio: não altera o resto do cenário
        speciesCache.Clear();
        waterMinDepth = 0.7f;
        foreach (var go in scene.GetRootGameObjects().Where(g => g.name == "Fauna").ToArray()) Object.DestroyImmediate(go);

        // material e paleta da fauna (compartilhados)
        Directory.CreateDirectory(SharedDir);
        var pal = SavePng($"{SharedDir}/T_FaunaPalette.png", Shades, Roles,   // regrava sempre: a paleta pode mudar
             (x, y) => ShadeColor(Hex(FaunaHex[y]), x),
            imp => { imp.filterMode = FilterMode.Point; imp.mipmapEnabled = false; imp.wrapMode = TextureWrapMode.Clamp; imp.sRGBTexture = true; });
        faunaMat = MakeMat($"{SharedDir}/M_Fauna.mat", Shader.Find("Universal Render Pipeline/Lit"), m =>
        {
            m.SetTexture("_BaseMap", pal); m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", 0.25f); m.SetFloat("_Cull", 0f);
        });

        var plats = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
            .Where(t => t.name.StartsWith("Platform_") && t.GetComponent<MeshRenderer>() != null && !t.name.Contains("Trigger")).ToList();
        faunaRects = plats.Select(t => new Rect(t.position.x - t.lossyScale.x * 0.5f, t.position.z - t.lossyScale.z * 0.5f, t.lossyScale.x, t.lossyScale.z)).ToList();
        var area = new Bounds(plats[0].position, Vector3.zero);
        foreach (var t in plats) area.Encapsulate(t.position);
        area.Expand(new Vector3(70f, 0f, 70f));
        var start = plats.First(t => t.name == "Platform_Start");
        var finish = plats.First(t => t.name == "Platform_Finish");
        float avgTop = plats.Average(t => t.position.y + t.lossyScale.y * 0.5f);
        // o primeiro animal de cada grupo fica perto do início, à vista da câmera inicial
        var nearest = plats.Where(t => t != start && t.GetComponent<Collider>() != null).OrderBy(t => (t.position - start.position).sqrMagnitude).First();
        var startFwd = nearest.position - start.position; startFwd.y = 0; startFwd.Normalize();
        var nearStart = new Bounds(start.position + startFwd * 22f, new Vector3(44f, 1f, 44f));

        // colisor temporário no terreno para achar o chão (removido no fim)
        var terrainGo = GameObject.Find($"{EnvName}/Terrain");
        terrainCollider = terrainGo != null ? terrainGo.AddComponent<MeshCollider>() : null;

        var root = new GameObject("Fauna").transform;
        int count = 0;

        // helpers de posição ao longo do percurso
        Vector3 AlongPath(float frac, float lateral, float up)
        {
            var ordered = plats.Where(t => t.GetComponent<Collider>() != null).OrderBy(t => (t.position - start.position).sqrMagnitude).ToList();
            var a = ordered[Mathf.Clamp(Mathf.FloorToInt(frac * (ordered.Count - 1)), 0, ordered.Count - 1)];
            var dir = (finish.position - start.position); dir.y = 0; dir.Normalize();
            var side = new Vector3(-dir.z, 0, dir.x);
            return a.position + side * lateral + Vector3.up * up;
        }
        Vector3 GatePerch(Transform plat, float x)
        {
            var gate = plat.Find("Visual/FinishGate");
            return gate != null ? gate.TransformPoint(new Vector3(x, 3.98f, 0f)) : plat.position + Vector3.up * 1.2f;
        }
        float GateScale(Transform plat) { var gate = plat.Find("Visual/FinishGate"); return gate != null ? gate.lossyScale.x : 1f; }
        Vector3 TreeTop(Transform plat)
        {
            var trees = plat.Find("Visual/Trees");
            if (trees == null || trees.childCount == 0) return plat.position + Vector3.up * 1.2f;
            var r = trees.GetChild(0).GetComponent<Renderer>();
            return new Vector3(r.bounds.center.x, r.bounds.max.y - 0.1f, r.bounds.center.z);
        }
        void Perch(string kind, Vector3 pos, float scale)
        {
            var fm = Spawn(root, kind, pos, scale, FaunaMotion.Mode.Perch, so => { so.FindProperty("speed").floatValue = 0f; so.FindProperty("flapAngle").floatValue = 0f; });
            fm.transform.rotation = Quaternion.Euler(0, Rf(0, 360), 0);
            count++;
        }
        void Birds(string kind, int n, float scale, bool glide, float minUp, float maxUp, float minR, float maxR, float flap = 3f)
        {
            for (int i = 0; i < n; i++)
            {
                var c = i == 0 ? start.position + startFwd * Rf(10f, 18f) + new Vector3(-startFwd.z, 0, startFwd.x) * Rf(-8f, 8f) + Vector3.up * Rf(minUp, maxUp)
                               : AlongPath(Rf(0.1f, 0.9f), Rf(-14f, 14f), Rf(minUp, maxUp));
                Flyer(root, kind, c, Rf(minR, maxR), Rf(3.5f, 5.5f) * (glide ? 0.8f : 1f), scale, glide, flap);
                count++;
            }
        }
        void Grounded(string kind, int n, float scale, float speed, float pause, float bobAmp, float bobFreq, float waddle = 0f, float minD = 12f, float maxD = 34f)
        {
            for (int i = 0; i < n; i++)
            {
                float minH = T.water != WaterMode.None ? T.waterY + 0.3f : -100f;
                if (!(i == 0 && FindGround(Mathf.Min(minD, 8f), maxD, minH, out var a, out _, nearStart)) && !FindGround(minD, maxD, minH, out a, out _, area)) continue;
                if (!SegmentFrom(a, 3f, 7f, false, out var b)) continue;
                Walker(root, kind, a, b, speed, scale, pause, bobAmp, bobFreq, waddle);
                count++;
            }
        }
        void Swimmers(string kind, int n, float scale, float speed, float depth, float minD = 6f, float maxD = 30f)
        {
            for (int i = 0; i < n; i++)
            {
                if (!(i == 0 && FindWater(minD, maxD, out var a, nearStart)) && !FindWater(minD, maxD, out a, area)) continue;
                a.y = T.waterY - depth;
                if (!SegmentFrom(a, 4f, 9f, true, out var b)) continue;
                b.y = a.y;
                Walker(root, kind, a, b, speed, scale, Rf(1f, 3f), 0.02f, 1.2f);
                count++;
            }
        }
        void Leapers(string kind, int n, float scale, float height, float duration, float interval, float minD = 8f, float maxD = 40f)
        {
            for (int i = 0; i < n; i++)
            {
                if (!(i == 0 && FindWater(minD, maxD, out var a, nearStart)) && !FindWater(minD, maxD, out a, area)) continue;
                if (!SegmentFrom(a, height * 1.5f, height * 2.5f, true, out var b)) continue;
                a.y = b.y = T.waterY - 0.2f;
                Spawn(root, kind, a, scale, FaunaMotion.Mode.Leap, so =>
                {
                    so.FindProperty("pointA").vector3Value = a; so.FindProperty("pointB").vector3Value = b;
                    so.FindProperty("leapHeight").floatValue = height; so.FindProperty("leapDuration").floatValue = duration;
                    so.FindProperty("leapInterval").floatValue = interval + Rf(-1f, 1.5f);
                });
                count++;
            }
        }
        void Flutterers(string kind, int n, Vector3 c, float radius, float speed, float scale, float flapFreq, float flapAngle)
        {
            for (int i = 0; i < n; i++)
            {
                Spawn(root, kind, c, scale, FaunaMotion.Mode.Flutter, so =>
                {
                    so.FindProperty("center").vector3Value = c + new Vector3(Rf(-1, 1), Rf(-0.5f, 0.5f), Rf(-1, 1)) * radius * 0.3f;
                    so.FindProperty("radius").floatValue = radius; so.FindProperty("speed").floatValue = speed;
                    so.FindProperty("flapFrequency").floatValue = flapFreq; so.FindProperty("flapAngle").floatValue = flapAngle;
                });
                count++;
            }
        }

        switch (T.key)
        {
            case "Amazonia":
                Birds("AraraVermelha", 4, 1.5f, false, 5f, 12f, 7f, 12f, 2.6f);
                Birds("Harpia", 1, 1.5f, true, 14f, 20f, 12f, 18f, 1.4f);
                Perch("Tucano", GatePerch(finish, 0.9f), 1.6f * GateScale(finish));
                Perch("Macaco", TreeTop(start), 1.5f);
                Perch("Preguica", GatePerch(finish, -0.55f) + Vector3.down * 0.35f * GateScale(finish), 1.3f * GateScale(finish));
                Leapers("BotoRosa", 3, 1f, 2f, 1.3f, 6f, 6f, 40f);
                Flutterers("Borboleta", 3, start.position + Vector3.up * 2f, 4f, 2.5f, 1.6f, 7f, 55f);
                Flutterers("Borboleta", 2, AlongPath(0.6f, 0f, 2f), 3f, 2.5f, 1.6f, 7f, 55f);
                break;
            case "Caatinga":
                Birds("Urubu", 3, 1.6f, true, 14f, 22f, 10f, 16f, 1.6f);
                Grounded("Calango", 4, 1.6f, 2.2f, 2.5f, 0.01f, 10f, 0f, 11f, 26f);
                Grounded("Tatu", 2, 1.3f, 0.7f, 2f, 0.02f, 8f, 4f, 12f, 30f);
                break;
            case "Cerrado":
                Grounded("LoboGuara", 2, 1.3f, 1.4f, 3f, 0.04f, 7f, 0f, 12f, 30f);
                Grounded("Ema", 3, 1.2f, 1.1f, 2.5f, 0.05f, 5f, 0f, 12f, 32f);
                Birds("AraraVermelha", 2, 1.5f, false, 6f, 12f, 8f, 12f, 2.6f);
                Perch("Tucano", GatePerch(finish, 0.9f), 1.6f * GateScale(finish));
                break;
            case "Pantanal":
                waterMinDepth = 0.25f;
                Swimmers("Jacare", 4, 1f, 0.4f, 0.1f, 4f, 26f);
                waterMinDepth = 0.7f;
                Grounded("Capivara", 5, 1f, 0.6f, 4f, 0.03f, 4f, 2f, 6f, 30f);
                Birds("Tuiuiu", 2, 1.4f, true, 7f, 14f, 9f, 14f, 1.8f);
                Birds("Garca", 3, 1.3f, false, 4f, 10f, 7f, 12f, 2f);
                break;
            case "Noronha":
                // golfinhos-rotadores, tartarugas-marinhas, atobás e fragatas
                Leapers("Boto", 5, 0.9f, 2.6f, 1.2f, 5f, 8f, 45f);
                Swimmers("Tartaruga", 3, 1f, 0.35f, 0.08f, 6f, 30f);
                Birds("Atoba", 3, 1.5f, true, 5f, 12f, 8f, 14f, 2.4f);
                Birds("Fragata", 2, 1.6f, true, 14f, 22f, 12f, 18f, 1.2f);
                break;
            case "Lencois":
                Birds("Garca", 3, 1.3f, false, 5f, 11f, 8f, 13f, 2f);
                Leapers("Peixe", 4, 1.5f, 1f, 0.8f, 4f, 6f, 35f);
                break;
            case "LagoAzul":
                Flutterers("Morcego", 6, AlongPath(0.3f, 3f, 7f), 6f, 9f, 2.2f, 9f, 50f);
                Flutterers("Morcego", 5, AlongPath(0.75f, -3f, 7f), 6f, 9f, 2.2f, 9f, 50f);
                waterMinDepth = 0.4f;
                Swimmers("Peixe", 6, 1.2f, 0.6f, 0.4f, 4f, 30f);
                waterMinDepth = 0.7f;
                break;
            case "Neve":
                for (int i = 0; i < 3; i++)
                {
                    if (!FindWater(4f, 22f, out var a, area)) continue;      // anda sobre o rio congelado
                    a.y = T.waterY;
                    if (!SegmentFrom(a, 3f, 6f, true, out var b)) continue;
                    b.y = a.y;
                    Walker(root, "Pinguim", a, b, 0.45f, 1.4f, Rf(2f, 4f), 0.03f, 6f, 9f);
                    count++;
                }
                Birds("GralhaAzul", 3, 1.6f, false, 5f, 11f, 6f, 10f, 3.2f);
                break;
            case "Vulcao":
                Birds("Urubu", 4, 1.6f, true, 14f, 24f, 10f, 18f, 1.6f);
                break;
            case "Ceu":
                Birds("Harpia", 2, 1.5f, true, 6f, 14f, 12f, 18f, 1.4f);
                Perch("Coruja", GatePerch(finish, 0.9f), 1.8f * GateScale(finish));
                Perch("Coruja", TreeTop(start), 1.8f);
                break;
        }

        if (terrainCollider != null) Object.DestroyImmediate(terrainCollider);
        terrainCollider = null;
        rng = keepRng;
        return $"Level_{level:00} ({T.title}) fauna: {count} animais";
    }
}
