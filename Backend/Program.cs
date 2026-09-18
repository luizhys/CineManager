using System;
using System.Diagnostics;

namespace CineManager
{
    /// <summary>
    /// Ponto de entrada do CineManager.
    ///
    /// Fluxo do sistema:
    ///   HTML -> JavaScript (fetch) -> C# (HttpListener)
    ///        -> ADO.NET (SqlConnection/SqlCommand) -> SQL Server
    /// </summary>
    public class Program
    {
        private const string ENDERECO_DO_SERVIDOR = "http://localhost:8080/";

        public static void Main(string[] argumentosDaLinhaDeComando)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Console.WriteLine("==================================================");
            Console.WriteLine("  CineManager - C# + ADO.NET + Microsoft SQL Server");
            Console.WriteLine("==================================================");
            Console.WriteLine();

            // 1) Carrega a connection string do arquivo ConnectionString.txt
            BancoDados.CarregarConfiguracao();
            Console.WriteLine("Conexao configurada: " + BancoDados.ConnectionStringSemSenha());

            // 2) Testa a conexao antes de subir o servidor
            string mensagemDeErro;

            if (!BancoDados.TestarConexao(out mensagemDeErro))
            {
                Console.WriteLine();
                Console.WriteLine("### NAO FOI POSSIVEL CONECTAR AO SQL SERVER ###");
                Console.WriteLine(mensagemDeErro);
                Console.WriteLine();
                Console.WriteLine("Confira:");
                Console.WriteLine(" 1. O servico do SQL Server esta iniciado?");
                Console.WriteLine(" 2. O script BancoDados.sql ja foi executado no SSMS?");
                Console.WriteLine(" 3. O nome da instancia em ConnectionString.txt esta correto?");
                Console.WriteLine();
                Console.WriteLine("O servidor vai subir mesmo assim, mas as telas ficarao sem dados.");
                Console.WriteLine();
            }

            // 3) Sobe o servidor HTTP (System.Net.HttpListener, sem framework)
            ServidorHttp servidorHttp = new ServidorHttp(ENDERECO_DO_SERVIDOR);

            AbrirNavegador(ENDERECO_DO_SERVIDOR);

            servidorHttp.Iniciar();
        }

        /// <summary>
        /// Abre o navegador na pagina do sistema. Se nao der certo,
        /// o endereco continua aparecendo no console.
        /// </summary>
        private static void AbrirNavegador(string enderecoDoServidor)
        {
            try
            {
                ProcessStartInfo configuracaoDoProcesso = new ProcessStartInfo(enderecoDoServidor);
                configuracaoDoProcesso.UseShellExecute = true;

                Process.Start(configuracaoDoProcesso);
            }
            catch (Exception)
            {
                Console.WriteLine("Abra o navegador manualmente em: " + enderecoDoServidor);
            }
        }
    }
}
