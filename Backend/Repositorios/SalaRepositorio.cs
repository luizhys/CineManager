using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;

namespace CineManager
{
    /// <summary>
    /// Todas as operacoes da tabela SALAS, em SQL puro com ADO.NET.
    /// </summary>
    public class SalaRepositorio
    {
        // ------------------------------------------------------------
        // READ - listar
        // ------------------------------------------------------------
        public List<Sala> ListarSalas()
        {
            List<Sala> listaDeSalas = new List<Sala>();

            string comandoTexto =
                "SELECT IdSala, Nome, Capacidade " +
                "FROM Salas " +
                "ORDER BY Nome;";

            using (SqlConnection conexaoBanco = BancoDados.AbrirConexao())
            using (SqlCommand comandoSql = new SqlCommand(comandoTexto, conexaoBanco))
            using (SqlDataReader leitorDados = comandoSql.ExecuteReader())
            {
                while (leitorDados.Read())
                {
                    listaDeSalas.Add(MontarSala(leitorDados));
                }
            }

            return listaDeSalas;
        }

        // ------------------------------------------------------------
        // READ - buscar um registro
        // ------------------------------------------------------------
        public Sala BuscarSalaPorId(int idSala)
        {
            string comandoTexto =
                "SELECT IdSala, Nome, Capacidade " +
                "FROM Salas " +
                "WHERE IdSala = @idSala;";

            using (SqlConnection conexaoBanco = BancoDados.AbrirConexao())
            using (SqlCommand comandoSql = new SqlCommand(comandoTexto, conexaoBanco))
            {
                comandoSql.Parameters.Add("@idSala", SqlDbType.Int).Value = idSala;

                using (SqlDataReader leitorDados = comandoSql.ExecuteReader())
                {
                    if (leitorDados.Read())
                    {
                        return MontarSala(leitorDados);
                    }
                }
            }

            throw new RegistroNaoEncontrado("Sala não encontrada.");
        }

        // ------------------------------------------------------------
        // CREATE
        // ------------------------------------------------------------
        public Sala CadastrarSala(Sala salaNova)
        {
            ValidarSala(salaNova);

            string comandoTexto =
                "INSERT INTO Salas (Nome, Capacidade) " +
                "VALUES (@nomeSala, @capacidadeSala); " +
                "SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using (SqlConnection conexaoBanco = BancoDados.AbrirConexao())
            using (SqlCommand comandoSql = new SqlCommand(comandoTexto, conexaoBanco))
            {
                comandoSql.Parameters.Add("@nomeSala", SqlDbType.VarChar, 100).Value = salaNova.Nome;
                comandoSql.Parameters.Add("@capacidadeSala", SqlDbType.Int).Value = salaNova.Capacidade;

                try
                {
                    salaNova.IdSala = Convert.ToInt32(comandoSql.ExecuteScalar());
                }
                catch (SqlException erroSql)
                {
                    throw TraduzirErroDoBanco(erroSql);
                }
            }

            return salaNova;
        }

        // ------------------------------------------------------------
        // UPDATE
        // ------------------------------------------------------------
        public Sala AtualizarSala(Sala salaAlterada)
        {
            ValidarSala(salaAlterada);

            // Uma sala nao pode ficar menor do que o maior assento ja
            // vendido nela, senao o ingresso ficaria "fora" da sala.
            int maiorAssentoVendido = BuscarMaiorAssentoVendidoDaSala(salaAlterada.IdSala);

            if (salaAlterada.Capacidade < maiorAssentoVendido)
            {
                throw new ConflitoDeDados(
                    "Esta sala já possui ingressos ativos até o assento " + maiorAssentoVendido +
                    ". A capacidade não pode ser menor que isso.");
            }

            string comandoTexto =
                "UPDATE Salas " +
                "SET Nome = @nomeSala, " +
                "    Capacidade = @capacidadeSala " +
                "WHERE IdSala = @idSala;";

            using (SqlConnection conexaoBanco = BancoDados.AbrirConexao())
            using (SqlCommand comandoSql = new SqlCommand(comandoTexto, conexaoBanco))
            {
                comandoSql.Parameters.Add("@nomeSala", SqlDbType.VarChar, 100).Value = salaAlterada.Nome;
                comandoSql.Parameters.Add("@capacidadeSala", SqlDbType.Int).Value = salaAlterada.Capacidade;
                comandoSql.Parameters.Add("@idSala", SqlDbType.Int).Value = salaAlterada.IdSala;

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
                    throw new RegistroNaoEncontrado("Sala não encontrada.");
                }
            }

