using UnityEngine;

// Visual da bola (só aparência): malha low-poly colorida, rastro quando está rápida e poeira
// nos pousos fortes, na cor do chão de cada bioma. Adicionado pelo GameManager em cada fase;
// os assets vêm de Resources/Ball (gerados por BallAssets no editor).
// Não mexe no Rigidbody, no colisor nem na escala da bola.
public class BallVisuals : MonoBehaviour
{
    private const float TrailSpeed = 7.5f;
    private const float DustMinImpact = 6.5f;

    // Cor da poeira por fase (1 a 10).
    private static readonly Color[] DustColors =
    {
        new Color(0.45f, 0.38f, 0.28f), // Floresta
        new Color(0.80f, 0.66f, 0.50f), // Caatinga
        new Color(0.72f, 0.44f, 0.32f), // Cerrado
        new Color(0.45f, 0.39f, 0.29f), // Pantanal
        new Color(0.93f, 0.86f, 0.70f), // Ilha
        new Color(0.98f, 0.96f, 0.91f), // Lençóis
        new Color(0.55f, 0.60f, 0.70f), // Gruta
        new Color(0.95f, 0.97f, 1.00f), // Neve
        new Color(0.32f, 0.29f, 0.28f), // Vulcão
        new Color(0.95f, 0.96f, 1.00f), // Céu
    };

    private Rigidbody rb;
    private TrailRenderer trail;
    private ParticleSystem dust;
    private ParticleSystem poof;
    private Color dustColor = new Color(0.7f, 0.65f, 0.55f);

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        var mesh = Resources.Load<Mesh>("Ball/Ball_Mesh");
        var mat = Resources.Load<Material>("Ball/M_Ball");
        var fx = Resources.Load<Material>("Ball/M_BallFx");
        var mf = GetComponent<MeshFilter>();
        var mr = GetComponent<MeshRenderer>();
        if (mesh != null && mf != null) mf.sharedMesh = mesh;
        if (mat != null && mr != null) mr.sharedMaterial = mat;

        int level = GameManager.Instance != null ? GameManager.Instance.CurrentLevel : 0;
        if (level >= 1 && level <= DustColors.Length) dustColor = DustColors[level - 1];
        // Um pouco mais clara que o chão, senão some contra ele.
        dustColor = Color.Lerp(dustColor, Color.white, 0.4f);

        if (fx != null)
        {
            BuildTrail(fx);
            BuildDust(fx);
            BuildPoof(fx);
        }

        var controller = GetComponent<PlayerController>();
        if (controller != null) controller.Landed += OnLanded;
        var checkpoint = GetComponent<CheckpointSystem>();
        if (checkpoint != null) checkpoint.Respawned += OnRespawned;
    }

    private void OnDestroy()
    {
        var controller = GetComponent<PlayerController>();
        if (controller != null) controller.Landed -= OnLanded;
        var checkpoint = GetComponent<CheckpointSystem>();
        if (checkpoint != null) checkpoint.Respawned -= OnRespawned;
        if (dust != null) Destroy(dust.gameObject);
        if (poof != null) Destroy(poof.gameObject);
    }

    private void BuildTrail(Material fx)
    {
        // Filho na posição do centro: a rotação da bola não afeta o rastro.
        var go = new GameObject("BallTrail");
        go.transform.SetParent(transform, false);
        trail = go.AddComponent<TrailRenderer>();
        trail.sharedMaterial = fx;
        trail.time = 0.2f;
        trail.minVertexDistance = 0.08f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.75f), new Keyframe(1f, 0f));
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.28f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = g;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.emitting = false;
    }

    private void BuildDust(Material fx)
    {
        // Fica fora da bola (no mundo), para a nuvem não girar nem seguir a bola.
        var go = new GameObject("BallDust");
        dust = go.AddComponent<ParticleSystem>();
        dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = dust.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.gravityModifier = -0.04f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;

        var emission = dust.emission;
        emission.rateOverTime = 0f;

        // Anel no chão, partículas saindo para os lados.
        var shape = dust.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.35f;
        shape.radiusThickness = 0f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var vel = dust.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var limit = dust.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.dampen = 0.12f;
        limit.limit = 0.4f;

        var size = dust.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.6f)));

        var color = dust.colorOverLifetime;
        color.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.75f, 0f), new GradientAlphaKey(0f, 1f) });
        color.color = g;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = fx;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    private void Update()
    {
        if (trail != null && rb != null)
            trail.emitting = rb.linearVelocity.magnitude > TrailSpeed;
    }

    // Pouso: só os fortes levantam poeira; mais forte = mais poeira.
    private void OnLanded(float impactSpeed, Vector3 point)
    {
        if (dust == null || impactSpeed < DustMinImpact)
            return;

        float k = Mathf.Clamp01((impactSpeed - DustMinImpact) / 10f);
        dust.transform.position = point + Vector3.up * 0.05f;
        var emit = new ParticleSystem.EmitParams
        {
            startColor = dustColor,
            applyShapeToPosition = true,
        };
        dust.Emit(emit, Mathf.RoundToInt(Mathf.Lerp(10f, 30f, k)));
    }

    // Respawn: limpa o rastro (não pode riscar a tela do vazio até o checkpoint) e solta o "poof".
    private void OnRespawned()
    {
        if (trail != null) trail.Clear();
        if (poof != null)
        {
            poof.transform.position = transform.position;
            poof.Emit(26);
        }
    }

    // "Poof" no ponto onde a bola reaparece: esfera de partículas claras que se espalham e somem.
    private void BuildPoof(Material fx)
    {
        var go = new GameObject("BallPoof");
        poof = go.AddComponent<ParticleSystem>();
        poof.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = poof.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        main.startColor = new Color(1f, 0.97f, 0.88f, 0.9f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;

        var emission = poof.emission;
        emission.rateOverTime = 0f;

        var shape = poof.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.45f;

        var limit = poof.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.dampen = 0.2f;
        limit.limit = 0.5f;

        var size = poof.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f)));

        var color = poof.colorOverLifetime;
        color.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        color.color = g;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = fx;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }
}
