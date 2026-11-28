using UnityEngine;

// Movimento simples dos animais do cenário (só visual, sem colisão).
// Circle: voa em círculos; Wander: anda/nada entre dois pontos com pausas;
// Leap: salta para fora da água de tempos em tempos; Flutter: voo irregular (borboletas, morcegos);
// Perch: parado, olhando em volta.
public class FaunaMotion : MonoBehaviour
{
    public enum Mode { Circle, Wander, Leap, Flutter, Perch }

    [SerializeField] private Mode mode = Mode.Circle;
    [SerializeField] private Vector3 center;
    [SerializeField] private float radius = 8f;
    [SerializeField] private float speed = 4f;
    [Range(0f, 1f)] [SerializeField] private float phase;

    [Header("Wander / Leap")]
    [SerializeField] private Vector3 pointA;
    [SerializeField] private Vector3 pointB;
    [SerializeField] private float pause = 2f;
    [SerializeField] private float leapHeight = 2f;
    [SerializeField] private float leapDuration = 1.4f;
    [SerializeField] private float leapInterval = 6f;

    [Header("Corpo")]
    [SerializeField] private float bob = 0.04f;
    [SerializeField] private float bobFrequency = 6f;
    [SerializeField] private float waddle;
    [SerializeField] private Transform head;

    [Header("Asas")]
    [SerializeField] private Transform wingL;
    [SerializeField] private Transform wingR;
    [SerializeField] private float flapAngle = 35f;
    [SerializeField] private float flapFrequency = 3f;
    [SerializeField] private bool glide;

    private float offset;
    private Vector3 lastPos;

    private void Start()
    {
        offset = phase * 37f;
        lastPos = transform.position;
    }

    private void Update()
    {
        float t = Time.time + offset;
        switch (mode)
        {
            case Mode.Circle: Circle(t); break;
            case Mode.Wander: Wander(t); break;
            case Mode.Leap: Leap(t); break;
            case Mode.Flutter: Flutter(t); break;
            case Mode.Perch: Perch(t); break;
        }
        Flap(t);
        lastPos = transform.position;
    }

    private void Circle(float t)
    {
        float ang = t * speed / Mathf.Max(0.5f, radius) + phase * Mathf.PI * 2f;
        var pos = center + new Vector3(Mathf.Cos(ang) * radius, Mathf.Sin(t * 0.5f) * 0.8f, Mathf.Sin(ang) * radius);
        var tangent = new Vector3(-Mathf.Sin(ang), 0f, Mathf.Cos(ang));
        transform.position = pos;
        transform.rotation = Quaternion.LookRotation(tangent) * Quaternion.Euler(0f, 0f, 18f);
    }

    private void Wander(float t)
    {
        float dist = Vector3.Distance(pointA, pointB);
        float travel = dist / Mathf.Max(0.05f, speed);
        float cycle = 2f * (travel + pause);
        float u = Mathf.Repeat(t, cycle);
        Vector3 from, to; float k; bool moving;
        if (u < pause) { from = pointA; to = pointB; k = 0f; moving = false; }
        else if (u < pause + travel) { from = pointA; to = pointB; k = (u - pause) / travel; moving = true; }
        else if (u < 2f * pause + travel) { from = pointB; to = pointA; k = 0f; moving = false; }
        else { from = pointB; to = pointA; k = (u - 2f * pause - travel) / travel; moving = true; }

        float s = k * k * (3f - 2f * k);
        var pos = Vector3.Lerp(from, to, s);
        if (moving) pos.y += Mathf.Abs(Mathf.Sin(t * bobFrequency)) * bob;
        transform.position = pos;
        var dir = to - from; dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            var look = Quaternion.LookRotation(dir.normalized);
            float roll = moving ? Mathf.Sin(t * bobFrequency) * waddle : 0f;
            transform.rotation = Quaternion.Slerp(transform.rotation, look * Quaternion.Euler(0f, 0f, roll), Time.deltaTime * 5f);
        }
        if (head != null)
            head.localRotation = moving ? Quaternion.identity : Quaternion.Euler(18f + Mathf.Sin(t * 1.7f) * 14f, Mathf.Sin(t * 0.6f) * 25f, 0f);
    }

    private void Leap(float t)
    {
        float cycle = Mathf.Max(leapDuration + 0.5f, leapInterval);
        int n = Mathf.FloorToInt(t / cycle);
        float u = (t - n * cycle) / leapDuration;
        bool reverse = n % 2 == 1;
        var a = reverse ? pointB : pointA; var b = reverse ? pointA : pointB;
        if (u <= 1f)
        {
            var pos = Vector3.Lerp(a, b, u) + Vector3.up * (4f * leapHeight * u * (1f - u));
            float vy = 4f * leapHeight * (1f - 2f * u) / leapDuration;
            var flat = (b - a) / leapDuration; flat.y = 0f;
            transform.position = pos;
            if (flat.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(flat.normalized * flat.magnitude + Vector3.up * vy);
        }
        else
        {
            transform.position = a + Vector3.down * 3f; // escondido embaixo d'água
        }
    }

    private void Flutter(float t)
    {
        float s = t * speed * 0.15f;
        var off = new Vector3(Mathf.PerlinNoise(s, 1.3f + phase * 9f) - 0.5f, (Mathf.PerlinNoise(2.7f + phase * 5f, s) - 0.5f) * 0.6f, Mathf.PerlinNoise(s + 7.1f, 4.4f) - 0.5f) * 2f * radius;
        var pos = center + off;
        transform.position = pos;
        var vel = pos - lastPos;
        if (vel.sqrMagnitude > 0.000001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(vel.normalized), Time.deltaTime * 8f);
    }

    private void Perch(float t)
    {
        if (head != null)
        {
            float step = Mathf.Round(Mathf.Sin(t * 0.35f) * 2f) / 2f;
            head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(0f, step * 60f, 0f), Time.deltaTime * 4f);
        }
    }

    private void Flap(float t)
    {
        if (wingL == null || wingR == null)
            return;

        float a;
        if (glide)
            a = Mathf.Repeat(t, 7f) < 1.2f ? Mathf.Sin(t * flapFrequency * Mathf.PI * 2f) * flapAngle : 4f + Mathf.Sin(t * 0.8f) * 3f;
        else
            a = Mathf.Sin(t * flapFrequency * Mathf.PI * 2f) * flapAngle;

        wingL.localRotation = Quaternion.Euler(0f, 0f, a);
        wingR.localRotation = Quaternion.Euler(0f, 0f, -a);
    }
}
