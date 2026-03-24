// ============================================================================
// CloudScript - PiVillage
// Deploy em: PlayFab Game Manager > Live Ops > Cloud Script > Revisions (Legacy)
// ============================================================================

// ============================================================================
// PET SYSTEM — constantes de produção
// ============================================================================

// Cocô
var POOP_INTERVAL_MS  = 4 * 60 * 60 * 1000;    // 1 cocô a cada 4 horas
var MAX_POOPS_PER_DAY = 5;                       // máximo 5 cocôs por dia
var DAY_MS            = 24 * 60 * 60 * 1000;    // janela de 24h para o limite diário

// Depleção de stats (pontos por ms): quanto cai até zerar partindo de 100
var HUNGER_FULL_MS      = 8  * 60 * 60 * 1000;  // fome: 100→0 em 8h
var CLEAN_FULL_MS       = 12 * 60 * 60 * 1000;  // limpeza: 100→0 em 12h
var MOOD_FULL_MS        = 24 * 60 * 60 * 1000;  // temperamento: 100→0 em 24h
var HEALTH_DECAY_MS     = 24 * 60 * 60 * 1000;  // saúde: 100→0 em 24h (só quando faminto E sujo)
var HEALTH_RECOVER_MS   = 6  * 60 * 60 * 1000;  // saúde: recupera 100 pontos em 6h (quando OK)

var HUNGER_RATE      = 100 / HUNGER_FULL_MS;
var CLEAN_RATE       = 100 / CLEAN_FULL_MS;
var MOOD_RATE        = 100 / MOOD_FULL_MS;
var HEALTH_DEC_RATE  = 100 / HEALTH_DECAY_MS;
var HEALTH_REC_RATE  = 100 / HEALTH_RECOVER_MS;

// Limiares para flags booleanas derivadas
var HUNGRY_THR      = 30;               // hunger  < 30  → isHungry
var DIRTY_THR       = 30;               // clean   < 30  → isDirty
var SICK_THR        = 30;               // health  < 30  → isSick
// isAngry é controlado por timer, não por threshold de mood
var ANGRY_DURATION_MS = 2 * 60 * 1000; // 2 minutos bravo ao receber carinho em excesso

// ============================================================================
// Funções auxiliares de persistência
// ============================================================================

function loadPetState() {
    var data = server.GetUserData({ PlayFabId: currentPlayerId, Keys: ["PetState"] });
    if (!data.Data || !data.Data["PetState"]) return null;
    try { return JSON.parse(data.Data["PetState"].Value); } catch (e) { return null; }
}

function savePetState(state) {
    server.UpdateUserData({
        PlayFabId : currentPlayerId,
        Data      : { "PetState": JSON.stringify(state) }
    });
}

// ============================================================================
// evaluateStats — depleta/recupera os 4 stats numéricos e deriva os flags
// ============================================================================

function evaluateStats(state) {
    if (!state || state.isDead) return state;

    var now = Date.now();

    // Inicializa campos em pets antigos ou recém-criados sem statsCheckedAt
    if (!state.statsCheckedAt || state.statsCheckedAt === 0) {
        state.statsCheckedAt = now;
        return deriveFlags(state);
    }

    var elapsed = now - state.statsCheckedAt;

    // Verifica se o timer de raiva expirou
    if ((state.angryUntilUtc || 0) > 0 && now >= state.angryUntilUtc) {
        state.angryUntilUtc = 0;
        state.mood = 10; // começa a recuperação com 10%
    }

    // Depleta hunger, cleanliness e mood pelo tempo decorrido
    state.hunger      = Math.max(0, (state.hunger      || 50) - elapsed * HUNGER_RATE);
    state.cleanliness = Math.max(0, (state.cleanliness || 50) - elapsed * CLEAN_RATE);
    // Mood só depleta se não está bravo (timer ativo congela o mood em 0)
    if (!(state.angryUntilUtc > 0)) {
        state.mood = Math.max(0, (state.mood || 50) - elapsed * MOOD_RATE);
    }

    // Saúde: decai quando faminto E sujo; recupera quando condições estão OK
    var badCondition = state.hunger < HUNGRY_THR && state.cleanliness < DIRTY_THR;
    if (badCondition) {
        state.health = Math.max(0, (state.health || 50) - elapsed * HEALTH_DEC_RATE);
    } else {
        state.health = Math.min(100, (state.health || 50) + elapsed * HEALTH_REC_RATE);
    }

    state.statsCheckedAt = now;

    return deriveFlags(state);
}

