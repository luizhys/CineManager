namespace CineManager
{
    /// <summary>
    /// Precos oficiais do ingresso. Ficam centralizados aqui para que
    /// TODO o sistema (compra, relatorios, etc.) use sempre o mesmo
    /// valor, definido pelo BACKEND.
    ///
    /// O front-end NUNCA envia o preco: ele so envia o TIPO de ingresso
    /// escolhido ("Inteira" ou "Meia"), e e o C# quem decide, aqui,
    /// quanto isso custa antes de gravar no banco. Assim o usuario nao
    /// consegue manipular o valor pago alterando o JavaScript no
    /// navegador.
    /// </summary>
    public static class Precos
    {
        public const decimal Inteira = 50.00m;
        public const decimal Meia = 25.00m;

        /// <summary>
        /// Traduz o tipo de ingresso escolhido no preco oficial.
        /// E a UNICA funcao do sistema que decide quanto um ingresso
        /// custa - nenhum outro lugar deve calcular ou aceitar preco
        /// vindo de fora.
        /// </summary>
        public static decimal ValorPara(string tipoIngresso)
        {
            if (tipoIngresso == "Inteira")
            {
                return Inteira;
            }

            if (tipoIngresso == "Meia")
            {
                return Meia;
            }

            throw new ErroDeValidacao("O tipo de ingresso deve ser Inteira ou Meia.");
        }
    }
}
