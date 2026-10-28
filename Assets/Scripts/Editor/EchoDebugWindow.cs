using System.Collections.Generic;
using EchoMaze.Core;
using EchoMaze.Data;
using EchoMaze.Gameplay;
using UnityEditor;
using UnityEngine;

namespace EchoMaze.EditorTools
{
    /// <summary>
    /// The instrument week 2's balance is measured with: force a ping, freeze the wave in place, read the
    /// distance profile behind it, and see which sounds the creatures were told about.
    ///
    /// It exists because the alternative is tuning by feel against a wave that lives for two thirds of a
    /// second, and the week 1 verdict ("nothing comes back") was only diagnosable once the numbers behind
    /// the arcs could be looked at. Everything here reads the runtime's own state — no second copy of the
    /// math that could disagree with the game and start a lie.
    /// </summary>
    public sealed class EchoDebugWindow : EditorWindow
    {
        /// <summary>A sound the world made, kept with what it cost and who heard it.</summary>
        struct NoiseEvent
        {
            public Vector2 at;
            public float loudness;
            public int heardBy;
            public float arcClock;
            public bool drewWave;
        }

        const int NoiseHistory = 48;

        readonly List<NoiseEvent> noise = new List<NoiseEvent>(NoiseHistory);
        readonly List<PingRecord> pings = new List<PingRecord>();

        EchoDirector director;
        EchoRenderer field;
        Player player;
        Crawler[] crawlers = { };
        PingRecord selected;
        PingRecord lastPing;
        Editor profileEditor;
        Vector2 scroll;
        bool showRawRays = true;
        bool showCarrierCircle = true;
        bool followLatest = true;
        bool noisePaused;
        float forceLoudness = 0.65f;
        float bareNoiseLoudness = 0.12f;
        double crawlerRefreshAt;

        [MenuItem("Tools/Echo Maze/Echo Debug Window")]
        static void Open()
        {
            EchoDebugWindow window = GetWindow<EchoDebugWindow>();
            window.titleContent = new GUIContent("Echo Debug", "Inspect the wave the game is actually drawing.");
            window.minSize = new Vector2(430f, 480f);
        }

        void OnEnable()
        {
            EditorApplication.update += OnUpdate;
            SceneView.duringSceneGui += OnSceneGui;
            Rebind(null);
        }

        void OnDisable()
        {
            EditorApplication.update -= OnUpdate;
            SceneView.duringSceneGui -= OnSceneGui;
            Unsubscribe();
        }

        void OnUpdate()
        {
            // Play mode is the only state where anything here changes on its own, so the window only
            // repaints itself then; in edit mode a repaint on demand keeps the Scene view from thrashing.
            if (!Application.isPlaying)
            {
                return;
            }

            Rebind(null);
            Repaint();
        }

        /// <summary>
        /// Finds the live director and moves the noise subscription to it. Called every frame in play mode
        /// and whenever the user picks something, because play-mode exits, scene reloads and a rebuilt
        /// blockout all take the old director with them, and a handler on a destroyed one is an exception
        /// in the middle of the session being measured.
        /// </summary>
        void Rebind(GameObject candidate)
        {
            EchoDirector found = candidate != null ? candidate.GetComponentInParent<EchoDirector>()
                                                   : UnityEngine.Object.FindAnyObjectByType<EchoDirector>();

            if (found == director)
            {
                return;
            }

            Unsubscribe();
            director = found;
            lastPing = null;

            if (director != null)
            {
                director.NoiseEmitted += OnNoise;
                director.PingEmitted += OnPing;
                field = director.GetComponentInChildren<EchoRenderer>();
                if (field == null)
                {
                    field = UnityEngine.Object.FindAnyObjectByType<EchoRenderer>();
                }
            }
            else
            {
                field = null;
                selected = null;
            }
        }

        void Unsubscribe()
        {
            if (director != null)
            {
                director.NoiseEmitted -= OnNoise;
                director.PingEmitted -= OnPing;
            }
        }

