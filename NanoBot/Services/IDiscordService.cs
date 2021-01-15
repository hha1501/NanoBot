using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NanoBot.Services
{
    public interface IDiscordService
    {
        Task InitializeAsync();

        Task StartAsync();

        Task StopAsync();
    }
}
