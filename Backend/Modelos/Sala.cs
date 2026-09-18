namespace CineManager
{
    /// <summary>
    /// Representa uma linha da tabela SALAS.
    /// A capacidade determina quantos assentos a sessao tera.
    /// </summary>
    public class Sala
    {
        public int IdSala { get; set; }
        public string Nome { get; set; }
        public int Capacidade { get; set; }
    }
}