        void OnNoise(Vector2 position, float loudness)
        {
            if (noisePaused || director == null)
            {
                return;
            }

            EchoTuningProfile profile = director.Profile;
            int listeners = 0;
            float hearing = profile != null ? ArcMath.HearingRadius(loudness, profile) : 0f;
            foreach (Crawler crawler in crawlers)
            {
                if (crawler != null && Vector2.Distance(crawler.Position, position) <= hearing)
                {
                    listeners++;
                }
            }

            // The director raises PingEmitted and then the same sound on the noise stream, from the same
            // origin at the same tick, so that is the only honest way to tell "this noise drew a wave" from
            // "this noise only travelled" (a footstep, a bump) without asking the runtime to say it.
            bool drewWave = lastPing != null &&
                            Mathf.Abs(lastPing.loudness - loudness) < 1e-4f &&
                            Vector2.Distance(lastPing.origin, position) < 1e-4f &&
                            Mathf.Abs(lastPing.emitTime - director.ArcClock) < 1e-4f;

            noise.Add(new NoiseEvent
            {
                at = position,
                loudness = loudness,
                heardBy = listeners,
                arcClock = director.ArcClock,
                drewWave = drewWave
            });

            if (noise.Count > NoiseHistory)
            {
                noise.RemoveAt(0);
            }
        }

        void OnPing(PingRecord record)
        {
            lastPing = record;
        }

        void OnGUI()
        {
            Rebind(Selection.activeGameObject);

            scroll = EditorGUILayout.BeginScrollView(scroll);

            if (director == null)
            {
                EditorGUILayout.HelpBox(
                    "No EchoDirector in the scene. Open Assets/Scenes/Level02_Corridor.unity or " +
                    "Assets/Scenes/Sandbox.unity and press Play — this window measures a running game, " +
                    "it does not build one.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            DrawEmission();
            DrawFreeze();
            DrawPingInspector();
            DrawPlots();
            DrawSceneViewOptions();
            DrawNoiseLog();
            DrawTuning();

            EditorGUILayout.EndScrollView();
        }

        void DrawEmission()
        {
            EditorGUILayout.LabelField("Emit", EditorStyles.boldLabel);

            EchoTuningProfile profile = director.Profile;
            forceLoudness = EditorGUILayout.Slider("Force ping loudness", forceLoudness, 0f, 1f);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Force ping at the player's pose", GUILayout.Height(28f)))
                {
                    director.ForcePing(forceLoudness);
                    if (profile != null)
                    {
                        EditorGUILayout.HelpBox("Reach " + ArcMath.PingRadius(forceLoudness, profile).ToString("F2") +
                                                " m, " + ArcMath.RayCount(forceLoudness, profile) + " rays, " +
                                                "enemy hearing " + ArcMath.HearingRadius(forceLoudness, profile).ToString("F2") +
                                                " m, cooldown " + (profile.cooldownBase + profile.cooldownPerLoudness * forceLoudness).ToString("F2") +
                                                " s. These are the same four numbers the game used.", MessageType.None);
                    }
                }

                using (new EditorGUI.DisabledScope(director.WeakPulseRemaining > 0f))
                {
                    if (GUILayout.Button("Weak pulse (F, 360°)"))
                    {
                        director.TryWeakPulse();
                    }
                }
            }

            EditorGUILayout.LabelField("Cooldown " + director.CooldownRemaining.ToString("F2") +
                                       " s, weak pulse " + director.WeakPulseRemaining.ToString("F2") + " s");

            bareNoiseLoudness = EditorGUILayout.Slider("Bare noise loudness", bareNoiseLoudness, 0f, 1f);
            using (new EditorGUI.DisabledScope(!Application.isPlaying || player == null))
            {
                if (GUILayout.Button("Report a bare noise at the player (draws nothing)"))
                {
                    director.ReportNoise(player.transform.position, bareNoiseLoudness);
                }
            }

            EditorGUILayout.LabelField("Source", director.SourceStatus);
            EditorGUILayout.LabelField("Gate", director.RawLoudness.ToString("F4") + " raw RMS → " +
                                       director.CurrentLoudness.ToString("F3") + " loudness");
        }

        void DrawFreeze()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Arc freeze", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("The wave is photographed, not chased: freezing holds the arc clock where it is, " +
                                    "and the step buttons move it on by hand. A ping emitted while frozen is born at " +
                                    "the frozen instant and stays there — that is how a screenshot of a wave gets " +
                                    "compared against another one.", MessageType.None);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                bool frozen = GUILayout.Toggle(director.IsFrozen, director.IsFrozen ? "Frozen — release" : "Freeze the arcs");
                if (frozen != director.IsFrozen)
                {
                    if (frozen)
                    {
                        director.FreezeArcs();
                    }
                    else
                    {
                        director.UnfreezeArcs();
                    }
                }

