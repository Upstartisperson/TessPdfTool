
namespace PdfRender {
    using Tesseract;
    using System.IO;
    using System.Text;

    public static class Info
    {

        static Info()
        {
            ConfigurationFilePath = ReadPath();
            FontDirectory = ConfigurationFilePath + FontDirectory;
        }


        private const string c_PathInfo = @"C:\Program Files\PdfTool\info.txt";
        public static string FontDirectory = @"\tessFont\tessdata";
        public static string ConfigurationFilePath;

        internal static string ReadPath()
        {
            FileStream stFile = new FileStream(c_PathInfo, FileMode.OpenOrCreate);
            byte[] data = new byte[stFile.Length];
            stFile.Read(data, 0, (int)stFile.Length);
            stFile.Close();
           
            return System.Text.Encoding.UTF8.GetString(data);
        }

        internal static void WritePath(string path)
        {
            FileStream stFile = new FileStream(c_PathInfo, FileMode.OpenOrCreate);
            byte[] data = Encoding.UTF8.GetBytes(path);
            stFile.Write(data, 0, data.Length);
            stFile.Close();
        }

    }

    public class PdfRenderer
    {
        string[] InputPath;
        string OutputPath;
        public PdfRenderer(string[] inputPath, string outputPath)
        {
            InputPath = inputPath;
            OutputPath = outputPath;
        }
        public void StartRender()
        {
            using (IResultRenderer renderer = Tesseract.PdfResultRenderer.CreatePdfRenderer(OutputPath, Info.FontDirectory, false))
            using (renderer.BeginDocument(OutputPath))
            using (TesseractEngine engine = new TesseractEngine(Info.ConfigurationFilePath + @"\tessData", "eng", EngineMode.Default))
            {
                foreach (string image in InputPath)
                {
                    using (var img = Pix.LoadFromFile(image))
                    using (var page = engine.Process(img))
                    {
                        renderer.AddPage(page);
                        Console.Write("Page: " + renderer.PageNumber);
                    }
                }
                     
            }
        }
    }

}
namespace MulitThreadedRender
{
    using System;
    using System.Threading;
    using System.IO;
    using PdfRender;
    using System.Text.RegularExpressions;
    using System.Text;
    using System.Diagnostics;
    using System.Dynamic;
    using System.Reflection;
    using PdfSharpCore;
    using PdfSharpCore.Pdf;
    using System.Runtime.InteropServices.Marshalling;
    using PdfSharpCore.Pdf.IO;
    using Microsoft.Win32.SafeHandles;
    using System.Net.NetworkInformation;
    using SixLabors.ImageSharp.Advanced;

    public enum AppMode : byte
    {
        SingleImage = 1,
        ImageSeries = 2,
        PdfCombine = 3,
        PdfOutline = 4
    }
    public struct threadData
    {
        public readonly string[] ImagePaths;
        public readonly string OutputPath;
        public ManualResetEvent ResetEvent;
        public threadData(string[] imagePaths, string outputPath, ManualResetEvent resetEvent) 
        {
            OutputPath = outputPath;
            ImagePaths = imagePaths;
            ResetEvent = resetEvent;
        }
    }
    class ThreadRender
    {
        public static ManualResetEvent resetevent = new ManualResetEvent(false);
        public const string PdfName = "Pdf";
        static void Main()
        {
            string st = System.Reflection.Assembly.GetExecutingAssembly().Location;
            Console.WriteLine(st);

            // FileStream filestream = new FileStream("\\info.txt", FileMode.OpenOrCreate);
            // byte[] data = new byte[filestream.Length];
            // filestream.Read(data, 0, (int)filestream.Length);

            //string str = System.Text.Encoding.UTF8.GetString(data);

            // Console.WriteLine(str);



            Console.WriteLine("Select Mode: ");
            Console.WriteLine("(1)" + AppMode.SingleImage);
            Console.WriteLine("(2)" + AppMode.ImageSeries);
            Console.WriteLine("(3)" + AppMode.PdfCombine);
            Console.WriteLine("(4)" + AppMode.PdfOutline);
            ConsoleKeyInfo mode;
            do
            {
                mode = Console.ReadKey();
            } while (mode.KeyChar < 49 || mode.KeyChar > 52);


            switch ((AppMode)(mode.KeyChar - 48))
            {
                case AppMode.SingleImage:
                    Console.WriteLine("Drag And Drop Your Image To The Console");
                    ManualResetEvent reset = new ManualResetEvent(false);
                    string image = GetPath();

                    string pdfPath = Regex.Replace(image, "(.png)|(.jpeg)", string.Empty);

                    Render(new threadData(new string[] { image.ToString() }, pdfPath, reset));

                    break;
                case AppMode.ImageSeries:

                    string inputDirectory;
                    string outputDirectory;
                    Console.WriteLine("Select Image Directory: ");
                    inputDirectory = GetPath();

                    Console.WriteLine("Select Output Directoy: ");
                    outputDirectory = GetPath() + "\\";

                    Console.WriteLine("Delete Tmp Pdfs (y/n)?");
                    bool deleteTmp = Console.ReadLine() == "y";

                    DoMultiThreadRender(GetFileSeries(inputDirectory), outputDirectory, deleteTmp);

                    break;
                case AppMode.PdfCombine:
                    Console.WriteLine("Select Pdf Directory: ");
                    string pdfDirectory = GetPath() + "\\";

                    Console.WriteLine();

                    Console.Write("Pdf Name :");

                    Console.WriteLine("Header Length: ");



                    string pdfName = Console.ReadLine();

                    CombinePdfs(GetFileSeries(pdfDirectory)).Save(pdfDirectory + pdfName + ".pdf");
                    break;


                case AppMode.PdfOutline:
                    Console.WriteLine("Select Pdf Path: ");
                    string path = GetPath();
                    PdfDocument activePdf = PdfReader.Open(path, PdfDocumentOpenMode.Import);
                    
                    Console.WriteLine("Select Outline Path: ");
                    string outlinePath = GetPath();

                    OutlineParse[] outlineParses = ParseOutline(outlinePath);
                    AddOutline(activePdf, outlineParses, 0).Save(path + "WithOutline");

                    break;


            }



        }

