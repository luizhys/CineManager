using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;

namespace CineManager
{
    /// <summary>
    /// Todas as operacoes da tabela SESSOES.
    /// As consultas usam INNER JOIN com Filmes e Salas para trazer
    /// o titulo e o nome da sala sem precisar repetir esses dados
    /// dentro da tabela Sessoes.
    /// </summary>
    public class SessaoRepositorio
    {
        /// <summary>
        /// Trecho SELECT reaproveitado pelas consultas de sessao.
        /// </summary>
        private const string CONSULTA_BASE =
            "SELECT " +
            "    Sessoes.IdSessao, " +
            "    Sessoes.IdFilme, " +
            "    Sessoes.IdSala, " +
            "    Sessoes.DataSessao, " +
            "    Sessoes.HorarioSessao, " +
            "    Sessoes.Preco, " +
            "    Sessoes.Tipo, " +
            "    Filmes.Titulo     AS TituloFilme, " +
            "    Filmes.Ativo      AS FilmeAtivo, " +
            "    Salas.Nome        AS NomeSala, " +
            "    Salas.Capacidade  AS CapacidadeSala " +
            "FROM Sessoes " +
            "INNER JOIN Filmes ON Sessoes.IdFilme = Filmes.IdFilme " +
            "INNER JOIN Salas  ON Sessoes.IdSala  = Salas.IdSala ";

        // ------------------------------------------------------------
        // READ - listar
        // ------------------------------------------------------------
        public List<Sessao> ListarSessoes(bool somenteFilmesAtivos)
        {
            List<Sessao> listaDeSessoes = new List<Sessao>();

            string comandoTexto = CONSULTA_BASE;

            if (somenteFilmesAtivos)
            {
                comandoTexto = comandoTexto + "WHERE Filmes.Ativo = 1 ";
            }

            comandoTexto = comandoTexto + "ORDER BY Sessoes.DataSessao, Sessoes.HorarioSessao;";

            using (SqlConnection conexaoBanco = BancoDados.AbrirConexao())
            using (SqlCommand comandoSql = new SqlCommand(comandoTexto, conexaoBanco))
            using (SqlDataReader leitorDados = comandoSql.ExecuteReader())
            {
                while (leitorDados.Read())
                {
                    listaDeSessoes.Add(MontarSessao(leitorDados));
                }
            }

            return listaDeSessoes;
        }

        // ------------------------------------------------------------
        // READ - buscar um registro
        // ------------------------------------------------------------
        public Sessao BuscarSessaoPorId(int idSessao)
        {
            string comandoTexto = CONSULTA_BASE + "WHERE Sessoes.IdSessao = @idSessao;";

            using (SqlConnection conexaoBanco = BancoDados.AbrirConexao())
            using (SqlCommand comandoSql = new SqlCommand(comandoTexto, conexaoBanco))
            {
                comandoSql.Parameters.Add("@idSessao", SqlDbType.Int).Value = idSessao;

                using (SqlDataReader leitorDados = comandoSql.ExecuteReader())
                {
                    if (leitorDados.Read())
                    {
                        return MontarSessao(leitorDados);
                    }
                }
            }

            throw new RegistroNaoEncontrado("Sessão não encontrada.");
        }

        // ------------------------------------------------------------
        // CREATE
        // ------------------------------------------------------------
        public Sessao CadastrarSessao(Sessao sessaoNova)
        {
            ValidarSessao(sessaoNova);

            string comandoTexto =
                "INSERT INTO Sessoes (IdFilme, IdSala, DataSessao, HorarioSessao, Preco, Tipo) " +
                "VALUES (@idFilme, @idSala, @dataSessao, @horarioSessao, @precoSessao, @tipoSessao); " +
                "SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using (SqlConnection conexaoBanco = BancoDados.AbrirConexao())
            using (SqlCommand comandoSql = new SqlCommand(comandoTexto, conexaoBanco))
            {
                PreencherParametrosDaSessao(comandoSql, sessaoNova);

                try
                {
                    sessaoNova.IdSessao = Convert.ToInt32(comandoSql.ExecuteScalar());
                }
                catch (SqlException erroSql)
                {
                    throw TraduzirErroDoBanco(erroSql);
                }
            }

            return BuscarSessaoPorId(sessaoNova.IdSessao);
        }

