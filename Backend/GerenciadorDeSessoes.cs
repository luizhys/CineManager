using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace CineManager
{
    /// <summary>
    /// Controla as sessoes de login do sistema.
    ///
    /// NAO usa ASP.NET, nem nenhum framework: e apenas um dicionario em
    /// memoria (thread-safe) que associa um token de sessao ao usuario
    /// que fez login. O token e gerado com System.Security.Cryptography,
    /// que ja faz parte do .NET.
    ///
    /// O token e enviado ao navegador dentro de um cookie HttpOnly
    /// (o JavaScript nao consegue ler nem forjar esse cookie). A cada
    /// requisicao, o ServidorHttp le o cookie, pergunta a este
    /// gerenciador quem e o dono daquele token e so assim o backend
    /// sabe qual usuario esta logado - a identificacao nao depende do
    /// que o JavaScript diz, e sim do que o proprio servidor guardou.
    /// </summary>
    public static class GerenciadorDeSessoes
    {
        public const string NomeDoCookie = "cinemanager_sessao";

        private static readonly ConcurrentDictionary<string, Usuario> sessoesAtivas =
            new ConcurrentDictionary<string, Usuario>();

        /// <summary>
        /// Cria uma sessao nova para o usuario autenticado e devolve o
        /// token que deve ser gravado no cookie.
        /// </summary>
        public static string AbrirSessao(Usuario usuarioAutenticado)
        {
            string tokenDaSessao = GerarToken();
            sessoesAtivas[tokenDaSessao] = usuarioAutenticado;

            return tokenDaSessao;
        }

        /// <summary>
        /// Devolve o usuario dono do token, ou null se o token nao
        /// existir (nunca logou, ou a sessao ja foi encerrada).
        /// </summary>
        public static Usuario ObterUsuarioDaSessao(string tokenDaSessao)
        {
            if (string.IsNullOrEmpty(tokenDaSessao))
            {
                return null;
            }

            Usuario usuarioDaSessao;
            sessoesAtivas.TryGetValue(tokenDaSessao, out usuarioDaSessao);

            return usuarioDaSessao;
        }

        /// <summary>
        /// Encerra a sessao (logout).
        /// </summary>
        public static void EncerrarSessao(string tokenDaSessao)
        {
            if (string.IsNullOrEmpty(tokenDaSessao))
            {
                return;
            }

            Usuario usuarioRemovido;
            sessoesAtivas.TryRemove(tokenDaSessao, out usuarioRemovido);
        }

        private static string GerarToken()
        {
            byte[] bytesAleatorios = new byte[32];

            using (RandomNumberGenerator geradorAleatorio = RandomNumberGenerator.Create())
            {
                geradorAleatorio.GetBytes(bytesAleatorios);
            }

            return Convert.ToBase64String(bytesAleatorios)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }
    }
}
