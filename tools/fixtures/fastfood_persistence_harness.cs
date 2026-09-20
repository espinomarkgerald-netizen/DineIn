
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Reflection;
using System.Runtime.Serialization;
namespace UnityEngine {
 public enum RuntimeInitializeLoadType { SubsystemRegistration }
 public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType _) {} }
 public static class Application { public static string persistentDataPath; }
 public static class Debug { public static void LogWarning(string s) {} public static void LogError(string s) { Console.WriteLine(s); } }
 public static class JsonUtility {
  public static string ToJson(object o,bool pretty=false)=>JsonSerializer.Serialize(o,o.GetType(),new JsonSerializerOptions{IncludeFields=true,WriteIndented=pretty});
  public static T FromJson<T>(string s) => (T)ConvertValue(JsonDocument.Parse(s).RootElement,typeof(T));
  static object ConvertValue(JsonElement e,Type t) {
   if(e.ValueKind==JsonValueKind.Null)return null;
   if(t==typeof(string))return e.GetString(); if(t==typeof(int))return e.GetInt32();
   if(t.IsGenericType && t.GetGenericTypeDefinition()==typeof(List<>)) {var list=(System.Collections.IList)Activator.CreateInstance(t);foreach(var item in e.EnumerateArray())list.Add(ConvertValue(item,t.GenericTypeArguments[0]));return list;}
   var result=Activator.CreateInstance(t,true);
   foreach(var f in t.GetFields(BindingFlags.Instance|BindingFlags.Public))if(e.TryGetProperty(f.Name,out var v))f.SetValue(result,ConvertValue(v,f.FieldType));
   return result;
  }
 }
}
namespace UnityEngine.SceneManagement {
 public struct Scene {public string name;public bool isLoaded;}
 public static class SceneManager { public static HashSet<string> loaded=new();public static string active="NewMainMenu"; public static Scene GetSceneByName(string s)=>new Scene{name=s,isLoaded=loaded.Contains(s)};public static Scene GetActiveScene()=>new Scene{name=active}; }
}
public class GameSaveManager {public static bool IsPersistenceSuspended;public static GameSaveManager Instance;public string CampaignSavePath=>Path.Combine(UnityEngine.Application.persistentDataPath,CampaignSaveStore.ResolveFileName("dinein_save.json"));}
public static class MultiplayerRestockBridge {public static bool IsActive;}
public class MoneyManager {public static MoneyManager Instance;public int Money;public bool Spend(int n,string s){Money-=n;return true;}public void Earn(int n,string s){Money+=n;}}
public class PlayFabAuthManager {public static PlayFabAuthManager Instance=new(){IsLoggedIn=true,PlayFabId="test-account"};public bool IsLoggedIn;public string PlayFabId;}
public class GameSaveData {public int money=5000,currentDay=1,saveSchemaVersion=3;public List<string> campaignCreditReceipts=new();}
public static class PersistenceHarness {
 static void Assert(bool pass,string label){if(!pass)throw new Exception(label);Console.WriteLine("PASS: "+label);}
 static string Json(int money,int day=1)=>UnityEngine.JsonUtility.ToJson(new GameSaveData{money=money,currentDay=day});
 static void Save(int money,string checkpoint=null)=>CampaignSaveStore.WritePair(new CampaignSaveStore.Snapshot{save=Json(money),checkpoint=checkpoint});
 public static void Main(string[] args) {
  UnityEngine.Application.persistentDataPath=Path.GetFullPath(args[0]);Directory.CreateDirectory(UnityEngine.Application.persistentDataPath);
  Save(7100);string casual=CampaignSaveStore.SavePath;string casualText=File.ReadAllText(casual);
  CampaignSaveStore.SelectRestaurant("Lobby2");Save(3200,Json(3400));string fast=CampaignSaveStore.SavePath;
  Assert(fast!=casual && CampaignSaveStore.CloudFileName=="FastFoodCampaignSave_v1.json","separate restaurant filenames and cloud keys");
  Assert(File.ReadAllText(casual)==casualText && CampaignSaveStore.Read().checkpoint==Json(3400),"Fast Food writes preserve Casual Dining and checkpoint");
  var pending=new CampaignSaveStore.Snapshot{save=Json(3600),checkpoint=null};pending.hash=CampaignSaveStore.Hash(pending.save,pending.checkpoint);
  CampaignSaveStore.AtomicWrite(fast+".pending_pair.json",UnityEngine.JsonUtility.ToJson(pending));
  var recovered=CampaignSaveStore.Read();
  Assert(recovered.save==Json(3600) && recovered.checkpoint==null && !File.Exists(fast+".pending_pair.json"),"interrupted result transaction recovers and retires checkpoint");
  Save(3800,Json(3800));CampaignSaveStore.SelectRestaurant("Lobby1");
  CampaignSaveStore.QueueConfirmedCredit("test-account",125,"credit-fast","Lobby2",fast);
  Assert(File.ReadAllText(casual)==casualText && File.Exists(Path.Combine(UnityEngine.Application.persistentDataPath,"campaign_credits/credit-fast.json")),"late credit stays with originating restaurant");
  CampaignSaveStore.SelectRestaurant("Lobby2");CampaignSaveStore.ApplyConfirmedCredits();
  Assert(CampaignSaveStore.Money==3925 && CampaignSaveStore.Read().save==CampaignSaveStore.Read().checkpoint,"confirmed credit updates both rollback documents once");
  CampaignSaveStore.QueueConfirmedCredit("test-account",125,"credit-fast","Lobby2",fast);
  Assert(CampaignSaveStore.Money==3925,"duplicate credit receipt cannot pay twice");
  string before=File.ReadAllText(fast);CampaignSaveStore.AtomicWrite(fast+".pending_pair.json","{\"version\":1,\"save\":\"bad\",\"hash\":\"invalid\"}");bool rejected=false;
  try{CampaignSaveStore.Read();}catch(InvalidDataException){rejected=true;}
  Assert(rejected && File.ReadAllText(fast)==before && File.Exists(fast+".pending_pair.json"),"invalid recovery journal preserves existing save and evidence");
  CampaignSaveStore.SelectRestaurant("Lobby1");Assert(CampaignSaveStore.Money==7100 && File.ReadAllText(casual)==casualText,"profile switching restores the correct balance");
  UnityEngine.SceneManagement.SceneManager.loaded.Add("Lobby1Tutorial");CampaignSaveStore.SelectRestaurant("Lobby2");
  Assert(CampaignSaveStore.RestaurantScene=="Lobby1" && !CampaignSaveStore.ChangeMoney(100),"tutorial session cannot switch or mutate campaign profiles");
 }
}
