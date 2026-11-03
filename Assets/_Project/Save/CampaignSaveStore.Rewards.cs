using System;
using System.IO;
using UnityEngine;

public static partial class CampaignSaveStore
{
    public static void QueueDailyRewardCash(string account, string restaurant, int amount, string receipt)
    {
        if (!AccountMatches(account) || amount <= 0 || string.IsNullOrEmpty(receipt))
            throw new InvalidOperationException("The reward does not belong to the active account.");
        // Reuse menu/login credit recovery without clearing unrelated pending purchase state.
        AtomicWrite(Path.Combine(Application.persistentDataPath, "campaign_credits", receipt + ".json"),
            JsonUtility.ToJson(new Credit { id = receipt, account = account, amount = amount, restaurant = restaurant }));
    }

    public static bool ApplyDailyRewardCash(string account, string restaurant, int amount, string receipt)
    {
        if (amount <= 0 || string.IsNullOrEmpty(receipt) || !AccountMatches(account) ||
            (restaurant != "Lobby1" && restaurant != "Lobby2" && restaurant != "Lobby3")) return false;
        bool active = RuntimeCampaign && RestaurantScene == restaurant;
        // Commit the completed shift, not its rollback checkpoint, before adding a prize.
        if (active && (GameSaveManager.Instance == null || !GameSaveManager.Instance.CommitRestaurantResults())) return false;
        string name = restaurant == "Lobby3" ? "dinein_finedining_save.json" : restaurant == "Lobby2" ? "dinein_fastfood_save.json" : "dinein_save.json";
        var pair = ReadCareer(LocalDirectory, name);
        if (!string.IsNullOrEmpty(pair.save) && !Valid(pair)) return false;
        GameSaveData data = string.IsNullOrEmpty(pair.save) ? new GameSaveData {
            money = MoneyManager.DefaultStartingMoney, polishDayStartMoney = MoneyManager.DefaultStartingMoney
        } : JsonUtility.FromJson<GameSaveData>(pair.save);
        bool alreadyApplied = data.campaignCreditReceipts?.Contains(receipt) == true;
        pair.save = ApplyRewardToDocument(data, amount, receipt);
        if (!string.IsNullOrEmpty(pair.checkpoint))
            pair.checkpoint = ApplyRewardToDocument(JsonUtility.FromJson<GameSaveData>(pair.checkpoint), amount, receipt);
        pair.hash = Hash(pair.save, pair.checkpoint);
        string path = Path.Combine(LocalDirectory, name);
        AtomicWrite(path + ".pending_pair.json", JsonUtility.ToJson(pair));
        ReadCareer(LocalDirectory, name); // Complete/recover the existing two-file journal.
        cachedStamp = null;
        if (active && !alreadyApplied && MoneyManager.Instance != null)
            MoneyManager.Instance.SetMoney(data.money, "Daily reward");
        NotifyLocalWrite();
        return true;
    }

    private static string ApplyRewardToDocument(GameSaveData data, int amount, string receipt)
    {
        data.campaignCreditReceipts ??= new System.Collections.Generic.List<string>();
        if (!data.campaignCreditReceipts.Contains(receipt))
        {
            data.money = checked(data.money + amount);
            data.polishDayStartMoney = checked(data.polishDayStartMoney + amount);
            data.campaignCreditReceipts.Add(receipt);
        }
        return JsonUtility.ToJson(data, true);
    }
}
