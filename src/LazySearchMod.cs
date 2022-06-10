using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

[assembly: ModInfo("LazySearch",
    Version = "1.2.1",
    // Side = "Client",
    Description = "A lazy approach to search for blocks in the World",
    Website = "https://github.com/testunde/LazySearchMod",
    Authors = new[] { "TeStUnDe" })]

namespace LazySearch
{
    public class LazySearchMod : ModSystem
    {
        public static bool logDebug = false;
        ICoreClientAPI capi = null;
        ICoreServerAPI sapi = null;
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
        void printServer(string msg)
        {
            if (logDebug)
            {
                sapi?.Logger.Debug("|LazySearch|: " + msg);
                sapi?.BroadcastMessageToAllGroups("|LazySearch|: " + msg, EnumChatType.OwnMessage);
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

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            sapi = api;
        }
    }
}
