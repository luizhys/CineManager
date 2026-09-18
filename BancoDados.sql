/* ============================================================
   CineManager - Script de criacao do banco de dados
   SGBD: Microsoft SQL Server
   Execute este arquivo inteiro no SQL Server Management Studio (SSMS).

   O script faz, nesta ordem:
     1. cria o banco CineManager (se ainda nao existir);
     2. seleciona o banco;
     3. apaga as tabelas antigas (permite reexecutar o script);
     4. cria as tabelas FILMES, SALAS, SESSOES e INGRESSOS;
     5. cria PRIMARY KEY, FOREIGN KEY, UNIQUE, CHECK e DEFAULT;
     6. insere os dados iniciais.
   ============================================================ */

IF DB_ID('CineManager') IS NULL
BEGIN
    CREATE DATABASE CineManager;
END
GO

USE CineManager;
GO

/* ------------------------------------------------------------
   1) LIMPEZA
   As tabelas sao apagadas da "filha" para a "mae" por causa
   das chaves estrangeiras (Ingressos depende de Sessoes,
   Sessoes depende de Filmes e Salas).
   ------------------------------------------------------------ */
IF OBJECT_ID('dbo.Ingressos', 'U') IS NOT NULL DROP TABLE dbo.Ingressos;
IF OBJECT_ID('dbo.Sessoes',   'U') IS NOT NULL DROP TABLE dbo.Sessoes;
IF OBJECT_ID('dbo.Salas',     'U') IS NOT NULL DROP TABLE dbo.Salas;
IF OBJECT_ID('dbo.Filmes',    'U') IS NOT NULL DROP TABLE dbo.Filmes;
IF OBJECT_ID('dbo.Usuarios',  'U') IS NOT NULL DROP TABLE dbo.Usuarios;
GO

/* ------------------------------------------------------------
   2) TABELA USUARIOS
   Entidade independente: guarda os usuarios do sistema de
   login/cadastro. E-mail, CPF e NomeUsuario sao UNIQUE: o
   proprio banco impede duplicidade, mesmo que alguem tente
   inserir direto pelo SQL (o C# tambem valida antes de inserir,
   mas a garantia final e sempre do banco).
   A senha nunca e guardada em texto puro: o C# grava apenas o
   hash (SHA-256) e o salt usados para gera-lo.
   ------------------------------------------------------------ */
CREATE TABLE dbo.Usuarios
(
    IdUsuario     INT           IDENTITY(1,1) NOT NULL,
    NomeCompleto  VARCHAR(150)  NOT NULL,
    NomeUsuario   VARCHAR(50)   NOT NULL,
    Email         VARCHAR(150)  NOT NULL,
    Cpf           VARCHAR(11)   NOT NULL,
    SenhaHash     VARCHAR(200)  NOT NULL,
    SenhaSalt     VARCHAR(200)  NOT NULL,
    /* Identifica o papel do usuario no sistema. So quem tiver
       TipoUsuario = 'Administrador' pode acessar o painel
       administrativo (o C# tambem confere isso a cada requisicao,
       nao e uma regra so do front-end). */
    TipoUsuario   VARCHAR(20)   NOT NULL CONSTRAINT DF_Usuarios_TipoUsuario DEFAULT ('Comum'),
    /* Permite desativar um usuario sem apagar seu historico (ingressos
       comprados continuam existindo, ligados a este IdUsuario). Um
       usuario com Ativo = 0 nao consegue mais fazer login - o C#
       confere isso em UsuarioRepositorio.Autenticar. */
    Ativo         BIT           NOT NULL CONSTRAINT DF_Usuarios_Ativo DEFAULT (1),
    DataCadastro  DATETIME      NOT NULL CONSTRAINT DF_Usuarios_DataCadastro DEFAULT (GETDATE()),

    CONSTRAINT PK_Usuarios              PRIMARY KEY (IdUsuario),
    CONSTRAINT UQ_Usuarios_NomeUsuario  UNIQUE (NomeUsuario),
    CONSTRAINT UQ_Usuarios_Email        UNIQUE (Email),
    CONSTRAINT UQ_Usuarios_Cpf          UNIQUE (Cpf),
    CONSTRAINT CK_Usuarios_NomeCompleto CHECK (LEN(LTRIM(RTRIM(NomeCompleto))) > 0),
    CONSTRAINT CK_Usuarios_NomeUsuario  CHECK (LEN(LTRIM(RTRIM(NomeUsuario))) > 0),
    /* CPF precisa ter exatamente 11 caracteres e todos numericos. */
    CONSTRAINT CK_Usuarios_Cpf          CHECK (LEN(Cpf) = 11 AND Cpf NOT LIKE '%[^0-9]%'),
    /* Formato minimo de e-mail: alguma coisa @ alguma coisa . alguma coisa */
    CONSTRAINT CK_Usuarios_Email        CHECK (Email LIKE '%_@_%.__%'),
    CONSTRAINT CK_Usuarios_TipoUsuario  CHECK (TipoUsuario IN ('Comum', 'Administrador'))
);
GO

