using System;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics.Models;
using System.Collections.Generic;
using Microsoft.Rest;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text;
using Google.Cloud.Language.V1;
using System.Net;
using System.IO;
using System.Runtime.Serialization.Json;
using Newtonsoft.Json.Linq;

namespace azureFunctionPDF3
{
    public class AnalizadorTexto
    {
        public static string TextoTraducido = string.Empty;
        public static string Clasificador(string textoGoogleClasificar)
        {
            string textoTraducido = TraducirGoogle(textoGoogleClasificar);

            var request = (HttpWebRequest)WebRequest.Create("https://language.googleapis.com/v1beta2/documents:classifyText?key=AIzaSyA0yCjU4vVubiB0kKzkokmdvgMEpYPKd7k");

            var DataAEnviar = "{\"document\":{\"type\":\"PLAIN_TEXT\",\"content\":\"" + textoTraducido.ToString() + "\"}}";
            var data = Encoding.ASCII.GetBytes(DataAEnviar);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.ContentLength = data.Length;
            using (var stream = request.GetRequestStream())
            {
                stream.Write(data, 0, data.Length);
            }
            string entidadEncontrada = "";
            var response = (HttpWebResponse)request.GetResponse();
            StreamReader streamReaderClasificacion = new StreamReader(response.GetResponseStream());
            string responseClasificacion = streamReaderClasificacion.ReadToEnd();
            var jObjectClasificacion = JsonConvert.DeserializeObject<JObject>(responseClasificacion.ToString()).First;
            var cantidad = ((Newtonsoft.Json.Linq.JArray)jObjectClasificacion.First).Count;
            for (int i = 0; i < cantidad; i++)
            {
                entidadEncontrada = entidadEncontrada + "\"" + jObjectClasificacion.First[i]["name"].ToString() + "\"" + ",";

            }
            entidadEncontrada = entidadEncontrada.Substring(0, entidadEncontrada.Length - 1);
            var inicioJson = "[";
            var finJson = "]";
            entidadEncontrada = inicioJson.ToString() + entidadEncontrada.ToString().PadRight(entidadEncontrada.Length - 1) + "]";


            return entidadEncontrada.ToString();
        }

        public static string TraducirGoogle (string textoATraducir)
        {
            object o = null;
            string cadena = "https://translation.googleapis.com/language/translate/v2?key=AIzaSyA0yCjU4vVubiB0kKzkokmdvgMEpYPKd7k&target=en&format=text&q=" + textoATraducir.ToString();
            var requestTranslate = (HttpWebRequest)WebRequest.Create(cadena);
            requestTranslate.Method = "POST";
            requestTranslate.ContentType = "application/x-www-form-urlencoded";
            requestTranslate.GetRequestStreamAsync();
            var postDataTranslate = "";
            var datatranslate = Encoding.ASCII.GetBytes(postDataTranslate);
            HttpWebResponse webResponse;
            webResponse = (HttpWebResponse)requestTranslate.GetResponse();
            StreamReader streamReader = new StreamReader(webResponse.GetResponseStream());
            string responseTransalte = streamReader.ReadToEnd();
            var jObject = JsonConvert.DeserializeObject<JObject>(responseTransalte.ToString()).First.First;
            var textoTraducido = jObject["translations"][0]["translatedText"].ToString();

            TextoTraducido = textoTraducido.ToString();

            return textoTraducido.ToString();
        }

        public static string ProcesarGoogle(string textoGoogle)
        {

            
            string textoTraducido = TextoTraducido.ToString();
            var request = (HttpWebRequest)WebRequest.Create("https://language.googleapis.com/v1beta2/documents:analyzeEntities?key=AIzaSyA0yCjU4vVubiB0kKzkokmdvgMEpYPKd7k");

            var DataAEnviar = "{\"document\":{\"type\":\"PLAIN_TEXT\",\"content\":\""+textoTraducido.ToString()+"\"}}";
            var data = Encoding.ASCII.GetBytes(DataAEnviar);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.ContentLength = data.Length;

            using (var stream = request.GetRequestStream())
            {
                stream.Write(data, 0, data.Length);
            }
            string entidadEncontrada = "";
            var response = (HttpWebResponse)request.GetResponse();
            StreamReader streamReaderClasificacion = new StreamReader(response.GetResponseStream());
            string responseClasificacion = streamReaderClasificacion.ReadToEnd();
            var jObjectClasificacion = JsonConvert.DeserializeObject<JObject>(responseClasificacion.ToString()).First;
            var cantidad = ((Newtonsoft.Json.Linq.JArray)jObjectClasificacion.First).Count;
            for (int i = 0; i<cantidad;i++)
            {
                entidadEncontrada = entidadEncontrada+ "\""+ jObjectClasificacion.First[i]["name"].ToString() + "\""+ ",";
               
            }

           

            entidadEncontrada = entidadEncontrada.Substring(0, entidadEncontrada.Length - 1);
            var inicioJson = "[";
            var finJson = "]";
            entidadEncontrada = inicioJson.ToString() + entidadEncontrada.ToString().PadRight(entidadEncontrada.Length - 1) + "]";
            

            return entidadEncontrada.ToString();
        }
        public static string AnalizarTexto(string textoPDF, string numPagina)
        {
            string result = string.Empty;
            var json = "[\"\"]";
            // Create a client.
            ITextAnalyticsAPI client = new TextAnalyticsAPI(new ApiKeyServiceClientCredentials());
            client.AzureRegion = AzureRegions.Southcentralus;
            


            KeyPhraseBatchResult result2;
            try
            {
                // Getting key-phrases
                var length = textoPDF.Length;
                textoPDF = textoPDF.Replace(".", "");
                result2 = client.KeyPhrasesAsync(new MultiLanguageBatchInput(
                            new List<MultiLanguageInput>()
                            {
                          new MultiLanguageInput("es", numPagina, textoPDF)
                            })).Result;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            if (result2.Documents.Count > 0)
            {
                json = RemoveSpecialCharacters(JsonConvert.SerializeObject(result2.Documents[0].KeyPhrases));
            }
            return json;

        }



        private static string RemoveSpecialCharacters(string str)
        {
            StringBuilder sb = new StringBuilder();
            string cadena = str.Replace(".", "");
            foreach (char c in cadena)
            {
                
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '.' || c == '_' || c == ' ' || c == 'á' || c == 'é' || c == 'í' || c == 'ó' || c == 'ú' || c == '{' || c == '}' || c == '[' || c == ']' || c == '"' || c == ',')
                {
                    
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }

    class ApiKeyServiceClientCredentials : ServiceClientCredentials
    {
        public override Task ProcessHttpRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            //request.Headers.Add("Ocp-Apim-Subscription-Key", "e073ce96819c428d8d7f373c26a6796c");
            request.Headers.Add("Ocp-Apim-Subscription-Key", "33d7cd69f0d94e579ba37e52f1327b3f");
            return base.ProcessHttpRequestAsync(request, cancellationToken);
        }
    }
}
