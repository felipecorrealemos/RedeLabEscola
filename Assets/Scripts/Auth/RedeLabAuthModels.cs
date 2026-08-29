using System;

namespace RedeLabEscola.Auth
{
    [Serializable]
    public sealed class RedeLabUser
    {
        public int id_usuario;
        public string nome;
        public string email;
        public int id_personagem;
    }

    [Serializable]
    public sealed class RedeLabSetCharacterResponse
    {
        public bool success;
        public int id_usuario;
        public int id_personagem;
    }

    [Serializable]
    public sealed class RedeLabOperationResponse
    {
        public bool success;
        public int id_usuario;
        public int registrosRemovidos;
    }

    [Serializable]
    public sealed class RedeLabCompleteMissionResponse
    {
        public bool success;
        public bool alreadyCompleted;
        public int id_usuario;
        public int id_fase;
        public int id_missao;
        public string codigo_missao;
    }

    [Serializable]
    public sealed class RedeLabCompletedMission
    {
        public int id_fase;
        public int id_missao;
        public string codigo;
        public int numero_missao;
        public string nome;
        public string fase_nome;
        public string data_conclusao;
    }

    [Serializable]
    public sealed class RedeLabProgress
    {
        public int id_usuario;
        public RedeLabCompletedMission[] missoes_concluidas;
    }

    [Serializable]
    public sealed class RedeLabMission
    {
        public int id_missao;
        public int id_fase;
        public string codigo;
        public int numero_missao;
        public string nome;
        public string descricao;
        public int ativa;
    }

    [Serializable]
    internal sealed class RedeLabMissionList
    {
        public RedeLabMission[] items;
    }

    [Serializable]
    internal sealed class RedeLabSetCharacterRequest
    {
        public int id_personagem;
    }

    [Serializable]
    internal sealed class RedeLabCompleteMissionRequest
    {
        public string codigo_missao;
    }

    [Serializable]
    internal sealed class RedeLabApiError
    {
        public string error;
    }
}
