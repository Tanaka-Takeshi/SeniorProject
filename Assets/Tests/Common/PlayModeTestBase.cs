using NUnit.Framework;
using UnityEngine;
using Game.Data;
using Game.Events;
using Game.Runtime;

namespace Game.Tests
{
    /// <summary>
    /// PlayModeテストの共通セットアップ／ヘルパーベースクラス
    /// </summary>
    public abstract class PlayModeTestBase
    {
        protected GameObject root, emGO, clockGO, locGO, inputGO;
        protected EventManager em;
        protected SimpleClock clock;
        protected SimpleLocationResolver locator;
        protected TestInputProxy input;

        private float _prevTimeScale;

        [SetUp]
        public virtual void BaseSetup()
        {
            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            root = new GameObject("ROOT");

            clockGO = new GameObject("Clock");
            clock = clockGO.AddComponent<SimpleClock>();
            clockGO.transform.SetParent(root.transform, false);

            locGO = new GameObject("Locator");
            locator = locGO.AddComponent<SimpleLocationResolver>();
            locGO.transform.SetParent(root.transform, false);

            inputGO = new GameObject("Input");
            input = inputGO.AddComponent<TestInputProxy>();
            inputGO.transform.SetParent(root.transform, false);

            emGO = new GameObject("EventManager");
            em = emGO.AddComponent<EventManager>();
            emGO.transform.SetParent(root.transform, false);

            // GlobalSettings 注入
            var settings = ScriptableObject.CreateInstance<Game.Config.GlobalSettings>();
            settings.dayLengthSeconds = 1440f;
            typeof(EventManager).GetField("globalSettings",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(em, settings);

            // DI注入
            typeof(EventManager).GetField("clockBehaviour",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(em, clock);
            typeof(EventManager).GetField("locationBehaviour",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(em, locator);
            typeof(EventManager).GetField("inputBehaviour",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(em, input);
        }

        [TearDown]
        public virtual void BaseTearDown()
        {
            Object.DestroyImmediate(root);
            Time.timeScale = _prevTimeScale;
        }

        // 共通ヘルパ
        protected void InitEvents(params EventData[] eventsToUse)
        {
            em.InitializeForTest(eventsToUse);
        }

        protected EventData MakeEvent(string id, string appear, string startDL, string endDL, string areaId,
            float alt = 0.5f, bool requiresButton = true, Game.Events.EventType type = Game.Events.EventType.Sub)
        {
            var e = ScriptableObject.CreateInstance<EventData>();
            e.eventId = id;
            e.type = type;
            e.appearAt = appear;
            e.startDeadline = startDL;
            e.endDeadline = endDL;
            e.location = new LocationRef { kind = LocationKind.AreaId, id = areaId };
            e.requiresButtonPress = requiresButton;
            e.dependencies = new System.Collections.Generic.List<string>();
            e.altCompleteThreshold = alt;
            e.weekdayRule = new WeekdayRule();
            return e;
        }

        protected void TickTo(EventManager manager, GameObject clockObj, string hhmm)
        {
            var sc = clockObj.GetComponent<SimpleClock>();
            sc.Jump(ParseGameSeconds(hhmm));
            manager.EvaluateFrame();
            manager.EvaluateFrame();
        }

        private static float ParseGameSeconds(string hhmm)
        {
            if (string.IsNullOrEmpty(hhmm)) return 0f;
            var sp = hhmm.Split(':');
            int hh = int.Parse(sp[0]);
            int mm = int.Parse(sp[1]);
            return hh * 60 + mm;
        }

        protected EventRuntime GetRuntime(EventManager manager, string id)
        {
            var dict = (System.Collections.Generic.Dictionary<string, EventRuntime>)
                typeof(EventManager).GetField("_events",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(manager);
            return dict[id];
        }
    }
}

