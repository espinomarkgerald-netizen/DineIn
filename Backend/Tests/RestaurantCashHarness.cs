using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;

// Simulated Unity/account boundaries. The algorithms under test are compiled from the production partial.
namespace UnityEngine {
    public static class Time { public static float realtimeSinceStartup = 1; }
    public static class JsonUtility {
        static readonly JsonSerializerOptions Options = new() { IncludeFields = true };
        public static string ToJson(object v, bool pretty = false) => JsonSerializer.Serialize(v, v.GetType(), Options);
        public static T FromJson<T>(string v) => JsonSerializer.Deserialize<T>(v, Options);
    }
}
public class MoneyTransactionSaveEntry { public int day, amountDelta; public string description; public bool adjustment; }
public class InventorySaveEntry { public int itemType, stock; }
public class GameSaveData {
    public int money, currentDay = 1, polishDayStartMoney;
    public List<int> successfulRestaurantDays = new();
    public List<string> campaignCreditReceipts = new();
    public List<MoneyTransactionSaveEntry> moneyTransactions = new();
    public List<InventorySaveEntry> inventoryStocks = new();
}
public class PlayFabAuthManager { public static PlayFabAuthManager Instance = new(); public bool IsLoggedIn = true; public string PlayFabId = "owner"; }
public static class CampaignCloudSync { public static bool CanEnterGameplay() => true; }
public static class LocalSaveManager {
    public const string CasualDiningId = "casual", FastFoodId = "fastfood", FineDiningId = "finedining";
    public class Profile { public string progressAccount = "owner"; }
    public static Profile Data = new(); public static bool NeedsRecovery;
}
public static partial class CampaignSaveStore {
    public class Snapshot { public string save, checkpoint, hash; }
    public class ProfileProgress { public List<string> unlockedRestaurantIds = new(), ownedItemIds = new(); }
    public class AccountSnapshot {
        public string account = "owner", device = "fixture"; public long revision;
        public Snapshot casual, fastFood, fineDining; public ProfileProgress profile = new();
    }
    public static bool AtMenu = true; public static int Money = 777;
    public static string LocalDirectory = Path.GetTempPath();
    public static AccountSnapshot Current; public static bool FailAfterCommit; public static int Installs, Reads;
    public static AccountSnapshot ReadAccount(bool observe = true) { Reads++; return Current; }
    public static object ReadAccountState() => null;
    public static void ObserveSnapshot(AccountSnapshot a, object b, string c, string d) { a.revision++; }
    public static void InstallAccount(AccountSnapshot a, bool acknowledge) {
        RequireCashPurchaseHistory(a, Current); Current = a; Installs++;
        if (FailAfterCommit) { FailAfterCommit = false; throw new IOException("Simulated post-commit interruption"); }
    }
    public static void NotifyLocalWrite() { combinedMoneyExpires = 0; }
    public static void Seal(AccountSnapshot a) { }
    public static string Hash(string a, string b) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes((a??"").Length+":"+a+(b??"").Length+":"+b)));
    public static bool Valid(Snapshot p) => p != null && p.hash == Hash(p.save,p.checkpoint) && !string.IsNullOrEmpty(p.save) &&
        UnityEngine.JsonUtility.FromJson<GameSaveData>(p.save).money >= 0 && (string.IsNullOrEmpty(p.checkpoint) || UnityEngine.JsonUtility.FromJson<GameSaveData>(p.checkpoint).money >= 0);
    public static bool ValidContent(AccountSnapshot a) => a != null && a.profile != null &&
        new[]{a.casual,a.fastFood,a.fineDining}.All(p => p != null && p.hash == Hash(p.save,p.checkpoint) && (string.IsNullOrEmpty(p.save) || Valid(p)));
}
public static class RestaurantCashHarness {
    static int checks;
    static void Check(bool v, string message) { if (!v) throw new Exception(message); checks++; }
    static void Reject(Action action, string message) { try { action(); } catch (InvalidOperationException) { checks++; return; } throw new Exception(message); }
    static CampaignSaveStore.Snapshot Pair(int cash, int? checkpoint = null) {
        string Doc(int n) => UnityEngine.JsonUtility.ToJson(new GameSaveData { money=n, polishDayStartMoney=n,
            successfulRestaurantDays=Enumerable.Range(1,40).ToList(), inventoryStocks=new(){new(){stock=19,itemType=7}} });
        var p = new CampaignSaveStore.Snapshot { save = cash < 0 ? null : Doc(cash), checkpoint=checkpoint.HasValue?Doc(checkpoint.Value):null };
        p.hash=CampaignSaveStore.Hash(p.save,p.checkpoint); return p;
    }
    static CampaignSaveStore.AccountSnapshot Fixture(int casual, int fast=-1, int fine=-1, bool ownFast=false) => new() {
        casual=Pair(casual), fastFood=Pair(fast), fineDining=Pair(fine),
        profile=new(){unlockedRestaurantIds=ownFast?new(){"casual","fastfood"}:new(){"casual"},ownedItemIds=new(){"costume:existing"}}
    };
    static CampaignSaveStore.AccountSnapshot Buy(CampaignSaveStore.AccountSnapshot a, string target="fastfood",int cost=10000) =>
        CampaignSaveStore.PrepareCashPurchase(a,target,cost,target=="fastfood"?10:30,target=="fastfood"?0:15,Guid.NewGuid().ToString("N"));
    public static void Main() {
        var a=Fixture(15000,50000,30000);
        Check(CampaignSaveStore.CombinedMoney(a)==15000,"Locked careers contributed cash");
        Check(CampaignSaveStore.CombinedMoney(Fixture(-1))==0,"Missing career invented starting money");
        var b=Buy(a);
        Check(CampaignSaveStore.CombinedMoney(b)==55000,"Newly owned career's preserved cash was not included");
        Check(UnityEngine.JsonUtility.FromJson<GameSaveData>(a.casual.save).money==15000,"Preparation changed original save");
        Check(b.fastFood.save==a.fastFood.save && b.fineDining.save==a.fineDining.save,"Locked career data changed");
        Check(b.profile.unlockedRestaurantIds.Contains("fastfood") && b.profile.ownedItemIds.Contains("costume:existing"),"Ownership/cosmetics lost");
        Check(UnityEngine.JsonUtility.FromJson<GameSaveData>(b.casual.save).inventoryStocks[0].stock==19,"Stock changed");
        Check(CampaignSaveStore.CombinedMoney(Buy(b))==55000,"Owned restaurant charged again");
        a=Fixture(30000,10000,99999,true); a.casual=Pair(30000,32000);
        b=Buy(a,"finedining",25000);
        Check(CampaignSaveStore.CombinedMoney(b)==114999,"Pooled purchase or preserved target balance wrong");
        Check(UnityEngine.JsonUtility.FromJson<GameSaveData>(b.casual.save).money==11250 && UnityEngine.JsonUtility.FromJson<GameSaveData>(b.fastFood.save).money==3750,"Proportional shares wrong");
        Check(UnityEngine.JsonUtility.FromJson<GameSaveData>(b.casual.checkpoint).money==13250,"Checkpoint can refund purchase");
        Check(b.fineDining.save==a.fineDining.save,"Purchase spent target starting cash");
        Reject(()=>CampaignSaveStore.RequireCashPurchaseHistory(a,b),"Old account snapshot erased payment history");
        CampaignSaveStore.RequireCashPurchaseHistory(b,b); checks++;
        a=Fixture(9,8,-1,true); b=Buy(a,"finedining",16);
        Check(CampaignSaveStore.CombinedMoney(b)==1,"Rounding created/lost cash");
        Check(UnityEngine.JsonUtility.FromJson<GameSaveData>(b.casual.save).money>=0 && UnityEngine.JsonUtility.FromJson<GameSaveData>(b.fastFood.save).money>=0,"Rounding overdrew career");
        Reject(()=>Buy(Fixture(9999)),"Insufficient funds accepted");
        Reject(()=>Buy(Fixture(99999),cost:0),"Zero price accepted");
        a=Fixture(99999); var d=UnityEngine.JsonUtility.FromJson<GameSaveData>(a.casual.save); d.successfulRestaurantDays=new(){1,1,1}; a.casual.save=UnityEngine.JsonUtility.ToJson(d);a.casual.hash=CampaignSaveStore.Hash(a.casual.save,null);
        Reject(()=>Buy(a),"Repeated/insufficient days bypassed requirements");
        a=Fixture(int.MaxValue,int.MaxValue,-1,true);
        Check(CampaignSaveStore.CombinedMoney(a)==4294967294L,"Balance overflow");
        b=Buy(a,"finedining",25000);Check(CampaignSaveStore.CombinedMoney(b)==4294942294L,"Proportional multiplication overflow");
        CampaignSaveStore.Current=Fixture(15000);CampaignSaveStore.NotifyLocalWrite();
        Check(CampaignSaveStore.DisplayMoney==15000,"Menu did not combine cash");
        CampaignSaveStore.AtMenu=false;Check(CampaignSaveStore.DisplayMoney==777,"Gameplay used combined money");CampaignSaveStore.AtMenu=true;
        PlayFabAuthManager.Instance.PlayFabId="other";Check(!CampaignSaveStore.TryGetCombinedMoney(out _,out _),"Other account saw cached balance");PlayFabAuthManager.Instance.PlayFabId="owner";
        CampaignSaveStore.FailAfterCommit=true;
        Check(!CampaignSaveStore.TryPurchaseRestaurantCash("owner","fastfood",10000,10,0,out _),"Interrupted commit reported success");
        Check(CampaignSaveStore.TryPurchaseRestaurantCash("owner","fastfood",10000,10,0,out _),"Retry could not recover owned restaurant");
        Check(CampaignSaveStore.Installs==1 && CampaignSaveStore.CombinedMoney(CampaignSaveStore.Current)==5000,"Retry charged twice");
        Check(!CampaignSaveStore.TryPurchaseRestaurantCash("other","finedining",25000,30,15,out _),"Cross-account purchase accepted");
        a=Fixture(30000,20000,-1,true);a.fastFood=Pair(20000,12000);
        Check(CampaignSaveStore.CombinedMoney(a)==42000,"Uncommitted/checkpoint funds overcounted");
        var random = new Random(412);
        for (int i=0;i<200;i++) {
            int c=random.Next(1,1000000), f=random.Next(1,1000000), cost=random.Next(1,c+f+1);
            a=Fixture(c,f,-1,true); b=Buy(a,"finedining",cost);
            Check(CampaignSaveStore.CombinedMoney(b)==(long)c+f-cost,"Random allocation changed total cash");
            Check(UnityEngine.JsonUtility.FromJson<GameSaveData>(b.casual.save).money>=0 && UnityEngine.JsonUtility.FromJson<GameSaveData>(b.fastFood.save).money>=0,"Random allocation overdrew a career");
        }
        CampaignSaveStore.Current=Fixture(12000);CampaignSaveStore.Current.casual.hash="invalid";CampaignSaveStore.NotifyLocalWrite();
        int reads=CampaignSaveStore.Reads;
        Check(!CampaignSaveStore.TryGetCombinedMoney(out _,out var failure) && !failure.Contains("invalid"),"Corrupt funds were usable or exposed internal details");
        Check(!CampaignSaveStore.TryGetCombinedMoney(out _,out _) && CampaignSaveStore.Reads==reads+1,"Failed balance reread files every frame");
        CampaignSaveStore.Current=Fixture(12000);UnityEngine.Time.realtimeSinceStartup+=1;
        Check(CampaignSaveStore.TryGetCombinedMoney(out var recovered,out _) && recovered==12000,"Failed balance did not refresh after recovery");
        Console.WriteLine("PASS: "+checks+" production cash-operation checks (simulated Unity/account persistence boundaries).");
    }
}
