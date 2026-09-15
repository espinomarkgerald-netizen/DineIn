// External-runner contract tests. Not executed on the user's computer.
// Run from repository root: node Backend/PlayFab/MultiplayerRunsV2.tests.cjs
'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, 'MultiplayerRunsV2.js'), 'utf8');
const STAT = 'CasualDiningLongestRunV2';
const LAST = 'CasualDiningLastRunV2';
const clone = value => JSON.parse(JSON.stringify(value));

function fixture() {
    const internal = {}, readonly = {}, scores = {};
    let failPlayerOnce = null;
    function get(store, args) {
        const Data = {};
        for (const key of args.Keys) {
            const value = store[args.PlayFabId]?.[key];
            if (value !== undefined) Data[key] = { Value: value };
        }
        return { Data };
    }
    function put(store, args) {
        store[args.PlayFabId] ??= {};
        Object.assign(store[args.PlayFabId], args.Data);
    }
    const context = {
        handlers: {}, currentPlayerId: 'AA',
        server: {
            GetUserInternalData: args => get(internal, args),
            GetUserReadOnlyData: args => get(readonly, args),
            UpdateUserInternalData: args => put(internal, args),
            UpdateUserReadOnlyData: args => {
                assert.equal(args.Permission, 'Private');
                put(readonly, args);
            },
            GetPlayerStatistics: args => ({ Statistics: scores[args.PlayFabId] === undefined ? []
                : [{ StatisticName: STAT, Value: scores[args.PlayFabId] }] }),
            UpdatePlayerStatistics: args => {
                if (args.PlayFabId === failPlayerOnce) {
                    failPlayerOnce = null;
                    throw new Error('Simulated participant write outage');
                }
                assert.equal(args.Statistics.length, 1);
                assert.equal(args.Statistics[0].StatisticName, STAT);
                // Live title Max configuration must be validated separately.
                scores[args.PlayFabId] = Math.max(scores[args.PlayFabId] || 0, args.Statistics[0].Value);
            }
        }
    };
    vm.createContext(context);
    vm.runInContext(source, context);
    function call(handler, account, record, final = false) {
        context.currentPlayerId = account;
        return context.handlers[handler]({ record: clone(record), final });
    }
    return {
        internal, scores,
        last: account => readonly[account]?.[LAST] ? JSON.parse(readonly[account][LAST]) : null,
        publish: (record, account = 'AA') => call('DineInPublishRunV2', account, record),
        claim: (record, account = 'BB', final = false) => call('DineInClaimRunV2', account, record, final),
        failOnce: account => { failPlayerOnce = account; }
    };
}

function run(day = 1, revision = 1, id = '00000000000000000000000000000001') {
    return {
        schema: 2, runId: id, revision, restaurant: 'CasualDining', rulesVersion: 'casual-session-2',
        gameVersion: 'contract-test', startedUtc: '2026-09-15T00:00:00.000Z', endedUtc: null, endReason: null,
        partySize: 2, hostActor: 1, hostAccountId: 'AA', completedDay: day, highestDay: Math.max(1, day),
        participants: ['AA', 'BB'].map((accountId, index) => ({ accountId, actor: index + 1,
            completedDay: day, highestDay: Math.max(1, day), provisionalDay: 0, provisionalHighestDay: 0,
            departed: false, disconnectDeadline: 0 }))
    };
}

const cases = [];
function test(name, body) { cases.push({ name, body }); }

test('only the authenticated host publishes a valid frozen roster', () => {
    const f = fixture(), r = run();
    assert.equal(f.publish(r, 'BB').accepted, false);
    r.participants[1].accountId = 'AA';
    assert.equal(f.publish(r).accepted, false);
    assert.deepEqual(f.scores, {});
});

test('zero completed days and stopped-day metadata remain distinct', () => {
    const f = fixture(), r = run(0);
    r.endReason = 'HostLeft';
    assert.equal(f.publish(r).accepted, true);
    assert.equal(f.scores.AA, 0);
    assert.equal(f.last('BB').highestDay, 1);
    assert.equal(f.last('BB').completedDay, 0);
    assert.equal(f.last('BB').final, true);
});

test('host upload secures offline guest scores beyond day 30', () => {
    const f = fixture(), r = run(42);
    r.highestDay = 43;
    for (const p of r.participants) p.highestDay = 43;
    assert.equal(f.publish(r).accepted, true);
    assert.equal(f.scores.AA, 42);
    assert.equal(f.scores.BB, 42);
    assert.equal(f.last('BB').highestDay, 43);
});

test('duplicate and old revisions cannot lower or rewrite committed credit', () => {
    const f = fixture(), r = run(8, 2);
    assert.equal(f.publish(r).accepted, true);
    assert.equal(f.publish(r).accepted, true);
    assert.equal(f.publish(run(3, 1)).accepted, true);
    const collision = run(20, 2);
    assert.equal(f.publish(collision).accepted, true);
    assert.equal(f.scores.BB, 8);
    assert.equal(f.last('BB').completedDay, 8);
    assert.equal(f.publish(run(7, 3)).accepted, false);
});

