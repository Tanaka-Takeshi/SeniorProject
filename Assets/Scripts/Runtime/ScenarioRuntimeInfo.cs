// Assets/Scripts/Runtime/ScenarioRuntimeInfo.cs
using System.Collections.Generic;
using System.Linq;

namespace Game.Runtime
{
    /// <summary>
    /// 実行開始時に ScenarioBootstrap が埋める、トグル適用後のスナップショット。
    /// デバッグUIから参照するだけの読み取り用ハブ。
    /// </summary>
    public static class ScenarioRuntimeInfo
    {
        public static bool DisabledAll { get; private set; }
        public static int SourceCount { get; private set; }     // Registry.events の件数（フィルタ前）
        public static string[] EnabledIds { get; private set; }   // 実際にロードされたID
        public static string[] ExcludedIds { get; private set; }  // フィルタで除外されたID（差集合）
        public static string[] IncludedFilter { get; private set; } // トグルの includeIds（表示用）
        public static string[] ExcludedFilter { get; private set; } // トグルの excludeIds（表示用）

        public static void Publish(
            IEnumerable<string> allSourceIds,
            IEnumerable<string> loadedIds,
            IEnumerable<string> includeFilter,
            IEnumerable<string> excludeFilter,
            bool disabledAll)
        {
            var src = (allSourceIds ?? Enumerable.Empty<string>()).Distinct().ToArray();
            var loaded = (loadedIds ?? Enumerable.Empty<string>()).Distinct().ToArray();

            DisabledAll = disabledAll;
            SourceCount = src.Length;
            EnabledIds = loaded;

            var srcSet = new HashSet<string>(src);
            var loadedSet = new HashSet<string>(loaded);
            // “元データにあるがロードされなかった”＝除外（トグルや条件で）
            srcSet.ExceptWith(loadedSet);
            ExcludedIds = srcSet.ToArray();

            IncludedFilter = (includeFilter ?? Enumerable.Empty<string>()).Distinct().ToArray();
            ExcludedFilter = (excludeFilter ?? Enumerable.Empty<string>()).Distinct().ToArray();
        }
    }
}
