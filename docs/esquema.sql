CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
CREATE TABLE dispositivo (
    id uuid NOT NULL,
    nome character varying(120) NOT NULL,
    endereco character varying(255) NOT NULL,
    descricao character varying(500),
    ativo boolean NOT NULL DEFAULT TRUE,
    intervalo_segundos integer NOT NULL DEFAULT 60,
    limite_tentativas smallint NOT NULL DEFAULT 3,
    status smallint NOT NULL DEFAULT 0,
    ultima_verificacao_em timestamp with time zone,
    tentativas_sem_resposta smallint NOT NULL DEFAULT 0,
    criado_em timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT pk_dispositivo PRIMARY KEY (id),
    CONSTRAINT ck_dispositivo_intervalo CHECK (intervalo_segundos BETWEEN 10 AND 3600),
    CONSTRAINT ck_dispositivo_status CHECK (status IN (0, 1, 2)),
    CONSTRAINT ck_dispositivo_tentativas CHECK (limite_tentativas BETWEEN 1 AND 10)
);

CREATE TABLE servico_tcp (
    id uuid NOT NULL,
    dispositivo_id uuid NOT NULL,
    nome character varying(120) NOT NULL,
    porta integer NOT NULL,
    ativo boolean NOT NULL DEFAULT TRUE,
    status smallint NOT NULL DEFAULT 0,
    tentativas_sem_resposta smallint NOT NULL DEFAULT 0,
    CONSTRAINT pk_servico_tcp PRIMARY KEY (id),
    CONSTRAINT ck_servico_tcp_porta CHECK (porta BETWEEN 1 AND 65535),
    CONSTRAINT ck_servico_tcp_status CHECK (status IN (0, 1, 2)),
    CONSTRAINT fk_servico_tcp_dispositivo FOREIGN KEY (dispositivo_id) REFERENCES dispositivo (id) ON DELETE CASCADE
);

CREATE TABLE alerta (
    id uuid NOT NULL,
    dispositivo_id uuid NOT NULL,
    servico_tcp_id uuid,
    tipo smallint NOT NULL,
    iniciado_em timestamp with time zone NOT NULL,
    encerrado_em timestamp with time zone,
    descricao character varying(300),
    CONSTRAINT pk_alerta PRIMARY KEY (id),
    CONSTRAINT ck_alerta_periodo CHECK (encerrado_em IS NULL OR encerrado_em >= iniciado_em),
    CONSTRAINT ck_alerta_tipo CHECK (tipo IN (0, 1)),
    CONSTRAINT fk_alerta_dispositivo FOREIGN KEY (dispositivo_id) REFERENCES dispositivo (id) ON DELETE CASCADE,
    CONSTRAINT fk_alerta_servico FOREIGN KEY (servico_tcp_id) REFERENCES servico_tcp (id) ON DELETE CASCADE
);

CREATE TABLE verificacao (
    id uuid NOT NULL,
    dispositivo_id uuid NOT NULL,
    servico_tcp_id uuid,
    data_hora timestamp with time zone NOT NULL,
    tipo smallint NOT NULL,
    sucesso boolean NOT NULL,
    latencia_ms integer,
    mensagem_erro character varying(300),
    CONSTRAINT pk_verificacao PRIMARY KEY (id),
    CONSTRAINT ck_verificacao_latencia CHECK (latencia_ms IS NULL OR latencia_ms >= 0),
    CONSTRAINT ck_verificacao_tipo CHECK (tipo IN (0, 1)),
    CONSTRAINT fk_verificacao_dispositivo FOREIGN KEY (dispositivo_id) REFERENCES dispositivo (id) ON DELETE CASCADE,
    CONSTRAINT fk_verificacao_servico FOREIGN KEY (servico_tcp_id) REFERENCES servico_tcp (id) ON DELETE CASCADE
);

CREATE INDEX ix_alerta_aberto ON alerta (dispositivo_id, iniciado_em DESC) WHERE encerrado_em IS NULL;

CREATE INDEX ix_alerta_servico ON alerta (servico_tcp_id);

CREATE INDEX ix_dispositivo_ativo ON dispositivo (ativo) WHERE ativo = TRUE;

CREATE UNIQUE INDEX uq_dispositivo_endereco ON dispositivo (endereco);

CREATE INDEX ix_servico_tcp_dispositivo ON servico_tcp (dispositivo_id);

CREATE UNIQUE INDEX uq_servico_tcp_porta ON servico_tcp (dispositivo_id, porta);

CREATE INDEX ix_verificacao_dispositivo_data ON verificacao (dispositivo_id, data_hora DESC);

CREATE INDEX ix_verificacao_servico_data ON verificacao (servico_tcp_id, data_hora DESC) WHERE servico_tcp_id IS NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261013222235_EsquemaInicial', '10.0.4');

COMMIT;

