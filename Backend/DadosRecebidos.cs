using System;
using System.Collections.Generic;
using System.Text.Json;

namespace CineManager
{
    /// <summary>
    /// Le o JSON que o JavaScript envia no corpo da requisicao.
    ///
    /// Usamos System.Text.Json, que ja vem dentro do proprio .NET
    /// (nao e um pacote externo nem um framework). Ele apenas converte
    /// texto JSON em valores C# e vice-versa; nao tem nenhuma relacao
    /// com o acesso ao banco, que continua 100% em ADO.NET.
    /// </summary>
    public class DadosRecebidos
    {
        private readonly JsonDocument documentoJson;
        private readonly JsonElement raizDoJson;

        public DadosRecebidos(string textoJson)
        {
            if (string.IsNullOrWhiteSpace(textoJson))
            {
                throw new ErroDeValidacao("Nenhum dado foi enviado na requisição.");
            }

            try
            {
                documentoJson = JsonDocument.Parse(textoJson);
                raizDoJson = documentoJson.RootElement;
            }
            catch (JsonException)
            {
                throw new ErroDeValidacao("Os dados enviados não estão em formato JSON válido.");
            }
        }

        private JsonElement BuscarCampo(string nomeDoCampo)
        {
            JsonElement campoEncontrado;

            if (raizDoJson.TryGetProperty(nomeDoCampo, out campoEncontrado))
            {
                return campoEncontrado;
            }

            throw new ErroDeValidacao("O campo " + nomeDoCampo + " não foi enviado.");
        }

        public bool PossuiCampo(string nomeDoCampo)
        {
            JsonElement campoEncontrado;
            return raizDoJson.TryGetProperty(nomeDoCampo, out campoEncontrado);
        }

        public string Texto(string nomeDoCampo)
        {
            JsonElement campoEncontrado = BuscarCampo(nomeDoCampo);

            if (campoEncontrado.ValueKind == JsonValueKind.Null)
            {
                return "";
            }

            if (campoEncontrado.ValueKind == JsonValueKind.String)
            {
                return campoEncontrado.GetString();
            }

            return campoEncontrado.ToString();
        }

        public string TextoOpcional(string nomeDoCampo)
        {
            if (!PossuiCampo(nomeDoCampo))
            {
                return "";
            }

            return Texto(nomeDoCampo);
        }

        public int NumeroInteiro(string nomeDoCampo)
        {
            JsonElement campoEncontrado = BuscarCampo(nomeDoCampo);

            if (campoEncontrado.ValueKind == JsonValueKind.Number)
            {
                return campoEncontrado.GetInt32();
            }

            int numeroConvertido;

            if (int.TryParse(campoEncontrado.ToString(), out numeroConvertido))
            {
                return numeroConvertido;
            }

            throw new ErroDeValidacao("O campo " + nomeDoCampo + " deve ser um número inteiro.");
        }

        public decimal NumeroDecimal(string nomeDoCampo)
        {
            JsonElement campoEncontrado = BuscarCampo(nomeDoCampo);

            if (campoEncontrado.ValueKind == JsonValueKind.Number)
            {
                return campoEncontrado.GetDecimal();
            }

            decimal numeroConvertido;

            bool conversaoDeuCerto = decimal.TryParse(
                campoEncontrado.ToString().Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out numeroConvertido);

            if (conversaoDeuCerto)
            {
                return numeroConvertido;
            }

            throw new ErroDeValidacao("O campo " + nomeDoCampo + " deve ser um número.");
        }

        /// <summary>
        /// Le um campo que deve ser uma LISTA de objetos JSON (usado na
        /// compra de varios ingressos de uma vez, onde cada elemento da
        /// lista e um assento). Devolve os elementos "crus" do JSON; os
        /// metodos estaticos TextoDoElemento/NumeroInteiroDoElemento logo
        /// abaixo leem os campos de cada um deles.
        /// </summary>
        public List<JsonElement> Lista(string nomeDoCampo)
        {
            JsonElement campoEncontrado = BuscarCampo(nomeDoCampo);

            if (campoEncontrado.ValueKind != JsonValueKind.Array)
            {
                throw new ErroDeValidacao("O campo " + nomeDoCampo + " deve ser uma lista.");
            }

            List<JsonElement> listaDeElementos = new List<JsonElement>();

            foreach (JsonElement elemento in campoEncontrado.EnumerateArray())
            {
                listaDeElementos.Add(elemento);
            }

            if (listaDeElementos.Count == 0)
            {
                throw new ErroDeValidacao("O campo " + nomeDoCampo + " precisa ter pelo menos um item.");
            }

            return listaDeElementos;
        }

        // ------------------------------------------------------------
        // Leitura de campos dentro de um elemento de uma lista (cada
        // "item" da compra em lote). Mesma logica dos metodos de
        // instancia acima, mas recebendo o JsonElement diretamente.
        // ------------------------------------------------------------
        public static string TextoDoElemento(JsonElement elemento, string nomeDoCampo)
        {
            JsonElement campoEncontrado;

            if (!elemento.TryGetProperty(nomeDoCampo, out campoEncontrado))
            {
                throw new ErroDeValidacao("O campo " + nomeDoCampo + " não foi enviado em um dos itens.");
            }

            if (campoEncontrado.ValueKind == JsonValueKind.Null)
            {
                return "";
            }

            return campoEncontrado.ValueKind == JsonValueKind.String
                ? campoEncontrado.GetString()
                : campoEncontrado.ToString();
        }

        public static string TextoOpcionalDoElemento(JsonElement elemento, string nomeDoCampo)
        {
            JsonElement campoEncontrado;

            if (!elemento.TryGetProperty(nomeDoCampo, out campoEncontrado) ||
                campoEncontrado.ValueKind == JsonValueKind.Null)
            {
                return "";
            }

            return campoEncontrado.ValueKind == JsonValueKind.String
                ? campoEncontrado.GetString()
                : campoEncontrado.ToString();
        }

        public static int NumeroInteiroDoElemento(JsonElement elemento, string nomeDoCampo)
        {
            JsonElement campoEncontrado;

            if (!elemento.TryGetProperty(nomeDoCampo, out campoEncontrado))
            {
                throw new ErroDeValidacao("O campo " + nomeDoCampo + " não foi enviado em um dos itens.");
            }

            if (campoEncontrado.ValueKind == JsonValueKind.Number)
            {
                return campoEncontrado.GetInt32();
            }

            int numeroConvertido;

            if (int.TryParse(campoEncontrado.ToString(), out numeroConvertido))
            {
                return numeroConvertido;
            }

            throw new ErroDeValidacao("O campo " + nomeDoCampo + " deve ser um número inteiro.");
        }

        public bool ValorBooleano(string nomeDoCampo)
        {
            JsonElement campoEncontrado = BuscarCampo(nomeDoCampo);

            if (campoEncontrado.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (campoEncontrado.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            string textoDoCampo = campoEncontrado.ToString().ToLower();

            if (textoDoCampo == "true" || textoDoCampo == "1")
            {
                return true;
            }

            if (textoDoCampo == "false" || textoDoCampo == "0")
            {
                return false;
            }

            throw new ErroDeValidacao("O campo " + nomeDoCampo + " deve ser verdadeiro ou falso.");
        }
    }
}
