using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

namespace DineIn.Appearance
{
    /// <summary>Cosmetic adapter attached to the existing auth owner; no separate save or account system.</summary>
    [RequireComponent(typeof(PlayFabAuthManager))]
    public sealed class PlayFabAppearanceAdapter : MonoBehaviour
    {
        private const string Key = "CustomizationV1"; // Preserve the established UserData key while versioning its payload.
        [SerializeField, Min(5)] private float retrySeconds = 30;
        private PlayFabAuthManager auth;
        private int generation;
        private bool requestPending;
        private void OnEnable()
        {
            auth = GetComponent<PlayFabAuthManager>();
            auth.OnLoginSuccess += LoggedIn; auth.OnLoggedOut += LoggedOut;
            PlayerCustomizationData.AppearanceChanged += PublishPending;
        }
        private void Start() { if (auth.IsLoggedIn) LoggedIn(); }
        private void OnDisable()
        {
            generation++; CancelInvoke(); requestPending = false;
            if (auth != null) { auth.OnLoginSuccess -= LoggedIn; auth.OnLoggedOut -= LoggedOut; }
            PlayerCustomizationData.AppearanceChanged -= PublishPending;
        }
        private bool Current(int token, string account) => isActiveAndEnabled && token == generation && auth != null &&
            auth.IsLoggedIn && account == "playfab:" + auth.PlayFabId && PlayerCustomizationData.AccountId == account;
        private PlayFabAuthenticationContext CaptureContext()
        {
            var source = PlayFabSettings.staticPlayer;
            if (source == null || !source.IsClientLoggedIn() || source.PlayFabId != auth.PlayFabId) return null;
            var context = new PlayFabAuthenticationContext(); context.CopyFrom(source);
            return context;
        }
        private void LoggedOut()
        {
            generation++; requestPending = false; CancelInvoke();
            PlayerCustomizationData.SelectProfile(null); PhotonCustomizationSync.PushToPhoton();
        }
        private void LoggedIn()
        {
            generation++; requestPending = true; CancelInvoke();
            PlayerCustomizationData.SelectProfile(auth.PlayFabId);
            requestPending = false; PhotonCustomizationSync.PushToPhoton();
            if (PlayerCustomizationData.AppearanceRecord.pendingUpload) { PublishPending(); return; }
            int token = generation; string account = PlayerCustomizationData.AccountId;
            long revision = PlayerCustomizationData.AppearanceRecord.revision;
            var context = CaptureContext();
            if (context == null) { ScheduleRetry(); return; }
            requestPending = true;
            PlayFabClientAPI.GetUserData(new GetUserDataRequest { AuthenticationContext = context, Keys = new List<string> { Key } }, result =>
            {
                if (!Current(token, account)) return;
                requestPending = false;
                if (result.Data != null && result.Data.TryGetValue(Key, out var data))
                    PlayerCustomizationData.TryAcceptCloud(account, revision, data.Value);
                PublishPending();
            }, error =>
            {
                if (!Current(token, account)) return;
                requestPending = false;
                // Missing connectivity never discards a local selection/draft.
                ScheduleRetry();
            });
        }
        private void PublishPending()
        {
            if (requestPending || auth == null || !auth.IsLoggedIn || PlayerCustomizationData.AccountId != "playfab:" + auth.PlayFabId) return;
            var record = PlayerCustomizationData.AppearanceRecord;
            if (!record.pendingUpload || string.IsNullOrEmpty(record.json)) return;
            int token = generation; string account = record.accountId; long revision = record.revision;
            var context = CaptureContext();
            if (context == null) { ScheduleRetry(); return; }
            requestPending = true;
            PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest { AuthenticationContext = context, Data = new Dictionary<string, string> { [Key] = record.json } }, result =>
            {
                if (!Current(token, account)) return;
                requestPending = false;
                var current = PlayerCustomizationData.AppearanceRecord;
                if (current.revision == revision) { current.pendingUpload = false; LocalSaveManager.Save(); }
                PublishPending(); // Serialize revisions so an older upload cannot arrive after a newer one.
            }, error =>
            {
                if (!Current(token, account)) return;
                requestPending = false; ScheduleRetry();
            });
        }
        private void ScheduleRetry()
        {
            CancelInvoke(nameof(Retry)); Invoke(nameof(Retry), retrySeconds);
        }
        private void Retry()
        {
            if (auth == null || !auth.IsLoggedIn || requestPending) return;
            if (PlayerCustomizationData.AppearanceRecord.pendingUpload) PublishPending(); else LoggedIn();
        }
        private void OnApplicationFocus(bool focused) { if (focused) Retry(); }
    }
}
