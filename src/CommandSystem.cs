using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using System.Threading;


namespace LazySearch
{
    public class CommandSystem : ModSystem
    {
        private ICoreClientAPI capi = null;

        public static string LsMsg(string msg) { return "|LazySearch|: " + msg; }

        void MsgPlayer(string msg)
        {
            capi?.ShowChatMessage(LsMsg(msg));
            PrintClient(msg, true);
        }

        private void PrintClient(string msg, bool playerAlreadyNotified = false)
        {
            if (LazySearchMod.LogDebug)
            {
                capi?.Logger.Debug(LsMsg(msg));
                if (!playerAlreadyNotified)
                {
                    capi?.ShowChatMessage(LsMsg("=&gt; " + msg));
                }
            }
        }

        public static int MaxBlocksToUncover
        { get; set; } = 100;

        private static readonly BlockPos spawnPos = new(0, 0, 0);

        private static Thread searchThread = null;

        private static BlockPos GetGameBlockPos(BlockPos worldBP)
        {
            BlockPos bp = worldBP.Copy();
            return bp - new BlockPos(spawnPos.X, 0, spawnPos.Z);
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
        }

        private TextCommandResult CmdStopSearch(TextCommandCallingArgs _args, bool silent = false)
        {
            if (searchThread != null && searchThread.IsAlive)
            {
                searchThread.Interrupt();
                var startTime = System.DateTime.Now;
                while (searchThread.IsAlive)
                {
                    if ((System.DateTime.Now - startTime).TotalSeconds > 3.0)
                    {
                        return TextCommandResult.Error(silent ? "" : LsMsg("Lazy search interrupt timed out."));
                    }
                    Thread.Sleep(10);
                }
                return TextCommandResult.Success(silent ? "" : LsMsg("Lazy search interrupt executed."));
            }
            return TextCommandResult.Success(silent ? "" : LsMsg("No Lazy search running."));
        }

        private TextCommandResult CmdClearHighlights(TextCommandCallingArgs args)
        {
            CmdStopSearch(null, true);
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

            // stop currently running thread
            if (searchThread != null && searchThread.IsAlive)
            {
                MsgPlayer("Lazy search already running. Interrupting current search...");
                searchThread.Interrupt();
                var startTime = System.DateTime.Now;
                while (searchThread.IsAlive)
                {
                    if ((System.DateTime.Now - startTime).TotalSeconds > 3.0)
                    {
                        return TextCommandResult.Error(LsMsg("Lazy search interrupt timed out."));
                    }
                    Thread.Sleep(10);
                }
            }

            EntityPlayer byEntity = capi.World.Player.Entity;
            BlockPos playerPos = byEntity.Pos.AsBlockPos;

            MsgPlayer("Starting Lazy search...");
            PrintClient("Player Pos: " + GetGameBlockPos(playerPos).ToString());

            BlockPosRenderer.ClearBlockPosList();

            IBlockAccessor bacc = capi.World.GetBlockAccessor(false, false, false);
            string blockWord = (string)args.Parsers[1].GetValue();
            int MaxBlocksToUncover_temp = MaxBlocksToUncover;

            // Create new thread with the search operation
            searchThread = new Thread(() =>
            {
                int blocksFound = 0;
                float searchedRadius = 0.0f;

                BlockPos bp;
                float tempRadius;
                Block b;
                string bName;
                float radius_f = (float)radius;
                bool valid_block_x, valid_block_xy, valid_block_xyz;
                int x, y, z;

                try
                {
                    ((System.Action)(() =>
                    {
                        for (int s = 0; s <= radius; s++) // s="shell"
                        {
                            for (x = -s; x <= +s; x++)
                            {
                                valid_block_x = (x == -s || x == +s);
                                for (y = -s; y <= +s; y++)
                                {
                                    if (Thread.CurrentThread.IsAlive &&
                                        Thread.CurrentThread.ThreadState == ThreadState.Running)
                                    {
                                        Thread.Sleep(0); // Allow interruption to be processed
                                    }

                                    valid_block_xy = valid_block_x || (y == -s || y == +s);
                                    for (z = -s; z <= +s; z++)
                                    {
                                        valid_block_xyz = valid_block_xy || (z == -s || z == +s);
                                        // any on shell
                                        if (valid_block_xyz)
                                        {
                                            if (blocksFound >= MaxBlocksToUncover_temp)
                                            {
                                                // already hit the maximum, just stop
                                                return; // return from from lambda-function
                                            }

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
                                            bName = b.Code?.GetName();
                                            if (bName == null)
                                            {
                                                continue;
                                            }
                                            if (bName.Contains(blockWord))
                                            {
                                                capi.Event.EnqueueMainThreadTask(() => PrintClient("found '" + bName +
                                                    "' at: " + GetGameBlockPos(bp).ToString()), "");
                                                blocksFound++;

                                                // call BlockPosRenderer
                                                BlockPosRenderer.PlotCoord(bp.Copy());
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }))();

                    capi.Event.EnqueueMainThreadTask(() => PrintClient("Lazy search done."), "");
                    string maxAmountHit = (blocksFound < MaxBlocksToUncover_temp) ? "" :
                        " (limited by maximal block highlight number, check .lz_mb to change)";

                    capi.Event.EnqueueMainThreadTask(() => MsgPlayer("Found " + blocksFound + " blocks with '" +
                        blockWord + "'. Max search radius: " + searchedRadius.ToString("F1") + "" + maxAmountHit), "");
                }
                catch (ThreadAbortException)
                {
                    capi.Event.EnqueueMainThreadTask(() => MsgPlayer("Lazy search interrupted. (" +
                        BlockPosRenderer.GetBlockCount() + " Blocks found)"), "");
                }
                catch (ThreadInterruptedException)
                {
                    capi.Event.EnqueueMainThreadTask(() => MsgPlayer("Lazy search interrupted. (" +
                        BlockPosRenderer.GetBlockCount() + " Blocks found)"), "");
                }
            });

            searchThread.Start();
            return TextCommandResult.Success();
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            capi = api;
            var parsers = api.ChatCommands.Parsers;

            api.ChatCommands.Create("lz_stop").WithDescription("lz_stop: stops the currently running search")
                .RequiresPrivilege(Privilege.chat).RequiresPlayer().HandleWith((args) => CmdStopSearch(args, false));

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
