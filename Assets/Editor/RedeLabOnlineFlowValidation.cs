using System;
using RedeLabEscola.Auth;
using RedeLabEscola.Menu;
using UnityEngine;

public static class RedeLabOnlineFlowValidation
{
    private static readonly string[] OfficeCodes =
    {
        "sala1_colocar_gabinete", "sala1_configurar_ip_pc", "sala1_abrir_porta",
        "sala2_colocar_gabinete", "sala2_configurar_ip_pc", "sala2_configurar_ip_portas", "sala2_abrir_portas",
        "sala3_colocar_gabinete", "sala3_configurar_ip_pc", "sala3_colocar_impressora",
        "sala3_configurar_ip_impressora", "sala3_imprimir_documento", "sala3_pegar_documento",
        "sala3_entregar_documento", "sala3_configurar_ip_porta", "sala3_abrir_porta"
    };

    private static readonly string[] FactoryCodes =
    {
        "fabrica_bracos_roboticos", "fabrica_limpar_entulhos_garra",
        "fabrica_pallets_esteira_empilhadeira", "fabrica_pallets_gerados_enviados"
    };

    public static void Run()
    {
        RedeLabUser userWithoutCharacter = JsonUtility.FromJson<RedeLabUser>(
            "{\"id_usuario\":7,\"nome\":\"Teste\",\"email\":\"teste@example.com\",\"id_personagem\":null}");
        Require(
            userWithoutCharacter != null && userWithoutCharacter.id_personagem == 0,
            "JsonUtility converte id_personagem NULL para estado nao selecionado");

        RedeLabMission[] office = CreateCatalog(1, 16);
        RedeLabMission[] factory = CreateCatalog(2, 4);

        Require(
            RedeLabLoadGameResolver.Resolve(new RedeLabProgress
            {
                missoes_concluidas = Array.Empty<RedeLabCompletedMission>()
            }, office, factory) == RedeLabLoadGameResult.NoProgress,
            "Load Game sem progresso");

        Require(
            RedeLabLoadGameResolver.Resolve(CreateProgress(1, 5, 0), office, factory)
                == RedeLabLoadGameResult.Office,
            "Load Game com Escritorio pendente");

        Require(
            RedeLabLoadGameResolver.Resolve(CreateProgress(1, 16, 2), office, factory)
                == RedeLabLoadGameResult.Factory,
            "Load Game com Escritorio completo e Fabrica pendente");

        Require(
            RedeLabLoadGameResolver.Resolve(CreateProgress(1, 16, 4), office, factory)
                == RedeLabLoadGameResult.CurrentGameCompleted,
            "Load Game com fases atuais completas");

        RedeLabLoadGameResolver.Resolution room2 = RedeLabLoadGameResolver.ResolveContext(
            CreateProgress(1, 3, 0), office, factory);
        Require(
            room2.Result == RedeLabLoadGameResult.Office
                && room2.Room == 2
                && room2.FirstPendingMission == "sala2_colocar_gabinete"
                && room2.CompletedMissionCodes.Length == 3,
            "Load Game resolve primeira missao pendente, sala e codigos restaurados");

        ValidateMissionPersistenceGuards();

        CharacterSelectionState.SetPendingGameplayScene("Stage2_Factory");
        Require(
            CharacterSelectionState.ConsumePendingGameplayScene("SampleScene") == "Stage2_Factory",
            "Destino pendente da selecao de personagem");
        Require(
            CharacterSelectionState.ConsumePendingGameplayScene("SampleScene") == "SampleScene",
            "Destino pendente e consumido somente uma vez");

        Debug.Log("Validacao dos fluxos online RedeLab concluida com sucesso.");
    }

    private static RedeLabMission[] CreateCatalog(int phaseId, int count)
    {
        string[] codes = phaseId == 1 ? OfficeCodes : FactoryCodes;
        RedeLabMission[] result = new RedeLabMission[count];
        for (int index = 0; index < count; index++)
        {
            result[index] = new RedeLabMission
            {
                id_fase = phaseId,
                id_missao = index + 1,
                codigo = codes[index],
                numero_missao = index + 1,
                ativa = 1
            };
        }
        return result;
    }

    private static RedeLabProgress CreateProgress(int firstPhaseId, int officeCount, int factoryCount)
    {
        RedeLabCompletedMission[] missions = new RedeLabCompletedMission[officeCount + factoryCount];
        int position = 0;
        for (int index = 0; index < officeCount; index++)
        {
            missions[position++] = new RedeLabCompletedMission
            {
                id_fase = firstPhaseId,
                id_missao = index + 1,
                codigo = OfficeCodes[index]
            };
        }
        for (int index = 0; index < factoryCount; index++)
        {
            missions[position++] = new RedeLabCompletedMission
            {
                id_fase = 2,
                id_missao = index + 1,
                codigo = FactoryCodes[index]
            };
        }
        return new RedeLabProgress { missoes_concluidas = missions };
    }

    private static void ValidateMissionPersistenceGuards()
    {
        Require(MissionManager.IsNewCompletionTransition(false, true), "Somente false -> true e conclusao nova");
        Require(!MissionManager.IsNewCompletionTransition(true, true), "Reavaliacao true -> true nao persiste");
        Require(!MissionManager.IsNewCompletionTransition(true, false), "Reversao true -> false nao persiste");
        Require(!RedeLabProgressService.IsRuntimePersistencePlatform, "Unity Editor bloqueia transporte de progresso");

        foreach (string code in OfficeCodes) Require(RedeLabProgressService.IsKnownMissionCode(code), "Codigo de escritorio permitido: " + code);
        foreach (string code in FactoryCodes) Require(RedeLabProgressService.IsKnownMissionCode(code), "Codigo de fabrica permitido: " + code);
        Require(!RedeLabProgressService.IsKnownMissionCode("missao_inventada"), "Codigo desconhecido bloqueado");

        RedeLabProgressService service = RedeLabProgressService.EnsureInstance();
        service.ResetSession();
        service.BeginOnlineGameplaySession(new[] { OfficeCodes[0] }, true);
        Require(service.IsRestoringSave, "Load Game sinaliza restauracao em andamento");
        Require(!service.TryQueueMissionCompletion(OfficeCodes[1]) && service.PendingCount == 0,
            "Restauracao no Editor gera zero fila e zero POST");
        service.FinishSaveRestore();
        Require(!service.TryQueueMissionCompletion(OfficeCodes[1]) && service.PendingCount == 0,
            "Gameplay no Editor gera zero fila e zero POST");
        service.ResetSession();
    }

    private static void Require(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("Falhou: " + description);
    }
}
