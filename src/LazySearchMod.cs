using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

[assembly: ModInfo("LazySearch",
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
        void printClient(string msg)
        {
            if (logDebug)
            {
                capi?.Logger.Warning("|TESTYC|: " + msg);
                capi?.ShowChatMessage("|TESTYC|: " + msg);
            }
        }
        void printServer(string msg)
        {
            if (logDebug)
            {
                sapi?.Logger.Warning("|TESTYC|: " + msg);
                sapi?.BroadcastMessageToAllGroups("|TESTYC|: " + msg, EnumChatType.OwnMessage);
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
