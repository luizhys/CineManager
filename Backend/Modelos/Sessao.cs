namespace CineManager
{
    /// <summary>
    /// Representa uma linha da tabela SESSOES.
    ///
    /// As colunas guardadas no banco sao apenas IdFilme e IdSala.
    /// Os campos TituloFilme, NomeSala, CapacidadeSala e FilmeAtivo
    /// NAO existem na tabela: eles vem do INNER JOIN com Filmes e
    /// Salas e servem apenas para a tela nao precisar de outra
    /// consulta. Assim evitamos repetir dados no banco.
    /// </summary>
    public class Sessao
    {
        public int IdSessao { get; set; }
        public int IdFilme { get; set; }
        public int IdSala { get; set; }
        public string DataSessao { get; set; }
        public string HorarioSessao { get; set; }
        public decimal Preco { get; set; }
        public string Tipo { get; set; }

        // Campos vindos do JOIN (nao sao colunas da tabela Sessoes)
        public string TituloFilme { get; set; }
        public bool FilmeAtivo { get; set; }
        public string NomeSala { get; set; }
        public int CapacidadeSala { get; set; }
    }
}