        private static string GetPath()
        {
            string path;
            do
            {
                path = Console.ReadLine();
            } while (path == null);
            path = path;
            return Regex.Replace(path, '"'.ToString(), string.Empty);
        }

        private static string[] GetFileSeries(string Path)
        {
            string[] files = Directory.GetFiles(Path);
            Dictionary<int, string> fileHash = new Dictionary<int, string>();


            foreach (string file in files)
            {
                var match = Regex.Match(GetFileName(file), @"\d+");

                // Extract and convert the matched values to integers
                int extractedNumbers = int.Parse(match.Value);

                fileHash.Add(extractedNumbers, file);
            }
            int i = 0;
            List<string> fileSort = new List<string>();
            while (true)
            {
                if (files.Length == fileSort.Count || i >= 10000) break;
                if (!fileHash.TryGetValue(i++, out string file)) continue;
                fileSort.Add(file);
            }
            return fileSort.ToArray();
        }

        private static PdfDocument AddOutline(PdfDocument pdf, OutlineParse[] outlineParses, int startIndex)
        {
            PdfOutlineCollection outlines = pdf.Outlines;
            outlines.Clear();
            Queue<OutlineParse> parses = new(outlineParses.ToArray());

            AddOutlineElement(pdf, outlines, parses, parses.Dequeue());


            return pdf;
        }




        private static void AddOutlineElement(PdfDocument pdf, PdfOutlineCollection outline, Queue<OutlineParse> parses, OutlineParse current)
        {
            OutlineParse Future;
            if (!parses.TryPeek(out Future))
            {
                outline.Add(current.Title, pdf.Pages[current.PageNum]);
                return;
            }

            if(Future.IndentLvl < current.IndentLvl)
            {
                outline.Add(current.Title, pdf.Pages[current.PageNum]);
                return;
            }

            if (Future.IndentLvl > current.IndentLvl)
            {
                AddOutlineElement(pdf, outline.Add(current.Title, pdf.Pages[current.PageNum]).Outlines, parses, parses.Dequeue());
                if (!parses.TryDequeue(out current)) return;
                AddOutlineElement(pdf, outline, parses, current);
            }

            if (Future.IndentLvl == current.IndentLvl)
            {
                outline.Add(current.Title, pdf.Pages[current.PageNum]);
                AddOutlineElement(pdf, outline, parses, parses.Dequeue());
                return;
            }
            return;
        }



        struct OutlineParse
        {
            public readonly string Title;
            public readonly int IndentLvl;
            public readonly int PageNum;
            public OutlineParse(string Title,  int IndentLvl, int PageNum)
            {
                this.Title = Title;
                this.IndentLvl = IndentLvl;
                this.PageNum = PageNum;
            }
        }

        private static OutlineParse ParseOutlineElement(string st, int header)
        {
            char[] chars = st.ToArray();
            int start = 0;
            bool inside = false;
            int i;
            for (i = 0; i < chars.Length; i++)
            {
                if (chars[i] == '"' && !inside)
                {
                    start = i + 1;
                    inside = true;
                }
                else if (chars[i] == '"')
                {
                    i--;
                    break;
                }

            }
            char[] name = new Span<char>(chars, start, i - start + 1).ToArray();
            string num = new string(new Span<char>(chars, i + 3, chars.Length - i - 3).ToArray());
            int value = int.Parse(num);
            int indentLvl = 0;
            for(int t = 0; t < start; t++)
            {
                if (chars[t] == '+') indentLvl++;
            }

            return new OutlineParse(new string(name), indentLvl, value + header);

        }

