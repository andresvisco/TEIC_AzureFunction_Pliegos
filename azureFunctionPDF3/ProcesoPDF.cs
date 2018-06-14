using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System.Net.Http;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage;
using System;
using System.Threading.Tasks;
using PdfSharp;
using System.Xml.Linq;
using System.Collections.Generic;
using iTextSharp.text;
using iTextSharp.text.pdf;
using static PdfSharp.Pdf.PdfDictionary;
using itextsharp.pdfa.iTextSharp.text.pdf;
using PdfSharp.Pdf.Content.Objects;
using System.Text;
using System.Linq;
using PdfSharp.Pdf.Content;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Host;
using System.Threading;
using System.Data.SqlClient;

namespace azureFunctionPDF3
{
    public class ProcesoPDF
    {
        //private PdfReader sourcePDF { get; set; }

        public PdfSharp.Pdf.PdfDocument doc { get; set; }
        public Stream strOriginal { get; set; }

        
        
        public ProcesoPDF()
        { }

        public ProcesoPDF(Stream streamPDF)
        {
            doc = PdfSharp.Pdf.IO.PdfReader.Open(streamPDF, PdfDocumentOpenMode.ReadOnly);
            strOriginal = streamPDF;
        }

        public string ParsearPDF()
        {

            string storageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            string azureFolderPath = "";
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("carpetapdf");
            CloudBlobDirectory cloudBlobDirectory = container.GetDirectoryReference(azureFolderPath);

            ////get source file to split
            string pdfFile = "pliego.pdf";
            CloudBlockBlob blockBlob1 = cloudBlobDirectory.GetBlockBlobReference(pdfFile);

            //convert to memory stream
            MemoryStream memStream = new MemoryStream();
            blockBlob1.DownloadToStreamAsync(memStream).Wait();


            PdfSharp.Pdf.PdfDocument outputDocument = new PdfSharp.Pdf.PdfDocument();

            //get source file to split
            for (var i = 0; i <= doc.Pages.Count - 1; i++)
            {
                //define output file for azure
                string outputPDFFile = "output" + i + ".pdf";
                CloudBlockBlob outputblockBlob = cloudBlobDirectory.GetBlockBlobReference(outputPDFFile);

                //create new document
                MemoryStream newPDFStream = new MemoryStream();

                PdfSharp.Pdf.PdfDocument pdfDoc = PdfSharp.Pdf.IO.PdfReader.Open(strOriginal, PdfDocumentOpenMode.Import);
                
                for (var j = doc.Pages.Count - 1; j >= 0; j--)
                {
                    if (j != i)
                    {
                        pdfDoc.Pages.RemoveAt(j);
                        
                    }
                }

                

                byte[] pdfData;
                using (var ms = new MemoryStream())
                {
                    //doc.Save(ms);
                    pdfDoc.Save(ms);
                    pdfData = ms.ToArray();

                    

                    //outputblockBlob.UploadFromStreamAsync(ms);
                    outputblockBlob.UploadFromByteArrayAsync(pdfData, 0, pdfData.Length);
                }

                
            }

            return "Ok";
        }


        public string ExtraerTextoFromPDF ()
        {
            var result = new StringBuilder();
            foreach (var page in doc.Pages)
            {
                ExtractText(ContentReader.ReadContent(page), result);
                result.AppendLine();
            }
            return result.ToString();
        }

        public async Task<string> ExtraerkeyPhrasesFromPDF(TraceWriter log)
        {
            var result = new StringBuilder();
            string keyPhrases = string.Empty;

            string keyPhrasesGoogle = string.Empty;
            string classifyTextGoogle = string.Empty;
            int numeroPagina = 1;
            string separador = ",";
            int cantidadLlamadasAPI = 0;
            foreach (var page in doc.Pages)
            {
                ExtractText(ContentReader.ReadContent(page), result);
                Dictionary<int, string> subPaginas = splitPagina(result.ToString());

                foreach (KeyValuePair<int, string> entry in subPaginas)
                {
                   try
                    {
                        if (cantidadLlamadasAPI == 3)
                        {
                            Thread.Sleep(400);
                            cantidadLlamadasAPI = 0;
                        }
                        else
                        {
                            cantidadLlamadasAPI = cantidadLlamadasAPI + 1;
                            classifyTextGoogle = AnalizadorTexto.Clasificador(entry.Value.ToString().Replace(".", ""));
                            keyPhrasesGoogle = AnalizadorTexto.ProcesarGoogle(entry.Value.ToString().Replace(".",""));
                            keyPhrases = AnalizadorTexto.AnalizarTexto(entry.Value.ToString().Replace(".", ""), numeroPagina.ToString());
                            Thread.Sleep(400);



                        }

                    }
                    catch (Exception ex)
                    {
                        log.Info("exception 2 " + ex.Message + " // String Entry Value: " + entry.Value.ToString().Replace(".", ""));
                    }

                    log.Info("Se procesó el bloque : " + entry.Key.ToString() + " de la página " + numeroPagina.ToString() + ". Total de páginas: " + doc.PageCount.ToString() );
                    log.Info(keyPhrases.Length.ToString());

                    var guardarKeys = GuardarKeys(doc.Guid.ToString(), numeroPagina, entry.Key, keyPhrases, keyPhrasesGoogle.ToString(), log);
                    var guardarClasificador = GuardarClasificador(doc.Guid.ToString(), classifyTextGoogle, log);

                    if (guardarKeys && guardarClasificador)
                    {
                        log.Info("Guardó Ok");
                    }
                    else
                    {
                        log.Info("Error");
                    }

                    

                }

                numeroPagina++;
            }





            // InsertarEnTxt(result1.ToString());

            return result.ToString();
        }

