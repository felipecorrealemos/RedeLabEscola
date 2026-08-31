CREATE DATABASE IF NOT EXISTS `redelab_escola`
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE `redelab_escola`;

CREATE TABLE `personagem` (
  `id_personagem` int NOT NULL AUTO_INCREMENT,
  `nome` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  PRIMARY KEY (`id_personagem`),
  UNIQUE KEY `uq_personagem_nome` (`nome`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `usuario` (
  `id_usuario` int NOT NULL AUTO_INCREMENT,
  `auth0_id` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `nome` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `email` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `id_personagem` int DEFAULT NULL,
  `data_cadastro` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ultimo_acesso` datetime DEFAULT NULL,
  PRIMARY KEY (`id_usuario`),
  UNIQUE KEY `uq_usuario_auth0` (`auth0_id`),
  UNIQUE KEY `uq_usuario_email` (`email`),
  KEY `fk_usuario_personagem` (`id_personagem`),
  CONSTRAINT `fk_usuario_personagem`
    FOREIGN KEY (`id_personagem`) REFERENCES `personagem` (`id_personagem`)
    ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `fase` (
  `id_fase` int NOT NULL AUTO_INCREMENT,
  `nome` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ativa` tinyint(1) NOT NULL DEFAULT '1',
  PRIMARY KEY (`id_fase`),
  UNIQUE KEY `uq_fase_nome` (`nome`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `missao` (
  `id_missao` int NOT NULL AUTO_INCREMENT,
  `id_fase` int NOT NULL,
  `codigo` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `numero_missao` int NOT NULL,
  `nome` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `descricao` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ativa` tinyint(1) NOT NULL DEFAULT '1',
  PRIMARY KEY (`id_missao`),
  UNIQUE KEY `uq_missao_fase_numero` (`id_fase`, `numero_missao`),
  UNIQUE KEY `uq_missao_fase_id` (`id_fase`, `id_missao`),
  UNIQUE KEY `uq_missao_codigo` (`codigo`),
  CONSTRAINT `fk_missao_fase`
    FOREIGN KEY (`id_fase`) REFERENCES `fase` (`id_fase`)
    ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `missao_concluida` (
  `id_usuario` int NOT NULL,
  `id_fase` int NOT NULL,
  `id_missao` int NOT NULL,
  `data_conclusao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id_usuario`, `id_fase`, `id_missao`),
  KEY `fk_missao_concluida_missao` (`id_fase`, `id_missao`),
  CONSTRAINT `fk_missao_concluida_missao`
    FOREIGN KEY (`id_fase`, `id_missao`) REFERENCES `missao` (`id_fase`, `id_missao`)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_missao_concluida_usuario`
    FOREIGN KEY (`id_usuario`) REFERENCES `usuario` (`id_usuario`)
    ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
