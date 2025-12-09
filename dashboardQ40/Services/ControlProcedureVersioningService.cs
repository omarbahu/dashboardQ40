using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using dashboardQ40.Models;
using dashboardQ40.Repositories;

namespace dashboardQ40.Services
{
    public class ControlProcedureVersioningService
    {
        private readonly ILogger<ControlProcedureVersioningService> _logger;
        private readonly WSCaptorControlProcedureRepository _cpRepo;

        public ControlProcedureVersioningService(
            ILogger<ControlProcedureVersioningService> logger,
            WSCaptorControlProcedureRepository cpRepo)
        {
            _logger = logger;
            _cpRepo = cpRepo;
        }

        public async Task ApplyAsync(string token, ApplyLimitsRequest req, CancellationToken ct = default)
        {
            _logger.LogInformation("ApplyAsync llamado para company {Company} con {Count} items",
                req.Company, req.Items?.Count ?? 0);

            if (req.Items == null || req.Items.Count == 0)
                return;

            // 1) Agrupar por CP + versión + nivel actual
            var groups = req.Items
                .GroupBy(i => new { i.ControlProcedure, i.CurrentVersion, i.CurrentLevel });

            foreach (var g in groups)
            {
                string cp = g.Key.ControlProcedure;
                string currentVersion = g.Key.CurrentVersion;
                short currentLevel = g.Key.CurrentLevel;

                _logger.LogInformation("Procesando CP {CP}, versión {Ver}, nivel {Lvl} con {Count} items",
                    cp, currentVersion, currentLevel, g.Count());

                // 2) Leer todas las filas de ControlProcedure para ese CP
                var rows = await _cpRepo.GetControlProcedureAsync(token, req.Company, cp, ct);
                if (rows == null || rows.Count == 0)
                {
                    _logger.LogWarning("No se encontraron filas en ControlProcedure para CP {CP}", cp);
                    continue;
                }

                // 3) Calcular nueva versión: max existente + 1
                int maxVer = rows
                    .Select(r => r.controlProcedureVersion)
                    .Distinct()
                    .Select(v => int.TryParse(v, out var n) ? n : 0)
                    .DefaultIfEmpty(0)
                    .Max();

                int newVerInt = maxVer + 1;
                string newVersion = newVerInt.ToString();

                _logger.LogInformation(
                    "CP {CP}: versión actual {CurVer}, nivel {CurLvl}. Nueva versión propuesta: {NewVer}",
                    cp, currentVersion, currentLevel, newVersion);

                // 4) Filtrar filas de la versión/nivel actual
                var sourceRows = rows
                    .Where(r => r.controlProcedureVersion == currentVersion
                             && r.controlProcedureLevel == currentLevel)
                    .ToList();

                if (sourceRows.Count == 0)
                {
                    _logger.LogWarning(
                        "CP {CP}: no se encontraron filas en ControlProcedure para versión {Ver} nivel {Lvl}",
                        cp, currentVersion, currentLevel);
                    continue;
                }

                // 5) Clonar a nueva versión/nivel (por ahora solo en memoria)
                var clonedRows = sourceRows
                    .Select(r => CloneControlProcedureRow(r, newVersion, newLevel: 1))
                    .ToList();

                _logger.LogInformation(
                    "CP {CP}: se clonaron {Count} filas de ControlProcedure a nueva versión {NewVer}, nivel 1",
                    cp, clonedRows.Count, newVersion);

                // TODO: más adelante:
                // - guardar estos clones en un "plan de clonado"
                // - clonar también Association, LaunchProgram, Operation, Version, Steps
                // - y finalmente hacer POST al WS


                _logger.LogInformation(
                    "CP {CP}: versión actual {CurVer}, nivel {CurLvl}. Nueva versión propuesta: {NewVer}",
                    cp, currentVersion, currentLevel, newVersion);

                // Aquí en el siguiente paso vamos a:
                // - filtrar filas de la versión/nivel actual
                // - construir las filas clonadas con newVersion y nivel 1
                // - luego escribirlas vía REST
            }
        }

        private ControlProcedureRow CloneControlProcedureRow(
    ControlProcedureRow src,
    string newVersion,
    short newLevel)
        {
            return new ControlProcedureRow
            {
                company = src.company,
                controlProcedure = src.controlProcedure,
                controlProcedureVersion = newVersion,
                controlProcedureLevel = newLevel,

                controlProcedureLevelName = src.controlProcedureLevelName,
                controlProcedureLevelDescrip = src.controlProcedureLevelDescrip,

                showInTerminal = src.showInTerminal,
                isManual = src.isManual,
                controlProcedureType = src.controlProcedureType,
                manualAffectNext = src.manualAffectNext,
                recordState = src.recordState,

                executedAffectNext = src.executedAffectNext,
                allowPartialReporting = src.allowPartialReporting,
                controlProcedureClass = src.controlProcedureClass,
                allowClosingIncompleteCP = src.allowClosingIncompleteCP,
                expireOnRelaunch = src.expireOnRelaunch,
                expireOnPhaseClosing = src.expireOnPhaseClosing,
                expireOnShiftChange = src.expireOnShiftChange,
                expireOnPhaseRemoval = src.expireOnPhaseRemoval,

                expirationTime = src.expirationTime,
                workplacePresenceRequired = src.workplacePresenceRequired,
                passwordRequired = src.passwordRequired,
                lockHigherRankLevels = src.lockHigherRankLevels,
                emptyCPROSavingBehaviour = src.emptyCPROSavingBehaviour,

                creationUser = src.creationUser,
                creationData = src.creationData,
                lastUpdateUser = src.lastUpdateUser,
                lastUpdateData = src.lastUpdateData
            };
        }

    }
}
