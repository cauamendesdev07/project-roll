using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Áudio do jogo: música contínua, ambiente por fase (com troca suave), rolamento da bola
// e efeitos. Os sons ficam em Resources/Audio (gerados por AudioSynth no editor).
// Volumes: GameSettings (geral no AudioListener; música; efeitos também controla o ambiente).
public class AudioManager : MonoBehaviour
{
    private const float MusicBase = 0.45f;
    private const float AmbientBase = 0.7f;
    private const float RollBase = 0.35f;
    private const float FadeTime = 1.5f;

    private static AudioManager instance;

    private AudioSource music, ambA, ambB, roll;
    private readonly AudioSource[] pool = new AudioSource[10];
    private int poolNext;
    private readonly Dictionary<string, AudioClip> clips = new();

    private AudioSource ambCurrent, ambOld;
    private float ambFade = 1f;
    private float rollTarget, rollLevel, rollPitch = 1f;

    private void Awake()
    {
        instance = this;
        music = NewSource("Music", true);
        ambA = NewSource("AmbientA", true);
        ambB = NewSource("AmbientB", true);
        roll = NewSource("Roll", true);
        for (int i = 0; i < pool.Length; i++) pool[i] = NewSource("Sfx" + i, false);

        music.clip = Clip("Music/music");
        if (music.clip != null) music.Play();
        roll.clip = Clip("Sfx/roll");
        roll.volume = 0f;
        if (roll.clip != null) roll.Play();
        ambCurrent = ambA;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private AudioSource NewSource(string name, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        s.spatialBlend = 0f;
        s.ignoreListenerPause = true;
        return s;
    }

    private AudioClip Clip(string name)
    {
        if (!clips.TryGetValue(name, out var c))
        {
            c = Resources.Load<AudioClip>("Audio/" + name);
            clips[name] = c;
        }
        return c;
    }

    // ---------- API para o jogo ----------

    // Efeito curto. pitchJitter varia o tom um pouco a cada vez.
    public static void Play(string name, float volume = 1f, float pitch = 1f, float pitchJitter = 0f)
    {
        if (instance == null) return;
        var clip = instance.Clip("Sfx/" + name);
        if (clip == null) return;
        var s = instance.pool[instance.poolNext];
        instance.poolNext = (instance.poolNext + 1) % instance.pool.Length;
        s.clip = clip;
        s.volume = Mathf.Clamp01(volume) * GameSettings.SfxVolume;
        s.pitch = pitch * (1f + Random.Range(-pitchJitter, pitchJitter));
        s.Play();
    }

    public static void PlayDelayed(string name, float delay, float volume = 1f, float pitch = 1f, float pitchJitter = 0f)
    {
        if (instance == null) return;
        instance.StartCoroutine(instance.Delayed(name, delay, volume, pitch, pitchJitter));
    }

    private IEnumerator Delayed(string name, float delay, float volume, float pitch, float pitchJitter)
    {
        yield return new WaitForSeconds(delay);
        Play(name, volume, pitch, pitchJitter);
    }

    // Rolamento: chamado pelo PlayerController a cada passo de física.
    public static void SetRolling(float speed, bool grounded)
    {
        if (instance == null) return;
        float k = grounded ? Mathf.Clamp01((speed - 0.5f) / 9f) : 0f;
        instance.rollTarget = k;
        instance.rollPitch = 0.7f + 0.6f * k;
    }

    // Troca o ambiente para a fase (0 = menu, sem ambiente).
    public void SetLevel(int level)
    {
        rollTarget = 0f;
        var clip = level > 0 ? Clip($"Ambient/amb_{level:00}") : null;
        if (ambCurrent.clip == clip && (clip == null || ambCurrent.isPlaying))
            return;

        ambOld = ambCurrent;
        ambCurrent = ambCurrent == ambA ? ambB : ambA;
        ambCurrent.clip = clip;
        ambCurrent.volume = 0f;
        if (clip != null)
        {
            ambCurrent.time = Random.Range(0f, clip.length);
            ambCurrent.Play();
        }
        ambFade = 0f;
    }

    private void Update()
    {
        float sfx = GameSettings.SfxVolume;
        music.volume = MusicBase * GameSettings.MusicVolume;

        // Troca do ambiente em tempo real (funciona com o jogo pausado).
        ambFade = Mathf.MoveTowards(ambFade, 1f, Time.unscaledDeltaTime / FadeTime);
        ambCurrent.volume = AmbientBase * sfx * ambFade;
        if (ambOld != null)
        {
            ambOld.volume = AmbientBase * sfx * (1f - ambFade);
            if (ambFade >= 1f) { ambOld.Stop(); ambOld = null; }
        }

        // Rolamento some na pausa.
        bool paused = GameManager.Instance != null && (GameManager.Instance.IsPaused || GameManager.Instance.IsTransitioning);
        float target = paused ? 0f : rollTarget;
        rollLevel = Mathf.MoveTowards(rollLevel, target, Time.unscaledDeltaTime * 4f);
        roll.volume = rollLevel * RollBase * sfx;
        roll.pitch = Mathf.Lerp(roll.pitch, rollPitch, Time.unscaledDeltaTime * 6f);
    }
}
