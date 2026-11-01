// Source/reference and simulated serialization checks. No Unity serializer or live cloud calls.
'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '../..');
const read = file => fs.readFileSync(path.join(root, file), 'utf8').replace(/\r\n/g, '\n');
const old = JSON.parse(read('Backend/Tests/Fixtures/PreSoloGameSaveFields.json'));
const source = read('Assets/_Project/Save/GameSaveData.cs').split('public class GameSaveData')[1];
const fields = [...source.matchAll(/^\s*public\s+[\w<>.,\[\] ]+?\s+(\w+)\s*(?:=|;)/gm)].map(m => m[1]);
let cases = 0;
function test(name, action) { action(); cases++; console.log('PASS ' + name); }
test('campaign schema 3 preserved; only one optional root field added', () => {
    assert.deepEqual(fields.filter(name => name !== 'soloRankingV1'), old.fields);
    assert.match(source, /saveSchemaVersion = 3/);
    const block = read('Assets/_Project/Networking/Multiplayer/SoloRunRecord.cs').split('public sealed class SoloRankingV1')[1].split('public const int Limit')[0];
    assert.match(block, /public string runId;/);
    assert.match(block, /public int dayOrdinal, lastCreditedDayOrdinal, completedDays;/);
});
test('simulated old-build read/write round trip preserves gameplay but drops ranking block', () => {
    const gameplay = { saveSchemaVersion: 3, currentDay: 18, currentPhase: 4, currentDayHalf: 0, money: 12345,
        approval: 76, campaignCompleted: false, employees: [{ employeeName: 'Existing', hired: true, assigned: true }],
        successfulRestaurantDays: [1, 2, 4], campaignCreditReceipts: ['existing-receipt'],
        fineDiningDayComplete: true, fineDiningReservations: { bookings: [{ id: 'kept' }] }, restockOrders: [] };
    for (const key of Object.keys(gameplay)) assert.ok(old.fields.includes(key), key);
    const updatedSave = JSON.stringify({ ...gameplay, soloRankingV1: { runId: '1'.repeat(32), dayOrdinal: 18,
        lastCreditedDayOrdinal: 18, completedDays: 18 } });
    // This models JsonUtility's known-field projection using the actual pre-change field list.
    // It intentionally does NOT claim to execute the Unity engine's serializer.
    const oldReader = Object.fromEntries(Object.entries(JSON.parse(updatedSave)).filter(([key]) => old.fields.includes(key)));
    assert.ok(oldReader.saveSchemaVersion <= 3 && oldReader.currentDay >= 1 && oldReader.money >= 0);
    const oldWriter = JSON.stringify(oldReader), reloaded = JSON.parse(oldWriter);
    assert.deepEqual(reloaded, gameplay);
    assert.equal(reloaded.soloRankingV1, undefined);
    assert.equal(JSON.parse(JSON.stringify(gameplay)).soloRankingV1, undefined); // legacy also stays unranked
});
test('cloud career strings round-trip without dropping optional fields or changing hash inputs', () => {
    const hash = (save, checkpoint) => crypto.createHash('sha256')
        .update(`${(save || '').length}:${save || ''}${(checkpoint || '').length}:${checkpoint || ''}`).digest('base64');
    const save = JSON.stringify({ saveSchemaVersion: 3, currentDay: 2, money: 1000,
        soloRankingV1: { runId: '1'.repeat(32), dayOrdinal: 2, lastCreditedDayOrdinal: 1, completedDays: 1 } });
    const career = { version: 1, save, checkpoint: save, hash: hash(save, save) };
    const cloud = JSON.parse(JSON.stringify({ version: 5, account: 'AA', casual: career }));
    assert.equal(cloud.casual.save, save);
    assert.equal(hash(cloud.casual.save, cloud.casual.checkpoint), cloud.casual.hash);
    const changed = save.replace('"completedDays":1', '"completedDays":2');
    assert.notEqual(hash(changed, save), career.hash);
    const accountSource = read('Assets/_Project/Save/CampaignSaveStore.Account.cs');
    assert.match(accountSource, /public int version = 5;/);
    assert.match(accountSource, /save = File.Exists\(path\) \? File.ReadAllText\(path\)/);
    assert.match(accountSource, /if \(value.version == 2\)/);
    assert.match(accountSource, /else if \(value.version == 3\)/);
    assert.match(accountSource, /else if \(value.version == 4\)/);
});
test('real flow hooks credit after settlement; retrying Day 30 has no advance hook', () => {
    const flow = read('Assets/_Project/Core/GameFlowManager.cs');
    const finalizer = flow.split('public void FinalizeRestaurantDayForResults')[1].split('public bool TryGetRestaurantDayOutcome')[0];
    assert.ok(finalizer.indexOf('EndOfDayFinance();') < finalizer.indexOf('SettleSoloRankingDay();'));
    assert.ok(finalizer.indexOf('SettleSoloRankingDay();') < finalizer.indexOf('CommitRestaurantResults()'));
    const recovery = flow.split('public bool ContinueRestaurantCampaignAfterRecovery()')[1].split('private void EvaluateRestaurantDay()')[0];
    assert.ok(!recovery.includes('AdvanceSoloRankingDay'));
    assert.match(recovery, /if \(currentDay >= campaignDayLimit\)[\s\S]*PrepareRestaurantDay\(\)/);
    const endless = flow.split('private void StartEndlessRestaurantDay()')[1].split('private void EnsureRestaurantDayPrepared')[0];
    assert.match(endless, /AdvanceSoloRankingDay\(\)/);
    assert.match(endless, /if \(MultiplayerRestaurantBridge.IsActive\) currentDay\+\+/); // unchanged displayed solo day
});
test('save/load/checkpoint hooks preserve settled state and recover only persisted uploads', () => {
    const save = read('Assets/_Project/Save/GameSaveManager.cs');
    const commit = save.split('public bool CommitRestaurantResults()')[1].split('private GameSaveData CaptureCurrentData')[0];
    assert.ok(commit.indexOf('CampaignSaveStore.WritePair') < commit.indexOf('RecoverPersistedSoloRanking(data)'));
    assert.match(save, /PreserveSoloCheckpointCredit\(data, SavePath\);\n        ApplySaveData\(data, true, false\)/);
    assert.match(save, /if \(loadPath == DayCheckpointPath\) PreserveSoloCheckpointCredit\(data, SavePath\)/);
    const initial = save.split('public void CreateInitialCampaign(GameSaveData cleanStart)')[1].split('public void ReloadCampaignFromDisk')[0];
    assert.match(initial, /File.Exists\(initialPath\)/);
    assert.match(initial, /cleanStart.soloRankingV1 =[\s\S]*SoloRankingV1.NewRun\(\)/);
    assert.ok(initial.indexOf('cleanStart.soloRankingV1 =') < initial.indexOf('CampaignSaveStore.AtomicWrite(initialPath'));
    const fresh = save.split('if (!HasSave() && !File.Exists(DayCheckpointPath))')[1].split('string loadPath')[0];
    assert.match(fresh, /BeginSoloRankingRun\(\)/);
    assert.ok(!save.split('string loadPath')[1].includes('BeginSoloRankingRun()'));
});
test('all three multiplayer scene bindings reach the shared reporting lifecycle', () => {
    const bindings = [ ['Lobby1 Multiplayer', 'MultiplayerRestaurantBridge'],
        ['Lobby2 Multiplayer', 'FastFoodMultiplayerSession'], ['Lobby3 Multiplayer', 'FineDiningMultiplayerSession'] ];
    for (const [name, binding] of bindings) {
        const scene = read(`Assets/_Project/Scenes/RoleBased/${name}.unity`);
        const id = scene.match(/gameplayBinding: \{fileID: (\d+)/)[1];
        const block = scene.match(new RegExp('^--- !u!\\d+ &' + id + '\\n([\\s\\S]*?)(?=^--- !u!)', 'm'))[1];
        const guid = read(`Assets/_Project/Networking/Multiplayer/${binding}.cs.meta`).match(/guid: (\w+)/)[1];
        assert.ok(block.includes('guid: ' + guid));
        assert.match(scene, /useManagementComputerForDayStart: 1/);
    }
    assert.match(read('Assets/_Project/Gameplay/GameManager/GameManager.cs'), /MultiplayerSessionManager.Instance.CompleteDay\(flow.CurrentDay/);
    assert.match(read('Assets/_Project/Networking/Multiplayer/MultiplayerSessionManager.cs'), /MultiplayerRunRecords.Record\(run, localActor, IsHostConnection\)/);
});
console.log(`${cases} static/simulated persistence checks passed. Actual Unity old-build round trip and live cloud sync NOT RUN.`);