                using (new EditorGUI.DisabledScope(!director.IsFrozen))
                {
                    EditorGUILayout.BeginHorizontal();
                    DrawStep("step 1 frame", 1f / 60f);
                    DrawStep("step 0.05 s", 0.05f);
                    DrawStep("step one arc interval", director.Profile != null ? director.Profile.arcInterval : 0.12f);
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.LabelField("Arc clock", director.ArcClock.ToString("F3") +
                                       (director.IsFrozen ? " s (held)" : " s"));
        }

        void DrawStep(string label, float seconds)
        {
            if (GUILayout.Button(label))
            {
                director.StepFrozenArcs(seconds);
            }
        }

        void DrawPingInspector()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Pings in the pool", EditorStyles.boldLabel);

            pings.Clear();
            IReadOnlyList<PingRecord> pool = director.PoolForDebugger;
            for (int i = 0; i < pool.Count; i++)
            {
                pings.Add(pool[i]);
            }

            if (followLatest)
            {
                PingRecord latest = null;
                for (int i = 0; i < pings.Count; i++)
                {
                    PingRecord candidate = pings[i];

                    // The seed, not the emit time: the director's seed counter is monotonic, and two pings
                    // emitted inside one editor frame (a slider dragged, a step button double-tapped) share
                    // an arc clock and would be indistinguishable by time.
                    if (candidate != null && candidate.alive &&
                        (latest == null || candidate.seed > latest.seed))
                    {
                        latest = candidate;
                    }
                }

                if (latest != null)
                {
                    selected = latest;
                }
            }

            followLatest = GUILayout.Toggle(followLatest, "Follow the newest live ping");

            int index = selected != null ? pings.IndexOf(selected) : -1;
            string[] names = new string[pings.Count];
            for (int i = 0; i < pings.Count; i++)
            {
                PingRecord ping = pings[i];
                names[i] = ping == null ? "empty"
                    : (ping.alive ? "live" : "dead") + "  seed " + ping.seed +
                      "  L " + ping.loudness.ToString("F2") +
                      "  " + (ping.fullCircle ? "ring" : "cone") +
                      "  " + ping.rayCount + " rays";
            }

            if (index >= 0)
            {
                int picked = GUILayout.SelectionGrid(index, names, 1);
                if (picked >= 0 && picked != index)
                {
                    selected = pings[picked];
                    followLatest = false;
                }
            }
            else
            {
                for (int i = 0; i < names.Length; i++)
                {
                    EditorGUILayout.LabelField(names[i], EditorStyles.miniLabel);
                }
            }

            if (selected == null)
            {
                EditorGUILayout.HelpBox("Nothing has been emitted yet.", MessageType.Info);
                return;
            }

            EchoTuningProfile profile = director.Profile;
            float age = director.ArcClock - selected.emitTime;
            float train = profile != null ? selected.TrainSeconds(profile) : 0f;

