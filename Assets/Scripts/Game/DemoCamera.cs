using UnityEngine;

// Câmera do menu, como um trailer: troca de plano a cada poucos segundos (corte seco),
// variando ângulo, distância, altura, lente e assunto. A bola não precisa estar sempre no centro;
// às vezes a câmera mostra um animal ou a paisagem. O primeiro plano de cada fase é um sobrevoo.
public class DemoCamera : MonoBehaviour
{
    private enum Shot { Orbit, Track, LowFront, Flyover, Dolly, HighFollow, Animal, Pan }

    private Transform ball;
    private Rigidbody ballBody;
    private Camera cam;
    private int obstacleMask;

    private Shot shot;
    private Shot lastShot = (Shot)(-1);
    private float shotStart, shotEnd;
    private Vector3 dir = Vector3.forward;          // direção em que a bola anda (suavizada)
    private Vector3 p0, p1;                          // posições de início/fim (sobrevoo, aproximação, câmera parada)
    private float angle, angleSpeed, radius, height, side, lead, roll, fovFrom, fovTo, yaw0;
    private float compose;                           // terço da tela onde fica o assunto (±), longe dos botões do menu
    private Transform animal;
    private Vector3 animalOffset;
    private Vector3 velocity;

    public void Init(Transform target, float startYaw)
    {
        ball = target;
        ballBody = target.GetComponent<Rigidbody>();
        cam = GetComponent<Camera>();
        obstacleMask = LayerMask.GetMask("Default", "CameraBlock");
        dir = Quaternion.Euler(0f, startYaw, 0f) * Vector3.forward;
        StartShot(Shot.Flyover);
    }

    // Recomeça com um sobrevoo (a bola foi colocada em outro ponto da fase).
    public void Restart()
    {
        if (ball != null) StartShot(Shot.Flyover);
    }

    private float Progress => Mathf.Clamp01((Time.time - shotStart) / Mathf.Max(0.01f, shotEnd - shotStart));
    private Vector3 Side => Vector3.Cross(Vector3.up, dir).normalized;
    private Vector3 BallCenter => ball.position + Vector3.up * 0.5f;

    private void NextShot()
    {
        // sorteio ponderado, sem repetir o plano anterior
        Shot[] pool = { Shot.Orbit, Shot.Orbit, Shot.Track, Shot.Track, Shot.LowFront, Shot.Flyover, Shot.Dolly, Shot.HighFollow, Shot.Animal, Shot.Animal, Shot.Pan };
        Shot s;
        int guard = 0;
        do s = pool[Random.Range(0, pool.Length)]; while (s == lastShot && guard++ < 10);
        StartShot(s);
    }

    private void StartShot(Shot s)
    {
        shot = s;
        lastShot = s;
        shotStart = Time.time;
        shotEnd = Time.time + Random.Range(3.5f, 5.5f);
        roll = 0f;
        velocity = Vector3.zero;
        compose = (Random.value < 0.5f ? -1f : 1f) * Random.Range(0.18f, 0.26f);
        var b = BallCenter;
        switch (s)
        {
            case Shot.Orbit:
                angle = Random.Range(0f, 360f);
                angleSpeed = Random.Range(6f, 14f) * (Random.value < 0.5f ? -1f : 1f);
                radius = Random.Range(7f, 16f);
                height = Random.Range(1.5f, 8f);
                lead = Random.Range(-3f, 3f);
                fovFrom = fovTo = Random.Range(45f, 62f);
                break;
            case Shot.Track:
                side = Random.value < 0.5f ? -1f : 1f;
                radius = Random.Range(5f, 9f);
                height = Random.Range(0.8f, 2.5f);
                lead = Random.Range(-2f, 4f);
                roll = Random.Range(-4f, 4f);
                fovFrom = fovTo = Random.Range(50f, 60f);
                break;
            case Shot.LowFront:
                p0 = b + dir * Random.Range(9f, 14f) + Side * Random.Range(-3f, 3f) + Vector3.up * Random.Range(0.3f, 1.2f);
                roll = Random.Range(-5f, 5f);
                fovFrom = 55f; fovTo = 48f;
                break;
            case Shot.Flyover:
                var a = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                p0 = b + a * Random.Range(30f, 40f) + Vector3.up * Random.Range(18f, 26f);
                p1 = b + Quaternion.Euler(0f, Random.Range(60f, 120f) * (Random.value < 0.5f ? -1f : 1f), 0f) * a * Random.Range(18f, 26f) + Vector3.up * Random.Range(9f, 14f);
                fovFrom = fovTo = 55f;
                break;
            case Shot.Dolly:
                p0 = b - dir * Random.Range(18f, 26f) + Side * Random.Range(-6f, 6f) + Vector3.up * Random.Range(3f, 7f);
                fovFrom = 52f; fovTo = 36f;
                break;
            case Shot.HighFollow:
                height = Random.Range(14f, 20f);
                radius = Random.Range(5f, 9f);
                fovFrom = fovTo = 50f;
                break;
            case Shot.Animal:
                animal = PickAnimal();
                if (animal == null) { StartShot(Shot.Orbit); return; }
                float scale = Mathf.Max(0.6f, animal.lossyScale.x);
                animalOffset = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(2.5f, 4.5f) * scale + Vector3.up * Random.Range(0.4f, 1.5f) * scale;
                fovFrom = 42f; fovTo = 36f;
                break;
            case Shot.Pan:
                p0 = b - dir * Random.Range(2f, 5f) + Vector3.up * Random.Range(2.5f, 5f);
                yaw0 = Quaternion.LookRotation(dir).eulerAngles.y + Random.Range(-120f, 120f);
                angleSpeed = Random.Range(6f, 10f) * (Random.value < 0.5f ? -1f : 1f);
                fovFrom = fovTo = 62f;
                break;
        }
        if (s == Shot.LowFront || s == Shot.Flyover || s == Shot.Dolly)
        {
            p0 = Unblock(b, p0);
            if (s == Shot.Flyover) p1 = Unblock(b, p1);
        }
        if (cam != null) cam.fieldOfView = fovFrom;
        // corte seco: já começa na posição do novo plano
        transform.position = DesiredPosition(true);
        transform.rotation = DesiredRotation();
    }

