using UnityEngine;

namespace EchoMaze.Gameplay
{
    /// <summary>
    /// How far the player has come, on disk: which chapters have been survived and the best run of each.
    ///
    /// Keyed by <b>scene name</b>, not by chapter number, so inserting a room in the middle of the story
    /// cannot silently hand a player somebody else's progress. The order of the chapters still lives in
    /// <see cref="GameFlow"/>; this file only answers "was this room finished, and how well".
    ///
    /// PlayerPrefs rather than a save file because the whole game has no other saved state, and one key per
    /// chapter is small enough to read on every menu frame without a cache to keep honest.
    /// </summary>
    public static class ChapterProgress
    {
        const string Prefix = "echo.story.";

        /// <summary>One chapter's record. <see cref="Complete"/> false means the other values mean nothing.</summary>
        public readonly struct Record
        {
            public Record(bool complete, bool hasBest, float seconds, int waves, int retries)
            {
                Complete = complete;
                HasBest = hasBest;
                Seconds = seconds;
                Waves = waves;
                Retries = retries;
            }

            public bool Complete { get; }
            public bool HasBest { get; }
            public float Seconds { get; }
            public int Waves { get; }
            public int Retries { get; }
        }

        static string Key(string scene, string leaf) => Prefix + scene + "." + leaf;

        public static bool IsComplete(string scene) =>
            !string.IsNullOrEmpty(scene) && PlayerPrefs.GetInt(Key(scene, "done"), 0) == 1;

        public static Record Read(string scene)
        {
            if (!IsComplete(scene))
            {
                return new Record(false, false, 0f, 0, 0);
            }

            const float noTime = -1f;
            float best = PlayerPrefs.GetFloat(Key(scene, "best"), noTime);
            bool hasBest = best >= 0f;
            return new Record(true, hasBest, hasBest ? best : 0f,
                              PlayerPrefs.GetInt(Key(scene, "waves"), 0),
                              PlayerPrefs.GetInt(Key(scene, "retries"), 0));
        }

        /// <summary>
        /// Records a finished chapter. The chapter is marked walked on the first completion whatever the
        /// numbers are; the stored run is only replaced when it is faster, so the record on the node is a
        /// best rather than a latest. Returns true when this run set the record (and so is worth a stamp).
        /// </summary>
        public static bool MarkComplete(string scene, int waves, int retries, float seconds)
        {
            if (string.IsNullOrEmpty(scene))
            {
                return false;
            }

            bool already = IsComplete(scene);
            bool improved = !already || !Read(scene).HasBest || seconds < PlayerPrefs.GetFloat(Key(scene, "best"), float.MaxValue);

            PlayerPrefs.SetInt(Key(scene, "done"), 1);
            if (improved)
            {
                PlayerPrefs.SetFloat(Key(scene, "best"), Mathf.Max(0f, seconds));
                PlayerPrefs.SetInt(Key(scene, "waves"), Mathf.Max(0, waves));
                PlayerPrefs.SetInt(Key(scene, "retries"), Mathf.Max(0, retries));
            }

            PlayerPrefs.Save();
            return improved;
        }

        /// <summary>Forgets one chapter. Used by the tests and by nothing in the game.</summary>
        public static void Forget(string scene)
        {
            foreach (string leaf in new[] { "done", "best", "waves", "retries" })
            {
                PlayerPrefs.DeleteKey(Key(scene, leaf));
            }

            PlayerPrefs.Save();
        }

        /// <summary>Forgets every chapter in the flow. The menu offers this; nothing else does.</summary>
        public static void ForgetRun(string[] scenes)
        {
            if (scenes == null)
            {
                return;
            }

            foreach (string scene in scenes)
            {
                Forget(scene);
            }
        }
    }
}
