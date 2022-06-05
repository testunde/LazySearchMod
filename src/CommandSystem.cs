using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;


namespace LazySearch
{
    public class CommandSystem : ModSystem
    {
        ICoreClientAPI capi = null;
        ICoreServerAPI sapi = null;
        void printClient(string msg)
        {
            if (LazySearchMod.logDebug)
            {
                capi?.Logger.Warning("|TESTYC|: " + msg);
                capi?.ShowChatMessage("|TESTYC|: " + msg);
            }
        }
        void printServer(string msg)
        {
            if (LazySearchMod.logDebug)
            {
                sapi?.Logger.Warning("|TESTYC|: " + msg);
                sapi?.BroadcastMessageToAllGroups("|TESTYC|: " + msg, EnumChatType.OwnMessage);
            }
        }

        public static int maxBlocksToUncover
        {
            get { return 100; }
        }

        static BlockPos spawnPos = new BlockPos(0, 0, 0);
        static BlockPos getGameBlockPos(BlockPos worldBP)
        {
            BlockPos bp = worldBP.Copy();
            return bp - new BlockPos(spawnPos.X, 0, spawnPos.Z);
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            capi = api;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            sapi = api;

            api.RegisterCommand("lz", "searches for blocks in the world", "",
                (IServerPlayer player, int groupId, CmdArgs args) =>
                    {
                        int radius = 20;
                        if (args.Length != 2 || !int.TryParse(args[0], out radius))
                        {
                            printServer("Syntax is: /lz radius blockString");
                            return;
                        }
                        EntityPlayer byEntity = player.Entity;
                        BlockPos playerPos = byEntity.Pos.AsBlockPos;
                        spawnPos = new BlockPos(player.GetSpawnPosition(false).XYZInt);

                        printServer("=&gt; Starting lazy search...");
                        printServer("Player Pos: " + getGameBlockPos(playerPos).ToString());

                        BlockPos minPos = playerPos - radius;
                        BlockPos maxPos = playerPos + radius;

                        int blocksFound = 0;
                        BlockPosRenderer.clearBlockPosList();

                        api.World.GetBlockAccessor(false, false, false).WalkBlocks(minPos, maxPos,
                        (Block b, BlockPos bp) =>
                        {
                            if (blocksFound >= maxBlocksToUncover)
                            {
                                return;
                            }

                            string bName = b.Code.GetName();
                            if (bName.Contains(args[1]))
                            {
                                printServer("found '" + bName + "' at: " + getGameBlockPos(bp).ToString());
                                blocksFound++;

                                // call BlockPosRenderer
                                BlockPosRenderer.plotCoord(bp.Copy());
                            }
                        }, true);

                        printServer("=&gt; Lazy search done.");
                    }, Privilege.chat);
        }
        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }
    }
}
