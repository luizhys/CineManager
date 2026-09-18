/* ============================================================
   CineManager - Consultas de teste
   Use este arquivo no SSMS para comprovar que os dados estao
   realmente gravados no SQL Server e para tirar os prints
   que vao para o README.
   ============================================================ */

USE CineManager;
GO

/* ------------------------------------------------------------
   1) CONTEUDO BRUTO DAS TABELAS
   ------------------------------------------------------------ */
SELECT * FROM dbo.Filmes;
SELECT * FROM dbo.Salas;
SELECT * FROM dbo.Sessoes;
SELECT * FROM dbo.Ingressos;
GO

/* ------------------------------------------------------------
   2) SESSOES COM JOIN
   Mostra como o banco junta as tres tabelas para montar a
   listagem de horarios da tela inicial.
   ------------------------------------------------------------ */
SELECT
    Sessoes.IdSessao,
    Filmes.Titulo        AS Filme,
    Salas.Nome           AS Sala,
    Salas.Capacidade,
    Sessoes.DataSessao,
    Sessoes.HorarioSessao,
    Sessoes.Preco,
    Sessoes.Tipo
FROM dbo.Sessoes
INNER JOIN dbo.Filmes ON Sessoes.IdFilme = Filmes.IdFilme
INNER JOIN dbo.Salas  ON Sessoes.IdSala  = Salas.IdSala
ORDER BY Sessoes.DataSessao, Sessoes.HorarioSessao;
GO

/* ------------------------------------------------------------
   3) CONSULTA DE INGRESSOS POR CPF
   Consulta auxiliar para conferencia manual no SSMS. O site NAO
   usa mais CPF para decidir quais ingressos mostrar em "Meus
   ingressos": aquela tela e sempre filtrada pelo IdUsuario do
   login (cookie de sessao), para isolar os ingressos de cada
   usuario - ver IngressoRepositorio.ListarIngressosDoUsuario.
   Troque o CPF abaixo pelo que voce usou no teste.
   ------------------------------------------------------------ */
DECLARE @cpfCliente VARCHAR(11) = '19283746546';

SELECT
    Ingressos.IdIngresso,
    Ingressos.NomeCliente,
    Ingressos.CpfCliente,
    Ingressos.EmailCliente,
    Ingressos.NumeroAssento,
    Ingressos.Preco,
    Ingressos.TipoIngresso,
    Ingressos.TipoDocumentoMeia,
    Ingressos.Status,
    Sessoes.DataSessao,
    Sessoes.HorarioSessao,
    Filmes.Titulo AS Filme,
    Salas.Nome    AS NomeSala
FROM dbo.Ingressos
INNER JOIN dbo.Sessoes ON Ingressos.IdSessao = Sessoes.IdSessao
INNER JOIN dbo.Filmes  ON Sessoes.IdFilme    = Filmes.IdFilme
INNER JOIN dbo.Salas   ON Sessoes.IdSala     = Salas.IdSala
WHERE Ingressos.CpfCliente = @cpfCliente
ORDER BY Sessoes.DataSessao, Sessoes.HorarioSessao;
GO

/* ------------------------------------------------------------
   4) ASSENTOS OCUPADOS DE UMA SESSAO
   Somente ingressos ATIVOS ocupam assento.
   ------------------------------------------------------------ */
DECLARE @idSessao INT = 1;

SELECT NumeroAssento
FROM dbo.Ingressos
WHERE IdSessao = @idSessao
  AND Status   = 'Ativo'
ORDER BY NumeroAssento;
GO

/* ------------------------------------------------------------
   5) OCUPACAO DE CADA SESSAO (JOIN + GROUP BY)
   Util para mostrar ao professor uma consulta mais elaborada.
   ------------------------------------------------------------ */
SELECT
    Sessoes.IdSessao,
    Filmes.Titulo AS Filme,
    Salas.Nome    AS Sala,
    Salas.Capacidade,
    COUNT(Ingressos.IdIngresso) AS AssentosVendidos,
    Salas.Capacidade - COUNT(Ingressos.IdIngresso) AS AssentosLivres
FROM dbo.Sessoes
INNER JOIN dbo.Filmes ON Sessoes.IdFilme = Filmes.IdFilme
INNER JOIN dbo.Salas  ON Sessoes.IdSala  = Salas.IdSala
LEFT  JOIN dbo.Ingressos ON Ingressos.IdSessao = Sessoes.IdSessao
                        AND Ingressos.Status   = 'Ativo'
GROUP BY Sessoes.IdSessao, Filmes.Titulo, Salas.Nome, Salas.Capacidade
ORDER BY Sessoes.IdSessao;
GO

