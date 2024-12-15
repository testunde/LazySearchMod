using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;


namespace LazySearch
{
    public class CommandSystem : ModSystem
    {
        ICoreClientAPI capi = null;

        string lsMsg(string msg) { return "|LazySearch|: " + msg; }

        void printClient(string msg)
        {
            if (LazySearchMod.logDebug)
            {
                capi?.Logger.Debug(lsMsg(msg));
                capi?.ShowChatMessage(lsMsg(msg));
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

        private TextCommandResult CmdClearHighlights(TextCommandCallingArgs args)
        {
            int oldBlockCount = BlockPosRenderer.getBlockCount();
            BlockPosRenderer.clearBlockPosList();
            return TextCommandResult.Success(lsMsg("Cleared " + oldBlockCount + " highlights."));
        }

        private TextCommandResult CmdMaximalBlocks(TextCommandCallingArgs args)
        {
            int maxBlocks = 0; // actual initialization-value does not matter
            if (args.ArgCount > 1 || (args.ArgCount == 1 && !int.TryParse(args[0].ToString(), out maxBlocks)))
            {
                return TextCommandResult.Success(lsMsg("Syntax is: .lz_mb [maxBlocks]"));
            }
            if (args.ArgCount == 0)
            {
                return TextCommandResult.Success(lsMsg("current 'maxBlocks': " + maxBlocksToUncover));
            }
            if (maxBlocks <= 0)
            {
                return TextCommandResult.Error(lsMsg("Argument 'maxBlocks' has to be a non-zero positive integer."));
            }

            maxBlocksToUncover = maxBlocks;

            // remove already visible block highlights but are over new limit
            BlockPosRenderer.delAllBlockPositionsButFirstN(maxBlocks);
            return TextCommandResult.Success(lsMsg("set 'maxBlocks' from " + maxBlocksToUncover + " to: " + maxBlocks));
        }

        private TextCommandResult CmdLazySearch(TextCommandCallingArgs args)
        {
            int radius;
            if (args.ArgCount < 1 || !int.TryParse(args[0].ToString(), out radius) || (args.ArgCount != 2 && radius >= 0)) // TODO: check for ToString() being neccessary
            {
                return TextCommandResult.Success(lsMsg("Syntax is: .lz <radius> <blockWord>"));
            }
            if (radius <= 0)
            {
                return TextCommandResult.Error(lsMsg("Argument 'maxBlocks' has to be a non-zero positive integer."));
            }
            EntityPlayer byEntity = capi.World.Player.Entity;
            BlockPos playerPos = byEntity.Pos.AsBlockPos;

            printClient("=&gt; Starting lazy search...");
            printClient("Player Pos: " + getGameBlockPos(playerPos).ToString());

            int blocksFound = 0;
            float searchedRadius = 0.0f;
            BlockPosRenderer.clearBlockPosList();

            IBlockAccessor bacc = capi.World.GetBlockAccessor(false, false, false);
            BlockPos bp;
            float tempRadius;
            Block b;
            string bName;
            float radius_f = (float)radius;
            bool valid_block;
            int x, y, z;
            string blockWord = args[1].ToString();

            for (int s = 0; s <= radius; s++) // s="shell"
            {
                for (x = -s; x <= +s; x++)
                {
                    for (y = -s; y <= +s; y++)
                    {
                        for (z = -s; z <= +s; z++)
                        {
                            valid_block = (x == -s || x == +s) || (y == -s || y == +s) || (z == -s || z == +s);  // any on shell
                            if (valid_block)
                            {
                                if (blocksFound >= maxBlocksToUncover)
                                {
                                    // already hit the maximum, just stop
                                    goto endQuadrupleLoop; // sry for using goto x)
                                }
                                else
                                {
                                    bp = (new BlockPos(x, y, z)) + playerPos;
                                    tempRadius = bp.DistanceTo(playerPos);
                                    if (tempRadius > radius_f)
                                    {
                                        // skip block in greater distance to player than given radius
                                        // (as search volume is cube with sidelength 1+2*radius centered at player)
                                        continue;
                                    }
                                    searchedRadius = tempRadius;
                                    b = bacc.GetBlock(bp);
                                    bName = b.Code.GetName();
                                    if (bName.Contains(blockWord))
                                    {
                                        printClient("found '" + bName + "' at: " + getGameBlockPos(bp).ToString());
                                        blocksFound++;

                                        // call BlockPosRenderer
                                        BlockPosRenderer.plotCoord(bp.Copy());
                                    }
                                }
                            }
                        }
                    }
                }
            }
        endQuadrupleLoop:

            printClient("=&gt; Lazy search done.");
            string maxAmountHit = (blocksFound < maxBlocksToUncover) ? "" : " (limited by maximal block highlight number, check .lz_mb to change)";
            return TextCommandResult.Success(lsMsg("Found " + blocksFound + " blocks with '" + args[1] + "'. Max Search radius: " + searchedRadius + "" + maxAmountHit));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            capi = api;
            var parsers = api.ChatCommands.Parsers;

            api.ChatCommands.Create("lz_cl").WithDescription("lz_cl: clears any visible highlights")
                .RequiresPrivilege(Privilege.chat).RequiresPlayer().HandleWith(CmdClearHighlights);

            api.ChatCommands.Create("lz_mb").WithDescription("lz_mb: get/set maximal blocks to highlight")
                .WithArgs(parsers.OptionalInt("max blocks to uncover")).RequiresPrivilege(Privilege.chat)
                .RequiresPlayer().HandleWith(CmdMaximalBlocks);

            api.ChatCommands.Create("lz").WithDescription("lz: searches for blocks in the world")
                .WithArgs(parsers.Int("radius from player position"), parsers.Word("string (word) searched in block path"))
                .RequiresPrivilege(Privilege.chat).RequiresPlayer().HandleWith(CmdLazySearch);
        }

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Client;
        }
    }
}
