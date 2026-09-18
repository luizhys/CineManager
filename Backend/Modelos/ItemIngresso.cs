namespace CineManager
{
    /// <summary>
    /// Um item dentro de uma COMPRA EM LOTE (varios ingressos de uma vez).
    ///
    /// Representa um unico assento escolhido pelo cliente, com o tipo de
    /// ingresso (Inteira/Meia) e o tipo de documento comprobatorio dele
    /// (obrigatorio so na meia-entrada). Os dados do cliente (nome, CPF,
    /// e-mail) e a sessao sao os mesmos para todos os itens da compra,
    /// entao ficam fora desta classe - veja IngressoRepositorio.ComprarIngressos.
    /// </summary>
    public class ItemIngresso
    {
        public int NumeroAssento { get; set; }
        public string TipoIngresso { get; set; }
        public string TipoDocumentoMeia { get; set; }
    }
}
