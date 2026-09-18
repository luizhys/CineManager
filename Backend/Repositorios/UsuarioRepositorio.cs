using System;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace CineManager
{
    /// <summary>
    /// Cadastro e autenticacao de usuarios.
    ///
    /// A senha NUNCA e gravada em texto puro. Para cada usuario e
    /// gerado um "salt" aleatorio, e o hash gravado no banco e
    /// SHA-256(salt + senha). No login, refazemos a mesma conta com
    /// o salt gravado e comparamos os hashes.
    ///
    /// Isso usa apenas System.Security.Cryptography, que faz parte
    /// do proprio .NET (nao e um pacote externo nem um framework).
    ///
    /// E-mail, CPF e nome de usuario sao UNIQUE no banco (constraints
    /// UQ_Usuarios_Email, UQ_Usuarios_Cpf, UQ_Usuarios_NomeUsuario).
    /// Aqui tratamos a violacao dessas constraints e devolvemos uma
    /// mensagem amigavel, nunca o erro tecnico do SQL Server.
    /// </summary>
    public class UsuarioRepositorio
    {
        // ------------------------------------------------------------
        // CREATE - cadastro
        // ------------------------------------------------------------
        public Usuario CadastrarUsuario(Usuario usuarioNovo, string senhaInformada)
        {
            ValidarUsuario(usuarioNovo, senhaInformada);

            string saltGerado = GerarSalt();
            string hashGerado = CalcularHash(senhaInformada, saltGerado);

            // Todo cadastro feito pela tela publica nasce como usuario
            // "Comum" (a coluna tem DEFAULT 'Comum' no banco, mas aqui
            // deixamos explicito no objeto devolvido ao front-end) e
            // Ativo (a coluna tem DEFAULT 1 no banco; mesma logica).
            usuarioNovo.TipoUsuario = "Comum";
            usuarioNovo.Ativo = true;

            string comandoTexto =
                "INSERT INTO Usuarios (NomeCompleto, NomeUsuario, Email, Cpf, SenhaHash, SenhaSalt) " +
                "VALUES (@nomeCompleto, @nomeUsuario, @email, @cpf, @senhaHash, @senhaSalt); " +
                "SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using (SqlConnection conexaoBanco = BancoDados.AbrirConexao())
            using (SqlCommand comandoSql = new SqlCommand(comandoTexto, conexaoBanco))
            {
                comandoSql.Parameters.Add("@nomeCompleto", SqlDbType.VarChar, 150).Value = usuarioNovo.NomeCompleto;
                comandoSql.Parameters.Add("@nomeUsuario", SqlDbType.VarChar, 50).Value = usuarioNovo.NomeUsuario;
                comandoSql.Parameters.Add("@email", SqlDbType.VarChar, 150).Value = usuarioNovo.Email;
                comandoSql.Parameters.Add("@cpf", SqlDbType.VarChar, 11).Value = usuarioNovo.Cpf;
                comandoSql.Parameters.Add("@senhaHash", SqlDbType.VarChar, 200).Value = hashGerado;
                comandoSql.Parameters.Add("@senhaSalt", SqlDbType.VarChar, 200).Value = saltGerado;

                try
                {
                    usuarioNovo.IdUsuario = Convert.ToInt32(comandoSql.ExecuteScalar());
                }
                catch (SqlException erroSql)
                {
                    throw TraduzirErroDoBanco(erroSql);
                }
            }

            return usuarioNovo;
        }

        // ------------------------------------------------------------
        // READ - autenticacao (login)
        // ------------------------------------------------------------
        /// <summary>
        /// Aceita nome de usuario OU e-mail no campo "login".
        /// Lanca ErroDeValidacao com mensagem generica se o usuario
        /// nao existir ou a senha estiver errada (nunca dizemos qual
        /// dos dois, para nao dar pista a quem esta tentando adivinhar).
        /// </summary>
        public Usuario Autenticar(string loginInformado, string senhaInformada)
        {
            string loginLimpo = (loginInformado ?? "").Trim();

            if (loginLimpo.Length == 0 || string.IsNullOrEmpty(senhaInformada))
            {
                throw new ErroDeValidacao("Informe usuário/e-mail e senha.");
            }

            string comandoTexto =
                "SELECT IdUsuario, NomeCompleto, NomeUsuario, Email, Cpf, SenhaHash, SenhaSalt, TipoUsuario, Ativo " +
                "FROM Usuarios " +
                "WHERE NomeUsuario = @login OR Email = @login;";

            Usuario usuarioEncontrado = null;

            using (SqlConnection conexaoBanco = BancoDados.AbrirConexao())
            using (SqlCommand comandoSql = new SqlCommand(comandoTexto, conexaoBanco))
            {
                comandoSql.Parameters.Add("@login", SqlDbType.VarChar, 150).Value = loginLimpo;

                using (SqlDataReader leitorDados = comandoSql.ExecuteReader())
                {
                    if (leitorDados.Read())
                    {
                        usuarioEncontrado = MontarUsuario(leitorDados);
                    }
                }
            }

            if (usuarioEncontrado == null)
            {
                throw new ErroDeValidacao("Usuário ou senha inválidos.");
            }

            string hashCalculado = CalcularHash(senhaInformada, usuarioEncontrado.SenhaSalt);

            if (hashCalculado != usuarioEncontrado.SenhaHash)
            {
                throw new ErroDeValidacao("Usuário ou senha inválidos.");
            }

            // So depois de confirmar a senha e que avisamos que a conta
            // esta desativada - assim nao damos pista, a quem nao sabe
            // a senha, de que aquele usuario existe e esta inativo.
            if (!usuarioEncontrado.Ativo)
            {
                throw new ErroDeValidacao("Este usuário está desativado. Procure um administrador.");
            }

            return usuarioEncontrado;
        }

        // ------------------------------------------------------------
        // Apoio - hashing de senha
        // ------------------------------------------------------------
        private string GerarSalt()
        {
            byte[] bytesAleatorios = new byte[16];

            using (RandomNumberGenerator geradorAleatorio = RandomNumberGenerator.Create())
            {
                geradorAleatorio.GetBytes(bytesAleatorios);
            }

            return Convert.ToBase64String(bytesAleatorios);
        }

        private string CalcularHash(string senha, string salt)
        {
            using (SHA256 algoritmoSha256 = SHA256.Create())
            {
                byte[] bytesParaHash = Encoding.UTF8.GetBytes(salt + senha);
                byte[] bytesDoHash = algoritmoSha256.ComputeHash(bytesParaHash);

                return Convert.ToBase64String(bytesDoHash);
            }
        }

        // ------------------------------------------------------------
        // Apoio - validacao e leitura
        // ------------------------------------------------------------
        private void ValidarUsuario(Usuario usuarioVerificado, string senhaInformada)
        {
            usuarioVerificado.NomeCompleto = Validacoes.TextoObrigatorio(usuarioVerificado.NomeCompleto, "nome completo", 150);
            usuarioVerificado.NomeUsuario = Validacoes.NomeDeUsuarioValido(usuarioVerificado.NomeUsuario);
            usuarioVerificado.Email = Validacoes.EmailValido(usuarioVerificado.Email);
            usuarioVerificado.Cpf = Validacoes.CpfValido(usuarioVerificado.Cpf);

            Validacoes.SenhaValida(senhaInformada);
        }

        private Usuario MontarUsuario(SqlDataReader leitorDados)
        {
            Usuario usuarioLido = new Usuario();

            usuarioLido.IdUsuario = leitorDados.GetInt32(leitorDados.GetOrdinal("IdUsuario"));
            usuarioLido.NomeCompleto = leitorDados.GetString(leitorDados.GetOrdinal("NomeCompleto"));
            usuarioLido.NomeUsuario = leitorDados.GetString(leitorDados.GetOrdinal("NomeUsuario"));
            usuarioLido.Email = leitorDados.GetString(leitorDados.GetOrdinal("Email"));
            usuarioLido.Cpf = leitorDados.GetString(leitorDados.GetOrdinal("Cpf"));
            usuarioLido.SenhaHash = leitorDados.GetString(leitorDados.GetOrdinal("SenhaHash"));
            usuarioLido.SenhaSalt = leitorDados.GetString(leitorDados.GetOrdinal("SenhaSalt"));
            usuarioLido.TipoUsuario = leitorDados.GetString(leitorDados.GetOrdinal("TipoUsuario"));
            usuarioLido.Ativo = leitorDados.GetBoolean(leitorDados.GetOrdinal("Ativo"));

            return usuarioLido;
        }

        /// <summary>
        /// Transforma a violacao de UNIQUE (2601/2627) em uma mensagem
        /// amigavel, identificando QUAL campo esta duplicado a partir
        /// do nome da constraint que aparece na mensagem do SQL Server.
        /// O usuario nunca ve o texto tecnico original.
        /// </summary>
        private Exception TraduzirErroDoBanco(SqlException erroSql)
        {
            if (erroSql.Number == 2601 || erroSql.Number == 2627)
            {
                string mensagemOriginal = erroSql.Message;

                if (mensagemOriginal.IndexOf("UQ_Usuarios_Email", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return new ConflitoDeDados("Este e-mail já está cadastrado.");
                }

                if (mensagemOriginal.IndexOf("UQ_Usuarios_Cpf", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return new ConflitoDeDados("Este CPF já está cadastrado.");
                }

                if (mensagemOriginal.IndexOf("UQ_Usuarios_NomeUsuario", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return new ConflitoDeDados("Este nome de usuário já está em uso.");
                }

                return new ConflitoDeDados("Já existe um usuário cadastrado com um desses dados.");
            }

            if (erroSql.Number == 547)
            {
                return new ConflitoDeDados("Os dados informados não respeitam as regras do banco de dados.");
            }

            return erroSql;
        }
    }
}
