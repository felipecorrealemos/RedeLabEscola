# RedeLab Escola — Projeto Unity

## Sobre o projeto

O **RedeLab Escola** é um projeto educacional desenvolvido na Unity com o objetivo de ensinar, de forma didática, visual e divertida, os conceitos básicos de redes de computadores.

A proposta do projeto é transformar o aprendizado de redes em uma experiência interativa, utilizando elementos de jogo em estilo **top-down 3D**, com gráficos simples em **low poly**, personagens cartunizados e ambientes inspirados em laboratórios, salas técnicas e escritórios.

O jogador controla um personagem em terceira pessoa/top-down, interage com computadores, mesas, pontos de rede, roteadores, switches e outros dispositivos, realizando tarefas relacionadas à montagem e configuração de uma rede local.

## Objetivo educacional

O principal objetivo do projeto é ajudar os alunos a compreenderem, na prática, conceitos fundamentais de redes de computadores, como:

- Endereço IP;
- Máscara de sub-rede;
- Gateway;
- Servidor;
- Cliente;
- Roteador;
- Switch;
- Ponto de rede;
- Conexão física;
- Configuração lógica;
- Teste de comunicação entre dispositivos.

A ideia é que o aluno aprenda não apenas lendo ou copiando comandos, mas interagindo com os elementos do cenário, configurando dispositivos e visualizando as consequências das suas ações dentro do jogo.

## Conceito do jogo

No jogo, o aluno poderá controlar um personagem dentro de um ambiente 3D aberto na parte superior, semelhante a uma sala ou laboratório sem teto, permitindo uma visão clara de cima para baixo.

Durante as missões, o jogador poderá carregar computadores, posicioná-los em mesas com ponto de rede, interagir com equipamentos e abrir uma interface de configuração para inserir dados como IP, máscara e gateway.

Quando a configuração estiver correta, o dispositivo será ativado e poderá liberar novas ações no ambiente, como abrir uma porta, acessar uma nova sala ou concluir uma missão.

## Exemplo de missão inicial

Uma das primeiras missões planejadas consiste em posicionar um computador em uma mesa com ponto de rede e selecionar um endereço IP válido dentro de um range disponível.

Nesta etapa inicial, o foco não será configurar máscara de sub-rede ou gateway manualmente. O objetivo principal será trabalhar o conceito de **faixa de endereços IP**, mostrando que cada dispositivo conectado à rede precisa receber um IP válido e único dentro do intervalo permitido.

Exemplo de range disponível:

```text
Range permitido: 192.168.0.10 até 192.168.0.50
