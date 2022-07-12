using Coolr.Api;
// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");

if(!Directory.Exists("./output"))
{
    Directory.CreateDirectory("./output");
}

var downloader = new Downloader();
downloader.ServerUrl = "https://portal.coolrgroup.com";
downloader.Username = "";
downloader.Password = "";
downloader.ControllerName = "AssetPurity";

var extraParameters = new Dictionary<string, string>();
extraParameters["includeProductDetails"] = "true";

downloader.Download(new DateTime(2021, 1, 1), extraParameters).Wait();