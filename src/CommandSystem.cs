using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using System.Threading;
using System.Diagnostics;


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
        { get; set; } = 2000;

        public static bool IsDownwardsSearch
        { get; set; } = false;

        private static readonly BlockPos spawnPos = new(0, 0, 0);

        private static Thread searchThread = null;
        private static CancellationTokenSource cts = null;

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
                cts.Cancel();
                Stopwatch startTime = Stopwatch.StartNew();
                while (searchThread.IsAlive)
                {
                    if (startTime.Elapsed.TotalSeconds > 3.0)
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

        private TextCommandResult CmdLazySearchDown(TextCommandCallingArgs args)
        {
            IsDownwardsSearch = true;
            return CmdLazySearch(args);
        }

        private TextCommandResult CmdLazySearchAllDirections(TextCommandCallingArgs args)
        {
            IsDownwardsSearch = false;
            return CmdLazySearch(args);
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
                cts.Cancel();
                Stopwatch startTime = Stopwatch.StartNew();
                while (searchThread.IsAlive)
                {
                    if (startTime.Elapsed.TotalSeconds > 3.0)
                    {
                        return TextCommandResult.Error(LsMsg("Lazy search interrupt timed out."));
                    }
                    Thread.Sleep(10);
                }
            }

            EntityPlayer byEntity = capi.World.Player.Entity;

            BlockPos playerPos = byEntity.Pos.AsBlockPos;
            BlockPosRenderer.SetSearchOrigin(playerPos);
            Vec3i maxWorldPos = capi.World.BlockAccessor.MapSize.Clone();
            if (IsDownwardsSearch) maxWorldPos.Y = int.Min(maxWorldPos.Y, playerPos.Y + 2); // only search downwards from players head position

            MsgPlayer("Starting Lazy search...");
            PrintClient("Player Pos: " + GetGameBlockPos(playerPos).ToString());

            BlockPosRenderer.ClearBlockPosList();

            IBlockAccessor bacc = capi.World.GetLockFreeBlockAccessor();
            string blockWord = (string)args.Parsers[1].GetValue();
            int MaxBlocksToUncover_temp = MaxBlocksToUncover;

            // Utilize thread pooling for full CPU utilization
            searchThread = new Thread(() =>
            {
                int totalBlocksFound = 0;
                float totalMaxSearchedRadius = 0.0f;
                float radius_f = (float)radius;

                object localVarLockObj = new();
                CountdownEvent countDownEvent = new(radius + 1); // +1 because shells go from 0 to radius
                cts = new CancellationTokenSource();
                CancellationToken token = cts.Token;
                Stopwatch threadRuntime = Stopwatch.StartNew();

                try
                {
                    int shell;
                    // Spawn one task per shell
                    for (shell = 0; shell <= radius; shell++)
                    {
                        BlockPosRenderer.SetShellSize(shell);
                        int s = shell; // Capture for lambda
                        ThreadPool.QueueUserWorkItem(_ =>
                        {
                            int localBlocksFound = 0;
                            float localMaxSearchedRadius = 0.0f;

                            try
                            {
                                ((System.Action)(() =>
                                {
                                    BlockPos bp;
                                    float tempRadius;
                                    Block b;
                                    string bName;
                                    int x, y, z;
                                    int x_world, y_world, z_world;
                                    bool valid_block_y, valid_block_yx, valid_block_yxz;

                                    // first do y, as this coordinate denotes the height and thus will hit the world limits first
                                    for (y = -s; y <= +s; y++)
                                    {
                                        y_world = y + playerPos.Y;
                                        if (y_world < 0 || y_world >= maxWorldPos.Y) continue;
                                        valid_block_y = (y == -s || y == +s);
                                        for (x = -s; x <= +s; x++)
                                        {
                                            x_world = x + playerPos.X;
                                            valid_block_yx = (valid_block_y || (x == -s || x == +s)) &&
                                            (x_world >= 0) && (x_world < maxWorldPos.X);
                                            for (z = -s; z <= +s; z++)
                                            {
                                                if (token.IsCancellationRequested) return; // return from from lambda-function

                                                z_world = z + playerPos.Z;
                                                valid_block_yxz = (valid_block_yx || (z == -s || z == +s)) &&
                                                (z_world >= 0) && (z_world < maxWorldPos.Z);

                                                // any on shell position, do:
                                                if (valid_block_yxz)
                                                {
                                                    lock (localVarLockObj)
                                                    {
                                                        if ((totalBlocksFound + localBlocksFound) >=
                                                            MaxBlocksToUncover_temp)
                                                        {
                                                            // already hit the maximum, just stop (update shared data beforehand)
                                                            totalBlocksFound += localBlocksFound;
                                                            totalMaxSearchedRadius = float.Max(totalMaxSearchedRadius,
                                                                localMaxSearchedRadius);
                                                            return; // return from from lambda-function
                                                        }
                                                    }

                                                    tempRadius = (new Vec3f(x, y, z)).Length();
                                                    if (tempRadius > radius_f)
                                                    {
                                                        // skip block in greater distance to player than given radius
                                                        // (as search volume is cube with sidelength 1+2*radius centered at player)
                                                        continue;
                                                    }
                                                    localMaxSearchedRadius = float.Max(localMaxSearchedRadius,
                                                        tempRadius);
                                                    bp = new BlockPos(x_world, y_world, z_world);
                                                    b = bacc.GetBlock(bp);
                                                    bName = b.Code?.GetName();
                                                    if (bName == null)
                                                    {
                                                        continue;
                                                    }
                                                    if (bName.Contains(blockWord))
                                                    {
                                                        capi.Event.EnqueueMainThreadTask(() => PrintClient("found '" +
                                                            bName + "' at: " + GetGameBlockPos(bp).ToString()), "");
                                                        localBlocksFound++;

                                                        // call BlockPosRenderer
                                                        BlockPosRenderer.PlotCoord(bp.Copy());
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    // Update shared data safely
                                    lock (localVarLockObj)
                                    {
                                        totalBlocksFound += localBlocksFound;
                                        totalMaxSearchedRadius = float.Max(totalMaxSearchedRadius,
                                            localMaxSearchedRadius);
                                    }
                                }))();
                            }
                            catch (ThreadAbortException)
                            {
                                capi.Event.EnqueueMainThreadTask(() =>
                                    MsgPlayer("Lazy search thread aborted (shell #" + s + ")"), "");
                            }
                            catch (ThreadInterruptedException)
                            {
                                capi.Event.EnqueueMainThreadTask(() =>
                                    MsgPlayer("Lazy search thread interrupted (shell #" + s + ")"), "");
                            }
                            finally
                            {
                                countDownEvent.Signal();
                            }
                        });

                        // Wait until ThreadPool has capacity before queueing next work item, thus processing smaller shell sizes first
                        while (ThreadPool.PendingWorkItemCount > 0)
                        {
                            lock (localVarLockObj)
                            {
                                if ((totalBlocksFound >= MaxBlocksToUncover_temp) || token.IsCancellationRequested)
                                {
                                    // Cancel queuing of further work items
                                    cts?.Cancel();
                                    break;
                                }
                            }
                            Thread.Sleep(1); // Small sleep to prevent busy waiting
                        }
                        if (token.IsCancellationRequested) break; // break from for loop (iterating over shells)
                    }

                    // Wait for completion
                    if (token.IsCancellationRequested)
                    {
                        countDownEvent.Signal(radius - shell); // trigger remaining signals to allow finishing
                    }
                    countDownEvent.Wait();
                }
                catch (System.Exception)
                {
                    cts?.Cancel();
                    if (countDownEvent.CurrentCount > 0)
                    {
                        capi.Event.EnqueueMainThreadTask(() =>
                            MsgPlayer("Still " + countDownEvent.CurrentCount + " signals are open to be received."), "");
                    }
                }
                finally
                {
                    cts?.Dispose();
                }

                capi.Event.EnqueueMainThreadTask(() => PrintClient("Lazy search done."), "");
                string maxAmountHit = (totalBlocksFound < MaxBlocksToUncover_temp) ? "" :
                        " (limited by maximal block highlight criterion (set to " + MaxBlocksToUncover_temp +
                        "), check .lz_mb to change)";

                capi.Event.EnqueueMainThreadTask(() => MsgPlayer("Found " + totalBlocksFound + " blocks with '" +
                        blockWord + "' in " + threadRuntime.Elapsed.TotalSeconds.ToString("F3") +
                        " seconds. Max search radius: " + totalMaxSearchedRadius.ToString("F1") + "" +
                        maxAmountHit), "");
            });

            searchThread.Start();
            return TextCommandResult.Success();
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            capi = api;
            CommandArgumentParsers parsers = api.ChatCommands.Parsers;

            api.ChatCommands.Create("lz_st")
                .WithDescription("lz_st: stops the currently running search")
                .RequiresPrivilege(Privilege.chat).RequiresPlayer().HandleWith((args) => CmdStopSearch(args, false));

            api.ChatCommands.Create("lz_cl")
                .WithDescription("lz_cl: clears any visible highlights")
                .RequiresPrivilege(Privilege.chat).RequiresPlayer().HandleWith(CmdClearHighlights);

            api.ChatCommands.Create("lz_mb")
                .WithDescription("lz_mb: get/set maximal block-highlights as criterion for searching")
                .WithArgs(parsers.OptionalInt("max blocks to uncover")).RequiresPrivilege(Privilege.chat)
                .RequiresPlayer().HandleWith(CmdMaximalBlocks);

            api.ChatCommands.Create("lz")
                .WithDescription("lz: searches for blocks in the world")
                .WithArgs(parsers.Int("radius from player position"),
                    parsers.Word("string (word) searched in block path"))
                .RequiresPrivilege(Privilege.chat).RequiresPlayer().HandleWith(CmdLazySearchAllDirections);

            api.ChatCommands.Create("lzd")
                .WithDescription("lzd: searches for blocks in the world, but below the players head")
                .WithArgs(parsers.Int("radius from player position"),
                    parsers.Word("string (word) searched in block path"))
                .RequiresPrivilege(Privilege.chat).RequiresPlayer().HandleWith(CmdLazySearchDown);
        }

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Client;
        }

        public override void Dispose()
        {
            // stop currently running thread
            if (searchThread != null && searchThread.IsAlive)
            {
                searchThread.Interrupt();
            }
            searchThread.Join();

            base.Dispose();
        }
    }
}