        // ------------------------------------------------------------
        // UPDATE
        // ------------------------------------------------------------
        public Sessao AtualizarSessao(Sessao sessaoAlterada)
        {
            ValidarSessao(sessaoAlterada);

            // Se a sessao ja tem ingressos ativos, trocar a sala poderia
            // deixar um assento vendido fora da capacidade da nova sala.
            ConferirTrocaDeSala(sessaoAlterada);

            string comandoTexto =
                "UPDATE Sessoes " +
                "SET IdFilme = @idFilme, " +
                "    IdSala = @idSala, " +
                "    DataSessao = @dataSessao, " +
                "    HorarioSessao = @horarioSessao, " +
                "    Preco = @precoSessao, " +
                "    Tipo = @tipoSessao " +
                "WHERE IdSessao = @idSessao;";

            using (SqlConnection conexaoBanco = BancoDados.AbrirConexao())
            using (SqlCommand comandoSql = new SqlCommand(comandoTexto, conexaoBanco))
            {
                PreencherParametrosDaSessao(comandoSql, sessaoAlterada);
                comandoSql.Parameters.Add("@idSessao", SqlDbType.Int).Value = sessaoAlterada.IdSessao;

                int linhasAfetadas;

                try
                {
                    linhasAfetadas = comandoSql.ExecuteNonQuery();
                }
                catch (SqlException erroSql)
                {
                    throw TraduzirErroDoBanco(erroSql);
                }

                if (linhasAfetadas == 0)
                {
                    throw new RegistroNaoEncontrado("Sessão não encontrada.");
                }
            }

            return BuscarSessaoPorId(sessaoAlterada.IdSessao);
        }

        // ------------------------------------------------------------
        // DELETE
        // ------------------------------------------------------------
        /// <summary>
        /// Exclui a sessao. Se existir qualquer ingresso ATIVO, a
        /// exclusao e recusada. Ingressos CANCELADOS sao apagados
        /// junto, dentro de uma TRANSACAO: ou tudo e apagado, ou
        /// nada e apagado.
        /// </summary>
        public void ExcluirSessao(int idSessao)
        {
            using (SqlConnection conexaoBanco = BancoDados.AbrirConexao())
            using (SqlTransaction transacaoBanco = conexaoBanco.BeginTransaction())
            {
                try
                {
                    string comandoContagem =
                        "SELECT COUNT(*) FROM Ingressos " +
                        "WHERE IdSessao = @idSessao AND Status = 'Ativo';";

                    using (SqlCommand comandoSql = new SqlCommand(comandoContagem, conexaoBanco, transacaoBanco))
                    {
                        comandoSql.Parameters.Add("@idSessao", SqlDbType.Int).Value = idSessao;

                        int ingressosAtivos = Convert.ToInt32(comandoSql.ExecuteScalar());

                        if (ingressosAtivos > 0)
                        {
                            throw new ConflitoDeDados(
                                "Existem " + ingressosAtivos + " ingresso(s) ativo(s) para esta sessão. Cancele-os primeiro.");
                        }
                    }

                    string comandoLimpezaIngressos =
                        "DELETE FROM Ingressos " +
                        "WHERE IdSessao = @idSessao AND Status = 'Cancelado';";

                    using (SqlCommand comandoSql = new SqlCommand(comandoLimpezaIngressos, conexaoBanco, transacaoBanco))
                    {
                        comandoSql.Parameters.Add("@idSessao", SqlDbType.Int).Value = idSessao;
                        comandoSql.ExecuteNonQuery();
                    }

                    string comandoExclusao =
                        "DELETE FROM Sessoes WHERE IdSessao = @idSessao;";

                    using (SqlCommand comandoSql = new SqlCommand(comandoExclusao, conexaoBanco, transacaoBanco))
                    {
                        comandoSql.Parameters.Add("@idSessao", SqlDbType.Int).Value = idSessao;

                        int linhasAfetadas = comandoSql.ExecuteNonQuery();

                        if (linhasAfetadas == 0)
                        {
                            throw new RegistroNaoEncontrado("Sessão não encontrada.");
                        }
                    }

                    transacaoBanco.Commit();
                }
                catch (Exception)
                {
                    transacaoBanco.Rollback();
                    throw;
                }
            }
        }

        // ------------------------------------------------------------
        // Apoio
        // ------------------------------------------------------------
        private void ConferirTrocaDeSala(Sessao sessaoAlterada)
        {
            string comandoTexto =
                "SELECT ISNULL(MAX(NumeroAssento), 0) " +
                "FROM Ingressos " +
                "WHERE IdSessao = @idSessao AND Status = 'Ativo';";

            int maiorAssentoVendido;

            using (SqlConnection conexaoBanco = BancoDados.AbrirConexao())
            using (SqlCommand comandoSql = new SqlCommand(comandoTexto, conexaoBanco))
            {
                comandoSql.Parameters.Add("@idSessao", SqlDbType.Int).Value = sessaoAlterada.IdSessao;
                maiorAssentoVendido = Convert.ToInt32(comandoSql.ExecuteScalar());
            }

            if (maiorAssentoVendido == 0)
            {
                return;
            }

            SalaRepositorio salaRepositorio = new SalaRepositorio();
            Sala salaEscolhida = salaRepositorio.BuscarSalaPorId(sessaoAlterada.IdSala);

            if (salaEscolhida.Capacidade < maiorAssentoVendido)
            {
                throw new ConflitoDeDados(
                    "Esta sessão já vendeu o assento " + maiorAssentoVendido +
                    ", que não existe na sala escolhida (capacidade " + salaEscolhida.Capacidade + ").");
            }
        }

