using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed partial class MultiplayerRunRecords
{
    private readonly Dictionary<string, int> seenSolo = new Dictionary<string, int>(StringComparer.Ordinal);
    private bool preserveUnreadableSolo, soloStorageDirty;
    private float soloStorageRetryAt;
    private static string SoloPath => Path.Combine(Application.persistentDataPath, "solo_results_v1.json");
    private void LoadSoloOutbox()
    {
        if (!File.Exists(SoloPath)) return;
        Outbox pending = null;
        try { pending = ReadSoloOutbox(SoloPath); }
        catch (Exception)
        {
            try
            {
                if (File.Exists(SoloPath + ".bak")) pending = ReadSoloOutbox(SoloPath + ".bak");
                File.Copy(SoloPath, SoloPath + ".unreadable." + DateTime.UtcNow.Ticks);
            }
            catch (Exception) { preserveUnreadableSolo = true; }
            Status = "Solo result storage needs recovery; original file preserved.";
        }
        if (pending != null)
        {
            outbox.pending.AddRange(pending.pending);
            foreach (var item in pending.pending)
            {
                string key = item.accountId + ":" + item.solo.restaurant + ":" + item.solo.runId;
                seenSolo[key] = Math.Max(seenSolo.TryGetValue(key, out int old) ? old : 0, item.solo.completedDays);
            }
        }
    }
    private static Outbox ReadSoloOutbox(string path)
    {
        var value = JsonUtility.FromJson<Outbox>(File.ReadAllText(path));
        if (!ValidOutbox(value) || value.pending.Exists(p => p.solo == null))
            throw new InvalidDataException("Invalid solo result outbox");
        return value;
    }
    private void RetrySoloStorage()
    {
        if (outbox != null && soloStorageDirty && !preserveUnreadableSolo && Time.realtimeSinceStartup >= soloStorageRetryAt)
            SaveSoloOutbox();
    }
    private void SaveSoloOutbox()
    {
        soloStorageDirty = true;
        soloStorageRetryAt = Time.realtimeSinceStartup + 30f;
        try
        {
            if (preserveUnreadableSolo) return;
            CampaignSaveStore.AtomicWrite(SoloPath, JsonUtility.ToJson(new Outbox {
                pending = outbox.pending.FindAll(p => p.solo != null) }));
            soloStorageDirty = false;
        }
        catch (Exception) { Status = "Solo receipt is in memory; local storage retry pending."; }
    }
    public static void RecordSolo(string accountId, SoloRunRecord record)
    {
        try
        {
            if (instance?.outbox == null || string.IsNullOrEmpty(accountId) || !SoloRunRecord.Valid(record)) return;
            string key = accountId + ":" + record.restaurant + ":" + record.runId;
            if (instance.seenSolo.TryGetValue(key, out int seen) && seen >= record.completedDays) return;
            var pending = instance.outbox.pending.Find(p => p.solo != null && p.accountId == accountId &&
                p.solo.restaurant == record.restaurant && p.solo.runId == record.runId);
            if (pending == null)
            {
                pending = new Pending { accountId = accountId };
                instance.outbox.pending.Add(pending);
            }
            if (pending.solo == null || pending.solo.completedDays < record.completedDays)
                pending.solo = JsonUtility.FromJson<SoloRunRecord>(JsonUtility.ToJson(record));
            instance.seenSolo[key] = pending.solo.completedDays;
            instance.SaveSoloOutbox();
            instance.nextAttempt = Mathf.Min(instance.nextAttempt, Time.realtimeSinceStartup + 1f);
        }
        catch (Exception error)
        {
            // Keep campaign saving and day transitions independent of reporting storage.
            Status = "Solo result pending: " + error.Message;
        }
    }
}