/* ------------------------------------------------------------
   6) FATURAMENTO POR FILME (somente ingressos ativos)
   ------------------------------------------------------------ */
SELECT
    Filmes.Titulo,
    COUNT(Ingressos.IdIngresso)   AS IngressosVendidos,
    ISNULL(SUM(Ingressos.Preco), 0) AS TotalArrecadado
FROM dbo.Filmes
LEFT JOIN dbo.Sessoes   ON Sessoes.IdFilme    = Filmes.IdFilme
LEFT JOIN dbo.Ingressos ON Ingressos.IdSessao = Sessoes.IdSessao
                       AND Ingressos.Status   = 'Ativo'
GROUP BY Filmes.Titulo
ORDER BY TotalArrecadado DESC;
GO

/* ------------------------------------------------------------
   7) PROVA DE QUE AS CONSTRAINTS FUNCIONAM
   Descomente um bloco por vez e execute: o SQL Server deve
   recusar o comando. Otimo print para a documentacao.
   ------------------------------------------------------------ */

-- 7.1) Assento repetido na mesma sessao (indice UNIQUE filtrado)
-- INSERT INTO dbo.Ingressos (IdSessao, NomeCliente, CpfCliente, EmailCliente, NumeroAssento, Preco, TipoIngresso, Status)
-- VALUES (1, 'Teste Duplicado', '98765432100', 'teste@email.com', 12, 50.00, 'Inteira', 'Ativo');

-- 7.2) CPF com menos de 11 digitos (CHECK)
-- INSERT INTO dbo.Ingressos (IdSessao, NomeCliente, CpfCliente, EmailCliente, NumeroAssento, Preco, TipoIngresso, Status)
-- VALUES (1, 'Teste CPF', '123', 'teste@email.com', 39, 50.00, 'Inteira', 'Ativo');

-- 7.3) Sessao apontando para um filme que nao existe (FOREIGN KEY)
-- INSERT INTO dbo.Sessoes (IdFilme, IdSala, DataSessao, HorarioSessao, Preco, Tipo)
-- VALUES (999, 1, '2026-10-01', '20:00', 50.00, 'Dublado');

-- 7.5) Preco de sessao diferente de R$ 50,00 (CHECK CK_Sessoes_Preco)
-- INSERT INTO dbo.Sessoes (IdFilme, IdSala, DataSessao, HorarioSessao, Preco, Tipo)
-- VALUES (1, 1, '2026-10-01', '20:00', 30.00, 'Dublado');

-- 7.6) Meia-entrada sem documento comprobatorio (CHECK CK_Ingressos_TipoDocumentoMeiaPorTipo)
-- INSERT INTO dbo.Ingressos (IdSessao, NomeCliente, CpfCliente, EmailCliente, NumeroAssento, Preco, TipoIngresso, TipoDocumentoMeia, Status)
-- VALUES (1, 'Teste Meia Sem Doc', '11122233300', 'teste@email.com', 40, 25.00, 'Meia', NULL, 'Ativo');

-- 7.7) Meia-entrada com preco errado (CHECK CK_Ingressos_PrecoPorTipo)
-- INSERT INTO dbo.Ingressos (IdSessao, NomeCliente, CpfCliente, EmailCliente, NumeroAssento, Preco, TipoIngresso, TipoDocumentoMeia, Status)
-- VALUES (1, 'Teste Meia Preco Errado', '11122233301', 'teste@email.com', 41, 50.00, 'Meia', 'Carteirinha estudantil', 'Ativo');

-- 7.4) Excluir um filme que possui sessoes (FOREIGN KEY)
-- DELETE FROM dbo.Filmes WHERE IdFilme = 1;

-- 7.5) Duracao invalida (CHECK)
-- INSERT INTO dbo.Filmes (Titulo, Genero, Duracao, Classificacao, Sinopse, Ativo)
-- VALUES ('Filme Invalido', 'Teste', 0, '12 anos', 'Teste de constraint', 1);

/* ------------------------------------------------------------
   8) CONFERIR AS CONSTRAINTS CRIADAS
   ------------------------------------------------------------ */
SELECT
    OBJECT_NAME(parent_object_id) AS Tabela,
    name                          AS Constraint_,
    type_desc                     AS Tipo
FROM sys.objects
WHERE type_desc LIKE '%CONSTRAINT%'
  AND OBJECT_NAME(parent_object_id) IN ('Filmes', 'Salas', 'Sessoes', 'Ingressos')
ORDER BY Tabela, Tipo, Constraint_;
GO

SELECT name AS IndiceUnico, object_name(object_id) AS Tabela, filter_definition AS Filtro
FROM sys.indexes
WHERE is_unique = 1 AND object_name(object_id) = 'Ingressos';
GO
