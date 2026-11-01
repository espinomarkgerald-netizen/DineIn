// Mocked Classic PlayFab: actual multiplayer and solo handlers, no live title.
'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const specs = [
    ['CasualDining', 'Lobby1', 'casual-session-2', 'casual-solo-1'],
    ['FastFood', 'Lobby2', 'fastfood-session-2', 'fastfood-solo-1'],
    ['FineDining', 'Lobby3', 'finedining-session-1', 'finedining-solo-1']
];
const clone = value => JSON.parse(JSON.stringify(value));
const solo = (spec, count, id = '1'.repeat(32)) => ({ schema: 1, restaurant: spec[0], sceneName: spec[1],
    rulesVersion: spec[3], runId: id, completedDays: count, gameVersion: 'test' });
const multiplayer = (spec, count, revision = 1) => ({ schema: 2, restaurant: spec[0], sceneName: spec[1] + ' Multiplayer',
    rulesVersion: spec[2], runId: '2'.repeat(32), revision, completedDay: count, highestDay: count,
    gameVersion: 'test', hostActor: 1, hostAccountId: 'AA', partySize: 2, startedUtc: '2026-11-01T00:00:00Z',
    participants: ['AA', 'BB'].map((accountId, i) => ({ accountId, actor: i + 1, completedDay: count, highestDay: count })) });
