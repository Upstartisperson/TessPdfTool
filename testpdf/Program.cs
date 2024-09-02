
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
        PdfCombine = 3
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
            ConsoleKeyInfo mode;
            do
            {
                mode = Console.ReadKey();
            } while (mode.KeyChar < 49 || mode.KeyChar > 51);


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

                    string pdfName = Console.ReadLine();

                    CombinePdfs(GetFileSeries(pdfDirectory)).Save(pdfDirectory + pdfName + ".pdf");

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


 
