using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace LazySearch
{
    public class LazySearchMod : ModSystem
    {
        public static bool logDebug = false;
        ICoreClientAPI capi = null;
        void msgPlayer(string msg)
        {
            capi?.ShowChatMessage("|LazySearch|: " + msg);
        }
        void printClient(string msg)
        {
            if (logDebug)
            {
                capi?.Logger.Debug("|LazySearch|: " + msg);
                capi?.ShowChatMessage("|LazySearch|: " + msg);
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

            printClient("LazySearch Mod started");
        }
    }
}
