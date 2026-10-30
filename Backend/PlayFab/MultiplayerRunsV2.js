// Classic PlayFab CloudScript. Merge into the title's existing revision.
// Numeric leaderboard truth is a server-written Max statistic. Clients cannot write it.
(function () {
    "use strict";
    function contract(restaurant) {
        if (restaurant === "CasualDining") return { stat: "CasualDiningLongestRunV2", last: "CasualDiningLastRunV2",
            receipt: "CDRun2Receipt", rules: "casual-session-2", scene: "Lobby1 Multiplayer" };
        if (restaurant === "FastFood") return { stat: "FastFoodLongestRunV2", last: "FastFoodLastRunV2",
            receipt: "FFRun2Receipt", rules: "fastfood-session-2", scene: "Lobby2 Multiplayer" };
        if (restaurant === "FineDining") return { stat: "FineDiningLongestRunV2", last: "FineDiningLastRunV2",
            receipt: "FDRun2Receipt", rules: "finedining-session-1", scene: "Lobby3 Multiplayer" };
        return null;
    }
    function fail(message) { return { accepted: false, message: message }; }
    function integer(value, min, max) { return typeof value === "number" && value % 1 === 0 && value >= min && value <= max; }
    function read(player, name, internal) {
        var request = { PlayFabId: player, Keys: [name] };
        var result = internal ? server.GetUserInternalData(request) : server.GetUserReadOnlyData(request);
        return result.Data && result.Data[name] ? JSON.parse(result.Data[name].Value) : null;
    }
    function write(player, name, value, internal) {
        var data = {}; data[name] = JSON.stringify(value);
        var request = { PlayFabId: player, Data: data };
        if (internal) server.UpdateUserInternalData(request);
        else { request.Permission = "Private"; server.UpdateUserReadOnlyData(request); }
    }
    function best(player, restaurant) {
        var stats = server.GetPlayerStatistics({ PlayFabId: player, StatisticNames: [contract(restaurant).stat] }).Statistics || [];
        return stats.length ? stats[0].Value : 0;
    }
    function valid(record) {
        var rules = record && contract(record.restaurant);
        if (!record || !rules || record.schema !== 2 || !/^[a-f0-9]{32}$/.test(record.runId || "") ||
            record.rulesVersion !== rules.rules || record.sceneName !== rules.scene ||
            !integer(record.partySize, 2, 4) || !integer(record.completedDay, 0, 1000000) ||
            !integer(record.highestDay, Math.max(1, record.completedDay), 1000001) ||
            !integer(record.revision, 1, 2147483647) || !record.participants || record.participants.length !== record.partySize ||
            typeof record.startedUtc !== "string" || !isFinite(Date.parse(record.startedUtc)) ||
            (record.endReason && ["HostLeft", "HostDisconnected", "Bankruptcy", "ApprovalCollapsed", "EarthConqueredDay30"].indexOf(record.endReason) < 0))
            return false;
        var accounts = {}, actors = {}, host = false;
        for (var i = 0; i < record.participants.length; i++) {
            var p = record.participants[i];
            if (!p || !/^[a-fA-F0-9]{1,64}$/.test(p.accountId || "") || !integer(p.actor, 1, 2147483647) ||
                accounts[p.accountId] || actors[p.actor] || !integer(p.completedDay, 0, record.completedDay) ||
                !integer(p.highestDay, Math.max(1, p.completedDay), record.highestDay)) return false;
            if (p.successfulDays != null && (!Array.isArray(p.successfulDays) || p.successfulDays.some(function (day, index, days) {
                return !integer(day, 1, p.completedDay) || days.indexOf(day) !== index ||
                    (["Bankruptcy", "ApprovalCollapsed", "EarthConqueredDay30"].indexOf(record.endReason) >= 0 && day === record.completedDay);
            }))) return false;
            accounts[p.accountId] = true; actors[p.actor] = true;
            if (p.accountId === record.hostAccountId && p.actor === record.hostActor) host = true;
        }
        return host;
    }
    function membership(record) {
        return record.participants.map(function (p) { return p.accountId + ":" + p.actor; }).sort().join("|");
    }
    // Eight fixed slots, regardless of run duration or number of runs. The score
    // is already committed to every participant before its host receipt can age out.
    function receiptKey(record) { return contract(record.restaurant).receipt + (parseInt(record.runId.substr(0, 4), 16) % 8); }
    function publishScore(record, participant, final) {
        var rules = contract(record.restaurant);
        server.UpdatePlayerStatistics({ PlayFabId: participant.accountId,
            Statistics: [{ StatisticName: rules.stat, Value: participant.completedDay }] });
        var previous = read(participant.accountId, rules.last, false);
        var sameRun = previous && previous.runId === record.runId;
        if (!previous || (sameRun && record.revision >= previous.revision &&
                (final || !previous.final)) || (!sameRun && Date.parse(record.startedUtc) > Date.parse(previous.startedUtc))) {
            write(participant.accountId, rules.last, {
                schema: 2, runId: record.runId, revision: record.revision, restaurant: record.restaurant,
                rulesVersion: record.rulesVersion, completedDay: participant.completedDay, highestDay: participant.highestDay,
                partySize: record.partySize, gameVersion: record.gameVersion, startedUtc: record.startedUtc,
                endedUtc: final ? record.endedUtc || new Date().toISOString() : null,
                final: final, outcome: final ? record.endReason || "PlayerLeft" : "InProgress",
                verification: "AuthenticatedPlayerHost"
            }, false);
        }
    }
    handlers.DineInPublishRunV2 = function (args) {
        var record = args && args.record;
        if (!valid(record) || record.hostAccountId !== currentPlayerId) return fail("Only the authenticated host can publish this run.");
        var key = receiptKey(record);
        var receipt = read(currentPlayerId, key, true);
        if (receipt && receipt.runId === record.runId) {
            if (membership(receipt) !== membership(record) || record.startedUtc !== receipt.startedUtc ||
                record.hostActor !== receipt.hostActor || record.gameVersion !== receipt.gameVersion
                || record.restaurant !== receipt.restaurant || record.rulesVersion !== receipt.rulesVersion
                || record.sceneName !== receipt.sceneName)
                return fail("Run membership and identity cannot change.");
            if (record.revision <= receipt.revision)
                return { accepted: true, bestCompletedDay: best(currentPlayerId, receipt.restaurant), successfulDays: (receipt.participants.filter(function(p) { return p.accountId === currentPlayerId; })[0] || {}).successfulDays || [] };
            else {
                if (record.completedDay < receipt.completedDay || record.highestDay < receipt.highestDay)
                    return fail("Run progress cannot decrease.");
                if (receipt.endReason && (!record.endReason || record.completedDay !== receipt.completedDay ||
                    record.highestDay !== receipt.highestDay)) return fail("This scored run already ended.");
                for (var j = 0; j < record.participants.length; j++) {
                    var p = record.participants[j];
                    var old = receipt.participants.filter(function (v) { return v.accountId === p.accountId; })[0];
                    if (old && (old.successfulDays || []).some(function(day) { return (p.successfulDays || []).indexOf(day) < 0; }))
                        return fail("Successful-day credit cannot decrease.");
                    if (old && (p.completedDay < old.completedDay || p.highestDay < old.highestDay ||
                        (old.departed && (!p.departed || p.completedDay > old.completedDay || p.highestDay > old.highestDay))))
                        return fail("Participant credit cannot change after departure.");
                }
            }
        }
        // Publish all frozen participants, including offline guests. A guest's
        // later receipt is an acknowledgement, never an authority for extra days.
        for (var i = 0; i < record.participants.length; i++)
            publishScore(record, record.participants[i], !!record.endReason || !!record.participants[i].departed);
        write(currentPlayerId, key, record, true);
        return { accepted: true, bestCompletedDay: best(currentPlayerId, record.restaurant), successfulDays: (record.participants.filter(function(p) { return p.accountId === currentPlayerId; })[0] || {}).successfulDays || [] };
    };
    handlers.DineInClaimRunV2 = function (args) {
        var record = args && args.record;
        // End reasons reported locally by a disconnected guest are descriptive
        // only; score authorization always comes from the stored host receipt.
        var reason = record && record.endReason;
        if (record) record.endReason = null;
        if (!valid(record)) return fail("Invalid run result.");
        record.endReason = reason;
        var own = record.participants.filter(function (p) { return p.accountId === currentPlayerId; })[0];
        if (!own) return fail("Account is not a participant.");
        var secured = best(currentPlayerId, record.restaurant);
        var receipt = read(record.hostAccountId, receiptKey(record), true);
        if (receipt && receipt.runId === record.runId && membership(receipt) === membership(record)
            && receipt.restaurant === record.restaurant && receipt.rulesVersion === record.rulesVersion
            && receipt.sceneName === record.sceneName && receipt.startedUtc === record.startedUtc
            && receipt.hostAccountId === record.hostAccountId && receipt.gameVersion === record.gameVersion) {
            var verified = receipt.participants.filter(function (p) { return p.accountId === currentPlayerId; })[0];
            if (!verified || own.completedDay > verified.completedDay || own.highestDay > verified.highestDay)
                return fail("Awaiting the host completion receipt.");
            if (args.final) publishScore(record, own, true);
            return { accepted: true, bestCompletedDay: best(currentPlayerId, record.restaurant), successfulDays: verified.successfulDays || [] };
        }
        // An aged-out receipt needs no write: the host already secured this or a
        // better score directly on the participant's account.
        return secured >= own.completedDay ? { accepted: true, bestCompletedDay: secured }
            : fail("Awaiting the host completion receipt.");
    };
}());