    // um animal perto da bola (ou qualquer um, se não houver perto)
    private Transform PickAnimal()
    {
        var all = FindObjectsByType<FaunaMotion>();
        if (all.Length == 0) return null;
        Transform best = null;
        float bestScore = float.MaxValue;
        foreach (var f in all)
        {
            float d = Vector3.Distance(f.transform.position, ball.position) + Random.Range(0f, 25f);
            if (d < bestScore) { bestScore = d; best = f.transform; }
        }
        return best;
    }

    private Vector3 DesiredPosition(bool snap)
    {
        var b = BallCenter;
        float t = Progress;
        Vector3 p;
        switch (shot)
        {
            case Shot.Orbit:
                p = b + Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0f, -radius) + Vector3.up * height;
                break;
            case Shot.Track:
                p = b + Side * side * radius + Vector3.up * height + dir * lead;
                break;
            // trajetórias fixas: o desvio de obstáculos é feito uma vez, no início do plano
            case Shot.LowFront:
                return p0;
            case Shot.Flyover:
                return Vector3.Lerp(p0, p1, Mathf.SmoothStep(0f, 1f, t));
            case Shot.Dolly:
                return Vector3.Lerp(p0, b + (p0 - b) * 0.45f, t);
            case Shot.HighFollow:
                p = b + Vector3.up * height - dir * radius;
                break;
            case Shot.Animal:
                return animal != null ? animal.position + animalOffset : transform.position;
            default: // Pan
                return p0;
        }
        return Unblock(b, p);
    }

    // se tiver algo entre a bola e a câmera (morro, encosta, prédio): primeiro tenta subir a câmera,
    // e só se não der aproxima da bola (a fase pode ficar num vale)
    private Vector3 Unblock(Vector3 from, Vector3 to)
    {
        for (int k = 0; k < 5; k++)
        {
            var p = to + Vector3.up * k * 7f;
            if (!Blocked(from, p, out _)) return p;
        }
        return Blocked(from, to, out float dist) ? from + (to - from).normalized * Mathf.Max(1.5f, dist - 0.3f) : to;
    }

    private bool Blocked(Vector3 from, Vector3 to, out float distance)
    {
        distance = 0f;
        var d = to - from;
        float len = d.magnitude;
        if (len < 0.5f) return false;
        if (Physics.SphereCast(from, 0.3f, d / len, out var hit, len, obstacleMask, QueryTriggerInteraction.Ignore)
            && !hit.collider.transform.IsChildOf(ball))
        {
            distance = hit.distance;
            return true;
        }
        return false;
    }

    private Quaternion DesiredRotation()
    {
        Vector3 look;
        switch (shot)
        {
            case Shot.Animal:
                look = animal != null ? animal.position + Vector3.up * 0.3f * animal.lossyScale.x : BallCenter;
                break;
            case Shot.Pan:
                var yaw = yaw0 + angleSpeed * (Time.time - shotStart);
                return Quaternion.Euler(4f, yaw, 0f);
            case Shot.Track:
            case Shot.HighFollow:
                look = BallCenter + dir * 3f;
                break;
            case Shot.Orbit:
                look = BallCenter + Side * lead;
                break;
            default:
                look = BallCenter;
                break;
        }
        var fwd = look - transform.position;
        if (fwd.sqrMagnitude < 0.01f) fwd = dir;
        // regra dos terços: desvia o olhar para o assunto cair à esquerda ou à direita dos botões
        var right = Vector3.Cross(Vector3.up, fwd.normalized);
        fwd += right * fwd.magnitude * compose;
        return Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(0f, 0f, roll);
    }

    private void LateUpdate()
    {
        if (ball == null) return;

        // direção de movimento da bola, suavizada (para planos "na frente" ou "de lado")
        if (ballBody != null)
        {
            var v = ballBody.linearVelocity; v.y = 0f;
            if (v.sqrMagnitude > 1f) dir = Vector3.Slerp(dir, v.normalized, Time.deltaTime * 1.5f).normalized;
        }

        if (Time.time >= shotEnd) { NextShot(); return; }
        if (shot == Shot.Orbit) angle += angleSpeed * Time.deltaTime;

        var target = DesiredPosition(false);
        bool follow = shot == Shot.Orbit || shot == Shot.Track || shot == Shot.HighFollow || shot == Shot.Animal;
        transform.position = follow ? Vector3.SmoothDamp(transform.position, target, ref velocity, 0.35f) : target;
        transform.rotation = Quaternion.Slerp(transform.rotation, DesiredRotation(), Time.deltaTime * (shot == Shot.Pan ? 20f : 5f));
        if (cam != null) cam.fieldOfView = Mathf.Lerp(fovFrom, fovTo, Progress);
    }
}