test('shorter later run changes last summary but preserves best score', () => {
    const f = fixture();
    f.publish(run(12));
    const short = run(3, 1, '00000000000000000000000000000002');
    short.startedUtc = '2026-09-16T00:00:00.000Z';
    f.publish(short);
    assert.equal(f.scores.BB, 12);
    assert.equal(f.last('BB').completedDay, 3);
});

test('provisional absence credit is excluded until a host-confirmed rejoin', () => {
    const f = fixture(), r = run(7);
    Object.assign(r.participants[1], { completedDay: 5, highestDay: 6, provisionalDay: 7, disconnectDeadline: 123 });
    f.publish(r);
    assert.equal(f.scores.BB, 5);
    const forged = clone(r);
    forged.participants[1].completedDay = 7;
    assert.equal(f.claim(forged).accepted, false);
    r.revision++;
    Object.assign(r.participants[1], { completedDay: 7, highestDay: 7, provisionalDay: 0, disconnectDeadline: 0 });
    assert.equal(f.publish(r).accepted, true);
    assert.equal(f.claim(r).accepted, true);
    assert.equal(f.scores.BB, 7);
});

test('departed participant credit and roster identity cannot change', () => {
    const f = fixture(), r = run(7);
    Object.assign(r.participants[1], { completedDay: 5, highestDay: 6, departed: true });
    f.publish(r);
    const increased = clone(r);
    increased.revision++;
    increased.participants[1].completedDay = 6;
    assert.equal(f.publish(increased).accepted, false);
    const replaced = clone(r);
    replaced.revision++;
    replaced.participants[1].accountId = 'CC';
    assert.equal(f.publish(replaced).accepted, false);
    assert.equal(f.scores.BB, 5);
});

test('terminal run cannot reopen or earn another day', () => {
    const f = fixture(), r = run(4);
    r.endReason = 'Bankruptcy';
    f.publish(r);
    const reopen = clone(r);
    reopen.revision++;
    reopen.endReason = null;
    assert.equal(f.publish(reopen).accepted, false);
    const more = run(5, 3);
    more.endReason = 'HostLeft';
    assert.equal(f.publish(more).accepted, false);
});

test('guest local final reason is descriptive and cannot award extra credit', () => {
    const f = fixture(), r = run(9);
    f.publish(r);
    r.revision++;
    r.endReason = 'HostDisconnected';
    assert.equal(f.claim(r, 'BB', true).accepted, true);
    assert.equal(f.last('BB').outcome, 'HostDisconnected');
    assert.equal(f.claim(r, 'CC', true).accepted, false);
    r.completedDay = 10; r.highestDay = 10;
    r.participants[1].completedDay = 10; r.participants[1].highestDay = 10;
    assert.equal(f.claim(r, 'BB', true).accepted, false);
    assert.equal(f.scores.BB, 9);
});

test('partial participant write failure retries safely before receipt acknowledgement', () => {
    const f = fixture(), r = run(11);
    f.failOnce('BB');
    assert.throws(() => f.publish(r), /write outage/);
    assert.equal(f.scores.AA, 11);
    assert.equal(f.scores.BB, undefined);
    assert.equal(f.claim(r).accepted, false);
    assert.equal(f.publish(r).accepted, true);
    assert.equal(f.claim(r).accepted, true);
    assert.equal(f.scores.AA, 11);
    assert.equal(f.scores.BB, 11);
});

test('receipt storage stays bounded and aged receipts cannot grant extra score', () => {
    const f = fixture(), original = run(12);
    f.publish(original);
    for (let i = 1; i <= 40; i++) {
        const id = i.toString(16).padStart(4, '0') + '0'.repeat(28);
        const r = run(3, 1, id);
        r.startedUtc = new Date(Date.UTC(2026, 8, 15, 0, i)).toISOString();
        f.publish(r);
    }
    assert.ok(Object.keys(f.internal.AA).length <= 8);
    assert.equal(f.claim(original).accepted, true);
    const inflated = run(20);
    assert.equal(f.claim(inflated).accepted, false);
    assert.equal(f.scores.BB, 12);
});

test('unsupported schema, negative credit and excessive day values are rejected', () => {
    for (const change of [{ schema: 1 }, { rulesVersion: 'old' }, { completedDay: -1 },
        { completedDay: 1000001, highestDay: 1000001 }, { partySize: 5 }]) {
        const f = fixture();
        assert.equal(f.publish(Object.assign(run(), change)).accepted, false);
        assert.deepEqual(f.scores, {});
    }
});

for (const { name, body } of cases) {
    body();
    process.stdout.write('PASS ' + name + '\n');
}
process.stdout.write(cases.length + ' isolated contract checks completed. Live title and Unity validation still required.\n');
