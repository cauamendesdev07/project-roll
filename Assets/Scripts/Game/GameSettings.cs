using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Opções do jogador (PlayerPrefs): controles, áudio e gráficos.
// Cada alteração salva na hora e já é aplicada.
public static class GameSettings
{
    public const float DefaultSensitivity = 0.05f; // valor que as cenas usavam
    public const float MinSensitivity = 0.01f;
    public const float MaxSensitivity = 0.2f;

    private const string P = "roll.opt.";

    public static float MouseSensitivity { get; private set; } = DefaultSensitivity;
    public static bool InvertY { get; private set; }
    public static float MasterVolume { get; private set; } = 1f;
    public static float MusicVolume { get; private set; } = 0.7f;
    public static float SfxVolume { get; private set; } = 1f;
    public static bool Fullscreen { get; private set; } = true;
    public static bool VSync { get; private set; } = true;
    public static bool Shadows { get; private set; } = true;
    public static bool PostProcessing { get; private set; } = true;

    // Avisado a cada alteração (o áudio do ticket 05 escuta para atualizar volumes).
    public static event Action Changed;

    private static bool loaded;

    public static void Load()
    {
        if (loaded)
            return;
        loaded = true;

        MouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(P + "sensitivity", DefaultSensitivity), MinSensitivity, MaxSensitivity);
        InvertY = PlayerPrefs.GetInt(P + "invertY", 0) == 1;
        MasterVolume = PlayerPrefs.GetFloat(P + "volMaster", 1f);
        MusicVolume = PlayerPrefs.GetFloat(P + "volMusic", 0.7f);
        SfxVolume = PlayerPrefs.GetFloat(P + "volSfx", 1f);
        Fullscreen = PlayerPrefs.GetInt(P + "fullscreen", Screen.fullScreen ? 1 : 0) == 1;
        VSync = PlayerPrefs.GetInt(P + "vsync", 1) == 1;
        Shadows = PlayerPrefs.GetInt(P + "shadows", 1) == 1;
        PostProcessing = PlayerPrefs.GetInt(P + "post", 1) == 1;

        ApplyAudio();
        ApplyDisplay();
    }

    public static void SetMouseSensitivity(float v) { MouseSensitivity = Mathf.Clamp(v, MinSensitivity, MaxSensitivity); SaveFloat("sensitivity", MouseSensitivity); }
    public static void SetInvertY(bool v) { InvertY = v; SaveBool("invertY", v); }
    public static void SetMasterVolume(float v) { MasterVolume = Mathf.Clamp01(v); SaveFloat("volMaster", MasterVolume); ApplyAudio(); }
    public static void SetMusicVolume(float v) { MusicVolume = Mathf.Clamp01(v); SaveFloat("volMusic", MusicVolume); }
    public static void SetSfxVolume(float v) { SfxVolume = Mathf.Clamp01(v); SaveFloat("volSfx", SfxVolume); }
    public static void SetFullscreen(bool v) { Fullscreen = v; SaveBool("fullscreen", v); ApplyDisplay(); }
    public static void SetVSync(bool v) { VSync = v; SaveBool("vsync", v); ApplyDisplay(); }
    public static void SetShadows(bool v) { Shadows = v; SaveBool("shadows", v); ApplyCameras(); }
    public static void SetPostProcessing(bool v) { PostProcessing = v; SaveBool("post", v); ApplyCameras(); }

    // ---------- Resolução ----------

    // Resoluções disponíveis (sem repetir largura × altura), da menor para a maior.
    public static List<Vector2Int> Resolutions()
    {
        var list = new List<Vector2Int>();
        foreach (var r in Screen.resolutions)
        {
            var v = new Vector2Int(r.width, r.height);
            if (!list.Contains(v)) list.Add(v);
        }
        if (list.Count == 0) list.Add(CurrentResolution);
        list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return list;
    }

    public static Vector2Int CurrentResolution =>
        PlayerPrefs.HasKey(P + "resW")
            ? new Vector2Int(PlayerPrefs.GetInt(P + "resW"), PlayerPrefs.GetInt(P + "resH"))
            : new Vector2Int(Screen.width, Screen.height);

    public static void SetResolution(Vector2Int res)
    {
        PlayerPrefs.SetInt(P + "resW", res.x);
        PlayerPrefs.SetInt(P + "resH", res.y);
        PlayerPrefs.Save();
        ApplyDisplay();
        Changed?.Invoke();
    }

    // ---------- Aplicação ----------

    private static void ApplyAudio()
    {
        AudioListener.volume = MasterVolume;
    }

    // Tela cheia, resolução e VSync. No editor não mexe (a Game view tem os próprios controles
    // e alterar QualitySettings ali mudaria o projeto).
    private static void ApplyDisplay()
    {
        if (Application.isEditor)
            return;

        QualitySettings.vSyncCount = VSync ? 1 : 0;
        var mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        if (PlayerPrefs.HasKey(P + "resW"))
        {
            var res = CurrentResolution;
            Screen.SetResolution(res.x, res.y, mode);
        }
        else
        {
            Screen.fullScreenMode = mode;
        }
    }

    // Sombras e pós-processamento por câmera (não altera o asset da URP).
    // O pós só é ligado nas câmeras que já o usavam na cena (o menu não usa).
    public static void ApplyCameras()
    {
        foreach (var cam in Camera.allCameras)
        {
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data == null)
                continue;

            var defaults = cam.GetComponent<CameraDefaults>();
            if (defaults == null)
            {
                defaults = cam.gameObject.AddComponent<CameraDefaults>();
                defaults.shadows = data.renderShadows;
                defaults.post = data.renderPostProcessing;
            }

            data.renderShadows = defaults.shadows && Shadows;
            data.renderPostProcessing = defaults.post && PostProcessing;
        }
    }

    private static void SaveFloat(string key, float v) { PlayerPrefs.SetFloat(P + key, v); PlayerPrefs.Save(); Changed?.Invoke(); }
    private static void SaveBool(string key, bool v) { PlayerPrefs.SetInt(P + key, v ? 1 : 0); PlayerPrefs.Save(); Changed?.Invoke(); }
}