/* ------------------------------------------------------------
   3) TABELA FILMES
   Entidade independente: nao depende de nenhuma outra tabela.
   ------------------------------------------------------------ */
CREATE TABLE dbo.Filmes
(
    IdFilme       INT           IDENTITY(1,1) NOT NULL,
    Titulo        VARCHAR(150)  NOT NULL,
    Genero        VARCHAR(50)   NOT NULL,
    Duracao       INT           NOT NULL,
    Classificacao VARCHAR(10)   NOT NULL,
    Sinopse       VARCHAR(500)  NULL,
    Ativo         BIT           NOT NULL CONSTRAINT DF_Filmes_Ativo DEFAULT (1),

    CONSTRAINT PK_Filmes                PRIMARY KEY (IdFilme),
    CONSTRAINT UQ_Filmes_Titulo         UNIQUE (Titulo),
    CONSTRAINT CK_Filmes_Duracao        CHECK (Duracao > 0),
    CONSTRAINT CK_Filmes_Titulo         CHECK (LEN(LTRIM(RTRIM(Titulo))) > 0),
    /* Classificacao indicativa: um padrao unico de texto em todo o
       sistema (banco, backend e front-end), para nunca existir
       "12", "12 anos" e "12+" representando a mesma coisa. */
    CONSTRAINT CK_Filmes_Classificacao  CHECK (Classificacao IN
        ('Livre', '10 anos', '12 anos', '14 anos', '16 anos', '18 anos'))
);
GO

/* ------------------------------------------------------------
   4) TABELA SALAS
   Tambem e uma entidade independente.
   ------------------------------------------------------------ */
CREATE TABLE dbo.Salas
(
    IdSala     INT          IDENTITY(1,1) NOT NULL,
    Nome       VARCHAR(100) NOT NULL,
    Capacidade INT          NOT NULL,

    CONSTRAINT PK_Salas             PRIMARY KEY (IdSala),
    CONSTRAINT UQ_Salas_Nome        UNIQUE (Nome),
    CONSTRAINT CK_Salas_Capacidade  CHECK (Capacidade > 0 AND Capacidade <= 300)
);
GO

/* ------------------------------------------------------------
   5) TABELA SESSOES
   Tabela associativa: liga um FILME a uma SALA em uma data/hora.
   Guarda apenas as chaves estrangeiras IdFilme e IdSala -
   o titulo do filme e o nome da sala NAO sao repetidos aqui.
   ------------------------------------------------------------ */
