using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;

namespace CineManager
{
    /// <summary>
    /// Todas as operacoes da tabela INGRESSOS.
    ///
    /// Aqui esta a regra mais importante do sistema: um assento
    /// nao pode ser vendido duas vezes na mesma sessao. A regra e
    /// verificada em C# (com UPDLOCK dentro de uma transacao) e
    /// garantida pelo banco (indice UNIQUE filtrado).
    /// </summary>
    public class IngressoRepositorio
    {
        private const string CONSULTA_BASE =
            "SELECT " +
            "    Ingressos.IdIngresso, " +
            "    Ingressos.IdSessao, " +
            "    Ingressos.IdUsuario, " +
            "    Ingressos.NomeCliente, " +
            "    Ingressos.CpfCliente, " +
            "    Ingressos.EmailCliente, " +
            "    Ingressos.NumeroAssento, " +
            "    Ingressos.Preco, " +
            "    Ingressos.TipoIngresso, " +
            "    Ingressos.TipoDocumentoMeia, " +
            "    Ingressos.Status, " +
            "    Sessoes.DataSessao, " +
            "    Sessoes.HorarioSessao, " +
            "    Filmes.Titulo AS TituloFilme, " +
            "    Salas.Nome    AS NomeSala " +
            "FROM Ingressos " +
            "INNER JOIN Sessoes ON Ingressos.IdSessao = Sessoes.IdSessao " +
            "INNER JOIN Filmes  ON Sessoes.IdFilme    = Filmes.IdFilme " +
            "INNER JOIN Salas   ON Sessoes.IdSala     = Salas.IdSala ";

        // ------------------------------------------------------------
        // READ - listar todos (painel administrativo)
        // ------------------------------------------------------------
        public List<Ingresso> ListarIngressos()
        {
            List<Ingresso> listaDeIngressos = new List<Ingresso>();

            string comandoTexto = CONSULTA_BASE + "ORDER BY Ingressos.IdIngresso DESC;";

            using (SqlConnection conexaoBanco = BancoDados.AbrirConexao())
            using (SqlCommand comandoSql = new SqlCommand(comandoTexto, conexaoBanco))
            using (SqlDataReader leitorDados = comandoSql.ExecuteReader())
            {
                while (leitorDados.Read())
                {
                    listaDeIngressos.Add(MontarIngresso(leitorDados));
                }
            }

            return listaDeIngressos;
        }

        // ------------------------------------------------------------
        // READ - listar apenas os ingressos do usuario autenticado
        // ("Meus ingressos")
        // ------------------------------------------------------------
        /// <summary>
        /// Devolve SOMENTE os ingressos cujo IdUsuario e igual ao do
        /// usuario autenticado - a consulta e vinculada ao usuario
        /// reconhecido pelo servidor (ServidorHttp.ExigirUsuarioAutenticado),
        /// nunca a um id que o cliente poderia informar na requisicao.
        /// Assim um usuario nunca enxerga ingresso de outra pessoa.
        /// </summary>
        public List<Ingresso> ListarIngressosDoUsuario(int idUsuarioAutenticado)
        {
            List<Ingresso> listaDeIngressos = new List<Ingresso>();

            string comandoTexto = CONSULTA_BASE +
                "WHERE Ingressos.IdUsuario = @idUsuario " +
                "ORDER BY Ingressos.IdIngresso DESC;";

            using (SqlConnection conexaoBanco = BancoDados.AbrirConexao())
            using (SqlCommand comandoSql = new SqlCommand(comandoTexto, conexaoBanco))
            {
                comandoSql.Parameters.Add("@idUsuario", SqlDbType.Int).Value = idUsuarioAutenticado;

                using (SqlDataReader leitorDados = comandoSql.ExecuteReader())
                {
                    while (leitorDados.Read())
                    {
                        listaDeIngressos.Add(MontarIngresso(leitorDados));
                    }
                }
            }

            return listaDeIngressos;
        }

