using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace LazySearch
{
    public class LazySearchMod : ModSystem
    {
        public static bool LogDebug
        { get; set; } = false;
        private ICoreClientAPI capi = null;

        private void PrintClient(string msg)
        {
            if (LogDebug)
            {
                capi?.Logger.Debug(CommandSystem.LsMsg(msg));
                capi?.ShowChatMessage(CommandSystem.LsMsg(msg));
            }
        }

        public override void StartPre(ICoreAPI api)
        {
            base.StartPre(api);
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            capi = api;

            PrintClient("LazySearch Mod started");
        }
    }
}