CREATE TABLE dbo.Sessoes
(
    IdSessao      INT           IDENTITY(1,1) NOT NULL,
    IdFilme       INT           NOT NULL,
    IdSala        INT           NOT NULL,
    DataSessao    DATE          NOT NULL,
    HorarioSessao TIME(0)       NOT NULL,
    Preco         DECIMAL(10,2) NOT NULL,
    Tipo          VARCHAR(20)   NOT NULL CONSTRAINT DF_Sessoes_Tipo DEFAULT ('Dublado'),

    CONSTRAINT PK_Sessoes          PRIMARY KEY (IdSessao),
    CONSTRAINT FK_Sessoes_Filmes   FOREIGN KEY (IdFilme) REFERENCES dbo.Filmes (IdFilme),
    CONSTRAINT FK_Sessoes_Salas    FOREIGN KEY (IdSala)  REFERENCES dbo.Salas  (IdSala),
    /* O preco do ingresso inteiro e fixo em R$ 50,00 em todo o
       sistema (a meia-entrada, R$ 25,00, e calculada a partir deste
       valor no momento da compra - ver Backend/Precos.cs). Nenhuma
       sessao pode gravar um preco diferente, nem os valores antigos
       (R$ 30, R$ 32, R$ 35) usados antes desta regra existir. */
    CONSTRAINT CK_Sessoes_Preco    CHECK (Preco = 50.00),
    CONSTRAINT CK_Sessoes_Tipo     CHECK (Tipo IN ('Dublado', 'Legendado')),
    /* Uma mesma sala nao pode ter duas sessoes no mesmo dia e horario. */
    CONSTRAINT UQ_Sessoes_SalaDataHorario UNIQUE (IdSala, DataSessao, HorarioSessao)
);
GO

/* ------------------------------------------------------------
   5b) TRIGGER - horarios sem sobreposicao na mesma sala
   Regra de negocio 10 do trabalho: a proxima sessao de uma sala
   so pode comecar quando a sessao anterior daquela sala JA TIVER
   TERMINADO (Inicio + Duracao do filme).

   Isso e OBRIGATORIO tambem no banco (nao so no C#), porque um
   INSERT/UPDATE feito direto no SQL (fora do backend) tem que
   respeitar a mesma regra. O calculo usa SEMPRE a duracao REAL
   do filme, vinda da tabela Filmes - nunca um horario fixo.

   Conceito (em minutos desde 00:00, para nao depender do tipo
   TIME do SQL Server em contas com horario):
       INICIO_NOVA < FIM_EXISTENTE  E  FIM_NOVA > INICIO_EXISTENTE
   ------------------------------------------------------------ */
DROP TRIGGER IF EXISTS dbo.trg_Sessoes_ValidarConflitoHorario;
GO

