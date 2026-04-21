using System.Collections.Generic;
using UnityEngine;

public class HighScoreManager : MonoBehaviour
{
    const int MaxScores = 5;
    const string ScoreKeyPrefix = "HighScore_";

    public void SaveScore(int newScore)
    {
        List<int> scores = LoadScores();

        scores.Add(newScore);
        scores.Sort((a, b) => b.CompareTo(a)); // highest first

        if (scores.Count > MaxScores)
            scores.RemoveRange(MaxScores, scores.Count - MaxScores);

        for (int i = 0; i < MaxScores; i++)
        {
            int value = i < scores.Count ? scores[i] : 0;
            PlayerPrefs.SetInt(ScoreKeyPrefix + i, value);
        }

        PlayerPrefs.Save();
    }

    public List<int> LoadScores()
    {
        List<int> scores = new List<int>();

        for (int i = 0; i < MaxScores; i++)
        {
            if (PlayerPrefs.HasKey(ScoreKeyPrefix + i))
            {
                scores.Add(PlayerPrefs.GetInt(ScoreKeyPrefix + i));
            }
        }

        return scores;
    }
}