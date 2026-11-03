using System;
using UnityEngine;

public partial class GameFlowManager
{
    private string rewardRunId;
    private int rewardDayOrdinal = 1;
    private void CaptureRewardProgress(GameSaveData data)
    {
        if (CampaignSaveStore.ProtectedSession) return;
        if (string.IsNullOrEmpty(rewardRunId)) rewardRunId = Guid.NewGuid().ToString("N");
        data.dailyRewardRunId = rewardRunId;
        data.dailyRewardDayOrdinal = rewardDayOrdinal;
    }
    private void LoadRewardProgress(GameSaveData data)
    {
        if (CampaignSaveStore.ProtectedSession) return;
        rewardRunId = !string.IsNullOrEmpty(data.dailyRewardRunId) ? data.dailyRewardRunId :
            !string.IsNullOrEmpty(data.soloRankingV1?.runId) ? data.soloRankingV1.runId : Guid.NewGuid().ToString("N");
        rewardDayOrdinal = Mathf.Max(1, data.dailyRewardDayOrdinal > 0 ? data.dailyRewardDayOrdinal : data.currentDay);
    }
    public DailyRewardCompletionContext RewardContext()
    {
        var auth = PlayFabAuthManager.Instance;
        if (auth == null || !auth.IsLoggedIn) return null;
        var session = MultiplayerSessionManager.Instance;
        bool multiplayer = MultiplayerDayBridge.IsActive;
        if (!multiplayer && (!CampaignSaveStore.RuntimeCampaign || string.IsNullOrEmpty(rewardRunId))) return null;
        return new DailyRewardCompletionContext {
            account = auth.PlayFabId, restaurant = CampaignSaveStore.ActiveRestaurantScene,
            mode = multiplayer ? "multiplayer" : "campaign", runId = multiplayer ? session.RunId : rewardRunId,
            day = multiplayer ? CurrentDay : rewardDayOrdinal
        };
    }
}