            EditorGUILayout.LabelField("Age", age.ToString("F3") + " s of " + train.ToString("F3") +
                                       " s train, " + (train - age).ToString("F3") + " s left");
            EditorGUILayout.LabelField("Carrier radius", ArcMath.BaseRadius(selected.radius,
                                       profile != null ? profile.waveSpeed : 0f, age).ToString("F2") +
                                       " m of " + selected.radius.ToString("F2") + " m measured");
            EditorGUILayout.LabelField("Samples", selected.rayCount + " cast, " +
                                       selected.ValidSampleCount + " hit, " +
                                       (selected.rayCount - selected.ValidSampleCount) + " empty (no information)");
            EditorGUILayout.LabelField("Distances", selected.dMin.ToString("F2") + " → " +
                                       selected.dMax.ToString("F2") + " m");
            EditorGUILayout.LabelField("Allocation", BufferKilobytes(selected) + " KB per record × " +
                                       pings.Count + " in the pool, capacity " +
                                       (profile != null ? profile.CapacityRays : 0) + " rays");

            if (field != null)
            {
                float usage = field.LastQuadCount / (float)EchoRenderer.MaxQuads;
                EditorGUILayout.LabelField("Quads", field.LastQuadCount + " / " + EchoRenderer.MaxQuads +
                                           " (" + (usage * 100f).ToString("F0") + "%)");
                if (field.OverflowCount > 0)
                {
                    EditorGUILayout.HelpBox(field.OverflowCount + " quads were dropped this frame: the arc buffer " +
                                           "overflowed, which draws as a wave with pieces missing, not an error.",
                                           MessageType.Warning);
                }
            }
        }

        static string BufferKilobytes(PingRecord ping)
        {
            int capacity = ping.d != null ? ping.d.Length : 0;

            // floats + one SurfaceKind enum (4 bytes) + one bool per sample, the whole pooled record.
            float bytes = capacity * (4f * 4f /* d, amp, brightness, curvature */ + 4f /* kind */ + 1f /* broken */)
                          + 64f;
            return (bytes / 1024f).ToString("F1");
        }

        void DrawPlots()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("The distance profile behind the arc", EditorStyles.boldLabel);

            if (selected == null)
            {
                return;
            }

            EchoTuningProfile profile = director.Profile;
            float reach = selected.radius > 0f ? selected.radius : (profile != null ? profile.maxRadius : 6.5f);

            DrawPlot("d[i]  hit distance (m)", selected.d, selected.rayCount, 0f, reach, Color.cyan);
            DrawPlot("amp[i]  amplitude (1 = drawn, 0 = absorber or no hit)", selected.amp, selected.rayCount,
                     0f, 1f, Color.white);
            DrawPlot("b[i]  brightness from curvature", selected.brightness, selected.rayCount, 0f, 1f,
                     new Color(1f, 0.85f, 0.3f));
            DrawPlot("curvature (normalised, floor " + (profile != null ? profile.curvatureFloor.ToString("F1") : "?") +
                     " → range " + (profile != null ? profile.curvatureRange.ToString("F1") : "?") + ")",
                     selected.curvature, selected.rayCount, 0f, profile != null ? profile.curvatureRange : 6f,
                     new Color(0.9f, 0.4f, 0.9f));

