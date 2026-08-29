# Fluxos online de inicio e carregamento

## Start Game

`Start Game` exige uma sessao autenticada. Se `/api/me` ja contem personagem 1 ou 2,
esse valor e sincronizado para `CharacterSelectionState` e O Escritorio (`SampleScene`)
e aberto diretamente. Se o servidor ainda nao possui personagem, o menu abre
`CharacterSelection`; a confirmacao chama `PUT /api/me/personagem` e somente navega depois
de HTTP 200.

O Start Game nao consulta, apaga ou modifica progresso existente. Nesta etapa ele apenas
inicia O Escritorio usando o personagem persistido.

## Load Game

`Load Game` executa, nesta ordem:

1. `GET /api/me` para atualizar usuario e personagem;
2. `GET /api/progresso/me`;
3. se houver progresso, `GET /api/missoes/fase/1` e `/fase/2`;
4. compara as conclusoes com o catalogo ativo retornado pela API;
5. abre `SampleScene` enquanto O Escritorio estiver pendente;
6. abre `Stage2_Factory` quando O Escritorio estiver completo e a Fabrica pendente;
7. permanece no menu se ambas estiverem completas, pois O Provedor nao existe ainda.

Sem missoes concluidas, o menu informa `Nenhum progresso salvo.`. Se o personagem estiver
ausente mas existir progresso, a selecao e aberta e preserva em memoria qual fase deveria
ser carregada depois da confirmacao.

Essa resolucao escolhe somente a fase. A restauracao da missao exata, objetos e estado da
cena pertence a proxima integracao com `MissionManager`.

## PlayerPrefs

O backend e a fonte persistente do personagem. Depois de uma resposta bem-sucedida da API,
`CharacterSelectionState` copia 1/2 para o `PlayerPrefs` ja existente porque
`PlayerCharacterVisualApplier` ainda depende desse mecanismo ao instanciar o visual. No
Editor, a selecao direta atualiza somente a memoria do processo para permitir testar cenas
isoladas; nao chama a API e nao grava PlayerPrefs.
