using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

namespace WebServer
{
    class Program
    {
        static readonly string RootDirectory = Directory.GetCurrentDirectory(); 
        static readonly IDictionary<string, string> CachedResponses = new Dictionary<string, string>();
        static readonly ReaderWriterLockSlim CacheLock = new ReaderWriterLockSlim();

        static void Main(string[] args)
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:5050/");
            listener.Start();

            Console.WriteLine("Web server started");

            while (true)
            {
                try
                {
                    // Objekat HttpListenerContext koji predstavlja zahtev klijenta
                    HttpListenerContext context = listener.GetContext();
                    ThreadPool.QueueUserWorkItem(ProcessRequest, context);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }
        static void ProcessRequest(object state)
        {
            HttpListenerContext context = (HttpListenerContext)state;

            string requestUrl = context.Request.Url!.LocalPath;
            string[] requestParams = requestUrl.Split('/', StringSplitOptions.RemoveEmptyEntries);

            string cacheKey = string.Join("&", requestParams);
            //cacheKey.Replace("&", " ");

            string responseString = "";

            // Proverava da li je odgovor već u kešu
            CacheLock.EnterReadLock();
            if (CachedResponses.TryGetValue(cacheKey, out string? cachedResponse))
            {
                
                Console.WriteLine("Cache hit for " + cacheKey + "!");
                Console.WriteLine("Request method: " + context.Request.HttpMethod);
                Console.WriteLine("Request user host address: " + context.Request.UserHostAddress);

                responseString = cachedResponse;
                CacheLock.ExitReadLock();
            }
            else
            {
                CacheLock.ExitReadLock();

                // Ako odgovor nije u kešu, generisati ga i upisati u keš
                responseString = GenerateResponse(requestParams);
                CacheLock.EnterWriteLock();
                CachedResponses[cacheKey] = responseString;
                CacheLock.ExitWriteLock();
            }

            // Slanje odgovora klijentu
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();

            Console.WriteLine($"Request {requestUrl} processed successfully");
        }
        static string GenerateResponse(string[] requestParams)
        {
            string searchPattern = string.Join("&", requestParams);
            searchPattern = searchPattern.Replace("&", " ");
            string[] words = searchPattern.Split(' ');
            string[] files = Directory.GetFiles(RootDirectory, "*.txt");

            string responseString = "<html><body><h1>Search results: </h1>";

            List<Fajl> filesInRoot = FetchOccurrences(searchPattern, RootDirectory);

            foreach (Fajl currFajl in filesInRoot)
            {
                responseString += "<p>File name: <strong> " + currFajl.fileName + "</strong></p>";
                foreach (string word in words)
                {
                    int count = 0;
                    string[] fileLines = File.ReadAllLines(currFajl.fileName);
                    foreach (string line in fileLines)
                    {
                        count += line.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                    .Count(x => string.Equals(x, word, StringComparison.OrdinalIgnoreCase));
                    }
                    if(count>0)
                    {
                        responseString += "<p style=\"background-color:powderblue;\">Number of word repetitions: " + word + " is " + count + "</p>";
                    }
                    else
                    {
                        responseString += "<p style=\"background-color:red;\">The word "+ word +" you searched for does not exist in the file!</p>";
                    }
            
                }
            }

            responseString += "</body></html>";
            return responseString;
        }

        static List<Fajl> FetchOccurrences(string searchWord, string root)
        {
            List<Fajl> foundFiles = new List<Fajl>();
            string rootDirectory = @root;
            string searchTerm = searchWord;
            int totalCount = 0;

            // Razdvoji traženi pojam na reči koristeći regularne izraze.
            string[] words = Regex.Split(searchTerm, @"\W+");

            // Pretraži datoteke sa .txt ekstenzijom u root direktorijumu
            foreach (string filePath in Directory.EnumerateFiles(rootDirectory, "*.txt", SearchOption.AllDirectories))
            {
                int count = 0;
                Fajl currentFile = new Fajl();

                // Pročitaj sadržaj datoteke
                string fileContents = File.ReadAllText(filePath).ToLower();

                // Razdvoji sadržaj datoteke na reči koristeći regularne izraze
                string[] fileWords = Regex.Split(fileContents, @"\W+");

                // Izbroj broj pojavljivanja svake tražene reči u sadržaju datoteke
                foreach (string word in words)
                {
                    count += fileWords.Count(w => w == word.ToLower());
                }

                // Izlistaj putanju datoteke i broj pojavljivanja traženog pojma u datoteci
                Console.WriteLine("{0}: {1} occurrences", filePath, count);
                if (count > 0)
                {
                    currentFile.fileName = Path.GetFileName(filePath);
                    currentFile.numberOfOccurrences = count;
                    foundFiles.Add(currentFile);
                }

                totalCount += count;
            }

            // Izlistaj ukupan broj pojavljivanja traženog pojma u svim datotekama.
            Console.WriteLine("Total occurrences: {0}", totalCount);
            return foundFiles;
        }

        public class Fajl
        {
            public int numberOfOccurrences;
            public string fileName = "";
        }
    }
}






