// Assets/Scripts/GameState/FlagService.cs
using System.Collections.Generic;
using UnityEngine;

public static class FlagService
{
    static readonly HashSet<string> _flags = new();
    const string PlayerPrefsKey = "GAME_FLAGS_CSV";

    public static bool Has(GameFlag key) => key != GameFlag.None && _flags.Contains(key.ToString());

    public static void Set(GameFlag key)
    {
        if (key == GameFlag.None) return;
        _flags.Add(key.ToString());
    }

    public static void Clear(GameFlag key)
    {
        if (key == GameFlag.None) return;
        _flags.Remove(key.ToString());
    }

    public static void Toggle(GameFlag key)
    {
        if (key == GameFlag.None) return;
        var k = key.ToString();
        if (!_flags.Add(k)) _flags.Remove(k);
    }

    public static void Save()
    {
        PlayerPrefs.SetString(PlayerPrefsKey, string.Join(",", _flags));
        PlayerPrefs.Save();
    }

    public static void Load()
    {
        _flags.Clear();
        var csv = PlayerPrefs.GetString(PlayerPrefsKey, "");
        if (string.IsNullOrEmpty(csv)) return;
        foreach (var s in csv.Split(','))
        {
            var k = s.Trim();
            if (!string.IsNullOrEmpty(k)) _flags.Add(k);
        }
    }
}
