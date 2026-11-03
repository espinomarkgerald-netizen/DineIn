// Static scene/reference and routing checks; not a Unity or two-device playthrough.
'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../..');
const read = p => fs.readFileSync(path.join(root, p), 'utf8').replace(/\r\n/g, '\n');
let cases = 0;
function test(name, action) { action(); cases++; console.log('PASS ' + name); }
function block(scene, id) {
    const found = scene.match(new RegExp('^--- !u!\\d+ &' + id + '\\n([\\s\\S]*?)(?=^--- !u!|$(?![\\s\\S]))', 'm'));
    assert.ok(found, 'Missing serialized object ' + id);
    return found[1];
}
function script(p) { return read(p + '.meta').match(/guid: (\w+)/)[1]; }
test('all restaurants bind to the same day coordinator and meal interaction bridge', () => {
    const dayGuid = script('Assets/_Project/Networking/Multiplayer/MultiplayerDayBridge.cs');
    const sessionGuid = script('Assets/_Project/Networking/Multiplayer/MultiplayerSessionManager.cs');
    for (const name of ['Lobby1 Multiplayer', 'Lobby2 Multiplayer', 'Lobby3 Multiplayer']) {
        const scene = read('Assets/_Project/Scenes/RoleBased/' + name + '.unity');
        assert.ok(scene.includes('guid: ' + sessionGuid), name + ' session manager');
        if (name !== 'Lobby1 Multiplayer') {
            const binding = name.startsWith('Lobby2') ? 'FastFoodMultiplayerSession' : 'FineDiningMultiplayerSession';
            const authored = scene.split('guid: ' + script('Assets/_Project/Networking/Multiplayer/' + binding + '.cs'))[1].split('--- !u!')[0];
            const day = authored.match(/dayBridge: \{fileID: (\d+)/)[1];
            assert.ok(block(scene, day).includes('guid: ' + dayGuid), name + ' day binding');
        }
        const ids = [...scene.matchAll(/^--- !u!\d+ &(\d+)/gm)].map(m => m[1]);
        assert.equal(ids.length, new Set(ids).size, name + ' duplicate object ids');
    }
    const spawn = read('Assets/_Project/Networking/Multiplayer/MultiplayerCustomerSpawn.cs');
    assert.match(spawn, /AddComponent<MultiplayerCustomerInteractionBridge>\(\)/);
    assert.match(read('Assets/_Project/Networking/Multiplayer/MultiplayerRestaurantBridge.Session.cs'), /AddComponent<MultiplayerDayBridge>\(\)/);
});
test('Casual multiplayer uses the existing floor approach, not a tray or mesh pivot', () => {
    const campaign = read('Assets/_Project/Scenes/RoleBased/Lobby1.unity');
    const multiplayer = read('Assets/_Project/Scenes/RoleBased/Lobby1 Multiplayer.unity');
    for (const scene of [campaign, multiplayer]) {
        const kitchen = block(scene, '952672341');
        const approach = kitchen.match(/outputPickupApproach: \{fileID: (\d+)/)[1];
        assert.equal(approach, '925100000000000002');
        assert.equal(block(scene, approach), block(campaign, approach));
        assert.ok(block(scene, '952672340').includes('{fileID: ' + approach + '}'));
        assert.equal(block(scene, approach).match(/m_Father: \{fileID: (\d+)/)[1], '952672340');
    }
});
test('Fine Dining bar and Fast Food pickup retain their restaurant approach bindings', () => {
    const fine = read('Assets/_Project/Scenes/RoleBased/Lobby3 Multiplayer.unity');
    const kitchen = fine.split('guid: ' + script('Assets/_Project/Restaurant/Items/KitchenManager.cs'))[1].split('--- !u!')[0];
    for (const field of ['outputPickupApproach', 'drinkPickupApproach']) {
        const id = kitchen.match(new RegExp(field + ': \\{fileID: (\\d+)'))[1];
        assert.notEqual(id, '0'); block(fine, id);
    }
    const tray = read('Assets/_Project/Restaurant/Items/FoodTrayInteractable.cs');
    const configure = tray.split('public void ConfigurePickupApproach(')[1].split('public Transform StandPoint')[0];
    assert.match(configure, /if \(approach != null\) pickupPoint = approach/);
    assert.match(tray, /return tray.TargetGroup.FastFood.PickupApproach/);
    const projection = read('Assets/_Project/Networking/Multiplayer/MultiplayerObjectBridge.cs');
    assert.match(projection, /RegisterObservedTray\(tray, preparedSlot\)/);
    assert.match(projection, /kitchen.drinkPickupApproach : kitchen.GetOutputPickupApproach/);
});
test('solo end waits for the finished day and personal reward, before any next-day commit', () => {
    const bridge = read('Assets/_Project/Networking/Multiplayer/MultiplayerDayBridge.cs');
    assert.match(bridge, /day.HasDayResults && session.ConnectedHumanCount < 2\s*&& AllRewardPresentationsFinished\(\) && session.EndAfterSoloDay\(\)/);
    const next = bridge.split('request.action == "next"')[1].split('finally')[0];
    assert.ok(next.indexOf('AllRewardPresentationsFinished()') < next.indexOf('EndAfterSoloDay()'));
    assert.ok(next.indexOf('session.ConnectedHumanCount < 2') < next.indexOf('CompleteRestaurantDay()'));
    const session = read('Assets/_Project/Networking/Multiplayer/MultiplayerSessionManager.cs');
    const end = session.split('public bool EndAfterSoloDay()')[1].split('private void PublishRun()')[0];
    assert.match(end, /!IsAuthority \|\| ConnectedHumanCount >= 2/);
    assert.match(end, /run.completedDay < GameFlowManager.Instance.CurrentDay/);
    assert.doesNotMatch(end, /ReachDay\(|currentDay\+\+|completedDay\s*=/);
    assert.match(end, /IsOpen = false/);
    assert.match(session, /!player.IsInactive && ValidActor\(player.ActorNumber\)/);
    const ui = read('Assets/_Project/Networking/Multiplayer/MultiplayerSessionUI.cs');
    assert.match(ui, /soloEnding.ShowAnnouncement/);
    assert.match(ui, /soloEnding.Hide\(\); session.LeaveToMenu\(\)/);
    for (const resource of ['RestaurantUnlockGuide', 'RestaurantBossTips'])
        assert.ok(fs.existsSync(path.join(root, 'Assets/_Project/Resources/UI/' + resource + '.prefab')));
});
test('menu and automatic reconnect select transport from saved room policy, with one fallback', () => {
    const menu = read('Assets/_Project/Networking/Photon/MultiplayerMenuController.cs');
    const recovery = read('Assets/_Project/Networking/Multiplayer/MultiplayerSessionManager.Recovery.cs');
    assert.match(menu, /!MultiplayerLastRun.UsesActorRejoin\(rejoinPointer\)/);
    assert.match(recovery, /!MultiplayerLastRun.UsesActorRejoin\(recoveryPointer\)/);
    assert.match(menu, /CanFallBackToJoin\(!fallbackRejoin, code\)/);
    assert.match(recovery, /fallbackJoin \? PhotonNetwork.JoinRoom\(recoveryPointer.room\) : PhotonNetwork.RejoinRoom/);
});
test('an owned billing task resumes counter pickup and guest papers use the host approach', () => {
    const bridge = read('Assets/_Project/Networking/Multiplayer/MultiplayerCustomerInteractionBridge.cs');
    const entry = bridge.split('public static bool TryHandleBill(CustomerGroup group)')[1].split('private void PauseBillApproach()')[0];
    const owned = entry.split('bridge.RetainOwnedTask(bridge.billTaskId);')[1].split('string requested')[0];
    assert.match(owned, /if \(!bridge.billTransitionPending && bridge.billMover == null\)\s*bridge.BeginBillApproach\(\)/);
    assert.doesNotMatch(owned, /hands.HasBill/);
    const stages = bridge.split('private void BeginBillApproach()')[1].split('private void MoveForBill(')[0];
    assert.match(stages, /IsMatchingHeldBill[\s\S]*MoveForBill\(stand, 2.75f, 6\)/);
    assert.match(stages, /MoveForBill\(localBill.StandPoint, localBill.GetInteractRadius\(\), 2\)/);
    assert.match(stages, /MoveForBill\(stand, 2.75f, 0\)/);
    const manager = read('Assets/_Project/Restaurant/Items/BillManager.cs');
    const projection = manager.split('public BillPaper PresentMultiplayerBill(')[1].split('public void RemoveObservedBill')[0];
    assert.match(projection, /existing.ConfigurePickupApproach\(billPickupApproach\)/);
    assert.ok(projection.indexOf('bill.ConfigurePickupApproach(billPickupApproach)') < projection.indexOf('bill.Init(group)'));
    const validation = read('Assets/_Project/Networking/Multiplayer/MultiplayerCustomerInteractionBridge.Billing.cs');
    assert.match(validation, /claims.GetLease\(claim\) != context.lease/);
    assert.match(validation, /NetworkPlayerMovementSync.AuthorityPosition\(root\)/);
});
console.log(cases + ' static scene/routing checks passed. Native pickup, UI, and multiplayer gameplay NOT RUN.');
