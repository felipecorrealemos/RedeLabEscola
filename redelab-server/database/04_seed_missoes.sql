USE `redelab_escola`;

INSERT INTO `missao`
  (`id_missao`, `id_fase`, `codigo`, `numero_missao`, `nome`, `descricao`, `ativa`)
VALUES
  (1, 1, 'sala1_colocar_gabinete', 1, 'Posicionar o gabinete da Sala 1', 'Levar o gabinete até o ponto correto da mesa da Sala 1.', 1),
  (2, 1, 'sala1_configurar_ip_pc', 2, 'Configurar o IP do computador da Sala 1', 'Configurar um endereço IP válido no computador da Sala 1.', 1),
  (3, 1, 'sala1_abrir_porta', 3, 'Abrir a porta da Sala 1', 'Utilizar o computador para comandar a abertura da porta da Sala 1.', 1),
  (4, 1, 'sala2_colocar_gabinete', 4, 'Posicionar o computador da Sala 2', 'Colocar o computador no ponto correto da mesa da Sala 2.', 1),
  (5, 1, 'sala2_configurar_ip_pc', 5, 'Configurar o IP do computador da Sala 2', 'Configurar um endereço IP válido no computador da Sala 2.', 1),
  (6, 1, 'sala2_configurar_ip_portas', 6, 'Configurar os dispositivos da porta dupla', 'Configurar corretamente os dois dispositivos de rede responsáveis pela porta dupla.', 1),
  (7, 1, 'sala2_abrir_portas', 7, 'Abrir a porta dupla da Sala 2', 'Utilizar o computador para comandar a abertura das duas folhas da porta.', 1),
  (8, 1, 'sala3_colocar_gabinete', 8, 'Conectar o computador à rede da Sala 3', 'Posicionar o computador corretamente e conectá-lo ao ponto de rede.', 1),
  (9, 1, 'sala3_configurar_ip_pc', 9, 'Configurar o IP do computador da Sala 3', 'Configurar um endereço IP válido no computador da Sala 3.', 1),
  (10, 1, 'sala3_colocar_impressora', 10, 'Conectar a impressora à rede', 'Posicionar a impressora corretamente e conectá-la ao ponto de rede.', 1),
  (11, 1, 'sala3_configurar_ip_impressora', 11, 'Configurar o IP da impressora', 'Configurar um endereço IP válido na impressora.', 1),
  (12, 1, 'sala3_imprimir_documento', 12, 'Imprimir o documento', 'Utilizar o computador para enviar o documento para a impressora.', 1),
  (13, 1, 'sala3_pegar_documento', 13, 'Recolher o documento impresso', 'Retirar da impressora o documento que foi impresso.', 1),
  (14, 1, 'sala3_entregar_documento', 14, 'Entregar o documento ao professor', 'Levar o documento impresso até o professor.', 1),
  (15, 1, 'sala3_configurar_ip_porta', 15, 'Configurar o dispositivo da porta final', 'Configurar corretamente o dispositivo de rede responsável pela porta final.', 1),
  (16, 1, 'sala3_abrir_porta', 16, 'Abrir a porta final do Escritório', 'Abrir a porta final após concluir os objetivos necessários da Sala 3.', 1),
  (17, 2, 'fabrica_bracos_roboticos', 1, 'Colocar os três braços robóticos em operação', 'Conectar e colocar em operação os três braços robóticos industriais.', 1),
  (18, 2, 'fabrica_limpar_entulhos_garra', 2, 'Remover os 7 entulhos com a garra', 'Utilizar a ponte rolante e a garra para remover os sete entulhos e enviá-los ao triturador.', 1),
  (19, 2, 'fabrica_pallets_esteira_empilhadeira', 3, 'Colocar 4 paletes na esteira com a empilhadeira', 'Utilizar a empilhadeira para colocar quatro paletes corretamente na esteira final.', 1),
  (20, 2, 'fabrica_pallets_gerados_enviados', 4, 'Produzir e enviar 3 paletes pela máquina', 'Operar a linha industrial até que três paletes sejam produzidos e enviados pela esteira de saída.', 1);