        // ------------------------------------------------------------
        // READ - assentos ocupados de uma sessao
        // ------------------------------------------------------------
        /// <summary>
        /// Devolve so os assentos de ingressos ATIVOS.
        /// Ingresso cancelado nao ocupa mais o assento.
        /// </summary>
        public List<int> BuscarAssentosOcupados(int idSessao)
        {
            List<int> assentosOcupados = new List<int>();

            string comandoTexto =
                "SELECT NumeroAssento " +
                "FROM Ingressos " +
                "WHERE IdSessao = @idSessao " +
                "  AND Status = 'Ativo' " +
                "ORDER BY NumeroAssento;";

            using (SqlConnection conexaoBanco = BancoDados.AbrirConexao())
            using (SqlCommand comandoSql = new SqlCommand(comandoTexto, conexaoBanco))
            {
                comandoSql.Parameters.Add("@idSessao", SqlDbType.Int).Value = idSessao;

                using (SqlDataReader leitorDados = comandoSql.ExecuteReader())
                {
                    while (leitorDados.Read())
                    {
                        assentosOcupados.Add(leitorDados.GetInt32(0));
                    }
                }
            }

            return assentosOcupados;
        }

        // ------------------------------------------------------------
        // CREATE - compra de ingresso
        // ------------------------------------------------------------
        public Ingresso ComprarIngresso(Ingresso ingressoNovo, int idUsuarioAutenticado)
        {
            // O dono do ingresso e sempre o usuario autenticado que o
            // ServidorHttp identificou pelo cookie de sessao - nunca um
            // valor vindo do corpo da requisicao.
            ingressoNovo.IdUsuario = idUsuarioAutenticado;

            // 1) Validacao dos dados do cliente
            ingressoNovo.NomeCliente = Validacoes.TextoObrigatorio(ingressoNovo.NomeCliente, "nome", 100);
            ingressoNovo.CpfCliente = Validacoes.CpfValido(ingressoNovo.CpfCliente);
            ingressoNovo.EmailCliente = Validacoes.EmailValido(ingressoNovo.EmailCliente);

            if (ingressoNovo.NomeCliente.Length < 3)
            {
                throw new ErroDeValidacao("Digite o nome completo.");
            }

            Validacoes.NumeroMaiorQueZero(ingressoNovo.NumeroAssento, "assento");

            // 1.1) Tipo de ingresso (Inteira/Meia) e, se for meia, o
            //      documento comprobatorio. A meia so e aceita com
            //      documento informado dentre os tipos permitidos.
            ingressoNovo.TipoIngresso = Validacoes.TipoDeIngressoValido(ingressoNovo.TipoIngresso);
            ingressoNovo.TipoDocumentoMeia =
                Validacoes.TipoDocumentoMeiaValido(ingressoNovo.TipoIngresso, ingressoNovo.TipoDocumentoMeia);

            // 2) A sessao precisa existir (so para confirmar o filme,
            //    a sala e a capacidade). O PRECO NUNCA vem da sessao
            //    nem da tela: quem decide o valor cobrado e sempre o
            //    backend, com base no TIPO de ingresso (Precos.ValorPara).
            //    Isso impede o cliente de manipular o preco pelo navegador.
            SessaoRepositorio sessaoRepositorio = new SessaoRepositorio();
            Sessao sessaoEscolhida = sessaoRepositorio.BuscarSessaoPorId(ingressoNovo.IdSessao);

            ingressoNovo.Preco = Precos.ValorPara(ingressoNovo.TipoIngresso);

            if (ingressoNovo.NumeroAssento > sessaoEscolhida.CapacidadeSala)
            {
                throw new ErroDeValidacao(
                    "A sala " + sessaoEscolhida.NomeSala + " possui apenas " +
                    sessaoEscolhida.CapacidadeSala + " assentos.");
            }

            // 3) Verificacao do assento + INSERT dentro de uma TRANSACAO.
            using (SqlConnection conexaoBanco = BancoDados.AbrirConexao())
            using (SqlTransaction transacaoBanco = conexaoBanco.BeginTransaction())
            {
                try
                {
                    // UPDLOCK + HOLDLOCK: seguram a linha ate o fim da
                    // transacao, impedindo que duas compras simultaneas
                    // enxerguem o mesmo assento como livre.
                    string comandoVerificacao =
                        "SELECT COUNT(*) " +
                        "FROM Ingressos WITH (UPDLOCK, HOLDLOCK) " +
                        "WHERE IdSessao = @idSessao " +
                        "  AND NumeroAssento = @numeroAssento " +
                        "  AND Status = 'Ativo';";

                    using (SqlCommand comandoSql = new SqlCommand(comandoVerificacao, conexaoBanco, transacaoBanco))
                    {
                        comandoSql.Parameters.Add("@idSessao", SqlDbType.Int).Value = ingressoNovo.IdSessao;
                        comandoSql.Parameters.Add("@numeroAssento", SqlDbType.Int).Value = ingressoNovo.NumeroAssento;

                        int assentosJaVendidos = Convert.ToInt32(comandoSql.ExecuteScalar());

                        if (assentosJaVendidos > 0)
                        {
                            throw new ConflitoDeDados(
                                "O assento " + ingressoNovo.NumeroAssento + " já foi vendido para esta sessão.");
                        }
                    }

                    string comandoInsercao =
                        "INSERT INTO Ingressos " +
                        "(IdSessao, IdUsuario, NomeCliente, CpfCliente, EmailCliente, NumeroAssento, Preco, TipoIngresso, TipoDocumentoMeia, Status) " +
                        "VALUES " +
                        "(@idSessao, @idUsuario, @nomeCliente, @cpfCliente, @emailCliente, @numeroAssento, @precoIngresso, @tipoIngresso, @tipoDocumentoMeia, 'Ativo'); " +
                        "SELECT CAST(SCOPE_IDENTITY() AS INT);";

                    using (SqlCommand comandoSql = new SqlCommand(comandoInsercao, conexaoBanco, transacaoBanco))
                    {
                        comandoSql.Parameters.Add("@idSessao", SqlDbType.Int).Value = ingressoNovo.IdSessao;
                        comandoSql.Parameters.Add("@idUsuario", SqlDbType.Int).Value = ingressoNovo.IdUsuario.Value;
                        comandoSql.Parameters.Add("@nomeCliente", SqlDbType.VarChar, 100).Value = ingressoNovo.NomeCliente;
                        comandoSql.Parameters.Add("@cpfCliente", SqlDbType.VarChar, 11).Value = ingressoNovo.CpfCliente;
                        comandoSql.Parameters.Add("@emailCliente", SqlDbType.VarChar, 150).Value = ingressoNovo.EmailCliente;
                        comandoSql.Parameters.Add("@numeroAssento", SqlDbType.Int).Value = ingressoNovo.NumeroAssento;
                        comandoSql.Parameters.Add("@tipoIngresso", SqlDbType.VarChar, 10).Value = ingressoNovo.TipoIngresso;
                        comandoSql.Parameters.Add("@tipoDocumentoMeia", SqlDbType.VarChar, 50).Value =
                            (object)ingressoNovo.TipoDocumentoMeia ?? DBNull.Value;

                        SqlParameter parametroPreco = comandoSql.Parameters.Add("@precoIngresso", SqlDbType.Decimal);
                        parametroPreco.Precision = 10;
                        parametroPreco.Scale = 2;
                        parametroPreco.Value = ingressoNovo.Preco;

                        ingressoNovo.IdIngresso = Convert.ToInt32(comandoSql.ExecuteScalar());
                    }

                    transacaoBanco.Commit();
                }
                catch (SqlException erroSql)
                {
                    transacaoBanco.Rollback();

                    // Se o indice UNIQUE barrou a insercao, o assento
                    // foi vendido por outra pessoa nesse meio tempo.
                    if (erroSql.Number == 2601 || erroSql.Number == 2627)
                    {
                        throw new ConflitoDeDados(
                            "O assento " + ingressoNovo.NumeroAssento + " acabou de ser vendido. Escolha outro.");
                    }

                    throw;
                }
                catch (Exception)
                {
                    transacaoBanco.Rollback();
                    throw;
                }
            }

            ingressoNovo.Status = "Ativo";
            ingressoNovo.TituloFilme = sessaoEscolhida.TituloFilme;
            ingressoNovo.NomeSala = sessaoEscolhida.NomeSala;
            ingressoNovo.DataSessao = sessaoEscolhida.DataSessao;
            ingressoNovo.HorarioSessao = sessaoEscolhida.HorarioSessao;

            return ingressoNovo;
        }

