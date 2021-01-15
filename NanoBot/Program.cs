using System;
using System.Threading.Tasks;

namespace NanoBot
{
    class Program
    {
        static async Task Main(string[] args)
        {
            BotManager botManager = new BotManager();
            await botManager.InitializeAsync();
            await botManager.StartAsync();

            bool exit = false;

            while (!exit)
            {
                string promt = Console.ReadLine();

                switch (promt)
                {
                    case "start":
                        await botManager.StartAsync();
                        break;  
                    case "stop":
                        await botManager.StopAsync();
                        break;
                    case "exit":
                        exit = true;
                        break;
                    default:
                        break;
                }
            }

        }
    }
}