// Deriva os flags booleanos a partir dos valores numéricos atuais
function deriveFlags(state) {
    var now = Date.now();
    state.isHungry = state.hunger      < HUNGRY_THR;
    state.isDirty  = state.cleanliness < DIRTY_THR;
    state.isSick   = state.health      < SICK_THR;
    state.isDead   = state.health      <= 0;
    // isAngry é puramente baseado no timer (carinho em excesso)
    state.isAngry  = (state.angryUntilUtc || 0) > now;
    return state;
}

// ============================================================================
// evaluatePoops — acumula cocôs no servidor respeitando limite diário
// ============================================================================

function evaluatePoops(state) {
    var now = Date.now();

    if (!state.lastPoopGeneratedUtc) {
        state.lastPoopGeneratedUtc = now;
        state.pendingPoops         = 0;
        state.poopsToday           = 0;
        state.poopDayStartUtc      = now;
        return state;
    }

    // Reseta contador diário após 24h
    if ((now - state.poopDayStartUtc) >= DAY_MS) {
        state.poopsToday      = 0;
        state.poopDayStartUtc = now;
    }

    if (state.isDead || state.poopsToday >= MAX_POOPS_PER_DAY) return state;

    var elapsed  = now - state.lastPoopGeneratedUtc;
    var newPoops = Math.floor(elapsed / POOP_INTERVAL_MS);

    if (newPoops > 0) {
        var canGenerate            = Math.min(newPoops, MAX_POOPS_PER_DAY - state.poopsToday);
        state.pendingPoops         = (state.pendingPoops || 0) + canGenerate;
        state.poopsToday           = (state.poopsToday   || 0) + canGenerate;
        state.lastPoopGeneratedUtc += canGenerate * POOP_INTERVAL_MS;
    }

    return state;
}

// ============================================================================
// Handlers
// ============================================================================

// ----------------------------------------------------------------------------
// CreatePet: cria o pet inicial com todos os stats em 50%
// ----------------------------------------------------------------------------
handlers.CreatePet = function (args) {
    var now = Date.now();

    var state = {
        petName              : args.petName  || "Pet",
        petType              : args.petType  || "Cat",
        petIndex             : args.petIndex || 0,

        // Stats numéricos (0–100), todos começam em 50%
        hunger               : 50,
        cleanliness          : 50,
        mood                 : 50,
        health               : 50,
        statsCheckedAt       : now,

        // Flags booleanas derivadas
        isHungry             : false,
        isDirty              : false,
        isSick               : false,
        isAngry              : false,
        isDead               : false,
        angryUntilUtc        : 0,

        // Cocô
        pendingPoops         : 0,
        poopsToday           : 0,
        lastPoopGeneratedUtc : now,
        poopDayStartUtc      : now
    };

    savePetState(state);
    return { success: true, state: state };
};

// ----------------------------------------------------------------------------
// GetPetState: avalia e retorna o estado atual do pet
// ----------------------------------------------------------------------------
handlers.GetPetState = function (args) {
    var state = loadPetState();
    if (!state) return { exists: false };

    // Migração: pets criados antes do sistema numérico não têm esses campos
    var now = Date.now();
    if (state.hunger === undefined || state.hunger === null) {
        state.hunger         = 50;
        state.cleanliness    = 50;
        state.mood           = 50;
        state.health         = 50;
        state.statsCheckedAt = now;
    }
    if (!state.lastPoopGeneratedUtc) {
        state.lastPoopGeneratedUtc = now - POOP_INTERVAL_MS;
        state.pendingPoops         = 0;
        state.poopsToday           = 0;
        state.poopDayStartUtc      = now;
    }

    state = evaluateStats(state);
    state = evaluatePoops(state);

    var newPoops       = state.pendingPoops || 0;
    state.pendingPoops = 0;

    savePetState(state);
    return { exists: true, state: state, newPoops: newPoops };
};

// ----------------------------------------------------------------------------
// FeedPet: restaura hunger (+60). Alimentar em excesso prejudica a saúde.
// ----------------------------------------------------------------------------
handlers.FeedPet = function (args) {
    var state = loadPetState();
    if (!state) return { success: false, error: "Pet nao encontrado." };

    state = evaluateStats(state);

    if (!state.isDead) {
        if (state.hunger >= 70) {
            // Alimentou em excesso → prejudica saúde
            state.health = Math.max(0, (state.health || 50) - 20);
        } else {
            state.hunger = Math.min(100, (state.hunger || 0) + 60);
        }
        state = deriveFlags(state);
    }

    savePetState(state);
    return { success: true, state: state };
};

