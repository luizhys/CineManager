using System;
using System.IO;
using Microsoft.Data.SqlClient;

namespace CineManager
{
    /// <summary>
    /// Responsavel por guardar a connection string e abrir conexoes
    /// com o Microsoft SQL Server usando ADO.NET.
    /// Nenhum ORM e utilizado: aqui so existe SqlConnection.
    /// </summary>
    public static class BancoDados
    {
        /// <summary>
        /// Endereco do banco. O valor real e lido do arquivo
        /// ConnectionString.txt; este e apenas o valor de reserva.
        /// </summary>
        public static string connectionString =
            @"Server=localhost\SQLEXPRESS;Database=CineManager;Trusted_Connection=True;TrustServerCertificate=True;";

        /// <summary>
        /// Le a primeira linha valida do arquivo ConnectionString.txt.
        /// </summary>
        public static void CarregarConfiguracao()
        {
            string caminhoDoArquivo = LocalizarArquivoDeConfiguracao();

            if (caminhoDoArquivo == null)
            {
                Console.WriteLine("Aviso: ConnectionString.txt nao encontrado. Usando a conexao padrao do codigo.");
                return;
            }

            string[] linhasDoArquivo = File.ReadAllLines(caminhoDoArquivo);

            foreach (string linhaAtual in linhasDoArquivo)
            {
                string linhaLimpa = linhaAtual.Trim();

                bool linhaVazia = linhaLimpa.Length == 0;
                bool linhaComentada = linhaLimpa.StartsWith("#") || linhaLimpa.StartsWith("//");

                if (!linhaVazia && !linhaComentada)
                {
                    connectionString = linhaLimpa;
                    Console.WriteLine("Configuracao lida de: " + caminhoDoArquivo);
                    return;
                }
            }

            Console.WriteLine("Aviso: ConnectionString.txt esta vazio. Usando a conexao padrao do codigo.");
        }

        /// <summary>
        /// Procura o ConnectionString.txt na pasta do executavel e nas
        /// pastas acima dela (o Visual Studio roda dentro de bin\Debug).
        /// </summary>
        private static string LocalizarArquivoDeConfiguracao()
        {
            string pastaAtual = AppContext.BaseDirectory;

            for (int nivel = 0; nivel < 6; nivel++)
            {
                string caminhoTestado = Path.Combine(pastaAtual, "ConnectionString.txt");

                if (File.Exists(caminhoTestado))
                {
                    return caminhoTestado;
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

        /// <summary>
        /// Abre e devolve uma conexao ja pronta para uso.
        /// Quem chama deve usar "using" para fechar a conexao.
        /// </summary>
        public static SqlConnection AbrirConexao()
        {
            SqlConnection conexaoBanco = new SqlConnection(connectionString);
            conexaoBanco.Open();
            return conexaoBanco;
        }

        /// <summary>
        /// Testa a conexao ao iniciar o programa, para avisar cedo
        /// quando o SQL Server estiver desligado ou mal configurado.
        /// </summary>
        public static bool TestarConexao(out string mensagemDeErro)
        {
            try
            {
                using (SqlConnection conexaoBanco = AbrirConexao())
                {
                    SqlCommand comandoSql = new SqlCommand("SELECT DB_NAME();", conexaoBanco);
                    string nomeDoBanco = Convert.ToString(comandoSql.ExecuteScalar());

                    Console.WriteLine("Conexao com o SQL Server OK. Banco em uso: " + nomeDoBanco);
                    mensagemDeErro = null;
                    return true;
                }
            }
            catch (Exception erro)
            {
                mensagemDeErro = erro.Message;
                return false;
            }
        }

        /// <summary>
        /// Mostra a conexao no console escondendo a senha, se houver.
        /// </summary>
        public static string ConnectionStringSemSenha()
        {
            string[] partesDaConexao = connectionString.Split(';');
            string textoFinal = "";

            foreach (string parteAtual in partesDaConexao)
            {
                if (parteAtual.Trim().Length == 0)
                {
                    continue;
                }

                if (parteAtual.ToLower().Contains("password"))
                {
                    textoFinal = textoFinal + "Password=*****;";
                }
                else
                {
                    textoFinal = textoFinal + parteAtual.Trim() + ";";
                }
            }

            return textoFinal;
        }
    }
}
