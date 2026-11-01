using System;
using UnityEngine;

public partial class GameFlowManager
{
    private SoloRankingV1 soloRanking;
    private string rankingAccount, rankingScene;
    private bool rankingServiceStarted;
    private bool RankingContext => !CampaignSaveStore.ProtectedSession &&
        CampaignSaveStore.RestaurantScene == rankingScene && restaurantSceneName == rankingScene &&
        !string.IsNullOrEmpty(rankingAccount) && CampaignSaveStore.ReadAccountState().account == rankingAccount;

    // Reporting must never throw into gameplay, reset, load or campaign persistence.
    private void RankingSafely(Action operation)
    {
        try { operation(); }
        catch (Exception error) { Debug.LogWarning("[Solo ranking] Reporting deferred: " + error.Message); }
    }
    public void BeginSoloRankingRun() => RankingSafely(() => {
        if (CampaignSaveStore.ProtectedSession) return;
        soloRanking = null; rankingAccount = rankingScene = null; rankingServiceStarted = false;
        rankingAccount = CampaignSaveStore.ReadAccountState().account;
        rankingScene = CampaignSaveStore.RestaurantScene;
        soloRanking = string.IsNullOrEmpty(rankingAccount) ? null : SoloRankingV1.NewRun();
        rankingServiceStarted = false;
    });
    private void LoadSoloRanking(GameSaveData data) => RankingSafely(() => {
        if (CampaignSaveStore.ProtectedSession) return;
        soloRanking = null; rankingAccount = rankingScene = null; rankingServiceStarted = false;
        rankingAccount = CampaignSaveStore.ReadAccountState().account;
        rankingScene = CampaignSaveStore.RestaurantScene;
        soloRanking = string.IsNullOrEmpty(rankingAccount) ? null : SoloRankingV1.Copy(data.soloRankingV1);
        rankingServiceStarted = false;
    });
    private void CaptureSoloRanking(GameSaveData data) => RankingSafely(() => {
        if (RankingContext) data.soloRankingV1 = SoloRankingV1.Copy(soloRanking);
    });
    private void StartSoloRankingService() => RankingSafely(() => {
        rankingServiceStarted = RankingContext && SoloRankingV1.Valid(soloRanking);
    });
    private void SettleSoloRankingDay() => RankingSafely(() => {
        if (rankingServiceStarted && RankingContext) SoloRankingV1.Settle(soloRanking);
        rankingServiceStarted = false;
    });
    private void AdvanceSoloRankingDay() => RankingSafely(() => {
        if (restaurantSessionState == RestaurantSessionState.DayComplete && RankingContext)
            SoloRankingV1.Advance(soloRanking);
        rankingServiceStarted = false;
    });
    // Only call with a save known to have persisted successfully, or a validated load.
    public void RecoverPersistedSoloRanking(GameSaveData data) => RankingSafely(() => {
        if (!RankingContext || !SoloRankingV1.Valid(soloRanking) ||
            !SoloRankingV1.Valid(data?.soloRankingV1) || data.soloRankingV1.runId != soloRanking.runId) return;
        var record = SoloRunRecord.FromProgress(data.soloRankingV1, rankingScene, Application.version);
        if (record != null) MultiplayerRunRecords.RecordSolo(rankingAccount, record);
    });
}
