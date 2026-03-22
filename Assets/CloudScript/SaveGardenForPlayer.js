// ============================================================================
// CloudScript - PiVillage
// Deploy em: PlayFab Game Manager > Live Ops > Cloud Script > Revisions (Legacy)
// ============================================================================

// ----------------------------------------------------------------------------
// RegisterPlayer: chamado no login para manter registro de todos os jogadores
// ----------------------------------------------------------------------------
handlers.RegisterPlayer = function (args) {
    var displayName = args.displayName || currentPlayerId;

    var titleData = server.GetTitleData({ Keys: ["PlayerRegistry"] });
    var registry  = [];

    if (titleData.Data && titleData.Data["PlayerRegistry"]) {
        try { registry = JSON.parse(titleData.Data["PlayerRegistry"]); } catch (e) {}
    }

    var found = false;
    for (var i = 0; i < registry.length; i++) {
        if (registry[i].id === currentPlayerId) {
            registry[i].displayName = displayName;
            found = true;
            break;
        }
    }
    if (!found) {
        registry.push({ id: currentPlayerId, displayName: displayName });
    }

    server.SetTitleData({ Key: "PlayerRegistry", Value: JSON.stringify(registry) });

    return { success: true, total: registry.length };
};

// ----------------------------------------------------------------------------
// GetAllPlayers: retorna TODOS os jogadores do título usando GetPlayersInSegment
// Usa o registro local para nomes; cai no PlayFabId se o nome não estiver salvo
// ----------------------------------------------------------------------------
handlers.GetAllPlayers = function (args) {
    // Carrega o registro de nomes (jogadores que logaram com o código novo)
    var titleData = server.GetTitleData({ Keys: ["PlayerRegistry"] });
    var registry  = [];
    if (titleData.Data && titleData.Data["PlayerRegistry"]) {
        try { registry = JSON.parse(titleData.Data["PlayerRegistry"]); } catch (e) {}
    }
    var nameMap = {};
    for (var i = 0; i < registry.length; i++) {
        nameMap[registry[i].id] = registry[i].displayName;
    }

    // Busca o segmento "All Players" para listar todos os jogadores já criados
    try {
        var segments  = server.GetAllSegments({});
        var segmentId = null;
        for (var s = 0; s < segments.Segments.length; s++) {
            if (segments.Segments[s].Name === "All Players") {
                segmentId = segments.Segments[s].Id;
                break;
            }
        }

        if (segmentId) {
            var segResult = server.GetPlayersInSegment({ SegmentId: segmentId, MaxBatchSize: 100 });
            var players   = [];
            for (var p = 0; p < segResult.PlayerProfiles.length; p++) {
                var profile = segResult.PlayerProfiles[p];
                var name    = nameMap[profile.PlayerId] || profile.DisplayName || profile.PlayerId;
                players.push({ id: profile.PlayerId, displayName: name });
            }
            return { players: players };
        }
    } catch (e) {
        // Se falhar, usa só o registro local
    }

    return { players: registry };
};

// ----------------------------------------------------------------------------
// SaveGardenForPlayer: salva flores no jardim de outro jogador
// ----------------------------------------------------------------------------
handlers.SaveGardenForPlayer = function (args) {
    var targetPlayerId = args.targetPlayerId;
    var flowersJson    = args.flowersJson;

    if (!targetPlayerId || !flowersJson) {
        return { success: false, error: "Parâmetros inválidos." };
    }

    try {
        server.UpdateUserData({
            PlayFabId  : targetPlayerId,
            Data       : { "FlowerPlacements": flowersJson },
            Permission : "Public"
        });

        return { success: true };
    } catch (e) {
        return { success: false, error: e.toString() };
    }
};
