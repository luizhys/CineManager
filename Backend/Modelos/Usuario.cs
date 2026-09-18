using System.Text.Json.Serialization;

namespace CineManager
{
    /// <summary>
    /// Representa uma linha da tabela USUARIOS.
    /// SenhaHash e SenhaSalt nunca sao devolvidos ao front-end:
    /// [JsonIgnore] impede que esses campos entrem no JSON de resposta,
    /// mesmo que algum repositorio esqueca de removê-los manualmente.
    /// </summary>
    public class Usuario
    {
        public int IdUsuario { get; set; }
        public string NomeCompleto { get; set; }
        public string NomeUsuario { get; set; }
        public string Email { get; set; }
        public string Cpf { get; set; }

        // "Comum" ou "Administrador". Vai para o cliente (o front-end
        // usa isso so para mostrar/esconder o menu de administracao;
        // quem realmente barra a operacao administrativa e o C#).
        public string TipoUsuario { get; set; }

        // Usuario desativado nao consegue mais logar (ver
        // UsuarioRepositorio.Autenticar). Vai para o cliente so como
        // informacao; quem realmente barra o login e o C#.
        public bool Ativo { get; set; }

        // Preenchidos apenas internamente (cadastro/login); nunca vao para o cliente.
        [JsonIgnore]
        public string SenhaHash { get; set; }
        [JsonIgnore]
        public string SenhaSalt { get; set; }
    }
}
