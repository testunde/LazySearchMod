using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;


namespace LazySearch
{
    public class CommandSystem : ModSystem
    {
        private ICoreClientAPI capi = null;

        public static string LsMsg(string msg) { return "|LazySearch|: " + msg; }

        private void PrintClient(string msg)
        {
            if (LazySearchMod.LogDebug)
            {
                capi?.Logger.Debug(LsMsg(msg));
                capi?.ShowChatMessage(LsMsg(msg));
            }
        }

        public static int MaxBlocksToUncover
        { get; set; } = 100;

        private static readonly BlockPos spawnPos = new(0, 0, 0);

        private static BlockPos GetGameBlockPos(BlockPos worldBP)
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
            int oldBlockCount = BlockPosRenderer.GetBlockCount();
            BlockPosRenderer.ClearBlockPosList();
            return TextCommandResult.Success(LsMsg("Cleared " + oldBlockCount + " highlights."));
        }

        private TextCommandResult CmdMaximalBlocks(TextCommandCallingArgs args)
        {
            if (args.ArgCount > 1)
            {
                return TextCommandResult.Success(LsMsg("Syntax is: .lz_mb [maxBlocks]"));
            }
            if (args.ArgCount == 0 || args.Parsers[0].IsMissing)
            {
                return TextCommandResult.Success(LsMsg("current 'maxBlocks': " + MaxBlocksToUncover));
            }
            int maxBlocks = (int)args.Parsers[0].GetValue();
            if (maxBlocks <= 0)
            {
                return TextCommandResult.Error(LsMsg("Argument 'maxBlocks' has to be a non-zero positive integer."));
            }

            string outString = "set 'maxBlocks' from " + MaxBlocksToUncover + " to: " + maxBlocks;
            MaxBlocksToUncover = maxBlocks;

            // remove already visible block highlights but are over new limit
            BlockPosRenderer.DeleteAllBlockPositionsButFirstN(maxBlocks);
            return TextCommandResult.Success(LsMsg(outString));
        }

        private TextCommandResult CmdLazySearch(TextCommandCallingArgs args)
        {
            if (args.ArgCount < 2 || args.ArgCount > 2 || args.Parsers[0].IsMissing || args.Parsers[1].IsMissing)
            {
                return TextCommandResult.Success(LsMsg("Syntax is: .lz <radius> <blockWord>"));
            }
            int radius = (int)args.Parsers[0].GetValue();
            if (radius <= 0)
            {
                return TextCommandResult.Error(LsMsg("Argument 'maxBlocks' has to be a non-zero positive integer."));
            }
            EntityPlayer byEntity = capi.World.Player.Entity;
            BlockPos playerPos = byEntity.Pos.AsBlockPos;

            PrintClient("=&gt; Starting lazy search...");
            PrintClient("Player Pos: " + GetGameBlockPos(playerPos).ToString());

            int blocksFound = 0;
            float searchedRadius = 0.0f;
            BlockPosRenderer.ClearBlockPosList();

            IBlockAccessor bacc = capi.World.GetBlockAccessor(false, false, false);
            BlockPos bp;
            float tempRadius;
            Block b;
            string bName;
            float radius_f = (float)radius;
            bool valid_block;
            int x, y, z;
            string blockWord = (string)args.Parsers[1].GetValue();

            ((System.Action)(() =>
            {
                for (int s = 0; s <= radius; s++) // s="shell"
                {
                    for (x = -s; x <= +s; x++)
                    {
                        for (y = -s; y <= +s; y++)
                        {
                            for (z = -s; z <= +s; z++)
                            {
                                valid_block = (x == -s || x == +s) || (y == -s || y == +s) || (z == -s || z == +s);
                                // any on shell
                                if (valid_block)
                                {
                                    if (blocksFound >= MaxBlocksToUncover)
                                    {
                                        // already hit the maximum, just stop
                                        return; // return from lambda-function
                                    }
                                    else
                                    {
                                        bp = new BlockPos(x, y, z);
                                        tempRadius = bp.ToVec3f().Length();
                                        if (tempRadius > radius_f)
                                        {
                                            // skip block in greater distance to player than given radius
                                            // (as search volume is cube with sidelength 1+2*radius centered at player)
                                            continue;
                                        }
                                        searchedRadius = tempRadius;
                                        bp += playerPos;
                                        b = bacc.GetBlock(bp);
                                        bName = b.Code.GetName();
                                        if (bName.Contains(blockWord))
                                        {
                                            PrintClient("found '" + bName + "' at: " + GetGameBlockPos(bp).ToString());
                                            blocksFound++;

                                            // call BlockPosRenderer
                                            BlockPosRenderer.PlotCoord(bp.Copy());
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }))();

            PrintClient("=&gt; Lazy search done.");
            string maxAmountHit = (blocksFound < MaxBlocksToUncover) ? "" :
                " (limited by maximal block highlight number, check .lz_mb to change)";
            return TextCommandResult.Success(LsMsg("Found " + blocksFound + " blocks with '" + args[1] +
                "'. Max Search radius: " + searchedRadius.ToString("F1") + "" + maxAmountHit));
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
                .WithArgs(parsers.Int("radius from player position"),
                    parsers.Word("string (word) searched in block path"))
                .RequiresPrivilege(Privilege.chat).RequiresPlayer().HandleWith(CmdLazySearch);
        }

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Client;
        }
    }
}
