using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace CineManager
{
    /// <summary>
    /// Servidor HTTP do CineManager.
    ///
    /// Usa System.Net.HttpListener, que faz parte do proprio .NET.
    /// NAO e ASP.NET Core nem qualquer outro framework web: o codigo
    /// abaixo le a rota na mao, chama o repositorio certo e devolve JSON.
    ///
    /// Ele tem duas tarefas:
    ///   1) entregar os arquivos da pasta Frontend (html, css, js);
    ///   2) responder as rotas que comecam com /api/.
    /// </summary>
    public class ServidorHttp
    {
        private readonly HttpListener escutadorHttp = new HttpListener();
        private readonly string enderecoDoServidor;
        private readonly string pastaDoFrontend;

        private readonly FilmeRepositorio filmeRepositorio = new FilmeRepositorio();
        private readonly SalaRepositorio salaRepositorio = new SalaRepositorio();
        private readonly SessaoRepositorio sessaoRepositorio = new SessaoRepositorio();
        private readonly IngressoRepositorio ingressoRepositorio = new IngressoRepositorio();
        private readonly UsuarioRepositorio usuarioRepositorio = new UsuarioRepositorio();

        private readonly JsonSerializerOptions opcoesDoJson = new JsonSerializerOptions
        {
            // Os nomes das propriedades C# (IdFilme) viram idFilme no JSON.
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public ServidorHttp(string endereco)
        {
            enderecoDoServidor = endereco;
            pastaDoFrontend = LocalizarPastaDoFrontend();
        }

        // ============================================================
        // CICLO DE VIDA DO SERVIDOR
        // ============================================================
        public void Iniciar()
        {
            escutadorHttp.Prefixes.Add(enderecoDoServidor);
            escutadorHttp.Start();

            Console.WriteLine("Servidor no ar em: " + enderecoDoServidor);
            Console.WriteLine("Pasta do front-end: " + (pastaDoFrontend ?? "NAO ENCONTRADA"));
            Console.WriteLine("Pressione CTRL + C para encerrar.");
            Console.WriteLine();

            while (escutadorHttp.IsListening)
            {
                HttpListenerContext contextoHttp = escutadorHttp.GetContext();
                AtenderRequisicao(contextoHttp);
            }
        }

        /// <summary>
        /// Recebe uma requisicao, decide quem vai responder e trata os erros.
        /// </summary>
        private void AtenderRequisicao(HttpListenerContext contextoHttp)
        {
            string metodoHttp = contextoHttp.Request.HttpMethod;
            string caminhoDaRota = contextoHttp.Request.Url.AbsolutePath;

            Console.WriteLine(metodoHttp + " " + caminhoDaRota);

            try
            {
                if (caminhoDaRota.StartsWith("/api/"))
                {
                    ResponderApi(contextoHttp, metodoHttp, caminhoDaRota);
                }
                else
                {
                    ResponderArquivoDoFrontend(contextoHttp, caminhoDaRota);
                }
            }
            catch (ErroDeValidacao erro)
            {
                EnviarJson(contextoHttp, 400, new { erro = erro.Message });
            }
            catch (RegistroNaoEncontrado erro)
            {
                EnviarJson(contextoHttp, 404, new { erro = erro.Message });
            }
            catch (ConflitoDeDados erro)
            {
                EnviarJson(contextoHttp, 409, new { erro = erro.Message });
            }
            catch (NaoAutenticado erro)
            {
                EnviarJson(contextoHttp, 401, new { erro = erro.Message });
            }
            catch (AcessoNegado erro)
            {
                EnviarJson(contextoHttp, 403, new { erro = erro.Message });
            }
            catch (SqlException erroSql)
            {
                // O detalhe tecnico fica no console, para a equipe.
                Console.WriteLine("ERRO DE BANCO: " + erroSql.Message);

                EnviarJson(contextoHttp, 500, new
                {
                    erro = "Não foi possível acessar o banco de dados. Verifique se o SQL Server está ligado e se a connection string está correta."
                });
            }
            catch (Exception erro)
            {
                // O usuario nunca ve a stack trace; ela fica no console.
                Console.WriteLine("ERRO INESPERADO: " + erro.ToString());

                EnviarJson(contextoHttp, 500, new { erro = "Ocorreu um erro inesperado no servidor." });
            }
        }

        // ============================================================
        // ROTEADOR DA API
        // ============================================================
        private void ResponderApi(HttpListenerContext contextoHttp, string metodoHttp, string caminhoDaRota)
        {
            // "/api/filmes/3/status" vira ["api", "filmes", "3", "status"]
            string[] partesDaRota = caminhoDaRota.Trim('/').Split('/');

            string nomeDoRecurso = partesDaRota.Length > 1 ? partesDaRota[1].ToLower() : "";
            string segundaParte = partesDaRota.Length > 2 ? partesDaRota[2] : "";
            string terceiraParte = partesDaRota.Length > 3 ? partesDaRota[3].ToLower() : "";

            if (nomeDoRecurso == "filmes")
            {
                ResponderRotaDeFilmes(contextoHttp, metodoHttp, segundaParte, terceiraParte);
                return;
            }

            if (nomeDoRecurso == "salas")
            {
                ResponderRotaDeSalas(contextoHttp, metodoHttp, segundaParte);
                return;
            }

            if (nomeDoRecurso == "sessoes")
            {
                ResponderRotaDeSessoes(contextoHttp, metodoHttp, segundaParte, terceiraParte);
                return;
            }

            if (nomeDoRecurso == "ingressos")
            {
                ResponderRotaDeIngressos(contextoHttp, metodoHttp, segundaParte, terceiraParte);
                return;
            }

            if (nomeDoRecurso == "usuarios")
            {
                ResponderRotaDeUsuarios(contextoHttp, metodoHttp, segundaParte);
                return;
            }

            if (nomeDoRecurso == "login")
            {
                ResponderRotaDeLogin(contextoHttp, metodoHttp);
                return;
            }

            if (nomeDoRecurso == "logout")
            {
                ResponderRotaDeLogout(contextoHttp, metodoHttp);
                return;
            }

            if (nomeDoRecurso == "sessao")
            {
                ResponderRotaDeSessaoAtual(contextoHttp, metodoHttp);
                return;
            }

            EnviarJson(contextoHttp, 404, new { erro = "Rota não encontrada: " + caminhoDaRota });
        }

        // ------------------------------------------------------------
        // /api/filmes
        // ------------------------------------------------------------
        private void ResponderRotaDeFilmes(HttpListenerContext contextoHttp, string metodoHttp,
                                           string segundaParte, string terceiraParte)
        {
            // GET /api/filmes           -> todos os filmes
            // GET /api/filmes?ativo=1   -> somente os filmes em cartaz
            if (metodoHttp == "GET" && segundaParte == "")
            {
                bool somenteAtivos = contextoHttp.Request.QueryString["ativo"] == "1";
                EnviarJson(contextoHttp, 200, filmeRepositorio.ListarFilmes(somenteAtivos));
                return;
            }

            // GET /api/filmes/{id}
            if (metodoHttp == "GET" && segundaParte != "")
            {
                int idFilme = LerNumeroDaRota(segundaParte, "id do filme");
                EnviarJson(contextoHttp, 200, filmeRepositorio.BuscarFilmePorId(idFilme));
                return;
            }

            // POST /api/filmes
            if (metodoHttp == "POST" && segundaParte == "")
            {
                ExigirAdministrador(contextoHttp);

                DadosRecebidos dadosDoFilme = LerCorpoDaRequisicao(contextoHttp);

                Filme filmeNovo = new Filme();
                filmeNovo.Titulo = dadosDoFilme.Texto("titulo");
                filmeNovo.Genero = dadosDoFilme.Texto("genero");
                filmeNovo.Duracao = dadosDoFilme.NumeroInteiro("duracao");
                filmeNovo.Classificacao = dadosDoFilme.Texto("classificacao");
                filmeNovo.Sinopse = dadosDoFilme.TextoOpcional("sinopse");
                filmeNovo.Ativo = dadosDoFilme.PossuiCampo("ativo") ? dadosDoFilme.ValorBooleano("ativo") : true;

                EnviarJson(contextoHttp, 201, filmeRepositorio.CadastrarFilme(filmeNovo));
                return;
            }

            // PUT /api/filmes/{id}/status
            if (metodoHttp == "PUT" && segundaParte != "" && terceiraParte == "status")
            {
                ExigirAdministrador(contextoHttp);

                int idFilme = LerNumeroDaRota(segundaParte, "id do filme");

                DadosRecebidos dadosDoFilme = LerCorpoDaRequisicao(contextoHttp);
                bool filmeAtivo = dadosDoFilme.ValorBooleano("ativo");

                EnviarJson(contextoHttp, 200, filmeRepositorio.AtivarOuDesativarFilme(idFilme, filmeAtivo));
                return;
            }

            // PUT /api/filmes/{id}
            if (metodoHttp == "PUT" && segundaParte != "" && terceiraParte == "")
            {
                ExigirAdministrador(contextoHttp);

                int idFilme = LerNumeroDaRota(segundaParte, "id do filme");

                DadosRecebidos dadosDoFilme = LerCorpoDaRequisicao(contextoHttp);

                Filme filmeAlterado = new Filme();
                filmeAlterado.IdFilme = idFilme;
                filmeAlterado.Titulo = dadosDoFilme.Texto("titulo");
                filmeAlterado.Genero = dadosDoFilme.Texto("genero");
                filmeAlterado.Duracao = dadosDoFilme.NumeroInteiro("duracao");
                filmeAlterado.Classificacao = dadosDoFilme.Texto("classificacao");
                filmeAlterado.Sinopse = dadosDoFilme.TextoOpcional("sinopse");

                EnviarJson(contextoHttp, 200, filmeRepositorio.AtualizarFilme(filmeAlterado));
                return;
            }

            // DELETE /api/filmes/{id}
            if (metodoHttp == "DELETE" && segundaParte != "")
            {
                ExigirAdministrador(contextoHttp);

                int idFilme = LerNumeroDaRota(segundaParte, "id do filme");
                filmeRepositorio.ExcluirFilme(idFilme);

                EnviarJson(contextoHttp, 200, new { mensagem = "Filme excluído com sucesso." });
                return;
            }

            EnviarJson(contextoHttp, 405, new { erro = "Operação não suportada em /api/filmes." });
        }

        // ------------------------------------------------------------
        // /api/salas
        // ------------------------------------------------------------
        private void ResponderRotaDeSalas(HttpListenerContext contextoHttp, string metodoHttp, string segundaParte)
        {
            if (metodoHttp == "GET" && segundaParte == "")
            {
                EnviarJson(contextoHttp, 200, salaRepositorio.ListarSalas());
                return;
            }

            if (metodoHttp == "GET" && segundaParte != "")
            {
                int idSala = LerNumeroDaRota(segundaParte, "id da sala");
                EnviarJson(contextoHttp, 200, salaRepositorio.BuscarSalaPorId(idSala));
                return;
            }

            if (metodoHttp == "POST" && segundaParte == "")
            {
                ExigirAdministrador(contextoHttp);

                DadosRecebidos dadosDaSala = LerCorpoDaRequisicao(contextoHttp);

                Sala salaNova = new Sala();
                salaNova.Nome = dadosDaSala.Texto("nome");
                salaNova.Capacidade = dadosDaSala.NumeroInteiro("capacidade");

                EnviarJson(contextoHttp, 201, salaRepositorio.CadastrarSala(salaNova));
                return;
            }

            if (metodoHttp == "PUT" && segundaParte != "")
            {
                ExigirAdministrador(contextoHttp);

                int idSala = LerNumeroDaRota(segundaParte, "id da sala");

                DadosRecebidos dadosDaSala = LerCorpoDaRequisicao(contextoHttp);

                Sala salaAlterada = new Sala();
                salaAlterada.IdSala = idSala;
                salaAlterada.Nome = dadosDaSala.Texto("nome");
                salaAlterada.Capacidade = dadosDaSala.NumeroInteiro("capacidade");

                EnviarJson(contextoHttp, 200, salaRepositorio.AtualizarSala(salaAlterada));
                return;
            }

            if (metodoHttp == "DELETE" && segundaParte != "")
            {
                ExigirAdministrador(contextoHttp);

                int idSala = LerNumeroDaRota(segundaParte, "id da sala");
                salaRepositorio.ExcluirSala(idSala);

                EnviarJson(contextoHttp, 200, new { mensagem = "Sala excluída com sucesso." });
                return;
            }

            EnviarJson(contextoHttp, 405, new { erro = "Operação não suportada em /api/salas." });
        }

        // ------------------------------------------------------------
        // /api/sessoes
        // ------------------------------------------------------------
        private void ResponderRotaDeSessoes(HttpListenerContext contextoHttp, string metodoHttp,
                                            string segundaParte, string terceiraParte)
        {
            // GET /api/sessoes/{id}/assentos
            if (metodoHttp == "GET" && segundaParte != "" && terceiraParte == "assentos")
            {
                int idSessao = LerNumeroDaRota(segundaParte, "id da sessão");

                Sessao sessaoEscolhida = sessaoRepositorio.BuscarSessaoPorId(idSessao);
                List<int> assentosOcupados = ingressoRepositorio.BuscarAssentosOcupados(idSessao);

                EnviarJson(contextoHttp, 200, new
                {
                    idSessao = sessaoEscolhida.IdSessao,
                    nomeSala = sessaoEscolhida.NomeSala,
                    capacidadeSala = sessaoEscolhida.CapacidadeSala,
                    assentosOcupados = assentosOcupados
                });
                return;
            }

            if (metodoHttp == "GET" && segundaParte == "")
            {
                bool somenteFilmesAtivos = contextoHttp.Request.QueryString["ativo"] == "1";
                EnviarJson(contextoHttp, 200, sessaoRepositorio.ListarSessoes(somenteFilmesAtivos));
                return;
            }

            if (metodoHttp == "GET" && segundaParte != "")
            {
                int idSessao = LerNumeroDaRota(segundaParte, "id da sessão");
                EnviarJson(contextoHttp, 200, sessaoRepositorio.BuscarSessaoPorId(idSessao));
                return;
            }

            if (metodoHttp == "POST" && segundaParte == "")
            {
                ExigirAdministrador(contextoHttp);

                DadosRecebidos dadosDaSessao = LerCorpoDaRequisicao(contextoHttp);

                Sessao sessaoNova = MontarSessaoDoCorpo(dadosDaSessao);

                EnviarJson(contextoHttp, 201, sessaoRepositorio.CadastrarSessao(sessaoNova));
                return;
            }

            if (metodoHttp == "PUT" && segundaParte != "")
            {
                ExigirAdministrador(contextoHttp);

                int idSessao = LerNumeroDaRota(segundaParte, "id da sessão");

                DadosRecebidos dadosDaSessao = LerCorpoDaRequisicao(contextoHttp);

                Sessao sessaoAlterada = MontarSessaoDoCorpo(dadosDaSessao);
                sessaoAlterada.IdSessao = idSessao;

                EnviarJson(contextoHttp, 200, sessaoRepositorio.AtualizarSessao(sessaoAlterada));
                return;
            }

            if (metodoHttp == "DELETE" && segundaParte != "")
            {
                ExigirAdministrador(contextoHttp);

                int idSessao = LerNumeroDaRota(segundaParte, "id da sessão");
                sessaoRepositorio.ExcluirSessao(idSessao);

                EnviarJson(contextoHttp, 200, new { mensagem = "Sessão excluída com sucesso." });
                return;
            }

            EnviarJson(contextoHttp, 405, new { erro = "Operação não suportada em /api/sessoes." });
        }

        private Sessao MontarSessaoDoCorpo(DadosRecebidos dadosDaSessao)
        {
            Sessao sessaoMontada = new Sessao();

            sessaoMontada.IdFilme = dadosDaSessao.NumeroInteiro("idFilme");
            sessaoMontada.IdSala = dadosDaSessao.NumeroInteiro("idSala");
            sessaoMontada.DataSessao = dadosDaSessao.Texto("dataSessao");
            sessaoMontada.HorarioSessao = dadosDaSessao.Texto("horarioSessao");
            sessaoMontada.Preco = dadosDaSessao.NumeroDecimal("preco");
            sessaoMontada.Tipo = dadosDaSessao.Texto("tipo");

            return sessaoMontada;
        }

        // ------------------------------------------------------------
        // /api/ingressos
        // ------------------------------------------------------------
        private void ResponderRotaDeIngressos(HttpListenerContext contextoHttp, string metodoHttp,
                                              string segundaParte, string terceiraParte)
        {
            // GET /api/ingressos -> listagem geral de vendas (painel administrativo)
            if (metodoHttp == "GET" && segundaParte == "")
            {
                ExigirAdministrador(contextoHttp);

                EnviarJson(contextoHttp, 200, ingressoRepositorio.ListarIngressos());
                return;
            }

            // GET /api/ingressos/meus -> "Meus ingressos": somente os
            // ingressos do usuario autenticado nesta sessao. O id vem
            // exclusivamente do cookie de sessao (via ExigirUsuarioAutenticado),
            // entao o usuario nunca consegue ver ingresso de outra pessoa
            // nem manipular a requisicao para trocar de dono.
            if (metodoHttp == "GET" && segundaParte == "meus")
            {
                Usuario usuarioLogado = ExigirUsuarioAutenticado(contextoHttp);

                EnviarJson(contextoHttp, 200, ingressoRepositorio.ListarIngressosDoUsuario(usuarioLogado.IdUsuario));
                return;
            }

            if (metodoHttp == "GET" && segundaParte != "")
            {
                ExigirAdministrador(contextoHttp);

                int idIngresso = LerNumeroDaRota(segundaParte, "id do ingresso");
                EnviarJson(contextoHttp, 200, ingressoRepositorio.BuscarIngressoPorId(idIngresso));
                return;
            }

            // POST /api/ingressos
            //
            // Aceita DOIS formatos no corpo, para nao quebrar quem ja
            // integra com a compra de UM ingresso so:
            //
            //   1) Compra individual (formato original): numeroAssento e
            //      tipoIngresso soltos no corpo.
            //   2) Compra EM LOTE (varios assentos de uma vez): em vez de
            //      numeroAssento/tipoIngresso, o corpo traz "itens", uma
            //      lista com um objeto {numeroAssento, tipoIngresso,
            //      tipoDocumentoMeia} para cada assento escolhido.
            if (metodoHttp == "POST" && segundaParte == "")
            {
                // A compra exige estar logado: e a sessao do backend, e
                // nao um campo do formulario, que decide de quem e o
                // ingresso (regra 13). O usuario nunca pode trocar esse
                // dono enviando outro id na requisicao.
                Usuario usuarioAutenticadoNaCompra = ExigirUsuarioAutenticado(contextoHttp);

                DadosRecebidos dadosDoIngresso = LerCorpoDaRequisicao(contextoHttp);

                if (dadosDoIngresso.PossuiCampo("itens"))
                {
                    int idSessaoDoLote = dadosDoIngresso.NumeroInteiro("idSessao");
                    string nomeClienteDoLote = dadosDoIngresso.Texto("nomeCliente");
                    string cpfClienteDoLote = dadosDoIngresso.Texto("cpfCliente");
                    string emailClienteDoLote = dadosDoIngresso.Texto("emailCliente");

                    List<ItemIngresso> itensDaCompra = new List<ItemIngresso>();

                    foreach (JsonElement elementoDoItem in dadosDoIngresso.Lista("itens"))
                    {
                        ItemIngresso itemDaCompra = new ItemIngresso();
                        itemDaCompra.NumeroAssento = DadosRecebidos.NumeroInteiroDoElemento(elementoDoItem, "numeroAssento");
                        itemDaCompra.TipoIngresso = DadosRecebidos.TextoDoElemento(elementoDoItem, "tipoIngresso");
                        itemDaCompra.TipoDocumentoMeia = DadosRecebidos.TextoOpcionalDoElemento(elementoDoItem, "tipoDocumentoMeia");

                        itensDaCompra.Add(itemDaCompra);
                    }

                    // OBS: assim como na compra individual, nenhum preco e
                    // lido do JSON: cada item e precificado pelo backend
                    // (Precos.ValorPara) dentro de ComprarIngressos.
                    EnviarJson(contextoHttp, 201, ingressoRepositorio.ComprarIngressos(
                        idSessaoDoLote, usuarioAutenticadoNaCompra.IdUsuario, nomeClienteDoLote,
                        cpfClienteDoLote, emailClienteDoLote, itensDaCompra));
                    return;
                }

                Ingresso ingressoNovo = new Ingresso();
                ingressoNovo.IdSessao = dadosDoIngresso.NumeroInteiro("idSessao");
                ingressoNovo.NomeCliente = dadosDoIngresso.Texto("nomeCliente");
                ingressoNovo.CpfCliente = dadosDoIngresso.Texto("cpfCliente");
                ingressoNovo.EmailCliente = dadosDoIngresso.Texto("emailCliente");
                ingressoNovo.NumeroAssento = dadosDoIngresso.NumeroInteiro("numeroAssento");
                ingressoNovo.TipoIngresso = dadosDoIngresso.Texto("tipoIngresso");
                ingressoNovo.TipoDocumentoMeia = dadosDoIngresso.TextoOpcional("tipoDocumentoMeia");

                // OBS: nenhum campo de preco e lido do JSON aqui de proposito.
                // O preco e sempre calculado pelo backend (Precos.ValorPara),
                // dentro de ComprarIngresso, a partir do TipoIngresso.

                EnviarJson(contextoHttp, 201, ingressoRepositorio.ComprarIngresso(ingressoNovo, usuarioAutenticadoNaCompra.IdUsuario));
                return;
            }

            // PUT /api/ingressos/{id}/cancelar (operação administrativa)
            if (metodoHttp == "PUT" && segundaParte != "" && terceiraParte == "cancelar")
            {
                ExigirAdministrador(contextoHttp);

                int idIngresso = LerNumeroDaRota(segundaParte, "id do ingresso");

                EnviarJson(contextoHttp, 200, ingressoRepositorio.CancelarIngresso(idIngresso));
                return;
            }

            EnviarJson(contextoHttp, 405, new { erro = "Operação não suportada em /api/ingressos." });
        }

        // ------------------------------------------------------------
        // /api/usuarios (cadastro)
        // ------------------------------------------------------------
        private void ResponderRotaDeUsuarios(HttpListenerContext contextoHttp, string metodoHttp, string segundaParte)
        {
            // POST /api/usuarios -> cadastra um novo usuario
            if (metodoHttp == "POST" && segundaParte == "")
            {
                DadosRecebidos dadosDoUsuario = LerCorpoDaRequisicao(contextoHttp);

                Usuario usuarioNovo = new Usuario();
                usuarioNovo.NomeCompleto = dadosDoUsuario.Texto("nomeCompleto");
                usuarioNovo.NomeUsuario = dadosDoUsuario.Texto("nomeUsuario");
                usuarioNovo.Email = dadosDoUsuario.Texto("email");
                usuarioNovo.Cpf = dadosDoUsuario.Texto("cpf");

                string senhaInformada = dadosDoUsuario.Texto("senha");

                EnviarJson(contextoHttp, 201, usuarioRepositorio.CadastrarUsuario(usuarioNovo, senhaInformada));
                return;
            }

            EnviarJson(contextoHttp, 405, new { erro = "Operação não suportada em /api/usuarios." });
        }

        // ------------------------------------------------------------
        // /api/login
        // ------------------------------------------------------------
        private void ResponderRotaDeLogin(HttpListenerContext contextoHttp, string metodoHttp)
        {
            // POST /api/login -> autentica um usuario ja cadastrado
            if (metodoHttp == "POST")
            {
                DadosRecebidos dadosDeLogin = LerCorpoDaRequisicao(contextoHttp);

                string loginInformado = dadosDeLogin.Texto("login");
                string senhaInformada = dadosDeLogin.Texto("senha");

                Usuario usuarioAutenticado = usuarioRepositorio.Autenticar(loginInformado, senhaInformada);

                // A sessao e criada e guardada aqui, dentro do proprio
                // servidor C#. O navegador so recebe um token opaco,
                // dentro de um cookie HttpOnly (o JavaScript nao consegue
                // le-lo nem forja-lo) - e por isso, a partir de agora, o
                // backend reconhece sozinho qual usuario esta logado.
                string tokenDaSessao = GerenciadorDeSessoes.AbrirSessao(usuarioAutenticado);
                DefinirCookieDeSessao(contextoHttp, tokenDaSessao);

                EnviarJson(contextoHttp, 200, usuarioAutenticado);
                return;
            }

            EnviarJson(contextoHttp, 405, new { erro = "Operação não suportada em /api/login." });
        }

        // ------------------------------------------------------------
        // /api/logout
        // ------------------------------------------------------------
        private void ResponderRotaDeLogout(HttpListenerContext contextoHttp, string metodoHttp)
        {
            // POST /api/logout -> encerra a sessao atual no servidor
            if (metodoHttp == "POST")
            {
                string tokenDaSessao = LerTokenDaSessao(contextoHttp);
                GerenciadorDeSessoes.EncerrarSessao(tokenDaSessao);
                RemoverCookieDeSessao(contextoHttp);

                EnviarJson(contextoHttp, 200, new { mensagem = "Sessão encerrada com sucesso." });
                return;
            }

            EnviarJson(contextoHttp, 405, new { erro = "Operação não suportada em /api/logout." });
        }

        // ------------------------------------------------------------
        // /api/sessao
        // ------------------------------------------------------------
        private void ResponderRotaDeSessaoAtual(HttpListenerContext contextoHttp, string metodoHttp)
        {
            // GET /api/sessao -> devolve o usuario que o SERVIDOR reconhece
            // como logado nesta sessao (a partir do cookie), permitindo que
            // o usuario continue identificado mesmo apos recarregar a pagina.
            if (metodoHttp == "GET")
            {
                string tokenDaSessao = LerTokenDaSessao(contextoHttp);
                Usuario usuarioDaSessao = GerenciadorDeSessoes.ObterUsuarioDaSessao(tokenDaSessao);

                if (usuarioDaSessao == null)
                {
                    EnviarJson(contextoHttp, 401, new { erro = "Não autenticado." });
                    return;
                }

                EnviarJson(contextoHttp, 200, usuarioDaSessao);
                return;
            }

            EnviarJson(contextoHttp, 405, new { erro = "Operação não suportada em /api/sessao." });
        }

        // ------------------------------------------------------------
        // Controle de acesso (autenticacao e permissao de administrador)
        // ------------------------------------------------------------
        /// <summary>
        /// Devolve o usuario logado a partir do cookie de sessao.
        /// Lanca 401 se nao houver sessao valida - usada nas rotas que
        /// exigem apenas estar autenticado.
        /// </summary>
        private Usuario ExigirUsuarioAutenticado(HttpListenerContext contextoHttp)
        {
            string tokenDaSessao = LerTokenDaSessao(contextoHttp);
            Usuario usuarioDaSessao = GerenciadorDeSessoes.ObterUsuarioDaSessao(tokenDaSessao);

            if (usuarioDaSessao == null)
            {
                throw new NaoAutenticado("Você precisa estar autenticado para realizar esta operação.");
            }

            return usuarioDaSessao;
        }

        /// <summary>
        /// Confere, DENTRO DO BACKEND, que o usuario logado e
        /// Administrador. Chamada no inicio de toda operacao
        /// administrativa (cadastro/edicao/exclusao de filmes, salas,
        /// sessoes e gerenciamento de ingressos), para que a permissao
        /// nao dependa apenas de esconder botoes no front-end.
        /// </summary>
        private void ExigirAdministrador(HttpListenerContext contextoHttp)
        {
            Usuario usuarioLogado = ExigirUsuarioAutenticado(contextoHttp);

            if (usuarioLogado.TipoUsuario != "Administrador")
            {
                throw new AcessoNegado("Apenas administradores podem realizar esta operação.");
            }
        }

        // ------------------------------------------------------------
        // Cookie de sessao (controlado pelo servidor, nunca pelo JS)
        // ------------------------------------------------------------
        private string LerTokenDaSessao(HttpListenerContext contextoHttp)
        {
            Cookie cookieDeSessao = contextoHttp.Request.Cookies[GerenciadorDeSessoes.NomeDoCookie];
            return cookieDeSessao?.Value;
        }

        private void DefinirCookieDeSessao(HttpListenerContext contextoHttp, string tokenDaSessao)
        {
            Cookie cookieDeSessao = new Cookie(GerenciadorDeSessoes.NomeDoCookie, tokenDaSessao)
            {
                Path = "/",
                HttpOnly = true
            };

            contextoHttp.Response.SetCookie(cookieDeSessao);
        }

        private void RemoverCookieDeSessao(HttpListenerContext contextoHttp)
        {
            Cookie cookieDeSessao = new Cookie(GerenciadorDeSessoes.NomeDoCookie, "")
            {
                Path = "/",
                HttpOnly = true,
                Expires = DateTime.UtcNow.AddDays(-1)
            };

            contextoHttp.Response.SetCookie(cookieDeSessao);
        }

        // ============================================================
        // ARQUIVOS DO FRONT-END
        // ============================================================
        private void ResponderArquivoDoFrontend(HttpListenerContext contextoHttp, string caminhoDaRota)
        {
            if (pastaDoFrontend == null)
            {
                EnviarTexto(contextoHttp, 500, "text/plain",
                    "A pasta Frontend nao foi encontrada. Confira se ela esta ao lado da pasta Backend.");
                return;
            }

            string nomeDoArquivo = caminhoDaRota.Trim('/');

            if (nomeDoArquivo.Length == 0)
            {
                nomeDoArquivo = "index.html";
            }

            string caminhoCompleto = Path.GetFullPath(Path.Combine(pastaDoFrontend, nomeDoArquivo));

            // Protecao contra pedidos do tipo /../../arquivo-secreto
            if (!caminhoCompleto.StartsWith(pastaDoFrontend))
            {
                EnviarTexto(contextoHttp, 403, "text/plain", "Acesso negado.");
                return;
            }

            if (!File.Exists(caminhoCompleto))
            {
                EnviarTexto(contextoHttp, 404, "text/plain", "Arquivo nao encontrado: " + nomeDoArquivo);
                return;
            }

            byte[] conteudoDoArquivo = File.ReadAllBytes(caminhoCompleto);

            contextoHttp.Response.StatusCode = 200;
            contextoHttp.Response.ContentType = DescobrirTipoDoArquivo(caminhoCompleto);
            contextoHttp.Response.ContentLength64 = conteudoDoArquivo.Length;
            contextoHttp.Response.OutputStream.Write(conteudoDoArquivo, 0, conteudoDoArquivo.Length);
            contextoHttp.Response.OutputStream.Close();
        }

        private string DescobrirTipoDoArquivo(string caminhoCompleto)
        {
            string extensaoDoArquivo = Path.GetExtension(caminhoCompleto).ToLower();

            if (extensaoDoArquivo == ".html") return "text/html; charset=utf-8";
            if (extensaoDoArquivo == ".css") return "text/css; charset=utf-8";
            if (extensaoDoArquivo == ".js") return "application/javascript; charset=utf-8";
            if (extensaoDoArquivo == ".json") return "application/json; charset=utf-8";
            if (extensaoDoArquivo == ".png") return "image/png";
            if (extensaoDoArquivo == ".jpg" || extensaoDoArquivo == ".jpeg") return "image/jpeg";
            if (extensaoDoArquivo == ".svg") return "image/svg+xml";
            if (extensaoDoArquivo == ".ico") return "image/x-icon";

            return "application/octet-stream";
        }

        /// <summary>
        /// Procura a pasta Frontend subindo a partir do executavel,
        /// porque o Visual Studio roda o programa dentro de bin\Debug.
        /// </summary>
        private string LocalizarPastaDoFrontend()
        {
            string pastaAtual = AppContext.BaseDirectory;

            for (int nivel = 0; nivel < 6; nivel++)
            {
                string caminhoTestado = Path.Combine(pastaAtual, "Frontend");

                if (Directory.Exists(caminhoTestado))
                {
                    return Path.GetFullPath(caminhoTestado);
                }

                DirectoryInfo pastaAcima = Directory.GetParent(pastaAtual);

                if (pastaAcima == null)
                {
                    return null;
                }

                pastaAtual = pastaAcima.FullName;
            }

            return null;
        }

        // ============================================================
        // ENTRADA E SAIDA
        // ============================================================
        private DadosRecebidos LerCorpoDaRequisicao(HttpListenerContext contextoHttp)
        {
            using (StreamReader leitorDoCorpo = new StreamReader(contextoHttp.Request.InputStream, Encoding.UTF8))
            {
                string textoJson = leitorDoCorpo.ReadToEnd();
                return new DadosRecebidos(textoJson);
            }
        }

        private int LerNumeroDaRota(string valorDaRota, string nomeDoCampo)
        {
            int numeroConvertido;

            if (int.TryParse(valorDaRota, out numeroConvertido))
            {
                return numeroConvertido;
            }

            throw new ErroDeValidacao("O " + nomeDoCampo + " informado não é um número válido.");
        }

        private void EnviarJson(HttpListenerContext contextoHttp, int codigoHttp, object objetoDeResposta)
        {
            string textoJson = JsonSerializer.Serialize(objetoDeResposta, opcoesDoJson);
            EnviarTexto(contextoHttp, codigoHttp, "application/json; charset=utf-8", textoJson);
        }

        private void EnviarTexto(HttpListenerContext contextoHttp, int codigoHttp, string tipoDoConteudo, string textoDaResposta)
        {
            byte[] bytesDaResposta = Encoding.UTF8.GetBytes(textoDaResposta);

            contextoHttp.Response.StatusCode = codigoHttp;
            contextoHttp.Response.ContentType = tipoDoConteudo;
            contextoHttp.Response.ContentLength64 = bytesDaResposta.Length;
            contextoHttp.Response.OutputStream.Write(bytesDaResposta, 0, bytesDaResposta.Length);
            contextoHttp.Response.OutputStream.Close();
        }
    }
}
