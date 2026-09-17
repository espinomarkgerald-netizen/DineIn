#if UNITY_EDITOR
using System;
using System.Reflection;
using Photon.Realtime;
using UnityEngine;

// Prepared for the opt-in external runner; never runs during asset import.
public static class MultiplayerRoomRestockRegressionCases
{
    [Serializable] private sealed class Layout { public Entry[] shelves; }
    [Serializable] private sealed class Entry { public string id; }
    public static void Run()
    {
        var partySize = typeof(MultiplayerMenuController).GetMethod("HasPlayablePartySize", BindingFlags.NonPublic | BindingFlags.Static);
        bool CanStart(int count, int capacity) => (bool)partySize.Invoke(null, new object[] { count, capacity });
        Check(CanStart(2, 4) && CanStart(3, 4) && CanStart(4, 4), "A four-slot room must support two through four participants.");
        Check(CanStart(2, 2) && CanStart(2, 3) && CanStart(3, 3), "Smaller room capacities must remain supported.");
        Check(!CanStart(1, 4) && !CanStart(5, 4) && !CanStart(3, 2) && !CanStart(4, 5), "Invalid party size or capacity was accepted.");
        const string dry = "RestockScene:DryStorageRoom[10]/DryRoomShelf[0]/shelfLevelTop[0]/shelfGrid[0]";
        Check(MultiplayerRestockBridge.SameShelf(dry, dry.Replace("Room[10]", "Room[7]")), "Root removal changed shelf identity.");
        Check(!MultiplayerRestockBridge.SameShelf(dry, dry.Replace("Shelf[0]", "Shelf[1]")), "Distinct physical shelves were merged.");
        Check(!MultiplayerRestockBridge.SameShelf(dry, dry.Replace("Top[0]", "Mid[1]")), "Shelf levels were merged.");
        Check(!MultiplayerRestockBridge.SameShelf(dry, dry.Replace("RestockScene:", "OtherScene:")), "An unrelated scene was accepted.");
        Check(!MultiplayerRestockBridge.SameShelf(null, null), "Missing shelf IDs were accepted.");
        var asset = Resources.Load<TextAsset>("MultiplayerRestockShelves");
        Check(asset != null, "Authoritative shelf layout is missing.");
        var layout = JsonUtility.FromJson<Layout>(asset.text);
        for (int i = 0; i < layout.shelves.Length; i++)
            for (int j = i + 1; j < layout.shelves.Length; j++)
                Check(!MultiplayerRestockBridge.SameShelf(layout.shelves[i].id, layout.shelves[j].id), "Authored shelves share a normalized ID.");
        var explain = typeof(MultiplayerMenuController).GetMethod("JoinFailureMessage", BindingFlags.NonPublic | BindingFlags.Static);
        string Message(short code) => (string)explain.Invoke(null, new object[] { code });
        Check(Message(ErrorCode.JoinFailedFoundActiveJoiner).Contains("different account"), "Duplicate account failure is hidden.");
        Check(Message(ErrorCode.JoinFailedFoundInactiveJoiner).Contains("90 seconds"), "Inactive reservation has no recovery guidance.");
        Check(Message(ErrorCode.GameDoesNotExist).Contains("version"), "Missing/version-separated room has no build guidance.");
        Check(Message(1234).Contains("1234"), "Unknown Photon failure lost its diagnostic code.");
    }
    private static void Check(bool okay, string message) { if (!okay) throw new InvalidOperationException(message); }
}
#endif
