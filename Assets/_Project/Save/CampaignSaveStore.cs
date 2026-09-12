using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Access to the existing Campaign files; never captures disposable session managers.</summary>
public static class CampaignSaveStore
{
    public static bool ProtectedSession => GameSaveManager.IsPersistenceSuspended ||
        MultiplayerRestockBridge.IsActive || SceneManager.GetSceneByName("Lobby1 Multiplayer").isLoaded ||
        SceneManager.GetSceneByName("Lobby1Tutorial").isLoaded;
    public static bool RuntimeCampaign => !ProtectedSession && SceneManager.GetSceneByName("Lobby1").isLoaded;
    public static bool AtMenu => !ProtectedSession && !RuntimeCampaign &&
        (SceneManager.GetActiveScene().name == "NewMainMenu" ||
         SceneManager.GetActiveScene().name == "NewGameMenu");
    public static bool NeedsReload { get; set; }
    public static bool IsCreditPending { get; set; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime()
    {
        NeedsReload = false;
        IsCreditPending = false;
        cachedStamp = null;
    }
    public static string SavePath => GameSaveManager.Instance != null
        ? GameSaveManager.Instance.CampaignSavePath : Path.Combine(Application.persistentDataPath, "dinein_save.json");
    public static string CheckpointPath => Path.Combine(Path.GetDirectoryName(SavePath),
        Path.GetFileNameWithoutExtension(SavePath) + "_day_start.json");

    [Serializable]
    public sealed class Snapshot
    {
        public int version = 1;
        public string save;
        public string checkpoint;
        public string hash;
    }

    public static Snapshot Read()
    {
        RecoverPendingWrite();
        var result = new Snapshot
        {
            save = File.Exists(SavePath) ? File.ReadAllText(SavePath) : null,
            checkpoint = File.Exists(CheckpointPath) ? File.ReadAllText(CheckpointPath) : null
        };
        result.hash = Hash(result.save, result.checkpoint);
        return result;
    }

    public static string Hash(string save, string checkpoint)
    {
        using var sha = SHA256.Create();
        // Length-prefix the two documents so their boundary is unambiguous.
        return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(
            (save ?? "").Length + ":" + (save ?? "") + (checkpoint ?? "").Length + ":" + (checkpoint ?? ""))));
    }

    public static bool Valid(Snapshot snapshot)
    {
        try
        {
            if (snapshot == null || snapshot.version != 1 || string.IsNullOrEmpty(snapshot.save) ||
                snapshot.hash != Hash(snapshot.save, snapshot.checkpoint)) return false;
            return ValidSave(snapshot.save) && (string.IsNullOrEmpty(snapshot.checkpoint) || ValidSave(snapshot.checkpoint));
        }
        catch { return false; }
    }

    private static bool ValidSave(string json)
    {
        if (!json.Contains("\"money\"") || !json.Contains("\"currentDay\"")) return false;
        var data = JsonUtility.FromJson<GameSaveData>(json);
        return data != null && data.money >= 0 && data.currentDay >= 1 && data.saveSchemaVersion <= 3;
    }

    public static void AtomicWrite(string path, string value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp = path + ".tmp";
        File.WriteAllText(temp, value);
        if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
        else File.Move(temp, path);
    }

    public static void Install(Snapshot snapshot)
    {
        if (!AtMenu || IsCreditPending || !Valid(snapshot)) throw new InvalidOperationException("Campaign import is not safe here.");
        // Keep the complete previous pair before touching either existing file.
        AtomicWrite(SavePath + ".before_cloud.json", JsonUtility.ToJson(Read()));
        WritePair(snapshot);
        NeedsReload = true;
    }

    private static void WritePair(Snapshot snapshot)
    {
        // A journal makes the two-file update recoverable after a crash between writes.
        snapshot.hash = Hash(snapshot.save, snapshot.checkpoint);
        AtomicWrite(SavePath + ".pending_pair.json", JsonUtility.ToJson(snapshot));
        RecoverPendingWrite();
        cachedStamp = null;
    }

    public static void RecoverPendingWrite()
    {
        string journal = SavePath + ".pending_pair.json";
        if (!File.Exists(journal) || ProtectedSession) return;
        Snapshot snapshot = JsonUtility.FromJson<Snapshot>(File.ReadAllText(journal));
        if (!Valid(snapshot)) throw new InvalidDataException("Campaign update journal is invalid; files preserved.");
        AtomicWrite(SavePath, snapshot.save);
        if (!string.IsNullOrEmpty(snapshot.checkpoint)) AtomicWrite(CheckpointPath, snapshot.checkpoint);
        else if (File.Exists(CheckpointPath)) File.Delete(CheckpointPath);
        File.Delete(journal);
        NeedsReload = true;
    }

    private static string cachedStamp;
    private static int cachedMoney = 5000;
    public static int Money
    {
        get
        {
            if (RuntimeCampaign && !NeedsReload && MoneyManager.Instance != null)
                return MoneyManager.Instance.Money;
            try
            {
                string path = File.Exists(CheckpointPath) ? CheckpointPath : SavePath;
                string stamp = path + File.GetLastWriteTimeUtc(path).Ticks + (File.Exists(path) ? new FileInfo(path).Length : 0);
                if (stamp != cachedStamp)
                {
                    cachedMoney = File.Exists(path) ? JsonUtility.FromJson<GameSaveData>(File.ReadAllText(path)).money : 5000;
                    cachedStamp = stamp;
                }
            }
            catch (Exception e) { Debug.LogWarning("[Campaign] Cannot read wallet: " + e.Message); }
            return cachedMoney;
        }
    }

    public static bool ChangeMoney(int delta)
    {
        if (ProtectedSession) return false;
        if (RuntimeCampaign && !NeedsReload && MoneyManager.Instance != null)
        {
            if (delta == int.MinValue) return false;
            if (delta < 0) return MoneyManager.Instance.Spend(-delta, "Campaign menu purchase");
            MoneyManager.Instance.Earn(delta, "Campaign menu credit");
            return true;
        }
        if (!AtMenu || IsCreditPending) return false;
        try
        {
            var snapshot = Read();
            if (!string.IsNullOrEmpty(snapshot.save) && !Valid(snapshot)) return false;
            string current = !string.IsNullOrEmpty(snapshot.checkpoint) ? snapshot.checkpoint : snapshot.save;
            var data = string.IsNullOrEmpty(current) ? new GameSaveData() : JsonUtility.FromJson<GameSaveData>(current);
            long balance = (long)data.money + delta;
            if (balance < 0 || balance > int.MaxValue) return false;
            data.money = (int)balance;
            string json = JsonUtility.ToJson(data, true);
            WritePair(new Snapshot { save = json,
                checkpoint = !string.IsNullOrEmpty(snapshot.checkpoint) ? json : null });
            NeedsReload = true;
            return true;
        }
        catch (Exception e) { Debug.LogError("[Campaign] Local wallet save failed: " + e.Message); return false; }
    }

    [Serializable] private sealed class Credit { public string id, account; public int amount; }
    [Serializable] private sealed class Binding { public string account; }
    public static bool AccountMatches(string account)
    {
        string path = SavePath + ".cloud_state.json";
        if (!File.Exists(path)) return true;
        var binding = JsonUtility.FromJson<Binding>(File.ReadAllText(path));
        return binding != null && (string.IsNullOrEmpty(binding.account) || binding.account == account);
    }
    public static void QueueConfirmedCredit(string account, int amount, string receipt)
    {
        string path = Path.Combine(Application.persistentDataPath, "campaign_credits");
        Directory.CreateDirectory(path);
        AtomicWrite(Path.Combine(path, receipt + ".json"),
            JsonUtility.ToJson(new Credit { id = receipt, account = account, amount = amount }));
        string binding = SavePath + ".cloud_state.json";
        if (!File.Exists(binding)) AtomicWrite(binding, JsonUtility.ToJson(new Binding { account = account }));
        IsCreditPending = false;
        ApplyConfirmedCredits();
    }
    public static void ApplyConfirmedCredits()
    {
        var auth = PlayFabAuthManager.Instance;
        if (!AtMenu || auth == null || !auth.IsLoggedIn || !AccountMatches(auth.PlayFabId)) return;
        string path = Path.Combine(Application.persistentDataPath, "campaign_credits");
        if (!Directory.Exists(path)) return;
        foreach (string file in Directory.GetFiles(path, "*.json"))
        {
            var credit = JsonUtility.FromJson<Credit>(File.ReadAllText(file));
            if (credit == null || credit.account != auth.PlayFabId || credit.amount <= 0) continue;
            var snapshot = Read();
            if (!string.IsNullOrEmpty(snapshot.save) && !Valid(snapshot))
                throw new InvalidDataException("Campaign is invalid; confirmed credit preserved for recovery.");
            string json = !string.IsNullOrEmpty(snapshot.checkpoint) ? snapshot.checkpoint : snapshot.save;
            var data = string.IsNullOrEmpty(json) ? new GameSaveData() : JsonUtility.FromJson<GameSaveData>(json);
            data.campaignCreditReceipts ??= new System.Collections.Generic.List<string>();
            if (data.campaignCreditReceipts.Contains(credit.id)) { File.Delete(file); continue; }
            if ((long)data.money + credit.amount > int.MaxValue) continue;
            data.money += credit.amount;
            data.campaignCreditReceipts.Add(credit.id);
            json = JsonUtility.ToJson(data, true);
            WritePair(new Snapshot { save = json,
                checkpoint = !string.IsNullOrEmpty(snapshot.checkpoint) ? json : null });
            File.Delete(file);
            NeedsReload = true;
        }
    }
}
