using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Discord.Commands;

namespace NanoBot.Modules
{
    class TextModule : ModuleBase<SocketCommandContext>
    {
        [Command("echo")]
        public async Task EchoAsync([Remainder] string text)
        {
            await ReplyAsync(text);
        }
    }
}
