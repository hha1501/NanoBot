using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Discord;
using Discord.WebSocket;
using Discord.Commands;

using NanoBot.Modules;

namespace NanoBot.Services
{
    class CommandManager : IDiscordService
    {
        private readonly DiscordSocketClient client;
        private readonly CommandService commandService;

        private IServiceProvider serviceProvider;

        public CommandManager(DiscordSocketClient client, IServiceProvider serviceProvider)
        {
            this.client = client;
            this.serviceProvider = serviceProvider;

            commandService = new CommandService();
        }
        public Task InitializeAsync()
        {
            commandService.Log += CommandServiceLog;
            return InstallModulesAsync();
        }

        public Task StartAsync()
        {
            client.MessageReceived += HandleCommandAsync;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            client.MessageReceived -= HandleCommandAsync;
            return Task.CompletedTask;
        }


        private Task CommandServiceLog(LogMessage arg)
        {
            Console.WriteLine(arg);
            return Task.CompletedTask;
        }

        private async Task HandleCommandAsync(SocketMessage messageParam)
        {
            // Don't process the command if it was a system message
            if (messageParam is SocketUserMessage message)
            {
                int commandStartIndex = -1;

                // Determine if the message is a command based on the prefix and make sure no bots trigger commands.
                if (!(message.HasCharPrefix('.', ref commandStartIndex) ||
                    message.HasMentionPrefix(client.CurrentUser, ref commandStartIndex)) ||
                    message.Author.IsBot)
                {
                    return;
                }

                var context = new SocketCommandContext(client, message);

                var result = await commandService.ExecuteAsync(context, commandStartIndex, serviceProvider);
            }
        }

        private async Task InstallModulesAsync()
        {
            await commandService.AddModuleAsync<AudioModule>(serviceProvider);
            await commandService.AddModuleAsync<TextModule>(serviceProvider);
        }
    }
}