        // ------------------------------------------------------------
        // CREATE - compra de VARIOS ingressos de uma vez (varios
        // assentos na mesma sessao, cada um com seu proprio tipo).
        // ------------------------------------------------------------
        /// <summary>
        /// Compra em lote: o cliente escolhe varios assentos na mesma
        /// sessao numa unica operacao (um assento Inteira, outro Meia,
        /// etc.). Cada assento continua virando UMA LINHA independente
        /// na tabela Ingressos - nunca sao agrupados em um so registro.
        ///
        /// Tudo acontece dentro de UMA UNICA TRANSACAO: se qualquer
        /// assento do lote ja tiver sido vendido (por outra pessoa, entre
        /// o carregamento da tela e a confirmacao), a transacao inteira e
        /// desfeita e NENHUM dos ingressos do lote fica gravado - a compra
        /// nunca fica "pela metade".
        /// </summary>
        public List<Ingresso> ComprarIngressos(int idSessao, int idUsuarioAutenticado, string nomeCliente, string cpfCliente,
                                                string emailCliente, List<ItemIngresso> itensDaCompra)
        {
            // 1) Validacao dos dados do cliente (unicos para o lote inteiro).
            nomeCliente = Validacoes.TextoObrigatorio(nomeCliente, "nome", 100);
            cpfCliente = Validacoes.CpfValido(cpfCliente);
            emailCliente = Validacoes.EmailValido(emailCliente);

            if (nomeCliente.Length < 3)
            {
                throw new ErroDeValidacao("Digite o nome completo.");
            }

            // 2) A sessao precisa existir. Igual a compra individual, o
            //    PRECO NUNCA vem do front-end: cada item e precificado
            //    aqui, a partir do seu proprio TipoIngresso.
            SessaoRepositorio sessaoRepositorio = new SessaoRepositorio();
            Sessao sessaoEscolhida = sessaoRepositorio.BuscarSessaoPorId(idSessao);

            // 3) Validacao de cada item do lote (assento, tipo, documento)
            //    e verificacao de assento duplicado DENTRO DO PROPRIO LOTE
            //    (o usuario nao pode escolher o mesmo assento duas vezes
            //    na mesma compra).
            HashSet<int> assentosJaVistosNoLote = new HashSet<int>();

            foreach (ItemIngresso itemDaCompra in itensDaCompra)
            {
                Validacoes.NumeroMaiorQueZero(itemDaCompra.NumeroAssento, "assento");

                if (itemDaCompra.NumeroAssento > sessaoEscolhida.CapacidadeSala)
                {
                    throw new ErroDeValidacao(
                        "A sala " + sessaoEscolhida.NomeSala + " possui apenas " +
                        sessaoEscolhida.CapacidadeSala + " assentos.");
                }

                if (!assentosJaVistosNoLote.Add(itemDaCompra.NumeroAssento))
                {
                    throw new ErroDeValidacao(
                        "O assento " + itemDaCompra.NumeroAssento + " foi selecionado mais de uma vez nesta compra.");
                }

                itemDaCompra.TipoIngresso = Validacoes.TipoDeIngressoValido(itemDaCompra.TipoIngresso);
                itemDaCompra.TipoDocumentoMeia =
                    Validacoes.TipoDocumentoMeiaValido(itemDaCompra.TipoIngresso, itemDaCompra.TipoDocumentoMeia);
            }

            List<Ingresso> ingressosComprados = new List<Ingresso>();

            // 4) Verificacao de cada assento + INSERT de cada ingresso,
            //    tudo dentro da MESMA transacao. Se um assento do lote
            //    ja estiver ocupado, a transacao inteira e desfeita.
            using (SqlConnection conexaoBanco = BancoDados.AbrirConexao())
            using (SqlTransaction transacaoBanco = conexaoBanco.BeginTransaction())
            {
                try
                {
                    foreach (ItemIngresso itemDaCompra in itensDaCompra)
                    {
                        // UPDLOCK + HOLDLOCK: segura a linha ate o fim da
                        // transacao do lote inteiro, impedindo que outra
                        // compra simultanea enxergue o mesmo assento livre.
                        string comandoVerificacao =
                            "SELECT COUNT(*) " +
                            "FROM Ingressos WITH (UPDLOCK, HOLDLOCK) " +
                            "WHERE IdSessao = @idSessao " +
                            "  AND NumeroAssento = @numeroAssento " +
                            "  AND Status = 'Ativo';";

                        using (SqlCommand comandoSql = new SqlCommand(comandoVerificacao, conexaoBanco, transacaoBanco))
                        {
                            comandoSql.Parameters.Add("@idSessao", SqlDbType.Int).Value = idSessao;
                            comandoSql.Parameters.Add("@numeroAssento", SqlDbType.Int).Value = itemDaCompra.NumeroAssento;

                            int assentosJaVendidos = Convert.ToInt32(comandoSql.ExecuteScalar());

                            if (assentosJaVendidos > 0)
                            {
                                throw new ConflitoDeDados(
                                    "O assento " + itemDaCompra.NumeroAssento + " já foi vendido para esta sessão. " +
                                    "Nenhum ingresso desta compra foi gravado; escolha outro assento e tente novamente.");
                            }
                        }

                        decimal precoDoItem = Precos.ValorPara(itemDaCompra.TipoIngresso);

                        string comandoInsercao =
                            "INSERT INTO Ingressos " +
                            "(IdSessao, IdUsuario, NomeCliente, CpfCliente, EmailCliente, NumeroAssento, Preco, TipoIngresso, TipoDocumentoMeia, Status) " +
                            "VALUES " +
                            "(@idSessao, @idUsuario, @nomeCliente, @cpfCliente, @emailCliente, @numeroAssento, @precoIngresso, @tipoIngresso, @tipoDocumentoMeia, 'Ativo'); " +
                            "SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        Ingresso ingressoGravado = new Ingresso();

                        using (SqlCommand comandoSql = new SqlCommand(comandoInsercao, conexaoBanco, transacaoBanco))
                        {
                            comandoSql.Parameters.Add("@idSessao", SqlDbType.Int).Value = idSessao;
                            comandoSql.Parameters.Add("@idUsuario", SqlDbType.Int).Value = idUsuarioAutenticado;
                            comandoSql.Parameters.Add("@nomeCliente", SqlDbType.VarChar, 100).Value = nomeCliente;
                            comandoSql.Parameters.Add("@cpfCliente", SqlDbType.VarChar, 11).Value = cpfCliente;
                            comandoSql.Parameters.Add("@emailCliente", SqlDbType.VarChar, 150).Value = emailCliente;
                            comandoSql.Parameters.Add("@numeroAssento", SqlDbType.Int).Value = itemDaCompra.NumeroAssento;
                            comandoSql.Parameters.Add("@tipoIngresso", SqlDbType.VarChar, 10).Value = itemDaCompra.TipoIngresso;
                            comandoSql.Parameters.Add("@tipoDocumentoMeia", SqlDbType.VarChar, 50).Value =
                                (object)itemDaCompra.TipoDocumentoMeia ?? DBNull.Value;

                            SqlParameter parametroPreco = comandoSql.Parameters.Add("@precoIngresso", SqlDbType.Decimal);
                            parametroPreco.Precision = 10;
                            parametroPreco.Scale = 2;
                            parametroPreco.Value = precoDoItem;

                            ingressoGravado.IdIngresso = Convert.ToInt32(comandoSql.ExecuteScalar());
                        }

                        ingressoGravado.IdSessao = idSessao;
                        ingressoGravado.IdUsuario = idUsuarioAutenticado;
                        ingressoGravado.NomeCliente = nomeCliente;
                        ingressoGravado.CpfCliente = cpfCliente;
                        ingressoGravado.EmailCliente = emailCliente;
                        ingressoGravado.NumeroAssento = itemDaCompra.NumeroAssento;
                        ingressoGravado.Preco = precoDoItem;
                        ingressoGravado.TipoIngresso = itemDaCompra.TipoIngresso;
                        ingressoGravado.TipoDocumentoMeia = itemDaCompra.TipoDocumentoMeia;
                        ingressoGravado.Status = "Ativo";
                        ingressoGravado.TituloFilme = sessaoEscolhida.TituloFilme;
                        ingressoGravado.NomeSala = sessaoEscolhida.NomeSala;
                        ingressoGravado.DataSessao = sessaoEscolhida.DataSessao;
                        ingressoGravado.HorarioSessao = sessaoEscolhida.HorarioSessao;

                        ingressosComprados.Add(ingressoGravado);
                    }

                    transacaoBanco.Commit();
                }
                catch (SqlException erroSql)
                {
                    transacaoBanco.Rollback();

                    if (erroSql.Number == 2601 || erroSql.Number == 2627)
                    {
                        throw new ConflitoDeDados(
                            "Um dos assentos escolhidos acabou de ser vendido. Nenhum ingresso desta compra foi " +
                            "gravado; escolha outro assento e tente novamente.");
                    }

                    throw;
                }
                catch (Exception)
                {
                    transacaoBanco.Rollback();
                    throw;
                }
            }

            return ingressosComprados;
        }