        private void PreencherParametrosDaSessao(SqlCommand comandoSql, Sessao sessaoInformada)
        {
            comandoSql.Parameters.Add("@idFilme", SqlDbType.Int).Value = sessaoInformada.IdFilme;
            comandoSql.Parameters.Add("@idSala", SqlDbType.Int).Value = sessaoInformada.IdSala;
            comandoSql.Parameters.Add("@dataSessao", SqlDbType.Date).Value = Validacoes.DataValida(sessaoInformada.DataSessao);
            comandoSql.Parameters.Add("@horarioSessao", SqlDbType.Time).Value = Validacoes.HorarioValido(sessaoInformada.HorarioSessao);
            comandoSql.Parameters.Add("@precoSessao", SqlDbType.Decimal).Value = sessaoInformada.Preco;
            comandoSql.Parameters.Add("@tipoSessao", SqlDbType.VarChar, 20).Value = sessaoInformada.Tipo;

            comandoSql.Parameters["@precoSessao"].Precision = 10;
            comandoSql.Parameters["@precoSessao"].Scale = 2;
        }

        private Sessao MontarSessao(SqlDataReader leitorDados)
        {
            Sessao sessaoLida = new Sessao();

            sessaoLida.IdSessao = leitorDados.GetInt32(leitorDados.GetOrdinal("IdSessao"));
            sessaoLida.IdFilme = leitorDados.GetInt32(leitorDados.GetOrdinal("IdFilme"));
            sessaoLida.IdSala = leitorDados.GetInt32(leitorDados.GetOrdinal("IdSala"));

            DateTime dataLida = leitorDados.GetDateTime(leitorDados.GetOrdinal("DataSessao"));
            sessaoLida.DataSessao = dataLida.ToString("yyyy-MM-dd");

            TimeSpan horarioLido = leitorDados.GetTimeSpan(leitorDados.GetOrdinal("HorarioSessao"));
            sessaoLida.HorarioSessao = horarioLido.ToString(@"hh\:mm");

            sessaoLida.Preco = leitorDados.GetDecimal(leitorDados.GetOrdinal("Preco"));
            sessaoLida.Tipo = leitorDados.GetString(leitorDados.GetOrdinal("Tipo"));

            sessaoLida.TituloFilme = leitorDados.GetString(leitorDados.GetOrdinal("TituloFilme"));
            sessaoLida.FilmeAtivo = leitorDados.GetBoolean(leitorDados.GetOrdinal("FilmeAtivo"));
            sessaoLida.NomeSala = leitorDados.GetString(leitorDados.GetOrdinal("NomeSala"));
            sessaoLida.CapacidadeSala = leitorDados.GetInt32(leitorDados.GetOrdinal("CapacidadeSala"));

            return sessaoLida;
        }

        private void ValidarSessao(Sessao sessaoVerificada)
        {
            Validacoes.NumeroMaiorQueZero(sessaoVerificada.IdFilme, "filme");
            Validacoes.NumeroMaiorQueZero(sessaoVerificada.IdSala, "sala");

            // O preco do ingresso inteiro e FIXO em todo o sistema
            // (R$ 50,00 - ver Backend/Precos.cs) e NAO pode variar por
            // sessao. Qualquer valor de "preco" enviado pela tela do
            // admin e ignorado: o backend sempre grava o preco oficial.
            sessaoVerificada.Preco = Precos.Inteira;

            Validacoes.TipoDeSessaoValido(sessaoVerificada.Tipo);
            Validacoes.DataValida(sessaoVerificada.DataSessao);
            Validacoes.HorarioValido(sessaoVerificada.HorarioSessao);

            // Confere se o filme e a sala realmente existem.
            // Sem isso o erro que apareceria seria o da FOREIGN KEY,
            // que tem uma mensagem tecnica demais para o usuario.
            FilmeRepositorio filmeRepositorio = new FilmeRepositorio();
            SalaRepositorio salaRepositorio = new SalaRepositorio();

            Filme filmeDaSessao = filmeRepositorio.BuscarFilmePorId(sessaoVerificada.IdFilme);
            salaRepositorio.BuscarSalaPorId(sessaoVerificada.IdSala);

            // Regra de negocio 10: horarios nao podem se sobrepor na
            // mesma sala. O calculo usa a DURACAO REAL do filme, vinda
            // do banco (filmeDaSessao.Duracao) - nunca um valor fixo.
            VerificarConflitoDeHorario(sessaoVerificada, filmeDaSessao);
        }

