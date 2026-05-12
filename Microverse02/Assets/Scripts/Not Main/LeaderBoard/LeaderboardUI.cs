using TMPro;
using UnityEngine;

public class LeaderboardUI : MonoBehaviour
{
    [SerializeField] TMP_Text leaderboardText;

    async void OnEnable()
    {
        await Refresh();
    }

    public async System.Threading.Tasks.Task Refresh()
    {
        if (leaderboardText == null) return;

        leaderboardText.text = "Loading...";

        var scores = await UGSLeaderboardManager.Instance.GetTopScores(5);

        if (scores == null)
        {
            leaderboardText.text = "Offline";
            return;
        }

        leaderboardText.text = "GLOBAL TOP 5\n\n";

        foreach (var entry in scores.Results)
        {
            leaderboardText.text += $"{entry.Rank + 1}. {entry.PlayerName}  {entry.Score}\n";
        }
    }
}