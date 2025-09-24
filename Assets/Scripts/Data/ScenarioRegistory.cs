// Assets/Scripts/Data/ScenarioRegistry.cs
using System.Collections.Generic;
using UnityEngine;
using Game.Data;

[CreateAssetMenu(menuName = "Game/Scenario Registry")]
public class ScenarioRegistry : ScriptableObject
{
    [Tooltip("登録する EventData（実データ）")]
    public List<EventData> events = new();

    [Header("Optional: 位置IDの定義（存在チェック用）")]
    public List<string> knownAreaIds = new();

    [Header("Optional: 設定の参照（空ならシーン側を使用）")]
    public Game.Config.GlobalSettings overrideSettings;
}

