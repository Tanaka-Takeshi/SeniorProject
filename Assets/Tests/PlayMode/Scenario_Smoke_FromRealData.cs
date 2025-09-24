// Assets/Tests/PlayMode/Scenario_Smoke_FromRealData.cs
using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Game.Data;
using Game.Events;
using Game.Tests; // PlayModeTestBase / TestHelpers
using static Game.Tests.TestHelpers;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// 実データ(ScenarioRegistry)を使ったスモークテスト。
    /// - Registry の events から最も早い appearAt を検出し、そこまで時間を進める
    /// - 少なくとも1件は Available シグナルが出ること
    /// - 設定は registry.overrideSettings があればそれを使い、無ければテスト用デフォルトを注入
    ///
    /// 使い方：
    /// - プロジェクト内の Resources に ScenarioRegistry アセットを置いて名前を
    ///   "ScenarioRegistry" にしておく（例：Assets/Resources/ScenarioRegistry.asset）
    /// </summary>
    public class Scenario_Smoke_FromRealData : PlayModeTestBase
    {
        [SetUp] public void Setup2() => BaseSetup();
        [TearDown] public void Teardown2() => BaseTearDown();

        // HH:MM を分（=秒）に
        private static int ParseGameMinutes(string hhmm)
        {
            if (string.IsNullOrEmpty(hhmm)) return 0;
            var sp = hhmm.Split(':');
            if (sp.Length < 2) return 0;
            int hh = int.Parse(sp[0]);
            int mm = int.Parse(sp[1]);
            return hh * 60 + mm;
        }

        // 分を "HH:MM" に
        private static string ToHHMM(int minutes)
        {
            minutes = Mathf.Max(0, minutes);
            int hh = minutes / 60;
            int mm = minutes % 60;
            return $"{hh:00}:{mm:00}";
        }

        /// <summary>
        /// 少なくとも1件の Available を確認するスモーク。
        /// </summary>
        [Test]
        public void RealData_AtLeast_One_Event_Becomes_Available()
        {
            // 1) Registry の取得（Resources からロード）
            var registry = Resources.Load<ScenarioRegistry>("ScenarioRegistry");
            if (registry == null)
            {
                Assert.Inconclusive("テスト用に 'Assets/Resources/ScenarioRegistry.asset' を作成し、events を設定してください。");
                return;
            }

            // 2) イベント実体
            var list = registry.events?.Where(e => e != null && !string.IsNullOrEmpty(e.eventId)).ToList();
            if (list == null || list.Count == 0)
            {
                Assert.Inconclusive("ScenarioRegistry.events に EventData が1件もありません。");
                return;
            }

            // 3) 設定の決定（overrideSettings があればそれを使う）
            var settings = registry.overrideSettings != null
                ? registry.overrideSettings
                : ScriptableObject.CreateInstance<Game.Config.GlobalSettings>();

            // 4) テスト用の最低限設定（デフォルト設定を使う場合のみ）
            if (registry.overrideSettings == null)
            {
                settings.dayLengthSeconds = 1440f; // 1分=1秒 の既存前提
            }

            // 5) DI 注入
            Inject(em, clock, locator, input, settings);

            // 6) 実データを EventManager に設定
            em.InitializeForTest(list.ToArray());

            // 7) appearAt の最小時刻を求める
            int minAppear = list
                .Select(e => ParseGameMinutes(e.appearAt))
                .DefaultIfEmpty(0)
                .Min();

            // 念のため少し前からスタート（-1分）→最小 appearAt に到達させる
            var startMM = Mathf.Max(0, minAppear - 1);
            clock.Jump(startMM);

            using var sig = new TestHelpers.SignalCatcher();

            // 最小 appearAt まで進める
            var targetHHMM = ToHHMM(minAppear);
            AdvanceTo(em, clockGO, targetHHMM);

            // 8) 何かしら Available が飛んでいるはず
            Assert.IsNotNull(sig.Available, "少なくとも1件は Available になる想定です（実データの appearAt を確認してください）。");

            // 追加の sanity：一つでも Available 状態のランタイムが存在すること
            bool anyAvailable = em.AllRuntimes().Any(rt => rt.State == EventState.Available);
            Assert.IsTrue(anyAvailable, "少なくとも1件が Available 状態である必要があります。");
        }
    }
}