        // ------------------------------------------------------------
        // UPDATE - cancelamento
        // ------------------------------------------------------------
        /// <summary>
        /// O ingresso NAO e apagado: apenas muda de status.
        /// Isso preserva o historico da venda e devolve o assento.
        /// </summary>
        public Ingresso CancelarIngresso(int idIngresso)
        {
            string comandoTexto =
                "UPDATE Ingressos " +
                "SET Status = 'Cancelado' " +
                "WHERE IdIngresso = @idIngresso " +
                "  AND Status = 'Ativo';";

            using (SqlConnection conexaoBanco = BancoDados.AbrirConexao())
            using (SqlCommand comandoSql = new SqlCommand(comandoTexto, conexaoBanco))
            {
                comandoSql.Parameters.Add("@idIngresso", SqlDbType.Int).Value = idIngresso;

                int linhasAfetadas = comandoSql.ExecuteNonQuery();

                if (linhasAfetadas == 0)
                {
                    // Ou o ingresso nao existe, ou ja estava cancelado.
                    // A busca abaixo dispara RegistroNaoEncontrado quando o id nao existe.
                    BuscarIngressoPorId(idIngresso);

                    throw new ConflitoDeDados("Este ingresso já está cancelado.");
                }
            }

            return BuscarIngressoPorId(idIngresso);
        }

