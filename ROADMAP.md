# Roadmap técnico — RedeLab Escola

Este documento reúne pendências técnicas e de produto já definidas. Os detalhes operacionais de publicação permanecem em [`DEPLOY_CHECKLIST.md`](DEPLOY_CHECKLIST.md).

## Redes e CIDR

- [ ] Exibir visualmente o CIDR atual dos roteadores (`/24`, `/25`, `/26`, `/29` etc.).
- [ ] Exibir a máscara decimal correspondente ao CIDR.
- [ ] Definir máscaras coerentes com a quantidade real de hosts necessária em cada rede, sem valores aleatórios.
- [ ] Manter a edição da máscara bloqueada para o jogador nas primeiras fases.
- [ ] Liberar a configuração manual da máscara em fases futuras.
- [ ] Validar se a máscara escolhida pelo jogador suporta a quantidade necessária de hosts.

## Simulador de montagem de rede

- [ ] Criar futuramente um modo de montagem de rede inspirado no Packet Tracer.
- [ ] Implementar drag-and-drop de PCs, switches, roteadores e servidores.
- [ ] Permitir conexões entre portas por meio de cabos.
- [ ] Permitir configuração de IP, máscara, gateway e DHCP.
- [ ] Organizar desafios com progressão por dificuldade.
- [ ] Integrar o simulador às missões 3D, preservando a aventura como contexto da aprendizagem.

## Main Menu e cenário

- [ ] Refinar visualmente a Main Menu sem mudar sua identidade atual.
- [ ] Adicionar mais livros e elementos decorativos.
- [ ] Melhorar a composição visual do cenário.

## Professor

- [ ] Corrigir o material ou a textura dos óculos do professor.
- [ ] Na Fase 1, sala 3, adicionar uma fala do professor quando o aluno entregar o documento.

## Domínio da escola e infraestrutura

- [ ] Avaliar a substituição futura do acesso por IP em [`https://192.168.1.59:8081`](https://192.168.1.59:8081) pelo nome amigável `redelab.escola`.
- [ ] Avaliar primeiro DNS ou arquivo `hosts` interno e o impacto sobre aplicações de outros professores no servidor compartilhado.
- [ ] Mapear os demais serviços do servidor antes de instalar Nginx ou alterar o uso das portas 80 e 443.
- [ ] Preservar HTTPS na futura adoção do domínio amigável.
- [ ] Manter, por enquanto, a porta `8081` para o WebGL e a porta `3001` para a API.

## Monitor administrativo

- [ ] Adicionar autenticação e autorização administrativa antes de considerar o monitor definitivo.
- [ ] Proteger especialmente o acesso ao feedback textual enviado pelos jogadores.

## Feedback do jogador

- [x] Implementar e versionar o backend append-only de feedback.
- [x] Implementar e versionar a visualização de feedback no monitor administrativo.
- [x] Implementar e versionar a interface Unity de envio e histórico do jogador.
- [ ] Conectar a tela final definitiva após a conclusão da fase `O_provedor`.
  - Esta fase e seu fluxo final não devem ser alterados até a tarefa específica correspondente.

## Deploy da escola

- [x] Implementar e versionar o script automático de deploy da escola.
- [x] Cobrir no fluxo automático: check, staging, testes, backup, migrations, promoção, restart seletivo e health check.
- [x] Documentar o procedimento operacional em [`DEPLOY_CHECKLIST.md`](DEPLOY_CHECKLIST.md).