        /// <summary>
        /// Impede cadastrar/editar uma sessao cujo horario se sobreponha
        /// a outra sessao JA EXISTENTE na mesma sala e no mesmo dia.
        ///
        /// Regra conceitual (a mesma usada no trigger do banco, em
        /// Backend/../BancoDados.sql, que garante a integridade mesmo
        /// para quem inserir direto via SQL):
        ///
        ///     INICIO_NOVA &lt; FIM_EXISTENTE  E  FIM_NOVA &gt; INICIO_EXISTENTE
        ///
        /// O fim de cada sessao e sempre HorarioSessao + Duracao do
        /// filme correspondente (nunca um horario fixo "de mentirinha").
        /// </summary>
        private void VerificarConflitoDeHorario(Sessao sessaoVerificada, Filme filmeDaSessao)
        {
            TimeSpan horarioNovo = Validacoes.HorarioValido(sessaoVerificada.HorarioSessao);
            DateTime dataNova = Validacoes.DataValida(sessaoVerificada.DataSessao);

            int inicioNovoEmMinutos = (int)horarioNovo.TotalMinutes;
            int fimNovoEmMinutos = inicioNovoEmMinutos + filmeDaSessao.Duracao;

            // Traz apenas as outras sessoes da MESMA sala e MESMA data
            // (excluindo a propria sessao, no caso de uma edicao), junto
            // com a duracao real do filme de cada uma delas.
            string comandoTexto =
                "SELECT Sessoes.HorarioSessao, Filmes.Duracao, Filmes.Titulo " +
                "FROM Sessoes " +
                "INNER JOIN Filmes ON Sessoes.IdFilme = Filmes.IdFilme " +
                "WHERE Sessoes.IdSala = @idSala " +
                "  AND Sessoes.DataSessao = @dataSessao " +
                "  AND Sessoes.IdSessao <> @idSessaoAtual;";

            using (SqlConnection conexaoBanco = BancoDados.AbrirConexao())
            using (SqlCommand comandoSql = new SqlCommand(comandoTexto, conexaoBanco))
            {
                comandoSql.Parameters.Add("@idSala", SqlDbType.Int).Value = sessaoVerificada.IdSala;
                comandoSql.Parameters.Add("@dataSessao", SqlDbType.Date).Value = dataNova;
                // Em um cadastro novo, IdSessao ainda e 0 - nenhuma sessao
                // existente tem esse id, entao a comparacao nao exclui nada.
                comandoSql.Parameters.Add("@idSessaoAtual", SqlDbType.Int).Value = sessaoVerificada.IdSessao;

                using (SqlDataReader leitorDados = comandoSql.ExecuteReader())
                {
                    while (leitorDados.Read())
                    {
                        TimeSpan horarioExistente = leitorDados.GetTimeSpan(0);
                        int duracaoExistente = leitorDados.GetInt32(1);

                        int inicioExistenteEmMinutos = (int)horarioExistente.TotalMinutes;
                        int fimExistenteEmMinutos = inicioExistenteEmMinutos + duracaoExistente;

                        bool haConflitoDeHorario =
                            inicioNovoEmMinutos < fimExistenteEmMinutos &&
                            fimNovoEmMinutos > inicioExistenteEmMinutos;

                        if (haConflitoDeHorario)
                        {
                            throw new ConflitoDeDados(
                                "Não é possível cadastrar esta sessão porque o horário entra em conflito com outra sessão da mesma sala.");
                        }
                    }
                }
            }
        }

        private Exception TraduzirErroDoBanco(SqlException erroSql)
        {
            if (erroSql.Number == 2601 || erroSql.Number == 2627)
            {
                return new ConflitoDeDados("Esta sala já possui uma sessão nesta data e horário.");
            }

            if (erroSql.Number == 547)
            {
                return new ConflitoDeDados("O filme ou a sala informados não existem no banco de dados.");
            }

            // Erro levantado pelo trigger trg_Sessoes_ValidarConflitoHorario
            // (RAISERROR sem numero de mensagem customizado usa 50000).
            // O C# ja verifica esse conflito ANTES de inserir (veja
            // VerificarConflitoDeHorario), entao isto e so uma segunda
            // linha de defesa para quem gravar direto via SQL.
            if (erroSql.Number == 50000)
            {
                return new ConflitoDeDados(
                    "Não é possível cadastrar esta sessão porque o horário entra em conflito com outra sessão da mesma sala.");
            }

            return erroSql;
        }
    }
}