            EditorGUILayout.LabelField("θ", "sample 0 is " +
                                       (selected.fullCircle ? "the aim line of a full ring" : "one edge of the cone") +
                                       ", " + (selected.rayCount > 1 ? selected.coneHalfRad * 2f * Mathf.Rad2Deg / (selected.rayCount - 1) : 0f).ToString("F2") +
                                       "° per sample");
        }

        void DrawPlot(string label, float[] values, int count, float min, float max, Color color)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
            if (values == null || count <= 0)
            {
                return;
            }

            const float height = 44f;
            Rect rect = GUILayoutUtility.GetRect(1f, height, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.14f, 1f));

            float span = Mathf.Max(1e-5f, max - min);
            float step = rect.width / count;
            float infinity = 0f;
            int drawn = 0;

            for (int i = 0; i < count; i++)
            {
                float value = values[i];
                if (float.IsPositiveInfinity(value) || float.IsNaN(value))
                {
                    infinity++;
                    continue;
                }

                float normalised = Mathf.Clamp01((value - min) / span);
                float bar = Mathf.Max(1f, normalised * (height - 2f));
                var column = new Rect(rect.x + i * step, rect.yMax - bar, Mathf.Max(1f, step), bar);
                EditorGUI.DrawRect(column, color);
                drawn++;
            }

            // The left edge of the plot is the edge of the cone, so a shape can be read against the axis.
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, height), new Color(1f, 1f, 1f, 0.3f));

            var text = new GUIContent(drawn + " samples, " + infinity + " empty");
            Vector2 size = EditorStyles.miniLabel.CalcSize(text);
            GUI.Label(new Rect(rect.xMax - size.x - 4f, rect.y + 1f, size.x, 14f), text, EditorStyles.miniLabel);
        }

        void DrawSceneViewOptions()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scene view", EditorStyles.boldLabel);
            showRawRays = EditorGUILayout.ToggleLeft(
                "Raw rays of the selected ping (green = hit, grey = nothing there, amber = a creature)", showRawRays);
            showCarrierCircle = EditorGUILayout.ToggleLeft(
                "Measured reach circle, and the arc's current radius", showCarrierCircle);
            crawlers = RefreshListeners();
        }

        /// <summary>
        /// The things that can hear, refound at most twice a second: the noise log asks "who could hear
        /// that" for every event, and a scene query per event is a cost the measurement tool should not
        /// add to the thing it measures. The player comes along because the bare-noise button wants their
        /// position, and both can be destroyed by a scene rebuild between two repaints.
        /// </summary>
        Crawler[] RefreshListeners()
        {
            if (EditorApplication.timeSinceStartup >= crawlerRefreshAt)
            {
                crawlerRefreshAt = EditorApplication.timeSinceStartup + 0.5;
                crawlers = UnityEngine.Object.FindObjectsByType<Crawler>(FindObjectsInactive.Exclude);
                player = UnityEngine.Object.FindAnyObjectByType<Player>();
            }

            return crawlers;
        }

        void DrawNoiseLog()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Noise events (the stream week 3's NoiseBoard takes over)", EditorStyles.boldLabel);

            noisePaused = GUILayout.Toggle(noisePaused, noisePaused ? "Paused — resume" : "Pause the log");
            if (GUILayout.Button("Clear"))
            {
                noise.Clear();
            }

            if (noise.Count == 0)
            {
                EditorGUILayout.LabelField("No sound yet this run.");
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("t", EditorStyles.miniBoldLabel, GUILayout.Width(46f));
            EditorGUILayout.LabelField("loudness", EditorStyles.miniBoldLabel, GUILayout.Width(60f));
            EditorGUILayout.LabelField("position", EditorStyles.miniBoldLabel, GUILayout.Width(100f));
            EditorGUILayout.LabelField("heard by", EditorStyles.miniBoldLabel, GUILayout.Width(58f));
            EditorGUILayout.LabelField("wave", EditorStyles.miniBoldLabel, GUILayout.Width(40f));
            EditorGUILayout.EndHorizontal();

            int from = Mathf.Max(0, noise.Count - 14);
            for (int i = noise.Count - 1; i >= from; i--)
            {
                NoiseEvent entry = noise[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(entry.arcClock.ToString("F2"), GUILayout.Width(46f));
                EditorGUILayout.LabelField(entry.loudness.ToString("F3"), GUILayout.Width(60f));
                EditorGUILayout.LabelField("(" + entry.at.x.ToString("F1") + ", " + entry.at.y.ToString("F1") + ")",
                                         GUILayout.Width(100f));
                EditorGUILayout.LabelField(entry.heardBy + " / " + crawlers.Length, GUILayout.Width(58f));
                EditorGUILayout.LabelField(entry.drewWave ? "yes" : "—", GUILayout.Width(40f));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.LabelField(noise.Count + " events this run. A footstep should be inaudible across " +
                                       "the room; a shout should be heard by everything in it. Both of those " +
                                       "are rho, and this column is where rho is judged.", EditorStyles.wordWrappedMiniLabel);
        }

        void DrawTuning()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tuning asset in force", EditorStyles.boldLabel);

            EchoTuningProfile profile = director.Profile;
            EditorGUILayout.ObjectField(profile, typeof(EchoTuningProfile), false);

            using (new EditorGUI.DisabledScope(profile == null))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("gainSign +1 (nearer sits closer)"))
                {
                    WriteToProfile(profile, p => p.gainSign = 1f);
                }

                if (GUILayout.Button("gainSign -1 (looks emitted)"))
                {
                    WriteToProfile(profile, p => p.gainSign = -1f);
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField("gainSign is " + (profile != null ? profile.gainSign.ToString("+0;-0") : "-") +
                                           ". The two buttons write the asset, so the flip survives a scene reload and " +
                                           "the answer can be recorded in PLAYTEST_REPORT.md instead of remembered.",
                                           EditorStyles.wordWrappedMiniLabel);
            }

            if (profile != null)
            {
                EditorGUILayout.LabelField("Every field on the profile", EditorStyles.miniBoldLabel);
                Editor.CreateCachedEditor(profile, null, ref profileEditor);
                profileEditor.OnInspectorGUI();
            }
        }

        static void WriteToProfile(EchoTuningProfile profile, System.Action<EchoTuningProfile> write)
        {
            if (profile == null)
            {
                return;
            }

            Undo.RecordObject(profile, "Echo tuning change from the debug window");
            write(profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        void OnSceneGui(SceneView view)
        {
            if (director == null || selected == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            EchoTuningProfile profile = director.Profile;

            if (showRawRays)
            {
                for (int i = 0; i < selected.rayCount; i++)
                {
                    float angle = ArcMath.SampleAngle(selected, i);
                    float distance = selected.d[i];
                    Color color = ColorForKind(selected.kind[i]);
                    Vector3 end = float.IsPositiveInfinity(distance)
                        ? (Vector3)ArcMath.Polar(selected.origin, angle, selected.radius)
                        : (Vector3)ArcMath.Polar(selected.origin, angle, distance);

                    Handles.color = float.IsPositiveInfinity(distance) ? Color.gray : color;
                    Handles.DrawLine((Vector3)selected.origin, end, 1f);
                }
            }

            if (showCarrierCircle && profile != null)
            {
                float age = director.ArcClock - selected.emitTime;
                DrawCircle(selected.origin, selected.radius, new Color(0.3f, 0.9f, 1f, 0.25f));

                float carrier = ArcMath.BaseRadius(selected.radius, profile.waveSpeed, age);
                if (ArcMath.IsAlive(carrier, profile.rKill))
                {
                    DrawCircle(selected.origin, carrier, new Color(0.3f, 0.9f, 1f, 0.8f));
                }

                // What the enemy heard from this same sound: the distance at which rho says a creature
                // turns toward you. Drawing it over the reach is the only way to see whether the pair of
                // numbers is a game at all.
                DrawCircle(selected.origin, ArcMath.HearingRadius(selected.loudness, profile),
                           new Color(1f, 0.35f, 0.25f, 0.6f));
            }
        }

        static void DrawCircle(Vector2 centre, float radius, Color color)
        {
            if (radius <= 0f)
            {
                return;
            }

            Handles.color = color;
            Handles.DrawWireArc(new Vector3(centre.x, centre.y, 0f), Vector3.forward, Vector3.up, 360f, radius);
        }

        static Color ColorForKind(SurfaceKind kind)
        {
            switch (kind)
            {
                case SurfaceKind.Crawler: return new Color(1f, 0.7f, 0.2f, 0.9f);
                case SurfaceKind.Absorber: return new Color(0.4f, 0.4f, 0.45f, 0.9f);
                case SurfaceKind.Spike: return new Color(1f, 0.35f, 0.35f, 0.9f);
                case SurfaceKind.None: return new Color(0.6f, 0.6f, 0.6f, 0.5f);
                default: return new Color(0.35f, 1f, 0.55f, 0.85f);
            }
        }
    }
}
