using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;

namespace CineManager
{
    /// <summary>
    /// Todas as operacoes da tabela FILMES.
    /// O SQL e escrito a mao e executado com SqlCommand.
    /// Nenhum valor do usuario e concatenado na string: tudo vai
    /// por SqlParameter, o que evita SQL Injection.
    /// </summary>
    public class FilmeRepositorio
    {
        // ------------------------------------------------------------
        // READ - listar
        // ------------------------------------------------------------
        public List<Filme> ListarFilmes(bool somenteAtivos)
        {
            List<Filme> listaDeFilmes = new List<Filme>();

            string comandoTexto =
                "SELECT IdFilme, Titulo, Genero, Duracao, Classificacao, Sinopse, Ativo " +
                "FROM Filmes ";

            if (somenteAtivos)
            {
                comandoTexto = comandoTexto + "WHERE Ativo = 1 ";
            }

            comandoTexto = comandoTexto + "ORDER BY Titulo;";

            using (SqlConnection conexaoBanco = BancoDados.AbrirConexao())
            using (SqlCommand comandoSql = new SqlCommand(comandoTexto, conexaoBanco))
            using (SqlDataReader leitorDados = comandoSql.ExecuteReader())
            {
                while (leitorDados.Read())
                {
                    listaDeFilmes.Add(MontarFilme(leitorDados));
                }
            }

            return listaDeFilmes;
        }

        // ------------------------------------------------------------
        // READ - buscar um registro
        // ------------------------------------------------------------
        public Filme BuscarFilmePorId(int idFilme)
        {
            string comandoTexto =
                "SELECT IdFilme, Titulo, Genero, Duracao, Classificacao, Sinopse, Ativo " +
                "FROM Filmes " +
                "WHERE IdFilme = @idFilme;";

            using (SqlConnection conexaoBanco = BancoDados.AbrirConexao())
            using (SqlCommand comandoSql = new SqlCommand(comandoTexto, conexaoBanco))
            {
                comandoSql.Parameters.Add("@idFilme", SqlDbType.Int).Value = idFilme;

                using (SqlDataReader leitorDados = comandoSql.ExecuteReader())
                {
                    if (leitorDados.Read())
                    {
                        return MontarFilme(leitorDados);
                    }
                }
            }

            throw new RegistroNaoEncontrado("Filme não encontrado.");
        }

        // ------------------------------------------------------------
        // CREATE
        // ------------------------------------------------------------
        public Filme CadastrarFilme(Filme filmeNovo)
        {
            ValidarFilme(filmeNovo);

            string comandoTexto =
                "INSERT INTO Filmes (Titulo, Genero, Duracao, Classificacao, Sinopse, Ativo) " +
                "VALUES (@titulo, @genero, @duracao, @classificacao, @sinopse, @ativo); " +
                "SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using (SqlConnection conexaoBanco = BancoDados.AbrirConexao())
            using (SqlCommand comandoSql = new SqlCommand(comandoTexto, conexaoBanco))
            {
                comandoSql.Parameters.Add("@titulo", SqlDbType.VarChar, 150).Value = filmeNovo.Titulo;
                comandoSql.Parameters.Add("@genero", SqlDbType.VarChar, 50).Value = filmeNovo.Genero;
                comandoSql.Parameters.Add("@duracao", SqlDbType.Int).Value = filmeNovo.Duracao;
                comandoSql.Parameters.Add("@classificacao", SqlDbType.VarChar, 10).Value = filmeNovo.Classificacao;
                comandoSql.Parameters.Add("@sinopse", SqlDbType.VarChar, 500).Value = filmeNovo.Sinopse;
                comandoSql.Parameters.Add("@ativo", SqlDbType.Bit).Value = filmeNovo.Ativo;

                try
                {
                    filmeNovo.IdFilme = Convert.ToInt32(comandoSql.ExecuteScalar());
                }
                catch (SqlException erroSql)
                {
                    throw TraduzirErroDoBanco(erroSql);
                }
            }

            return filmeNovo;
        }

        // ------------------------------------------------------------
        // UPDATE
        // ------------------------------------------------------------
        public Filme AtualizarFilme(Filme filmeAlterado)
        {
            ValidarFilme(filmeAlterado);

            string comandoTexto =
                "UPDATE Filmes " +
                "SET Titulo = @titulo, " +
                "    Genero = @genero, " +
                "    Duracao = @duracao, " +
                "    Classificacao = @classificacao, " +
                "    Sinopse = @sinopse " +
                "WHERE IdFilme = @idFilme;";

            using (SqlConnection conexaoBanco = BancoDados.AbrirConexao())
            using (SqlCommand comandoSql = new SqlCommand(comandoTexto, conexaoBanco))
            {
                comandoSql.Parameters.Add("@titulo", SqlDbType.VarChar, 150).Value = filmeAlterado.Titulo;
                comandoSql.Parameters.Add("@genero", SqlDbType.VarChar, 50).Value = filmeAlterado.Genero;
                comandoSql.Parameters.Add("@duracao", SqlDbType.Int).Value = filmeAlterado.Duracao;
                comandoSql.Parameters.Add("@classificacao", SqlDbType.VarChar, 10).Value = filmeAlterado.Classificacao;
                comandoSql.Parameters.Add("@sinopse", SqlDbType.VarChar, 500).Value = filmeAlterado.Sinopse;
                comandoSql.Parameters.Add("@idFilme", SqlDbType.Int).Value = filmeAlterado.IdFilme;

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
                    throw new RegistroNaoEncontrado("Filme não encontrado.");
                }
            }

