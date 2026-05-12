using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Leaderboards;

public class UGSLeaderboardManager : MonoBehaviour
{
    public static UGSLeaderboardManager Instance { get; private set; }

    [SerializeField] string leaderboardId = "global_score";

    public bool IsReady { get; private set; }

    async void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        await Initialise();
    }

    async Task Initialise()
    {
        try
        {
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            IsReady = true;
            Debug.Log("UGS Leaderboard ready.");
        }
        catch (Exception e)
        {
            IsReady = false;
            Debug.LogWarning("UGS failed: " + e.Message);
        }
    }

    public async Task SubmitScore(int score)
    {
        if (!IsReady) return;

        try
        {
            await LeaderboardsService.Instance.AddPlayerScoreAsync(leaderboardId, score);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Submit score failed: " + e.Message);
        }
    }

    public async Task<Unity.Services.Leaderboards.Models.LeaderboardScoresPage> GetTopScores(int limit = 5)
    {
        if (!IsReady) return null;

        try
        {
            return await LeaderboardsService.Instance.GetScoresAsync(
                leaderboardId,
                new GetScoresOptions { Limit = limit }
            );
        }
        catch (Exception e)
        {
            Debug.LogWarning("Load scores failed: " + e.Message);
            return null;
        }
    }
}