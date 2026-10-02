using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

namespace DineIn.Appearance
{
    /// <summary>Existing cosmetic adapter. Account snapshots own upload ordering;
    /// the old UserData key is read for migration, never a competing writer.</summary>
    [RequireComponent(typeof(PlayFabAuthManager))]
    public sealed class PlayFabAppearanceAdapter : MonoBehaviour
    {
        private const string Key = "CustomizationV1";
        private PlayFabAuthManager auth;
        private void OnEnable()
        {
            auth = GetComponent<PlayFabAuthManager>();
            auth.OnLoginSuccess += LoggedIn; auth.OnLoggedOut += LoggedOut;
            PlayerCustomizationData.AppearanceChanged += Changed;
        }
        private void Start() { if (auth.IsLoggedIn) LoggedIn(); else LoggedOut(); }
        private void OnDisable()
        {
            if (auth != null) { auth.OnLoginSuccess -= LoggedIn; auth.OnLoggedOut -= LoggedOut; }
            PlayerCustomizationData.AppearanceChanged -= Changed;
        }
        private void LoggedIn()
        {
            PlayerCustomizationData.SelectProfile(auth.PlayFabId);
            CampaignCloudSync.RequestSync();
            PhotonCustomizationSync.PushToPhoton();
        }
        private void LoggedOut()
        {
            PlayerCustomizationData.SelectProfile(CampaignSaveStore.ReadAccountState().account);
            PhotonCustomizationSync.PushToPhoton();
        }
        private void Changed() => CampaignSaveStore.NotifyLocalWrite();
        public static void ReadLegacy(PlayFabAuthenticationContext context, Action<string> success, Action<string> failure)
        {
            PlayFabClientAPI.GetUserData(new GetUserDataRequest { AuthenticationContext = context, Keys = new List<string> { Key } },
                result => success(result.Data != null && result.Data.TryGetValue(Key, out var data) ? data.Value : null),
                error => failure(error.ErrorMessage));
        }
        public static void InstallSyncedAppearance(string account, string json)
        {
            if (!string.IsNullOrEmpty(json) && !PlayerCustomizationData.TryReadAppearance(json, out _))
                throw new InvalidOperationException("Invalid saved appearance.");
            PlayerCustomizationData.SelectProfile(account);
            var record = PlayerCustomizationData.AppearanceRecord;
            if (record.json != json) record.revision++;
            record.json = json; record.pendingUpload = false;
            PlayerCustomizationData.LoadFromJson(json);
            PlayerCustomizationData.NotifyCloudAppearanceApplied();
        }
        public static void Acknowledge(string account, string json)
        {
            if (PlayerCustomizationData.AccountId != "playfab:" + account) return;
            var record = PlayerCustomizationData.AppearanceRecord;
            if (record.json != json || !record.pendingUpload) return;
            record.pendingUpload = false; LocalSaveManager.Save();
        }
    }
}