function fixture() {
    const scores = {}, internal = {}, readonly = {}, writes = [];
    let failWriteAccount = null, failRead = false;
    const get = (store, args) => ({ Data: Object.fromEntries(args.Keys.filter(k => store[args.PlayFabId]?.[k] !== undefined)
        .map(k => [k, { Value: store[args.PlayFabId][k] }])) });
    const put = (store, args) => Object.assign(store[args.PlayFabId] ??= {}, args.Data);
    const context = { handlers: {}, currentPlayerId: 'AA', server: {
        GetUserInternalData: a => get(internal, a), GetUserReadOnlyData: a => get(readonly, a),
        UpdateUserInternalData: a => put(internal, a), UpdateUserReadOnlyData: a => put(readonly, a),
        UpdatePlayerStatistics: a => {
            if (failWriteAccount === a.PlayFabId) { failWriteAccount = null; throw Error('simulated write outage'); }
            for (const s of a.Statistics) {
                const board = scores[s.StatisticName] ??= {};
                board[a.PlayFabId] = Math.max(board[a.PlayFabId] || 0, s.Value); // requires live Max configuration
            }
            writes.push(clone(a));
        },
        GetPlayerStatistics: a => {
            if (failRead) { failRead = false; throw Error('simulated post-write read outage'); }
            return { Statistics: a.StatisticNames.filter(name => scores[name]?.[a.PlayFabId] !== undefined)
                .map(name => ({ StatisticName: name, Value: scores[name][a.PlayFabId] })) };
        }
    } };
    vm.createContext(context);
    for (const file of ['MultiplayerRunsV2.js', 'SoloRunsV1.js']) vm.runInContext(fs.readFileSync(path.join(__dirname, file), 'utf8'), context);
    return { scores, writes, internal,
        call(name, record, account = 'AA', final = false) { context.currentPlayerId = account; return context.handlers[name]({ record: clone(record), final }); },
        failWrite(account) { failWriteAccount = account; }, failRead() { failRead = true; }
    };
}
let cases = 0;
function test(name, action) { action(); cases++; console.log('PASS ' + name); }
for (const spec of specs) {
    test(spec[0] + ': independent solo/multiplayer Max scores, duplicates and shorter runs', () => {
        const f = fixture(), s = solo(spec, 9), m = multiplayer(spec, 6);
        assert.equal(f.call('DineInPublishSoloRunV1', s).bestCompletedDays, 9);
        assert.equal(f.call('DineInPublishRunV2', m).accepted, true);
        assert.equal(f.call('DineInPublishSoloRunV1', s).bestCompletedDays, 9);
        assert.equal(f.call('DineInPublishSoloRunV1', solo(spec, 2, '3'.repeat(32))).bestCompletedDays, 9);
        const shorter = multiplayer(spec, 2); shorter.runId = '4'.repeat(32); shorter.startedUtc = '2026-11-02T00:00:00Z';
        assert.equal(f.call('DineInPublishRunV2', shorter).accepted, true);
        assert.equal(f.scores[spec[0] + 'LongestRunV2'].AA, 6);
        assert.equal(f.scores[spec[0] + 'LongestRunV2'].BB, 6);
        assert.deepEqual(f.scores[spec[0] + 'SoloLongestRunV1'], { AA: 9 });
        assert.equal(Object.keys(f.scores).length, 2);
    });
    test(spec[0] + ': solo validation/authentication and no participant awards', () => {
        for (const patch of [{ schema: 2 }, { sceneName: spec[1] + ' Multiplayer' }, { rulesVersion: spec[2] },
            { runId: 'bad' }, { completedDays: -1 }, { completedDays: 0 }, { completedDays: 1.5 },
            { completedDays: 1000001 }, { accountId: 'BB' }, { participants: [] }, { gameVersion: '' }]) {
            const f = fixture(); assert.equal(f.call('DineInPublishSoloRunV1', Object.assign(solo(spec, 3), patch)).accepted, false);
            assert.equal(f.writes.length, 0);
        }
        const f = fixture(); assert.equal(f.call('DineInPublishSoloRunV1', solo(spec, 3), null).accepted, false);
        assert.equal(f.call('DineInPublishSoloRunV1', solo(spec, 3), 'BB').accepted, true);
        assert.deepEqual(f.scores[spec[0] + 'SoloLongestRunV1'], { BB: 3 });
    });
    test(spec[0] + ': failed and unknown solo writes retry without additional score', () => {
        const f = fixture(), record = solo(spec, 7); f.failWrite('AA');
        assert.throws(() => f.call('DineInPublishSoloRunV1', record));
        f.failRead(); assert.throws(() => f.call('DineInPublishSoloRunV1', record));
        assert.equal(f.call('DineInPublishSoloRunV1', record).bestCompletedDays, 7);
        assert.equal(f.call('DineInPublishSoloRunV1', record).bestCompletedDays, 7);
    });
    test(spec[0] + ': host/guest eligibility, failed settlement, departure and partial retry unchanged', () => {
        const f = fixture(), record = multiplayer(spec, 7);
        Object.assign(record.participants[1], { completedDay: 5, highestDay: 6, provisionalDay: 7 });
        assert.equal(f.call('DineInPublishRunV2', record, 'BB').accepted, false);
        f.failWrite('BB'); assert.throws(() => f.call('DineInPublishRunV2', record));
        assert.equal(f.call('DineInPublishRunV2', record).accepted, true);
        assert.equal(f.scores[spec[0] + 'LongestRunV2'].BB, 5);
        const inflated = clone(record); inflated.participants[1].completedDay = 7; inflated.participants[1].highestDay = 7;
        assert.equal(f.call('DineInClaimRunV2', inflated, 'BB').accepted, false);
        record.revision++; Object.assign(record.participants[1], { completedDay: 7, highestDay: 7, provisionalDay: 0 });
        assert.equal(f.call('DineInPublishRunV2', record).accepted, true);
        record.revision++; record.participants[1].departed = true;
        assert.equal(f.call('DineInPublishRunV2', record).accepted, true);
        record.revision++; record.completedDay = record.highestDay = 8; record.endReason = 'Bankruptcy';
        record.participants[0].completedDay = record.participants[0].highestDay = 8;
        assert.equal(f.call('DineInPublishRunV2', record).accepted, true);
        assert.deepEqual(f.scores[spec[0] + 'LongestRunV2'], { AA: 8, BB: 7 });
        assert.equal(f.call('DineInPublishRunV2', record).accepted, true);
        assert.equal(f.call('DineInClaimRunV2', record, 'BB', true).accepted, true);
    });
}
console.log(cases + ' mocked six-board contract scenarios passed. No live PlayFab verification.');
