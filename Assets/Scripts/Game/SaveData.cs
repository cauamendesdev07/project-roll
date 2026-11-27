using UnityEngine;

// Progresso salvo (PlayerPrefs): fases liberadas e recordes por fase.
public static class SaveData
{
    public const int LevelCount = 10;
    private const string UnlockedKey = "roll.unlocked";
    private static string TimeKey(int level) => $"roll.best.time.{level}";
    private static string DeathsKey(int level) => $"roll.best.deaths.{level}";

    // Maior fase liberada (1 a 10).
    public static int Unlocked => Mathf.Clamp(PlayerPrefs.GetInt(UnlockedKey, 1), 1, LevelCount);

    public static bool HasProgress => Unlocked > 1 || HasBest(1);

    public static bool IsUnlocked(int level) => level >= 1 && level <= Unlocked;

    public static bool HasBest(int level) => PlayerPrefs.HasKey(TimeKey(level));

    public static float BestTime(int level) => PlayerPrefs.GetFloat(TimeKey(level), 0f);

    public static int BestDeaths(int level) => PlayerPrefs.GetInt(DeathsKey(level), 0);

    // Registra a conclusão de uma fase: libera a próxima e guarda os recordes (só se melhorarem).
    public static void RecordCompletion(int level, float time, int deaths)
    {
        if (level < 1 || level > LevelCount)
            return;

        if (level + 1 > Unlocked)
            PlayerPrefs.SetInt(UnlockedKey, Mathf.Min(level + 1, LevelCount));

        if (!HasBest(level) || time < BestTime(level))
            PlayerPrefs.SetFloat(TimeKey(level), time);

        if (!PlayerPrefs.HasKey(DeathsKey(level)) || deaths < BestDeaths(level))
            PlayerPrefs.SetInt(DeathsKey(level), deaths);

        PlayerPrefs.Save();
    }

    public static void ResetProgress()
    {
        PlayerPrefs.DeleteKey(UnlockedKey);
        for (int i = 1; i <= LevelCount; i++)
        {
            PlayerPrefs.DeleteKey(TimeKey(i));
            PlayerPrefs.DeleteKey(DeathsKey(i));
        }
        PlayerPrefs.Save();
    }
}
