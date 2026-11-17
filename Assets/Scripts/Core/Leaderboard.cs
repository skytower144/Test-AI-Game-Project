using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chopper
{
    [Serializable]
    public class ScoreEntry
    {
        public string name;
        public int score;
    }

    /// <summary>역대 최고 점수 1~5위를 PlayerPrefs에 저장/조회한다.</summary>
    public static class Leaderboard
    {
        public const int MaxEntries = 5;
        private const string PrefsKey = "Chopper.Leaderboard.v1";

        [Serializable]
        private class SaveData
        {
            public List<ScoreEntry> entries = new List<ScoreEntry>();
        }

        public static List<ScoreEntry> Load()
        {
            var result = new List<ScoreEntry>();
            try
            {
                string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
                if (!string.IsNullOrEmpty(json))
                {
                    SaveData data = JsonUtility.FromJson<SaveData>(json);
                    if (data != null && data.entries != null)
                    {
                        for (int i = 0; i < data.entries.Count; i++)
                        {
                            ScoreEntry entry = data.entries[i];
                            if (entry == null) continue;
                            if (string.IsNullOrEmpty(entry.name)) entry.name = GameSession.DefaultPlayerName;
                            if (entry.score < 0) entry.score = 0;
                            result.Add(entry);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Leaderboard load failed, starting empty: " + e.Message);
                result.Clear();
            }

            // 안정 정렬(삽입 정렬): 동점이면 먼저 기록된 항목이 위.
            for (int i = 1; i < result.Count; i++)
            {
                ScoreEntry key = result[i];
                int j = i - 1;
                while (j >= 0 && result[j].score < key.score)
                {
                    result[j + 1] = result[j];
                    j--;
                }
                result[j + 1] = key;
            }

            if (result.Count > MaxEntries) result.RemoveRange(MaxEntries, result.Count - MaxEntries);
            return result;
        }

        /// <summary>
        /// 현재 점수를 기록하고 갱신된 1~5위 목록을 반환한다.
        /// rank는 현재 점수의 순위(0부터), 순위권 밖이면 -1.
        /// </summary>
        public static List<ScoreEntry> Submit(string playerName, int score, out int rank)
        {
            List<ScoreEntry> entries = Load();

            int index = entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                if (score > entries[i].score)
                {
                    index = i;
                    break;
                }
            }

            entries.Insert(index, new ScoreEntry { name = playerName, score = Mathf.Max(0, score) });
            if (entries.Count > MaxEntries) entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
            rank = index < MaxEntries ? index : -1;

            Save(entries);
            return entries;
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }

        private static void Save(List<ScoreEntry> entries)
        {
            try
            {
                PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(new SaveData { entries = entries }));
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Leaderboard save failed: " + e.Message);
            }
        }
    }
}