            return BuscarSalaPorId(salaAlterada.IdSala);
        }

        // ------------------------------------------------------------
        // DELETE
        // ------------------------------------------------------------
        public void ExcluirSala(int idSala)
        {
            string comandoContagem =
                "SELECT COUNT(*) FROM Sessoes WHERE IdSala = @idSala;";

            string comandoExclusao =
                "DELETE FROM Salas WHERE IdSala = @idSala;";

            using (SqlConnection conexaoBanco = BancoDados.AbrirConexao())
            {
                using (SqlCommand comandoSql = new SqlCommand(comandoContagem, conexaoBanco))
                {
                    comandoSql.Parameters.Add("@idSala", SqlDbType.Int).Value = idSala;

                    int totalDeSessoes = Convert.ToInt32(comandoSql.ExecuteScalar());

                    if (totalDeSessoes > 0)
                    {
                        throw new ConflitoDeDados(
                            "Esta sala possui " + totalDeSessoes + " sessão(ões) cadastrada(s) e não pode ser excluída.");
                    }
                }

                using (SqlCommand comandoSql = new SqlCommand(comandoExclusao, conexaoBanco))
                {
                    comandoSql.Parameters.Add("@idSala", SqlDbType.Int).Value = idSala;

                    int linhasAfetadas = comandoSql.ExecuteNonQuery();

                    if (linhasAfetadas == 0)
                    {
                        throw new RegistroNaoEncontrado("Sala não encontrada.");
                    }
                }
            }
        }

        // ------------------------------------------------------------
        // Apoio
        // ------------------------------------------------------------
        private int BuscarMaiorAssentoVendidoDaSala(int idSala)
        {
            string comandoTexto =
                "SELECT ISNULL(MAX(Ingressos.NumeroAssento), 0) " +
                "FROM Ingressos " +
                "INNER JOIN Sessoes ON Ingressos.IdSessao = Sessoes.IdSessao " +
                "WHERE Sessoes.IdSala = @idSala " +
                "  AND Ingressos.Status = 'Ativo';";

            using (SqlConnection conexaoBanco = BancoDados.AbrirConexao())
            using (SqlCommand comandoSql = new SqlCommand(comandoTexto, conexaoBanco))
            {
                comandoSql.Parameters.Add("@idSala", SqlDbType.Int).Value = idSala;
                return Convert.ToInt32(comandoSql.ExecuteScalar());
            }
        }

        private Sala MontarSala(SqlDataReader leitorDados)
        {
            Sala salaLida = new Sala();

            salaLida.IdSala = leitorDados.GetInt32(leitorDados.GetOrdinal("IdSala"));
            salaLida.Nome = leitorDados.GetString(leitorDados.GetOrdinal("Nome"));
            salaLida.Capacidade = leitorDados.GetInt32(leitorDados.GetOrdinal("Capacidade"));

            return salaLida;
        }

        private void ValidarSala(Sala salaVerificada)
        {
            salaVerificada.Nome = Validacoes.TextoObrigatorio(salaVerificada.Nome, "nome da sala", 100);

            Validacoes.NumeroMaiorQueZero(salaVerificada.Capacidade, "capacidade");

            if (salaVerificada.Capacidade > 300)
            {
                throw new ErroDeValidacao("A capacidade da sala deve ser no máximo 300.");
            }
        }

        private Exception TraduzirErroDoBanco(SqlException erroSql)
        {
            if (erroSql.Number == 2601 || erroSql.Number == 2627)
            {
                return new ConflitoDeDados("Já existe uma sala cadastrada com esse nome.");
            }

            return erroSql;
        }
    }
}
