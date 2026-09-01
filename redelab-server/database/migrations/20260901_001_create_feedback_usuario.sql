CREATE TABLE IF NOT EXISTS `feedback_usuario` (
  `id_feedback` bigint unsigned NOT NULL AUTO_INCREMENT,
  `id_usuario` int NOT NULL,
  `tipo` enum('sugestao','bug','comentario') COLLATE utf8mb4_unicode_ci NOT NULL,
  `comentario` varchar(1000) COLLATE utf8mb4_unicode_ci NOT NULL,
  `versao_jogo` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `data_envio` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY (`id_feedback`),
  KEY `idx_feedback_usuario_data` (`id_usuario`, `data_envio`, `id_feedback`),
  KEY `idx_feedback_tipo_data` (`tipo`, `data_envio`, `id_feedback`),
  CONSTRAINT `fk_feedback_usuario`
    FOREIGN KEY (`id_usuario`) REFERENCES `usuario` (`id_usuario`)
    ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
