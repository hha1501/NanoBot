using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Discord;
using Discord.WebSocket;

using NanoBot.Services;

namespace NanoBot
{
    class BotManager
    {
        private DiscordSocketClient client;

        private DiscordServiceManager serviceManager;

        private IServiceProvider serviceProvider;

        public BotManager()
        {
            client = new DiscordSocketClient();
            ConfigureServices();
        }

        public async Task InitializeAsync()
        {
            client.Log += Log;

            await serviceManager.InitializeAllServices();
        }


        public async Task StartAsync()
        {
            // Should not have leaked this token :(
            // Fortunately Discord's token-scanner noticed immediately and resetted this token :)
            await client.LoginAsync(TokenType.Bot, "TOKEN");
            await client.StartAsync();
            await serviceManager.StartAllServices();
        }

        public async Task StopAsync()
        {
            await serviceManager.StopAllServices();
            await client.StopAsync();
        }


        private Task Log(LogMessage message)
        {
            Console.WriteLine(message);

            return Task.CompletedTask;
        }

        private void ConfigureServices()
        {
            IServiceCollection services = new ServiceCollection()
                .AddSingleton<DiscordSocketClient>(client);

            serviceManager = new DiscordServiceManager()
                .RegisterService<AudioService>(services)
                .RegisterService<CommandManager>(services);

            serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions() { ValidateOnBuild = true });
            serviceManager.ActivateAllServices(serviceProvider);
        }
    }
}