        private static OutlineParse[] ParseOutline(string outlinePath)
        {
            StringReader sr;
            try
            {
                FileStream fileStream = new FileStream(outlinePath, FileMode.Open);
                byte[] bytes = new byte[fileStream.Length];
                fileStream.Read(bytes, 0, bytes.Length);
                string str = System.Text.Encoding.UTF8.GetString(bytes);
                sr = new StringReader(str);
            }
            catch
            {
                Console.WriteLine("Failed To open Outline File");
                return null;
            }

            //Do Meta Data

           
            int header = int.Parse(sr.ReadLine());
          
            
            List<OutlineParse> result = new();
            string ln = sr.ReadLine();
            while (ln != null)
            {
               if (!string.IsNullOrWhiteSpace(ln))
               {
                    result.Add(ParseOutlineElement(ln, header));
               }
               ln = sr.ReadLine();
            }
            return result.ToArray();
        }
        private static string GetFileName(string Path)
        {
            StringReader sr = new StringReader(Path);
            int start = 0;
            int i = 0;
            int count = 0;
            while(-1 != sr.Peek())
            {
                if((char)sr.Read() == '\\') 
                {
                    start = i + 1;
                    count = 0;
                }
                i++;
                count++;
            }
            sr = new StringReader(Path);
            char[] characters = new char[i];
          
            sr.ReadBlock(characters, 0, i);
            

            StringBuilder sb = new StringBuilder();
            
            for(i = start; i < characters.Length; i++)
            {
                sb.Append(characters[i].ToString());
            }
            sr.Close();

            return sb.ToString();
        }
        private static void DoMultiThreadRender(string[] imgPaths, string OutputPath, bool deleteTmp)
        {
            string[] pdfNames;

            Directory.CreateDirectory(OutputPath);
            Directory.CreateDirectory(OutputPath + "\\tmp");

            Console.WriteLine();
            Console.WriteLine("Processing Images..");
            Console.WriteLine();

            ThreadPool.GetMaxThreads(out int maxThread, out int maxIOThread);
            maxThread = (64 <= imgPaths.Length) ? 64 : imgPaths.Length;
            int filesPerThread = imgPaths.Length / maxThread;
            pdfNames = new string[maxThread];

            int extra = imgPaths.Length % maxThread;
            if (filesPerThread == 0)
            {
                filesPerThread = 1;
                maxThread = imgPaths.Length;
                extra = 0;
            }


            List<ManualResetEvent> resetevents = new();
            int i = 0;
            for (int t = 0; t < maxThread; t++)
            {
                List<string> workerFile = new();
                if (extra-- > 0)
                {
                    workerFile.Add(imgPaths[i++]);

                }
                for (int j = 0; j < filesPerThread; j++)
                {
                    workerFile.Add(imgPaths[i++]);
                }
                StringBuilder sb = new StringBuilder();
                sb.Append(OutputPath);
                sb.Append("\\tmp\\");
                sb.Append(PdfName);
                sb.Append(t.ToString());
                pdfNames[t] = sb.ToString();
                ManualResetEvent reset = new ManualResetEvent(false);
                resetevents.Add(reset); //so cool wanna code but cant
                threadData workerData = new threadData(workerFile.ToArray(), sb.ToString(), reset); //Need better output paths that are indivual
                ThreadPool.QueueUserWorkItem(Render, workerData);
                pdfNames[t] += ".pdf";

            }

            for (i = 0; i < resetevents.Count; i++)
            {
                resetevents[i].WaitOne();
            }
            Thread.Sleep(1000);

            WaitHandle.WaitAll(resetevents.ToArray());

            CombinePdfs(pdfNames).Save(OutputPath + "\\MainPdf.pdf");

            if (deleteTmp)
            {
                Directory.Delete(OutputPath + "\\tmp", true);
            }

            Console.WriteLine("Pdf Complete!");
        }

        internal static PdfDocument CombinePdfs(string[] pdfPaths)
        {
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Rendering 'MainPdf.pdf'...");

            PdfDocument mainPdf = new PdfDocument();
            for (int t = 0; t < pdfPaths.Length; t++)
            {
                PdfDocument activePdf = PdfReader.Open(pdfPaths[t], PdfDocumentOpenMode.Import);
                for (int j = 0; j < activePdf.PageCount; j++)
                {
                    mainPdf.AddPage(activePdf.Pages[j]);
                }
            }
            
            return mainPdf;
         }


        static void Render(object state)
        {
            
            string[] images = ((threadData)state).ImagePaths;
            string outputPath = ((threadData)state).OutputPath;
            PdfRenderer pdfrenderer = new PdfRenderer(images, outputPath);
            pdfrenderer.StartRender();
            ((threadData)state).ResetEvent.Set();

        }
        
    }

    




}


 