        // ------------------------------------------------------------
        // READ - buscar um registro
        // ------------------------------------------------------------
        public Ingresso BuscarIngressoPorId(int idIngresso)
        {
            string comandoTexto = CONSULTA_BASE + "WHERE Ingressos.IdIngresso = @idIngresso;";

            using (SqlConnection conexaoBanco = BancoDados.AbrirConexao())
            using (SqlCommand comandoSql = new SqlCommand(comandoTexto, conexaoBanco))
            {
                comandoSql.Parameters.Add("@idIngresso", SqlDbType.Int).Value = idIngresso;

                using (SqlDataReader leitorDados = comandoSql.ExecuteReader())
                {
                    if (leitorDados.Read())
                    {
                        return MontarIngresso(leitorDados);
                    }
                }
            }

            throw new RegistroNaoEncontrado("Ingresso não encontrado.");
        }

        // ------------------------------------------------------------
        // Apoio
        // ------------------------------------------------------------
        private Ingresso MontarIngresso(SqlDataReader leitorDados)
        {
            Ingresso ingressoLido = new Ingresso();

            ingressoLido.IdIngresso = leitorDados.GetInt32(leitorDados.GetOrdinal("IdIngresso"));
            ingressoLido.IdSessao = leitorDados.GetInt32(leitorDados.GetOrdinal("IdSessao"));

            int ordinalIdUsuario = leitorDados.GetOrdinal("IdUsuario");
            ingressoLido.IdUsuario = leitorDados.IsDBNull(ordinalIdUsuario)
                ? (int?)null
                : leitorDados.GetInt32(ordinalIdUsuario);

            ingressoLido.NomeCliente = leitorDados.GetString(leitorDados.GetOrdinal("NomeCliente"));
            ingressoLido.CpfCliente = leitorDados.GetString(leitorDados.GetOrdinal("CpfCliente"));
            ingressoLido.EmailCliente = leitorDados.GetString(leitorDados.GetOrdinal("EmailCliente"));
            ingressoLido.NumeroAssento = leitorDados.GetInt32(leitorDados.GetOrdinal("NumeroAssento"));
            ingressoLido.Preco = leitorDados.GetDecimal(leitorDados.GetOrdinal("Preco"));
            ingressoLido.TipoIngresso = leitorDados.GetString(leitorDados.GetOrdinal("TipoIngresso"));

            int ordinalDocumento = leitorDados.GetOrdinal("TipoDocumentoMeia");
            ingressoLido.TipoDocumentoMeia = leitorDados.IsDBNull(ordinalDocumento)
                ? null
                : leitorDados.GetString(ordinalDocumento);

            ingressoLido.Status = leitorDados.GetString(leitorDados.GetOrdinal("Status"));

            DateTime dataLida = leitorDados.GetDateTime(leitorDados.GetOrdinal("DataSessao"));
            ingressoLido.DataSessao = dataLida.ToString("yyyy-MM-dd");

            TimeSpan horarioLido = leitorDados.GetTimeSpan(leitorDados.GetOrdinal("HorarioSessao"));
            ingressoLido.HorarioSessao = horarioLido.ToString(@"hh\:mm");

            ingressoLido.TituloFilme = leitorDados.GetString(leitorDados.GetOrdinal("TituloFilme"));
            ingressoLido.NomeSala = leitorDados.GetString(leitorDados.GetOrdinal("NomeSala"));

            return ingressoLido;
        }
    }
}