CREATE TRIGGER dbo.trg_Sessoes_ValidarConflitoHorario
ON dbo.Sessoes
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM inserted AS Nova
        INNER JOIN dbo.Filmes AS FilmeDaNova
                ON FilmeDaNova.IdFilme = Nova.IdFilme
        INNER JOIN dbo.Sessoes AS Existente
                ON Existente.IdSala     = Nova.IdSala
               AND Existente.DataSessao = Nova.DataSessao
               AND Existente.IdSessao  <> Nova.IdSessao
        INNER JOIN dbo.Filmes AS FilmeDaExistente
                ON FilmeDaExistente.IdFilme = Existente.IdFilme
        WHERE
            -- inicio da nova, em minutos desde 00:00 < fim da existente
            (DATEPART(HOUR, Nova.HorarioSessao) * 60 + DATEPART(MINUTE, Nova.HorarioSessao))
                <
            (DATEPART(HOUR, Existente.HorarioSessao) * 60 + DATEPART(MINUTE, Existente.HorarioSessao)
                + FilmeDaExistente.Duracao)
        AND
            -- fim da nova, em minutos desde 00:00 > inicio da existente
            (DATEPART(HOUR, Nova.HorarioSessao) * 60 + DATEPART(MINUTE, Nova.HorarioSessao)
                + FilmeDaNova.Duracao)
                >
            (DATEPART(HOUR, Existente.HorarioSessao) * 60 + DATEPART(MINUTE, Existente.HorarioSessao))
    )
    BEGIN
        RAISERROR(
            'Não é possível cadastrar esta sessão porque o horário entra em conflito com outra sessão da mesma sala.',
            16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END
END
GO

/* ------------------------------------------------------------
   6) TABELA INGRESSOS
   Depende de SESSOES. Guarda o preco pago porque ele e um dado
   historico: se o preco da sessao mudar depois, o ingresso ja
   vendido deve continuar registrando o valor que o cliente pagou.
   ------------------------------------------------------------ */
CREATE TABLE dbo.Ingressos
(
    IdIngresso    INT           IDENTITY(1,1) NOT NULL,
    IdSessao      INT           NOT NULL,
    /* Dono do ingresso: o usuario autenticado que realizou a compra.
       E por esta coluna - e NUNCA pelo CPF digitado no formulario -
       que o backend descobre quais ingressos pertencem a cada
       usuario (rota "Meus ingressos"). Fica NULL apenas nos
       registros de demonstracao inseridos por este script antes de
       existir usuario dono (ver secao 8); toda compra feita pelo
       sistema a partir de agora grava o IdUsuario da sessao logada. */
    IdUsuario     INT           NULL,
    NomeCliente   VARCHAR(100)  NOT NULL,
    CpfCliente    VARCHAR(11)   NOT NULL,
    EmailCliente  VARCHAR(150)  NOT NULL,
    NumeroAssento INT           NOT NULL,
    Preco         DECIMAL(10,2) NOT NULL,
    /* Tipo do ingresso vendido. Define o preco cobrado (o C# e quem
       decide o valor a partir daqui - ver Backend/Precos.cs -, o
       front-end nunca envia o preco). */
    TipoIngresso  VARCHAR(10)   NOT NULL CONSTRAINT DF_Ingressos_TipoIngresso DEFAULT ('Inteira'),
    /* Documento que comprova o direito a meia-entrada (carteirinha
       estudantil, documento de idoso, etc.). So e preenchido quando
       TipoIngresso = 'Meia'; fica NULL na inteira. */
    TipoDocumentoMeia VARCHAR(50) NULL,
    Status        VARCHAR(20)   NOT NULL CONSTRAINT DF_Ingressos_Status DEFAULT ('Ativo'),
    DataCompra    DATETIME      NOT NULL CONSTRAINT DF_Ingressos_DataCompra DEFAULT (GETDATE()),

    CONSTRAINT PK_Ingressos            PRIMARY KEY (IdIngresso),
    CONSTRAINT FK_Ingressos_Sessoes    FOREIGN KEY (IdSessao) REFERENCES dbo.Sessoes (IdSessao),
    /* Liga o ingresso ao usuario autenticado que comprou. NULL e
       permitido apenas para nao quebrar registros historicos sem
       dono; o C# sempre preenche esta coluna a partir da sessao
       de login (GerenciadorDeSessoes), nunca a partir do que o
       navegador envia no corpo da requisicao. */
    CONSTRAINT FK_Ingressos_Usuarios   FOREIGN KEY (IdUsuario) REFERENCES dbo.Usuarios (IdUsuario),
    CONSTRAINT CK_Ingressos_Status     CHECK (Status IN ('Ativo', 'Cancelado')),
    CONSTRAINT CK_Ingressos_Assento    CHECK (NumeroAssento > 0),
    CONSTRAINT CK_Ingressos_Preco      CHECK (Preco >= 0),
    CONSTRAINT CK_Ingressos_Nome       CHECK (LEN(LTRIM(RTRIM(NomeCliente))) >= 3),
    /* CPF precisa ter exatamente 11 caracteres e todos numericos. */
    CONSTRAINT CK_Ingressos_Cpf        CHECK (LEN(CpfCliente) = 11 AND CpfCliente NOT LIKE '%[^0-9]%'),
    /* Formato minimo de e-mail: alguma coisa @ alguma coisa . alguma coisa */
    CONSTRAINT CK_Ingressos_Email      CHECK (EmailCliente LIKE '%_@_%.__%'),
    CONSTRAINT CK_Ingressos_TipoIngresso CHECK (TipoIngresso IN ('Inteira', 'Meia')),
    /* Regra de negocio principal do preco: inteira custa SEMPRE
       R$ 50,00 e meia-entrada custa SEMPRE R$ 25,00. O banco garante
       essa regra mesmo que, por algum bug, o C# tente gravar outro
       valor - e a ultima linha de defesa contra preco manipulado. */
    CONSTRAINT CK_Ingressos_PrecoPorTipo CHECK (
        (TipoIngresso = 'Inteira' AND Preco = 50.00) OR
        (TipoIngresso = 'Meia'    AND Preco = 25.00)
    ),
    /* A meia-entrada exige documento comprobatorio; a inteira nao usa
       (e nao pode ter) documento algum. */
    CONSTRAINT CK_Ingressos_TipoDocumentoMeiaPorTipo CHECK (
        (TipoIngresso = 'Meia'    AND TipoDocumentoMeia IS NOT NULL AND LEN(LTRIM(RTRIM(TipoDocumentoMeia))) > 0) OR
        (TipoIngresso = 'Inteira' AND TipoDocumentoMeia IS NULL)
    ),
    CONSTRAINT CK_Ingressos_TipoDocumentoMeiaValido CHECK (
        TipoDocumentoMeia IS NULL OR
        TipoDocumentoMeia IN ('Carteirinha estudantil', 'Documento de identificação de idoso')
    )
);
GO

/* ------------------------------------------------------------
   7) REGRA PRINCIPAL DO SISTEMA
   Nao pode existir mais de um ingresso ATIVO para o mesmo
   assento na mesma sessao.

   Usamos um indice UNIQUE com filtro (WHERE Status = 'Ativo'),
   assim ingressos CANCELADOS nao entram na regra e o assento
   volta a ficar disponivel apos um cancelamento.
   O proprio banco garante essa integridade, mesmo que duas
   pessoas tentem comprar o mesmo assento ao mesmo tempo.
   ------------------------------------------------------------ */
CREATE UNIQUE INDEX UQ_Ingressos_AssentoAtivoPorSessao
    ON dbo.Ingressos (IdSessao, NumeroAssento)
    WHERE Status = 'Ativo';
GO

/* ------------------------------------------------------------
   8) DADOS INICIAIS
   Sao os mesmos filmes, salas e sessoes que existiam no
   JavaScript do front-end original.
   ------------------------------------------------------------ */
INSERT INTO dbo.Filmes (Titulo, Genero, Duracao, Classificacao, Sinopse, Ativo)
VALUES
    ('O Último Guardião',  'Aventura',          120, '12 anos', 'Um aventureiro precisa proteger um antigo segredo.',         1),
    ('Além das Estrelas',  'Ficção Científica', 135, '14 anos', 'Uma missão espacial encontra algo que não deveria existir.', 1),
    ('A Cidade Perdida',   'Ação',              110, '16 anos', 'Uma expedição descobre uma cidade escondida há séculos.',    1),
    ('Noite de Mistério',  'Suspense',          105, '14 anos', 'Uma investigação revela segredos dentro de um cinema.',      1);
GO

INSERT INTO dbo.Salas (Nome, Capacidade)
VALUES
    ('Sala 1', 40),
    ('Sala 2', 32),
    ('Sala 3', 48);
GO

/* As sessoes referenciam o filme e a sala por SUBCONSULTA,
   e nao por numero fixo, para o script continuar correto
   mesmo que os IDENTITY comecem em outro valor.

   Sessoes espalhadas por VARIOS DIAS (16 a 21/09/2026), com os 4
   filmes distribuidos entre as 3 salas e horarios diferentes a
   cada dia. Nenhum horario se sobrepoe a outro na mesma sala: o
   inicio de cada sessao so acontece depois do FIM (inicio +
   duracao) da sessao anterior naquela sala - a mesma regra que o
   trigger trg_Sessoes_ValidarConflitoHorario (secao 5b) valida
   sozinho a cada INSERT.

   Duracao de cada filme, para conferencia dos horarios abaixo:
     O Último Guardião  = 120 min     A Cidade Perdida  = 110 min
     Além das Estrelas  = 135 min     Noite de Mistério = 105 min */
INSERT INTO dbo.Sessoes (IdFilme, IdSala, DataSessao, HorarioSessao, Preco, Tipo)
VALUES
    /* ---------------------- 16/09/2026 ---------------------- */
    -- Sala 1: O Último Guardião (120min) 19:30-21:30, depois 21:45-23:45
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'O Último Guardião'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 1'), '2026-09-16', '19:30', 50.00, 'Dublado'),
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'O Último Guardião'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 1'), '2026-09-16', '21:45', 50.00, 'Dublado'),

    -- Sala 2: Além das Estrelas (135min) 20:00-22:15, depois 22:30-00:45
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'Além das Estrelas'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 2'), '2026-09-16', '20:00', 50.00, 'Legendado'),
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'Além das Estrelas'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 2'), '2026-09-16', '22:30', 50.00, 'Legendado'),

    -- Sala 3: A Cidade Perdida (110min) 19:00-20:50, depois 21:30-23:20
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'A Cidade Perdida'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 3'), '2026-09-16', '19:00', 50.00, 'Dublado'),
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'A Cidade Perdida'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 3'), '2026-09-16', '21:30', 50.00, 'Dublado'),

    /* ---------------------- 17/09/2026 ---------------------- */
    -- Sala 1: Noite de Mistério (105min) 15:00-16:45, depois Além das Estrelas (135min) 18:00-20:15
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'Noite de Mistério'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 1'), '2026-09-17', '15:00', 50.00, 'Dublado'),
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'Além das Estrelas'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 1'), '2026-09-17', '18:00', 50.00, 'Legendado'),

    -- Sala 2: O Último Guardião (120min) 16:00-18:00, depois A Cidade Perdida (110min) 19:00-20:50
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'O Último Guardião'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 2'), '2026-09-17', '16:00', 50.00, 'Dublado'),
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'A Cidade Perdida'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 2'), '2026-09-17', '19:00', 50.00, 'Legendado'),

    -- Sala 3: Além das Estrelas (135min) 15:30-17:45, depois Noite de Mistério (105min) 20:00-21:45
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'Além das Estrelas'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 3'), '2026-09-17', '15:30', 50.00, 'Legendado'),
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'Noite de Mistério'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 3'), '2026-09-17', '20:00', 50.00, 'Dublado'),

    /* ---------------------- 18/09/2026 ---------------------- */
    -- Sala 1: O Último Guardião (120min) 14:00-16:00, depois A Cidade Perdida (110min) 18:00-19:50
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'O Último Guardião'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 1'), '2026-09-18', '14:00', 50.00, 'Dublado'),
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'A Cidade Perdida'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 1'), '2026-09-18', '18:00', 50.00, 'Dublado'),

    -- Sala 2: Noite de Mistério (105min) 15:00-16:45, depois O Último Guardião (120min) 19:00-21:00
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'Noite de Mistério'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 2'), '2026-09-18', '15:00', 50.00, 'Legendado'),
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'O Último Guardião'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 2'), '2026-09-18', '19:00', 50.00, 'Legendado'),

    -- Sala 3: A Cidade Perdida (110min) 16:00-17:50, depois Além das Estrelas (135min) 20:00-22:15
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'A Cidade Perdida'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 3'), '2026-09-18', '16:00', 50.00, 'Dublado'),
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'Além das Estrelas'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 3'), '2026-09-18', '20:00', 50.00, 'Dublado'),

    /* ---------------------- 19/09/2026 ---------------------- */
    -- Sala 1: Além das Estrelas (135min) 16:00-18:15, depois Noite de Mistério (105min) 20:00-21:45
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'Além das Estrelas'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 1'), '2026-09-19', '16:00', 50.00, 'Legendado'),
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'Noite de Mistério'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 1'), '2026-09-19', '20:00', 50.00, 'Legendado'),

    -- Sala 2: A Cidade Perdida (110min) 17:00-18:50, depois O Último Guardião (120min) 20:00-22:00
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'A Cidade Perdida'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 2'), '2026-09-19', '17:00', 50.00, 'Dublado'),
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'O Último Guardião'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 2'), '2026-09-19', '20:00', 50.00, 'Dublado'),

    -- Sala 3: O Último Guardião (120min) 15:00-17:00, depois A Cidade Perdida (110min) 19:30-21:20
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'O Último Guardião'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 3'), '2026-09-19', '15:00', 50.00, 'Legendado'),
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'A Cidade Perdida'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 3'), '2026-09-19', '19:30', 50.00, 'Legendado'),

    /* ---------------------- 20/09/2026 ---------------------- */
    -- Sala 1: A Cidade Perdida (110min) 15:30-17:20, depois O Último Guardião (120min) 19:00-21:00
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'A Cidade Perdida'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 1'), '2026-09-20', '15:30', 50.00, 'Dublado'),
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'O Último Guardião'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 1'), '2026-09-20', '19:00', 50.00, 'Dublado'),

    -- Sala 2: Além das Estrelas (135min) 14:30-16:45, depois Noite de Mistério (105min) 18:00-19:45
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'Além das Estrelas'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 2'), '2026-09-20', '14:30', 50.00, 'Legendado'),
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'Noite de Mistério'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 2'), '2026-09-20', '18:00', 50.00, 'Legendado'),

    -- Sala 3: Noite de Mistério (105min) 16:00-17:45, depois Além das Estrelas (135min) 19:30-21:45
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'Noite de Mistério'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 3'), '2026-09-20', '16:00', 50.00, 'Dublado'),
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'Além das Estrelas'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 3'), '2026-09-20', '19:30', 50.00, 'Dublado'),

    /* ---------------------- 21/09/2026 ---------------------- */
    -- Sala 1: O Último Guardião (120min) 14:30-16:30, depois Além das Estrelas (135min) 19:00-21:15
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'O Último Guardião'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 1'), '2026-09-21', '14:30', 50.00, 'Legendado'),
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'Além das Estrelas'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 1'), '2026-09-21', '19:00', 50.00, 'Legendado'),

    -- Sala 2: A Cidade Perdida (110min) 15:00-16:50, depois Noite de Mistério (105min) 18:30-20:15
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'A Cidade Perdida'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 2'), '2026-09-21', '15:00', 50.00, 'Dublado'),
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'Noite de Mistério'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 2'), '2026-09-21', '18:30', 50.00, 'Dublado'),

    -- Sala 3: Além das Estrelas (135min) 16:00-18:15, depois O Último Guardião (120min) 20:30-22:30
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'Além das Estrelas'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 3'), '2026-09-21', '16:00', 50.00, 'Legendado'),
    ((SELECT IdFilme FROM dbo.Filmes WHERE Titulo = 'O Último Guardião'),
     (SELECT IdSala  FROM dbo.Salas  WHERE Nome   = 'Sala 3'), '2026-09-21', '20:30', 50.00, 'Legendado');
GO

/* Usuario de demonstracao para testar o login (usuario COMUM,
   sem acesso ao painel administrativo).
   Usuario: demo   |   Senha: demo123
   O CPF abaixo tem os digitos verificadores validos (algoritmo
   oficial), a mesma regra que Validacoes.CpfValido passou a exigir.
   O hash abaixo foi gerado pelo proprio C# (SHA-256 + salt); veja
   Backend/Repositorios/UsuarioRepositorio.cs para o algoritmo. */
INSERT INTO dbo.Usuarios (NomeCompleto, NomeUsuario, Email, Cpf, SenhaHash, SenhaSalt, TipoUsuario)
VALUES
    ('Usuário Demonstração', 'demo', 'demo@cinemanager.com', '12345678909',
     'NvgUBAwKXIrmFyhvLFqVr4ViFKH/PPV8BF5Zq6yWKno=', 'ZmFrZS1zYWx0LWRlbW8=', 'Comum');
GO

/* Usuario ADMINISTRADOR padrao, exigido pelo trabalho.
   Usuario: admin   |   Senha: Admin@123
   O CPF abaixo (111.444.777-35) tambem tem os digitos verificadores
   validos - e um CPF classico usado como exemplo em material
   didatico de validacao, nunca um CPF real de alguem.
   (mesma logica de hash acima: SHA-256(salt + senha), gerado pelo
   proprio algoritmo do C#). Documentado tambem no README.md. */
INSERT INTO dbo.Usuarios (NomeCompleto, NomeUsuario, Email, Cpf, SenhaHash, SenhaSalt, TipoUsuario)
VALUES
    ('admin', 'admin', 'admin@gmail.com', '11144477735',
     '62KQ1UPOTaoKIO6vdroz3IHUcge4KWFeMJKLAJUojcQ=', 's9ELVZJ7xJZ4pfC33GFJow==', 'Administrador');
GO

/* Ingressos de demonstracao: um inteira e um meia-entrada,
   ja com os precos oficiais (R$ 50,00 / R$ 25,00) e com CPFs que
   tambem passam pelos digitos verificadores do CPF. Ambos ficam
   vinculados ao usuario "demo" (IdUsuario), o mesmo padrao que o
   C# usa para toda compra feita por um usuario autenticado - assim
   dá para testar a tela "Meus ingressos" logando como demo. */
INSERT INTO dbo.Ingressos (IdSessao, IdUsuario, NomeCliente, CpfCliente, EmailCliente, NumeroAssento, Preco, TipoIngresso, TipoDocumentoMeia, Status)
VALUES
    ((SELECT MIN(IdSessao) FROM dbo.Sessoes),
     (SELECT IdUsuario FROM dbo.Usuarios WHERE NomeUsuario = 'demo'),
     'Cliente Demonstração', '19283746546', 'demo@email.com', 12, 50.00, 'Inteira', NULL, 'Ativo');
GO

INSERT INTO dbo.Ingressos (IdSessao, IdUsuario, NomeCliente, CpfCliente, EmailCliente, NumeroAssento, Preco, TipoIngresso, TipoDocumentoMeia, Status)
VALUES
    ((SELECT MIN(IdSessao) FROM dbo.Sessoes),
     (SELECT IdUsuario FROM dbo.Usuarios WHERE NomeUsuario = 'demo'),
     'Cliente Meia-Entrada', '98765432100', 'meia@email.com', 13, 25.00, 'Meia', 'Carteirinha estudantil', 'Ativo');
GO

/* ------------------------------------------------------------
   9) CONFERENCIA RAPIDA
   ------------------------------------------------------------ */
SELECT 'Usuarios'  AS Tabela, COUNT(*) AS TotalDeRegistros FROM dbo.Usuarios
UNION ALL
SELECT 'Filmes',    COUNT(*) FROM dbo.Filmes
UNION ALL
SELECT 'Salas',     COUNT(*) FROM dbo.Salas
UNION ALL
SELECT 'Sessoes',   COUNT(*) FROM dbo.Sessoes
UNION ALL
SELECT 'Ingressos', COUNT(*) FROM dbo.Ingressos;
GO

PRINT 'Banco CineManager criado com sucesso.';
GO
