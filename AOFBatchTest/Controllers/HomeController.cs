using AOFBatchTest.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Linq.SqlClient;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Xml;
using System.Data.Entity;

namespace AOFBatchTest.Controllers
{
    public class HomeController : Controller
    {
        ContextModel _context = new ContextModel();

        [HttpGet]
        public ActionResult Index()
        {
            var latestAOFs = _context.AofFiles.OrderByDescending(a => a.Id).FirstOrDefault();
            return View();
        }

        public void ParseXML(byte[] buffer)
        {
            // XmlReader xmlReader = XmlReader.Create(path);

            MemoryStream ms = new MemoryStream(buffer);
            XmlReader xmlReader = XmlReader.Create(ms);

            bool openItem = false;

            List<InvoicePrerequisite> items = new List<InvoicePrerequisite>();

            try
            {
                var Stopwatch = new Stopwatch();
                Stopwatch.Start();


                while (xmlReader.Read())
                {
                    if (xmlReader.NodeType == XmlNodeType.Element)
                    {
                        //skip to node.
                        xmlReader.ReadToFollowing("Merchantheader");

                        do
                        {
                            int total_Trx = 0;
                            int total_Rfd = 0;
                            InvoicePrerequisite item = new InvoicePrerequisite();

                            xmlReader.ReadToFollowing("POSProductrecord");
                            do
                            {
                                if (xmlReader.ReadToDescendant("Totaal_Trx"))
                                {
                                    total_Trx += Convert.ToInt32(xmlReader.ReadElementContentAsString());
                                }

                                if (xmlReader.ReadToNextSibling("Totaal_Refund"))
                                {
                                    total_Rfd += Convert.ToInt32(xmlReader.ReadElementContentAsString());

                                }
                                while (xmlReader.Read() && xmlReader.Name != "POSProductrecord") ;
                            }
                            while (xmlReader.ReadToNextSibling("POSProductrecord"));


                            item.NumberOfTransactions = total_Trx;
                            item.NumberOfRefunds = total_Rfd;

                            items.Add(item);

                            while (xmlReader.Read() && xmlReader.Name != "Merchantheader") ;
                        }
                        while (xmlReader.ReadToNextSibling("Merchantheader"));
                    }
                }
                Stopwatch.Stop();
                var ts = Stopwatch.Elapsed.Seconds;
            }
            catch (XmlException xe)
            {
                openItem = false;
            }

            AddToDB(items);
        }

        private void AddToDB(List<InvoicePrerequisite> items)
        {

            var Stopwatch = new Stopwatch();
            Stopwatch.Start();

            //Pass in cnx, tablename, and list of imports
            BulkInsert(_context.Database.Connection.ConnectionString, "InvoicePrerequisites", items);

            Stopwatch.Stop();
            var ts2 = Stopwatch.Elapsed.Seconds;

        }

        public static void BulkInsert<T>(string connection, string tableName, IList<T> list)
        {
            using (var bulkCopy = new SqlBulkCopy(connection))
            {
                bulkCopy.BatchSize = list.Count;
                bulkCopy.DestinationTableName = tableName;

                var table = new DataTable();
                var props = TypeDescriptor.GetProperties(typeof(T));

                for (int i = 0; i < props.Count; i++)
                {
                    PropertyDescriptor prop = props[i];
                    table.Columns.Add(prop.Name, prop.PropertyType);
                }

                var values = new object[props.Count];

                foreach (var item in list)
                {
                    for (var i = 0; i < values.Length; i++)
                    {
                        values[i] = props[i].GetValue(item);
                    }

                    table.Rows.Add(values);
                }

                bulkCopy.WriteToServer(table);
            }
        }

        [HttpPost]
        public ActionResult Index(HttpPostedFileBase file)
        {
            if (file.ContentLength > 0)
            {
                //var fileName = Path.GetFileName(file.FileName);
                //var path = Path.Combine(Server.MapPath("~/App_Data/uploads"), fileName);
                //file.SaveAs(path);

                string strConn = _context.Database.Connection.ConnectionString;
                SqlConnection cnn = new SqlConnection(strConn);
                SqlCommand cmd = new SqlCommand();

                cmd.Connection = cnn;

                SqlParameter Content = cmd.Parameters.Add("@Content", SqlDbType.VarBinary);
                SqlParameter length = cmd.Parameters.Add("@length", SqlDbType.Int);
                SqlParameter offset = cmd.Parameters.Add("@offset", SqlDbType.BigInt);
                SqlParameter fileId = cmd.Parameters.Add("@fileId", SqlDbType.Int);

                int bufferSize = 10000000;
                byte[] buffer = new byte[bufferSize];

                cnn.Open();

                var Stopwatch = new Stopwatch();
                Stopwatch.Start();

                var inputStream = file.InputStream;

                // Read bytes from http input stream
                using (BinaryReader br = new BinaryReader(inputStream))
                {
                    byte[] blob = br.ReadBytes((int)inputStream.Length);
                    inputStream.Position = 0;

                    int fileSize = blob.Length;
                    int index = 0;
                    int readBytes = 0;

                    while (index < fileSize)
                    {
                        //Add file in database
                        if (index == 0)
                        {
                            readBytes = bufferSize;
                            inputStream.Read(buffer, 0, readBytes);

                            cmd.CommandText = "INSERT INTO AofFiles (Date, Content)" + "VALUES (@Date, @Content)";
                            AofFile aof = new AofFile()
                            {
                                Date = DateTime.Now,
                                Content = buffer
                            };

                            cmd.Parameters.Add("@Date", SqlDbType.VarChar).Value = aof.Date;
                            Content.Value = aof.Content;

                            length.Value = readBytes;
                            offset.Value = 0;
                            fileId.Value = 0;

                            cmd.ExecuteNonQuery();

                            index += bufferSize;
                        }

                        else
                        {
                            //get latest added record id
                            var latestAOFId = _context.AofFiles.OrderByDescending(a => a.Id).First().Id;

                            cmd.CommandText = "UPDATE AofFiles SET content.Write(@Content, @offset, @length) where Id=@fileId";

                            if (index + bufferSize > fileSize)
                                readBytes = fileSize - index;
                            else
                                readBytes = bufferSize;

                            inputStream.Read(buffer, 0, readBytes);

                            Content.Value = buffer;
                            Content.Size = readBytes;

                            length.Value = readBytes;
                            offset.Value = index;
                            fileId.Value = latestAOFId;

                            cmd.ExecuteNonQuery();
                            index += bufferSize;
                        }
                    }

                    cnn.Close();

                    Stopwatch.Stop();
                    var ts = Stopwatch.Elapsed.Seconds;

                    //parse
                    ParseXML(blob);
                }
            }


            return RedirectToAction("Index");
        }
    }
}