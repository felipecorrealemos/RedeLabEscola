using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace RedeLabEscola.Auth
{
    public sealed class RedeLabApiClient
    {
        private readonly string baseUrl;
        private readonly Func<string> accessTokenProvider;

        public string BaseUrl => baseUrl;
        public event Action OnUnauthorized;

        public RedeLabApiClient(string baseUrl, Func<string> accessTokenProvider)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException("A URL base da API e obrigatoria.", nameof(baseUrl));
            }

            this.baseUrl = baseUrl.TrimEnd('/');
            this.accessTokenProvider = accessTokenProvider
                ?? throw new ArgumentNullException(nameof(accessTokenProvider));
        }

        public IEnumerator SyncUser(Action<RedeLabUser> onSuccess, Action<string> onError)
        {
            return SendJsonRequest("POST", "/api/auth/sync", null, onSuccess, onError);
        }

        public IEnumerator GetMe(Action<RedeLabUser> onSuccess, Action<string> onError)
        {
            return SendJsonRequest("GET", "/api/me", null, onSuccess, onError);
        }

        public IEnumerator SetCharacter(
            int characterId,
            Action<RedeLabSetCharacterResponse> onSuccess,
            Action<string> onError)
        {
            RedeLabSetCharacterRequest body = new RedeLabSetCharacterRequest { id_personagem = characterId };
            return SendJsonRequest(
                "PUT",
                "/api/me/personagem",
                JsonUtility.ToJson(body),
                onSuccess,
                onError);
        }

        public IEnumerator GetProgress(Action<RedeLabProgress> onSuccess, Action<string> onError)
        {
            return SendJsonRequest("GET", "/api/progresso/me", null, onSuccess, onError);
        }

        public IEnumerator CompleteMission(
            string missionCode,
            Action<RedeLabCompleteMissionResponse> onSuccess,
            Action<string> onError)
        {
            RedeLabCompleteMissionRequest body = new RedeLabCompleteMissionRequest
            {
                codigo_missao = missionCode
            };
            return SendJsonRequest(
                "POST",
                "/api/progresso/concluir",
                JsonUtility.ToJson(body),
                onSuccess,
                onError);
        }

        public IEnumerator DeleteProgress(Action<RedeLabOperationResponse> onSuccess, Action<string> onError)
        {
            return SendJsonRequest("DELETE", "/api/progresso/me", null, onSuccess, onError);
        }

        public IEnumerator ClearCharacter(Action<RedeLabOperationResponse> onSuccess, Action<string> onError)
        {
            return SendJsonRequest("DELETE", "/api/me/personagem", null, onSuccess, onError);
        }

        public IEnumerator ResetNewGame(Action<RedeLabOperationResponse> onSuccess, Action<string> onError)
        {
            return SendJsonRequest("DELETE", "/api/me/novo-jogo", null, onSuccess, onError);
        }

        public IEnumerator GetMissionsForPhase(
            int phaseId,
            Action<RedeLabMission[]> onSuccess,
            Action<string> onError)
        {
            return SendRawRequest("GET", "/api/missoes/fase/" + phaseId, null, responseBody =>
            {
                try
                {
                    RedeLabMissionList list = JsonUtility.FromJson<RedeLabMissionList>(
                        "{\"items\":" + responseBody + "}");
                    onSuccess?.Invoke(list != null && list.items != null
                        ? list.items
                        : Array.Empty<RedeLabMission>());
                }
                catch (Exception exception)
                {
                    onError?.Invoke("Nao foi possivel interpretar o catalogo de missoes: " + exception.Message);
                }
            }, onError);
        }

        private IEnumerator SendJsonRequest<T>(
            string method,
            string path,
            string requestBody,
            Action<T> onSuccess,
            Action<string> onError) where T : class
        {
            return SendRawRequest(method, path, requestBody, responseBody =>
            {
                try
                {
                    T parsed = JsonUtility.FromJson<T>(responseBody);
                    if (parsed == null)
                    {
                        onError?.Invoke("A API respondeu com JSON invalido.");
                        return;
                    }
                    onSuccess?.Invoke(parsed);
                }
                catch (Exception exception)
                {
                    onError?.Invoke("Nao foi possivel interpretar a resposta da API: " + exception.Message);
                }
            }, onError);
        }

        private IEnumerator SendRawRequest(
            string method,
            string path,
            string requestBody,
            Action<string> onSuccess,
            Action<string> onError)
        {
            string accessToken = accessTokenProvider();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                onError?.Invoke("A sessao nao possui um Access Token.");
                yield break;
            }

            using (UnityWebRequest request = new UnityWebRequest(baseUrl + path, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                if (requestBody != null || method == "POST" || method == "PUT")
                {
                    request.uploadHandler = new UploadHandlerRaw(
                        Encoding.UTF8.GetBytes(requestBody ?? string.Empty));
                    request.SetRequestHeader("Content-Type", "application/json");
                }

                request.SetRequestHeader("Accept", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + accessToken);
                request.timeout = 20;

                yield return request.SendWebRequest();

                long statusCode = request.responseCode;
                string responseBody = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    if (statusCode == 401) OnUnauthorized?.Invoke();
                    onError?.Invoke(BuildErrorMessage(statusCode, responseBody, request.error));
                    yield break;
                }

                onSuccess?.Invoke(responseBody);
            }
        }

        private static string BuildErrorMessage(long statusCode, string responseBody, string transportError)
        {
            string apiMessage = string.Empty;
            if (!string.IsNullOrWhiteSpace(responseBody))
            {
                try
                {
                    RedeLabApiError parsed = JsonUtility.FromJson<RedeLabApiError>(responseBody);
                    apiMessage = parsed != null ? parsed.error : string.Empty;
                }
                catch (Exception)
                {
                    // A resposta pode ser HTML ou texto em falhas de infraestrutura.
                }
            }

            string detail = !string.IsNullOrWhiteSpace(apiMessage) ? apiMessage : transportError;
            if (statusCode > 0)
            {
                return string.IsNullOrWhiteSpace(detail)
                    ? $"A API respondeu com HTTP {statusCode}."
                    : $"A API respondeu com HTTP {statusCode}: {detail}";
            }

            return "Nao foi possivel acessar a API em " + detail;
        }
    }
}
