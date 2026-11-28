using System.Collections.Generic;
using UnityEngine;

// Robozinho do menu: joga a fase sozinho, como um iniciante. Monta um caminho ligando as
// plataformas (sempre a mais próxima ainda não visitada), anda até a próxima e pula na beirada
// ou quando ela é mais alta. Hesita, mira meio torto e às vezes erra o pulo (e cai).
[RequireComponent(typeof(PlayerController))]
public class DemoPilot : MonoBehaviour
{
    private readonly List<Collider> route = new();
    private PlayerController controller;
    private CheckpointSystem checkpoint;
    private int target = 1;
    private Vector3 aimOffset;
    private float hesitateUntil;
    private float lastJump = -10f;
    private bool jumpQueued;

    public Vector3 Move { get; private set; }

    public bool ConsumeJump()
    {
        bool j = jumpQueued;
        jumpQueued = false;
        return j;
    }

    private void Start()
    {
        controller = GetComponent<PlayerController>();
        controller.Pilot = this;
        checkpoint = GetComponent<CheckpointSystem>();
        if (checkpoint != null) checkpoint.Respawned += OnRespawned;
        BuildRoute();
        hesitateUntil = Time.time + 0.8f;
        StartCoroutine(PlaceSomewhere());
    }

    // Começa num ponto qualquer do percurso (não sempre na largada). Espera o primeiro passo de
    // física para o CheckpointSystem já ter guardado a largada, e então troca o checkpoint.
    private System.Collections.IEnumerator PlaceSomewhere()
    {
        yield return new WaitForFixedUpdate();
        if (route.Count < 3) yield break;
        int k = Random.Range(0, route.Count - 2);
        var c = route[k];
        if (c == null || !c.enabled) yield break;
        var b = c.bounds;
        var pos = new Vector3(b.center.x, b.max.y + 0.6f, b.center.z);
        var rb = GetComponent<Rigidbody>();
        if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; rb.position = pos; }
        transform.position = pos;
        if (checkpoint != null) checkpoint.SetCheckpoint(pos);
        foreach (var trail in GetComponentsInChildren<TrailRenderer>()) trail.Clear();
        target = Mathf.Min(k + 1, route.Count - 1);
        var cam = Camera.main != null ? Camera.main.GetComponent<DemoCamera>() : null;
        if (cam != null) cam.Restart();
    }

    private void OnDestroy()
    {
        if (checkpoint != null) checkpoint.Respawned -= OnRespawned;
        if (controller != null && controller.Pilot == this) controller.Pilot = null;
    }

    // Caminho guloso: da largada, sempre a plataforma mais próxima ainda não visitada
    // (subir custa mais que descer, porque o pulo tem limite).
    private void BuildRoute()
    {
        var all = new List<Collider>();
        foreach (var c in FindObjectsByType<Collider>())
            if (!c.isTrigger && c.name.StartsWith("Platform_") && c.gameObject.layer == 0 && !IsHiddenPath(c))
                all.Add(c);
        Collider current = null;
        float best = float.MaxValue;
        foreach (var c in all)
        {
            float d = (c.bounds.center - transform.position).sqrMagnitude;
            if (d < best) { best = d; current = c; }
        }
        if (current == null) return;
        route.Add(current);
        all.Remove(current);
        while (all.Count > 0 && route.Count < 60)
        {
            Collider next = null;
            best = float.MaxValue;
            foreach (var c in all)
            {
                float cost = EdgeDistance(current.bounds, c.bounds) + Mathf.Max(0f, c.bounds.max.y - current.bounds.max.y) * 2.5f;
                if (cost < best) { best = cost; next = c; }
            }
            if (next == null || best > 30f) break;
            route.Add(next);
            all.Remove(next);
            current = next;
        }
        target = Mathf.Min(1, route.Count - 1);
        NewAim();
    }

    // Caminho escondido (fase 5: a linha preta): o robô nunca vai por ele, para não entregar o segredo.
    private static bool IsHiddenPath(Collider c)
    {
        var r = c.GetComponent<MeshRenderer>();
        return r != null && r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("MAT_Platform_Black");
    }

    private static float EdgeDistance(Bounds a, Bounds b)
    {
        float dx = Mathf.Max(0f, Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x));
        float dz = Mathf.Max(0f, Mathf.Max(a.min.z - b.max.z, b.min.z - a.max.z));
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private void NewAim() => aimOffset = new Vector3(Random.Range(-0.35f, 0.35f), 0f, Random.Range(-0.35f, 0.35f));

    private void OnRespawned()
    {
        // recomeça da plataforma mais perto de onde renasceu
        int nearest = 0;
        float best = float.MaxValue;
        for (int i = 0; i < route.Count; i++)
        {
            if (route[i] == null) continue;
            float d = (route[i].bounds.center - transform.position).sqrMagnitude;
            if (d < best) { best = d; nearest = i; }
        }
        target = Mathf.Min(nearest + 1, route.Count - 1);
        hesitateUntil = Time.time + Random.Range(0.6f, 1.2f);
        jumpQueued = false;
    }

    private void Update()
    {
        Move = Vector3.zero;
        if (route.Count < 2 || target >= route.Count || route[target] == null)
            return;

        var col = route[target];
        // chegou na plataforma alvo: passa para a próxima (às vezes para um instante)
        var ground = controller.GroundCollider;
        if (controller.IsGrounded && ground != null && (ground == col || ground.transform.IsChildOf(col.transform)))
        {
            if (target < route.Count - 1) target++;
            hesitateUntil = Time.time + (Random.value < 0.3f ? Random.Range(0.2f, 0.7f) : 0f);
            NewAim();
            return;
        }
        if (Time.time < hesitateUntil || !col.enabled)
            return;

        var b = col.bounds;
        var aim = new Vector3(b.center.x + aimOffset.x * b.extents.x, b.max.y, b.center.z + aimOffset.z * b.extents.z);
        var flat = aim - transform.position; flat.y = 0f;
        float dist = flat.magnitude;
        if (dist < 0.01f) return;
        var dir = flat / dist;
        // iniciante: não vai a toda velocidade e freia perto do alvo
        Move = dir * Mathf.Lerp(0.3f, 0.9f, Mathf.Clamp01(dist / 3f));

        if (controller.IsGrounded && Time.time - lastJump > 0.45f)
        {
            bool groundAhead = Physics.Raycast(transform.position + dir * 1.1f + Vector3.up * 0.2f, Vector3.down, 1.6f, PlayerController.GroundMask, QueryTriggerInteraction.Ignore);
            float rise = b.max.y - (transform.position.y - 0.5f);
            bool needJump = !groundAhead || (rise > 0.7f && dist < 5f);
            if (needJump && Random.value < 0.9f)   // de vez em quando hesita na beirada (e cai)
            {
                jumpQueued = true;
                lastJump = Time.time;
            }
        }
    }
}
