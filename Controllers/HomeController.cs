using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;

namespace Test.Controllers
{
    public class HomeController : Controller
    {        
        const string tmpFolder = "MusicRecogniser";
        // ADD YOUR API-KEY HERE
        const string GoogleApiKey = "";
        string artist = "";

        public IActionResult Test()
        {
            ViewData["message"] = "";
            List<(string, string, string)> videos = new List<(string, string, string)>();
            ViewData["videos"] = videos;
            return View();
        }

        [HttpPost]
        public ActionResult Test(string URL)
        {
            ViewData["artist"] = "";
            //   (Thumb , URL   , Title )
            List<(string, string, string)> videos = new List<(string, string, string)>();
            ViewData["videos"] = videos;
            string result = "";
            try{
                // GET FILE-SYSTEM READY
                // CREATE TEMPORARY FOLDER
                if(!(bash("ls /tmp | grep " + tmpFolder).Equals(tmpFolder)))
                {
                    bash("cd " + tmpFolder);
                }
                string filePath = "/tmp/" + tmpFolder + "/";
                
                // SET UNIQUE FILE NAME
                string fileName = DateTime.Now.ToString().Replace("/","-").Replace(" ","_");
                Random rand = new Random();
                fileName +=  rand.Next(0,1_000_000).ToString();
                // fileName = "10-18-18_6:43:40_PM313579";
                string extension = ".mkv";
                try
                {
                    // DOWNLOAD YOUTUBE VIDEO (BEST AVAILABLE QUALLITY)
                    string cmd = "youtube-dl " + URL + " -o \"" + filePath + fileName + "\" -q";
                    result = bash(cmd, OUTPUT.GET_ERROR);
                    // Debug.Print("CMD: " + cmd + "\n" + RESULT: " + result);

                    // GET FILETYPE EXTENSION
                    extension = bash("ls " + filePath + " | grep " + fileName).Remove(0,fileName.Length);
                }
                catch
                {
                    goto ErrorHandler_DOWNLOAD;
                }
                
                // CONVERT VIDEO TO MP3 (only first 60 seconds)
                try
                {
                    string cmd = "ffmpeg -i \"" + filePath + fileName + extension + "\" -ss 0 -t 60 -loglevel error \"" + filePath + fileName + ".mp3\" -y";
                    cmd = cmd.Replace("\n","");
                    result = bash(cmd, OUTPUT.GET_ERROR);
                    // Debug.Print("CMD: " + cmd + "\n" + RESULT: " + result);
                    if(result.Length > 0 ) goto ErrorHandler_CONVERT;
                }
                catch
                {
                    goto ErrorHandler_CONVERT;
                }

                // RECOGNIZE AUDIO FILE
                try
                {
                    string cmd = "curl -F \"return=artist\" -F \"file=@" + filePath + fileName + ".mp3\" https://api.audd.io/";
                    result = bash(cmd);           
                    Debug.Print("CMD: " + cmd);
                    Debug.Print("\nMIDDLE PRINT \n");
                    Debug.Print("RESULT: " + result);
                    
                    if(result.Contains("\"status\":\"success\""))
                    {
                        //
                        // {"status":"success","result":{"artist":"The Chainsmokers","title":"Closer (R3hab Remix)","album":...
                        //
                        // string txt = "{{\"status\":\"success\",\"result\":{{\"artist\":\"The Chainsmokers\",\"title\":\"Closer (R3hab Remix)\",\"album\":\"";
                        // result = txt;
                        string queryString = "\"artist\":\"";
                        string tempString = result.Substring(result.IndexOf(queryString)+queryString.Length);
                        tempString = tempString.Remove(tempString.IndexOf("\""));
                        artist = tempString;
                    }
                    else
                        goto ErrorHandler_RECOGNIZE;
                }
                catch
                {
                    goto ErrorHandler_RECOGNIZE;
                }
                
                // DELETE TEMPORARY FILES
                try
                {
                    bash("rm " + filePath + fileName + ".*");
                }catch{}
                
                // GENERATE YOUTUBE LIST BASED ON ARTIST NAME
                try
                {
                    videos = YouTubeSearch(artist);
                }catch{}
            }catch{
                goto ErrorHandler_DOWNLOAD;
            }
            ViewData["message"] = "Acquired search results: ";
            ViewData["artist"] = artist;
            ViewData["videos"] = videos;

            return View();

        ErrorHandler_DOWNLOAD:
            if (result.Contains("ERROR:"))
            {
                result = result.Substring(result.IndexOf("ERROR"));
                result = result.Remove(result.IndexOf("\n"));
            }
            else
                result = "ERROR: Unknown URL!";

            ViewData["message"] = result;
            return View();

        ErrorHandler_CONVERT:
            ViewData["message"] = result;
            return View();

        ErrorHandler_RECOGNIZE:
            ViewData["message"] = "ERROR: Music is unrecognizable or no music is discovered!";
            return View();
        }

        enum OUTPUT{ GET_OUTPUT, GET_ERROR }
        private static string bash(string cmdString, OUTPUT output = OUTPUT.GET_OUTPUT)
        {
            ProcessStartInfo processStartInfo = new ProcessStartInfo();
            processStartInfo.FileName = "/bin/bash";
            processStartInfo.Arguments = $"-c \"{cmdString}\"";
            if (output == OUTPUT.GET_ERROR) processStartInfo.RedirectStandardError = true;
            else processStartInfo.RedirectStandardOutput = true;
            processStartInfo.UseShellExecute = false;
            processStartInfo.CreateNoWindow = true;
            
            Process process = Process.Start(processStartInfo);
            string result;
            if(output == OUTPUT.GET_ERROR) result = process.StandardError.ReadToEnd();
            else result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return result;
        }

        private List<(string, string, string)> YouTubeSearch(string query, int MaxResults = 50)
        {
            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = GoogleApiKey,
                ApplicationName = this.GetType().ToString()
            });

            var searchListRequest = youtubeService.Search.List("snippet");
            searchListRequest.Q = query;
            searchListRequest.MaxResults = MaxResults;

            var searchListResponse = searchListRequest.Execute();

            List<(string, string, string)> videos = new List<(string, string, string)>();

            foreach (var searchResult in searchListResponse.Items)
            {
                if (searchResult.Id.Kind == "youtube#video")
                {
                    videos.Add((searchResult.Snippet.Thumbnails.Default__.Url,
                                "https://www.youtube.com/watch?v="+searchResult.Id.VideoId,
                                searchResult.Snippet.Title));
                }
            }

            return videos;
        }
    }
}