        //INSERT INTO dbo.PliegosClassifyText(IdPdf, Categorias) VALUES('e2c003f5-a6d2-4885-a06d-23fbdd3bc404','"[bla bla bla]"')

        private bool GuardarClasificador(string IdPdf, string Classify, TraceWriter log)
        {
            try
            {
                SqlConnection sqlConnection1 = new SqlConnection("Server=tcp:techintpliegossqlserver.database.windows.net,1433;Initial Catalog=TECHINTPliegosSqlDB;;Persist Security Info=False;User ID=andres.visco;Password=2363Andy;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;");
                System.Data.SqlClient.SqlCommand cmd = new System.Data.SqlClient.SqlCommand();
                cmd.CommandType = System.Data.CommandType.Text;
                cmd.CommandText = "INSERT INTO dbo.PliegosClassifyText(IdPdf, Categorias) VALUES('" + IdPdf + "','"+Classify.ToString()+"')";
                cmd.Connection = sqlConnection1;

                sqlConnection1.Open();
                cmd.ExecuteNonQuery();
                sqlConnection1.Close();

                return true;
            }
            catch (Exception ex)
            {
                log.Info(ex.Message.ToString());
                return false;
            }

        }
        public SqlConnection sqlConnection1 = new SqlConnection("Server=tcp:techintpliegossqlserver.database.windows.net,1433;Initial Catalog=TECHINTPliegosSqlDB;;Persist Security Info=False;User ID=andres.visco;Password=2363Andy;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;");
        private bool GuardarKeys(string IdPdf, int NumPagina, int Bloque, string Keys, string KeysGoogle, TraceWriter log)
        {
            try
            {
                
                System.Data.SqlClient.SqlCommand cmd = new System.Data.SqlClient.SqlCommand();
                cmd.CommandType = System.Data.CommandType.Text;
                cmd.CommandText = "INSERT INTO dbo.Pliegos(IdPdf, pag, bloque, Keys, KeysGoogleEntidades) VALUES('" + IdPdf + "', " + NumPagina + ", " + Bloque+ ", '" + Keys + "', '" + KeysGoogle+ "')";
                cmd.Connection = sqlConnection1;

                sqlConnection1.Open();
                cmd.ExecuteNonQuery();
                sqlConnection1.Close();

                return true;
            }catch(Exception ex)
            {
                log.Info(ex.Message.ToString());
                return false;
            }

        }

        private string InsertarEnTxt(string texto)
        {

            string storageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            string azureFolderPath = "";
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("carpetapdf");
            CloudBlobDirectory cloudBlobDirectory = container.GetDirectoryReference(azureFolderPath);

            container.CreateIfNotExistsAsync();

            CloudBlockBlob blob = container.GetBlockBlobReference("resultadoKeyPhrases.json");
            blob.UploadTextAsync(texto);

            return "";
        }

        private Dictionary<int, string> splitPagina(string texto)
        {
            int partLength = 5000;
            //string texto = "Silver badges are awarded for longer term goals. Silver badges are uncommon.";
            string[] words = texto.Split(' ');
            var parts = new Dictionary<int, string>();
            string part = string.Empty;
            int partCounter = 0;
            foreach (var word in words)
            {
                if (part.Length + word.Length < partLength)
                {
                    part += string.IsNullOrEmpty(part) ? word : " " + word;
                }
                else
                {
                    parts.Add(partCounter, part);
                    part = word;
                    partCounter++;
                }
            }
            parts.Add(partCounter, part);

            return parts;
            //foreach (var item in parts)
            //{
            //    Console.WriteLine("Part {0} (length = {2}): {1}", item.Key, item.Value, item.Value.Length);
            //}
            //Console.ReadLine();
        }



        #region CObject Visitor
        private static void ExtractText(CObject obj, StringBuilder target)
        {
            if (obj is CArray)
                ExtractText((CArray)obj, target);
            else if (obj is CComment)
                ExtractText((CComment)obj, target);
            else if (obj is CInteger)
                ExtractText((CInteger)obj, target);
            else if (obj is CName)
                ExtractText((CName)obj, target);
            else if (obj is CNumber)
                ExtractText((CNumber)obj, target);
            else if (obj is COperator)
                ExtractText((COperator)obj, target);
            else if (obj is CReal)
                ExtractText((CReal)obj, target);
            else if (obj is CSequence)
                ExtractText((CSequence)obj, target);
            else if (obj is CString)
                ExtractText((CString)obj, target);
            else
                throw new NotImplementedException(obj.GetType().AssemblyQualifiedName);
        }

        private static void ExtractText(CArray obj, StringBuilder target)
        {
            foreach (var element in obj)
            {
                ExtractText(element, target);
            }
        }
        private static void ExtractText(CComment obj, StringBuilder target) { /* nothing */ }
        private static void ExtractText(CInteger obj, StringBuilder target) { /* nothing */ }
        private static void ExtractText(CName obj, StringBuilder target) { /* nothing */ }
        private static void ExtractText(CNumber obj, StringBuilder target) { /* nothing */ }
        private static void ExtractText(COperator obj, StringBuilder target)
        {
            if (obj.OpCode.OpCodeName == OpCodeName.Tj || obj.OpCode.OpCodeName == OpCodeName.TJ)
            {
                foreach (var element in obj.Operands)
                {
                    ExtractText(element, target);
                }
                target.Append(" ");
            }
        }
        private static void ExtractText(CReal obj, StringBuilder target) { /* nothing */ }
        private static void ExtractText(CSequence obj, StringBuilder target)
        {
            foreach (var element in obj)
            {
                ExtractText(element, target);
            }
        }
        private static void ExtractText(CString obj, StringBuilder target)
        {
            target.Append(obj.Value);
        }

        
        #endregion

    }
}
