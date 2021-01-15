using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Discord;
using Discord.WebSocket;
using Discord.Commands;

using NanoBot.Services;

namespace NanoBot.Modules
{
    class AudioModule : ModuleBase<SocketCommandContext>
    {
        private readonly AudioService voiceService;

        public AudioModule(AudioService audioService)
        {
            voiceService = audioService;
        }


        [Command("summon", RunMode = RunMode.Async)]
        [Alias("connect")]
        public async Task SummonAsync()
        {
            await voiceService.ConnectToVoiceChannelAsync(Context);
        }


        [Command("disconnect", RunMode = RunMode.Async)]
        public async Task DisconnectAsync()
        {
            await voiceService.DisconnectFromVoiceChannelAsync(Context);
        }

        [Command("spawntiger", RunMode = RunMode.Async)]
        [Alias("tiger", "chuicc")]
        public async Task SpawnTigerAsync()
        {
            await Context.Channel.SendFileAsync(@"C:\Users\ErikHa\Desktop\Audio clips\tiger.jpg");
            await voiceService.PlayAudioClipAsync(Context, "clip1");
        }

        [Command("bomman", RunMode = RunMode.Async)]
        [Alias("devl")]
        public async Task SpawnBommanAsync()
        {
            await voiceService.PlayAudioClipAsync(Context, "clip2");
        }

        [Command("play", RunMode = RunMode.Sync)]
        public async Task PlayAudioFromYoutubeAsync(string url)
        {
            await voiceService.PlayAudioFromYoutubeAsync(Context, url);
        }

        [Command("volume")]
        public async Task SetVolumeAsync(int volumePercent)
        {
            await voiceService.SetVolumeAsync(Context, volumePercent * 0.01f);
        }

        [Command("stop", RunMode = RunMode.Async)]
        public async Task StopAsync()
        {
            await voiceService.StopRunningTaskAsync(Context);
        }
    }
}
