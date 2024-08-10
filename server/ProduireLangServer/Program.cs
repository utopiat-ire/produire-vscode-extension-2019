// Copyright(C) 2019-2024 utopiat.net https://github.com/utopiat-ire/
using System;
using System.Text;

namespace ProduireLangServer
{
	class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = new UTF8Encoding(); // UTF8N for non-Windows platform
            var app = new App(Console.OpenStandardInput(), Console.OpenStandardOutput());
            Logger.Instance.Attach(app);
            try
            {
                app.Listen().Wait();
            }
            catch (AggregateException ex)
            {
                Console.Error.WriteLine(ex.InnerExceptions[0]);
                Environment.Exit(-1);
            }
        }
    }
}