// ----------------------------------------------------------------------------
// BathePet: restaura cleanliness para 100
// ----------------------------------------------------------------------------
handlers.BathePet = function (args) {
    var state = loadPetState();
    if (!state) return { success: false, error: "Pet nao encontrado." };

    state = evaluateStats(state);

    if (!state.isDead) {
        state.cleanliness = 100;
        state = deriveFlags(state);
    }

    savePetState(state);
    return { success: true, state: state };
};

// ----------------------------------------------------------------------------
// GiveCarinho: restaura mood (+25). Carinho em excesso irrita o pet.
// ----------------------------------------------------------------------------
handlers.GiveCarinho = function (args) {
    var state = loadPetState();
    if (!state) return { success: false, error: "Pet nao encontrado." };

    state = evaluateStats(state);

    if (!state.isDead && !state.isAngry) {
        if (state.mood >= 100) {
            // Carinho em excesso: mood vai a 0 e pet fica bravo por 2 minutos
            state.mood         = 0;
            state.angryUntilUtc = Date.now() + ANGRY_DURATION_MS;
        } else {
            state.mood = Math.min(100, (state.mood || 0) + 25);
        }
        state = deriveFlags(state);
    }

    savePetState(state);
    return { success: true, state: state };
};

// ----------------------------------------------------------------------------
// GiveMedicine: restaura health (+50). Remédio desnecessário irrita o pet.
// ----------------------------------------------------------------------------
handlers.GiveMedicine = function (args) {
    var state = loadPetState();
    if (!state) return { success: false, error: "Pet nao encontrado." };

    // Só bloqueia se o pet JÁ estava morto antes desta chamada
    if (state.isDead) return { success: false, error: "Pet esta morto." };

    state = evaluateStats(state);

    // Cura se saúde < 50 (doente ou agonizando por race condition no evaluateStats)
    if (state.health < 50) {
        state.health = Math.min(100, (state.health || 0) + 50);
        state = deriveFlags(state);
    } else {
        // Remédio desnecessário → irrita o pet
        state.mood = Math.max(0, (state.mood || 50) - 30);
        state = deriveFlags(state);
    }

    savePetState(state);
    return { success: true, state: state };
};

// ----------------------------------------------------------------------------
// RevivePet: revive o pet morto com todos os stats em 50%
// ----------------------------------------------------------------------------
handlers.RevivePet = function (args) {
    var state = loadPetState();
    if (!state) return { success: false, error: "Pet nao encontrado." };

    if (!state.isDead) return { success: false, error: "Pet nao esta morto." };

    var now = Date.now();
    state.hunger         = 50;
    state.cleanliness    = 50;
    state.mood           = 50;
    state.health         = 50;
    state.statsCheckedAt = now;
    state.isDead         = false;
    state.isSick         = false;
    state.isAngry        = false;
    state.isHungry       = false;
    state.isDirty        = false;

    savePetState(state);
    return { success: true, state: state };
};

// ----------------------------------------------------------------------------
// CollectPoop: registra coleta de cocô e incrementa moedas do jogador
// ----------------------------------------------------------------------------
handlers.CollectPoop = function (args) {
    var coinsPerPoop = args.coinsPerPoop || 5;

    var userData = server.GetUserData({ PlayFabId: currentPlayerId, Keys: ["Coins"] });
    var coins    = 0;
    if (userData.Data && userData.Data["Coins"])
        coins = parseInt(userData.Data["Coins"].Value) || 0;

    coins += coinsPerPoop;

    server.UpdateUserData({
        PlayFabId : currentPlayerId,
        Data      : { "Coins": coins.toString() }
    });

    return { success: true, coins: coins };
};

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
// ----------------------------------------------------------------------------
handlers.GetAllPlayers = function (args) {
    var titleData = server.GetTitleData({ Keys: ["PlayerRegistry"] });
    var registry  = [];
    if (titleData.Data && titleData.Data["PlayerRegistry"]) {
        try { registry = JSON.parse(titleData.Data["PlayerRegistry"]); } catch (e) {}
    }
    var nameMap = {};
    for (var i = 0; i < registry.length; i++) {
        nameMap[registry[i].id] = registry[i].displayName;
    }

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
    } catch (e) {}

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
