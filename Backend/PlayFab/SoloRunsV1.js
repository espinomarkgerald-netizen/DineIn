// Classic CloudScript; merge into the existing revision. No deployment is performed by this file.
// Authenticated self-reporting, NOT server-verified gameplay. Requires Max / Never statistics.
(function () {
    "use strict";
    function contract(restaurant) {
        if (restaurant === "CasualDining") return { stat: "CasualDiningSoloLongestRunV1", scene: "Lobby1", rules: "casual-solo-1" };
        if (restaurant === "FastFood") return { stat: "FastFoodSoloLongestRunV1", scene: "Lobby2", rules: "fastfood-solo-1" };
        if (restaurant === "FineDining") return { stat: "FineDiningSoloLongestRunV1", scene: "Lobby3", rules: "finedining-solo-1" };
        return null;
    }
    function fail(message) { return { accepted: false, message: message }; }
    handlers.DineInPublishSoloRunV1 = function (args) {
        var record = args && args.record;
        var rules = record && contract(record.restaurant);
        if (!currentPlayerId || !record || !rules || record.schema !== 1 ||
            record.sceneName !== rules.scene || record.rulesVersion !== rules.rules ||
            !/^[a-f0-9]{32}$/.test(record.runId || "") ||
            typeof record.completedDays !== "number" || record.completedDays % 1 !== 0 ||
            record.completedDays < 1 || record.completedDays > 1000000 ||
            typeof record.gameVersion !== "string" || !record.gameVersion || record.gameVersion.length > 64 ||
            record.accountId != null || record.hostAccountId != null || record.participants != null)
            return fail("Invalid solo run result.");
        // Absolute Max writes are safe to retry after any partial/unknown API failure.
        // Never read/compare/write a lower best or increment a statistic for an upload.
        server.UpdatePlayerStatistics({ PlayFabId: currentPlayerId,
            Statistics: [{ StatisticName: rules.stat, Value: record.completedDays }] });
        var statistics = server.GetPlayerStatistics({ PlayFabId: currentPlayerId, StatisticNames: [rules.stat] }).Statistics || [];
        var best = statistics.length ? statistics[0].Value : 0;
        if (best < record.completedDays) return fail("Statistic Max configuration needs verification.");
        return { accepted: true, runId: record.runId, restaurant: record.restaurant,
            completedDays: record.completedDays, bestCompletedDays: best };
    };
}());
