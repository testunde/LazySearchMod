using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;


namespace LazySearch
{
    public class CommandSystem : ModSystem
    {
        ICoreClientAPI capi = null;
        void msgPlayer(string msg)
        {
            capi?.ShowChatMessage("|LazySearch|: " + msg);
        }
        void printClient(string msg)
        {
            if (LazySearchMod.logDebug)
            {
                capi?.Logger.Debug("|LazySearch|: " + msg);
                capi?.ShowChatMessage("|LazySearch|: " + msg);
            }
        }

        public static int maxBlocksToUncover
        { get; set; } = 100;

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

            api.RegisterCommand("lz_mb", "get/set maximal blocks to highlight", "[optional:maxBlocks]",
                (int groupId, CmdArgs args) =>
                {
                    int maxBlocks = 0; // actual initialization-value does not matter
                    if (args.Length > 1 || (args.Length == 1 && !int.TryParse(args[0], out maxBlocks)))
                    {
                        msgPlayer("Syntax is: .lz_mb optional:maxBlocks");
                        return;
                    }
                    if (args.Length == 0)
                    {
                        msgPlayer("current 'maxBlocks': " + maxBlocksToUncover);
                        return;
                    }
                    if (maxBlocks <= 0)
                    {
                        msgPlayer("Argument 'maxBlocks' has to be a non-zero positive integer.");
                        return;
                    }

                    msgPlayer("set 'maxBlocks' from " + maxBlocksToUncover + " to: " + maxBlocks);
                    maxBlocksToUncover = maxBlocks;

                    // remove already visible block highlights but are over new limit
                    BlockPosRenderer.delAllBlockPositionsButFirstN(maxBlocks);
                });

            api.RegisterCommand("lz", "searches for blocks in the world", "[radius from player position] [string which is searched in block path]",
                (int groupId, CmdArgs args) =>
                    {
                        int radius = 20; // actual initialization-value does not matter
                        if (args.Length < 1 || !int.TryParse(args[0], out radius) || (args.Length != 2 && radius >= 0))
                        {
                            msgPlayer("Syntax is: .lz radius blockString");
                            return;
                        }
                        if (radius < 0)
                        {
                            msgPlayer("Clearing highlights.");
                            BlockPosRenderer.clearBlockPosList();
                            return;
                        }
                        EntityPlayer byEntity = capi.World.Player.Entity;
                        BlockPos playerPos = byEntity.Pos.AsBlockPos;

                        printClient("=&gt; Starting lazy search...");
                        printClient("Player Pos: " + getGameBlockPos(playerPos).ToString());

                        BlockPos minPos = playerPos - radius;
                        BlockPos maxPos = playerPos + radius;

                        int blocksFound = 0;
                        float searchedRadius = 0.0f;
                        BlockPosRenderer.clearBlockPosList();

                        api.World.GetBlockAccessor(false, false, false).WalkBlocks(minPos, maxPos,
                        (Block b, BlockPos bp) =>
                        {
                            if (blocksFound >= maxBlocksToUncover)
                            {
                                // already hit the maximum, just skip the rest
                            }
                            else
                            {
                                float tempRadius = bp.DistanceTo(playerPos);
                                if (tempRadius <= ((float)radius))
                                {
                                    searchedRadius = tempRadius;
                                    string bName = b.Code.GetName();
                                    if (bName.Contains(args[1]))
                                    {
                                        printClient("found '" + bName + "' at: " + getGameBlockPos(bp).ToString());
                                        blocksFound++;

                                        // call BlockPosRenderer
                                        BlockPosRenderer.plotCoord(bp.Copy());
                                    }
                                }
                                else
                                {
                                    // skip block in greater distance to player than given radius
                                    // (as search volume is cube with sidelength 1+2*radius centered at player)
                                }
                            }
                        }, true);

                        printClient("=&gt; Lazy search done.");
                        string maxAmountHit = (blocksFound < maxBlocksToUncover) ? "" : " (limited by maximal block highlight number, check .lz_mb to change)";
                        msgPlayer("Found " + blocksFound + " blocks with '" + args[1] + "'. Max Search radius: " + searchedRadius + "" + maxAmountHit);
                    });
        }

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Client;
        }
    }
}