            return BuscarFilmePorId(filmeAlterado.IdFilme);
        }

        // ------------------------------------------------------------
        // UPDATE - ativar / desativar
        // ------------------------------------------------------------
        public Filme AtivarOuDesativarFilme(int idFilme, bool filmeAtivo)
        {
            string comandoTexto =
                "UPDATE Filmes " +
                "SET Ativo = @ativo " +
                "WHERE IdFilme = @idFilme;";

            using (SqlConnection conexaoBanco = BancoDados.AbrirConexao())
            using (SqlCommand comandoSql = new SqlCommand(comandoTexto, conexaoBanco))
            {
                comandoSql.Parameters.Add("@ativo", SqlDbType.Bit).Value = filmeAtivo;
                comandoSql.Parameters.Add("@idFilme", SqlDbType.Int).Value = idFilme;

                int linhasAfetadas = comandoSql.ExecuteNonQuery();

                if (linhasAfetadas == 0)
                {
                    throw new RegistroNaoEncontrado("Filme não encontrado.");
                }
            }

            return BuscarFilmePorId(idFilme);
        }

        // ------------------------------------------------------------
        // DELETE
        // ------------------------------------------------------------
        public void ExcluirFilme(int idFilme)
        {
            // Integridade referencial: o filme so pode ser excluido
            // se nao existir nenhuma sessao apontando para ele.
            string comandoContagem =
                "SELECT COUNT(*) FROM Sessoes WHERE IdFilme = @idFilme;";

            string comandoExclusao =
                "DELETE FROM Filmes WHERE IdFilme = @idFilme;";

            using (SqlConnection conexaoBanco = BancoDados.AbrirConexao())
            {
                using (SqlCommand comandoSql = new SqlCommand(comandoContagem, conexaoBanco))
                {
                    comandoSql.Parameters.Add("@idFilme", SqlDbType.Int).Value = idFilme;

                    int totalDeSessoes = Convert.ToInt32(comandoSql.ExecuteScalar());

                    if (totalDeSessoes > 0)
                    {
                        throw new ConflitoDeDados(
                            "Este filme possui " + totalDeSessoes + " sessão(ões) cadastrada(s). " +
                            "Exclua as sessões ou desative o filme.");
                    }
                }

                using (SqlCommand comandoSql = new SqlCommand(comandoExclusao, conexaoBanco))
                {
                    comandoSql.Parameters.Add("@idFilme", SqlDbType.Int).Value = idFilme;

                    int linhasAfetadas = comandoSql.ExecuteNonQuery();

                    if (linhasAfetadas == 0)
                    {
                        throw new RegistroNaoEncontrado("Filme não encontrado.");
                    }
                }
            }
        }

        // ------------------------------------------------------------
        // Apoio
        // ------------------------------------------------------------

        /// <summary>
        /// Le a linha atual do SqlDataReader e monta o objeto Filme.
        /// </summary>
        private Filme MontarFilme(SqlDataReader leitorDados)
        {
            Filme filmeLido = new Filme();

            filmeLido.IdFilme = leitorDados.GetInt32(leitorDados.GetOrdinal("IdFilme"));
            filmeLido.Titulo = leitorDados.GetString(leitorDados.GetOrdinal("Titulo"));
            filmeLido.Genero = leitorDados.GetString(leitorDados.GetOrdinal("Genero"));
            filmeLido.Duracao = leitorDados.GetInt32(leitorDados.GetOrdinal("Duracao"));
            filmeLido.Classificacao = leitorDados.GetString(leitorDados.GetOrdinal("Classificacao"));
            filmeLido.Ativo = leitorDados.GetBoolean(leitorDados.GetOrdinal("Ativo"));

            int posicaoSinopse = leitorDados.GetOrdinal("Sinopse");
            filmeLido.Sinopse = leitorDados.IsDBNull(posicaoSinopse) ? "" : leitorDados.GetString(posicaoSinopse);

            return filmeLido;
        }

        private void ValidarFilme(Filme filmeVerificado)
        {
            filmeVerificado.Titulo = Validacoes.TextoObrigatorio(filmeVerificado.Titulo, "título", 150);
            filmeVerificado.Genero = Validacoes.TextoObrigatorio(filmeVerificado.Genero, "gênero", 50);
            filmeVerificado.Sinopse = (filmeVerificado.Sinopse ?? "").Trim();

            Validacoes.NumeroMaiorQueZero(filmeVerificado.Duracao, "duração");
            Validacoes.ClassificacaoValida(filmeVerificado.Classificacao);

            if (filmeVerificado.Sinopse.Length > 500)
            {
                throw new ErroDeValidacao("A sinopse deve ter no máximo 500 caracteres.");
            }
        }

        /// <summary>
        /// Transforma erros do SQL Server em mensagens que fazem
        /// sentido para o usuario final.
        /// 2601 / 2627 = violacao de UNIQUE.
        /// </summary>
        private Exception TraduzirErroDoBanco(SqlException erroSql)
        {
            if (erroSql.Number == 2601 || erroSql.Number == 2627)
            {
                return new ConflitoDeDados("Já existe um filme cadastrado com esse título.");
            }

            if (erroSql.Number == 547)
            {
                return new ConflitoDeDados("Os dados informados não respeitam as regras do banco de dados.");
            }

            return erroSql;
        }
    }
}
