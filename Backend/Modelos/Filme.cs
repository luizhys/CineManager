namespace CineManager
{
    /// <summary>
    /// Representa uma linha da tabela FILMES.
    /// Cada propriedade corresponde a uma coluna do SQL Server.
    /// </summary>
    public class Filme
    {
        public int IdFilme { get; set; }
        public string Titulo { get; set; }
        public string Genero { get; set; }
        public int Duracao { get; set; }
        public string Classificacao { get; set; }
        public string Sinopse { get; set; }
        public bool Ativo { get; set; }
    }
}
