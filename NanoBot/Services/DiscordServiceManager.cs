using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

namespace NanoBot.Services
{
    class DiscordServiceManager
    {
        private List<Type> discordServiceTypes;

        private List<IDiscordService> discordServices;

        public DiscordServiceManager()
        {
            discordServiceTypes = new List<Type>();
            discordServices = new List<IDiscordService>();
        }

        public DiscordServiceManager RegisterService<TService>(IServiceCollection services) where TService : class, IDiscordService
        {
            discordServiceTypes.Add(typeof(TService));
            services.AddSingleton<TService>();

            return this;
        }

        public void ActivateAllServices(IServiceProvider serviceProvider)
        {
            foreach (Type serviceType in discordServiceTypes)
            {
                discordServices.Add((IDiscordService)serviceProvider.GetRequiredService(serviceType));
            }
        }

        public async Task InitializeAllServices()
        {
            foreach (IDiscordService service in discordServices)
            {
                await service.InitializeAsync();
            }
        }

        public async Task StartAllServices()
        {
            foreach (IDiscordService service in discordServices)
            {
                await service.StartAsync();
            }
        }

        public async Task StopAllServices()
        {
            foreach (IDiscordService service in discordServices)
            {
                await service.StopAsync();
            }
        }

    }
}
