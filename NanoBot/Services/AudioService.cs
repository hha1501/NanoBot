using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Audio;

using NanoBot.Audio;

namespace NanoBot.Services
{
    class AudioService : IDiscordService
    {
        private readonly DiscordSocketClient client;

        private readonly ConcurrentDictionary<ulong, AudioManager> audioManagerDictionary;

        public AudioService(DiscordSocketClient client)
        {
            this.client = client;
            audioManagerDictionary = new ConcurrentDictionary<ulong, AudioManager>();
        }
        public Task InitializeAsync()
        {
            client.Ready += OnClientReady;
            return Task.CompletedTask;
        }

        public Task StartAsync()
        {
            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            await DisconnectFromAllAsync();
            audioManagerDictionary.Clear();
        }

        public Task OnClientReady()
        {
            foreach (SocketGuild guild in client.Guilds)
            {
                audioManagerDictionary.TryAdd(guild.Id, new AudioManager(guild));
            }

            return Task.CompletedTask;
        }

        public async Task ConnectToVoiceChannelAsync(SocketCommandContext context)
        {
            // Check if user is in a voice channel.
            IVoiceChannel targetVoiceChannel = (context.User as IGuildUser)?.VoiceChannel;

            if (targetVoiceChannel == null)
            {
                await context.Channel.SendMessageAsync("User must be in a voice channel first");
                return;
            }

            if (audioManagerDictionary.TryGetValue(context.Guild.Id, out AudioManager audioManager))
            {
                IVoiceChannel currentVoiceChannel = audioManager.VoiceChannel;
                if (currentVoiceChannel != null)
                {
                    // Check if bot is already in the same voice channel.
                    if (currentVoiceChannel.Id == targetVoiceChannel.Id)
                    {
                        await context.Channel.SendMessageAsync("I'm already in");
                        return;
                    }
                    else
                    {
                        // Disconnect from the current voice channel first.
                        await audioManager.DisconnectAsync();
                    }
                }

                await audioManager.ConnectAsync(targetVoiceChannel);
                await context.Channel.SendMessageAsync($"Connected to {targetVoiceChannel.Name}");
            }
            else
            {
                // Log
            }
        }

        public async Task DisconnectFromVoiceChannelAsync(SocketCommandContext context)
        {
            if (audioManagerDictionary.TryGetValue(context.Guild.Id, out AudioManager audioManager))
            {
                // Check if bot is in a voice channel.
                if (audioManager.VoiceChannel != null)
                {
                    await audioManager.DisconnectAsync();
                    await context.Channel.SendMessageAsync($"Disconnected");
                }
                else
                {
                    await context.Channel.SendMessageAsync("I'm not connected to any voice channel");
                }
            }
            else
            {
                // Log
            }
        }

        public async Task DisconnectFromAllAsync()
        {
            await Task.WhenAll(audioManagerDictionary.Values.Select((audioManager) => audioManager.DisconnectAsync()));
        }

        public async Task PlayAudioClipAsync(SocketCommandContext context, string fileName)
        {
            // Check if bot is in a voice channel.
            if (audioManagerDictionary.TryGetValue(context.Guild.Id, out AudioManager audioManager))
            {
                // Check if bot is in a voice channel.
                if (audioManager.VoiceChannel != null)
                {
                    await audioManager.PlayAudioClip(fileName);
                }
                else
                {
                    await context.Channel.SendMessageAsync("I'm not connected to any voice channel");
                }

            }
            else
            {
                // Log
            }
        }

        public async Task PlayAudioFromYoutubeAsync(SocketCommandContext context, string youtubeUrl)
        {
            // TODO:
            // A service should be as generic as possible:
            // Methods should ask for only needed resources.
            // Implement some kind of return value/callback so that consumer like AudioModule can acknowlegde and notify users. Task<bool> should be sufficient.
            // 
            // Services should not send any text message directly.

            // TODO:
            // PlayAudioClip() and PlayAudioFromYoutube() should be moved to their implementors.

            // TODO:
            // AudioService now maintains AudioState(s)
            // Cleanup AudioManager. AudioManager should be a provider which provides AudioClient to consumers (youtube music streaming, audio clip player, ...)
            // A consumer can be model as an interface IAudioTask.
            // IAudioTask: Task Start(AudioOutStream rawPcmStream), Task Stop(), ...
            // An instance of IAudioTask can Stop() itself by means of a CancellationToken.

            // Then AudioManager is responsible for running that Task:
            // AudioManager.RunTask(IAudioTask audioTask).


            // Might need a dedicated YoutubeAudioStreamingService which handles playlist, pause, stop, skip, ...


            // TODO:
            // Move the dictionary lookup somewhere else.



            if (audioManagerDictionary.TryGetValue(context.Guild.Id, out AudioManager audioManager))
            {
                // Check if bot is in a voice channel.
                if (audioManager.VoiceChannel != null)
                {
                    await audioManager.StreamAudio(youtubeUrl);
                }
                else
                {
                    await context.Channel.SendMessageAsync("I'm not connected to any voice channel");
                }
            }
            else
            {
                // Log
            }
        }

        public async Task SetVolumeAsync(SocketCommandContext context, float volume)
        {
            if (audioManagerDictionary.TryGetValue(context.Guild.Id, out AudioManager audioManager))
            {
                // Check if bot is in a voice channel.
                if (audioManager.VoiceChannel != null)
                {
                    audioManager.Volume = volume;
                }
                else
                {
                    await context.Channel.SendMessageAsync("I'm not connected to any voice channel");
                }
            }
            else
            {
                // Log
            }
        }

        public async Task StopRunningTaskAsync(SocketCommandContext context)
        {
            if (audioManagerDictionary.TryGetValue(context.Guild.Id, out AudioManager audioManager))
            {
                if (audioManager.IsRunning)
                {
                    await audioManager.StopAsync();

                    await context.Channel.SendMessageAsync("Stopped");
                }
            }
            else
            {
                // Log
            }
        }
    }
}
