using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using System.Collections.Generic;
using System;

namespace LazySearch
{
    public class BlockPosRenderer : ModSystem, IRenderer
    {
        private ICoreClientAPI capi;
        private IRenderAPI rpi;
        private MeshRef mRefBoundingBox = null;
        private MeshRef mRefHighlight = null;
        private IShaderProgram prog = null;
        private static readonly List<BlockPos> bPosList = new();
        private static readonly Vec3d searchOrigin = new();
        private static readonly object shellSizeLock = new();
        private static int shellSize = -1;
        private readonly int frameColorHighlight = ColorUtil.ToRgba(255, 255, 0, 255);
        private readonly int frameColorBoundingBox = ColorUtil.ToRgba(127, 255, 127, 0);

        public double RenderOrder
        {
            get { return 0.01; }
        }

        public int RenderRange
        {
            get { return 99; }
        }

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            capi = api;
            rpi = capi.Render;
            capi.Event.RegisterRenderer(this, EnumRenderStage.AfterBlit);
        }

        public static int GetBlockCount()
        {
            lock (bPosList)
            {
                return bPosList.Count;
            }
        }

        public static void PlotCoord(BlockPos bp)
        {
            lock (bPosList)
            {
                // TODO: instead of only keeping the block positions, already build the mesh data here
                bPosList.Add(bp.Copy());
            }
        }

        public static void SetSearchOrigin(BlockPos origin)
        {
            searchOrigin.Set(origin.ToVec3d());
        }

        public static void SetShellSize(int size)
        {
            lock (shellSizeLock)
            {
                shellSize = size;
            }
        }

        public static void ClearBlockPosList()
        {
            lock (bPosList)
            {
                bPosList.Clear();
            }
            lock (shellSizeLock)
            {
                shellSize = -1;
            }
        }

        public static void DeleteAllBlockPositionsButFirstN(int n)
        {
            lock (bPosList)
            {
                if (bPosList.Count <= n) return; // nothing to do

                int nToDelete = bPosList.Count - n;
                bPosList.RemoveRange(n, nToDelete);
            }
        }

        private void InitShaderProgram()
        {
            // mesh data for bounding box
            if (mRefBoundingBox != null && !mRefBoundingBox.Disposed) mRefBoundingBox.Dispose();
            MeshData mDataBoundingBox = LineMeshUtil.GetCube(frameColorBoundingBox);
            mDataBoundingBox.Scale(new Vec3f(1f, 1f, 1f), 0.5f, 0.5f, 0.5f);
            mDataBoundingBox.Flags = new int[mDataBoundingBox.VerticesCount];
            for (int i = 0; i < mDataBoundingBox.Flags.Length; i++)
            {
                mDataBoundingBox.Flags[i] = 256;
            }
            mDataBoundingBox.Flags = new int[24];
            mDataBoundingBox.CompactBuffers();
            mRefBoundingBox = rpi.UploadMesh(mDataBoundingBox);

            // mesh ref for block highlighting
            if (mRefHighlight != null && !mRefHighlight.Disposed) mRefHighlight.Dispose();
            MeshData mDataHighlight = LineMeshUtil.GetCube(frameColorHighlight);
            mDataHighlight.Scale(new Vec3f(1f, 1f, 1f), 0.5f, 0.5f, 0.5f);
            mDataHighlight.Flags = new int[mDataHighlight.VerticesCount];
            for (int i = 0; i < mDataHighlight.Flags.Length; i++)
            {
                mDataHighlight.Flags[i] = 256;
            }
            mDataHighlight.Flags = new int[24];
            mDataHighlight.CompactBuffers();
            mRefHighlight = rpi.UploadMesh(mDataHighlight);

            // shader program
            if (prog != null && !prog.Disposed) prog.Dispose();
            prog = rpi.GetEngineShader(EnumShaderProgram.Wireframe);
        }

        private float getLineWidth(float distance, float sizeExponentOffset = 0.0f)
        {
            return 1.6f * float.Pow(2f, 5f + sizeExponentOffset) / (distance + 0.01f);
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            EntityPlayer plr = capi.World.Player.Entity;
            if (plr.Properties.Client.Renderer is not EntityShapeRenderer) return;

            // create copy of list as otherwise it could be modified during iteration
            List<BlockPos> bPosList_local;
            lock (bPosList)
            {
                bPosList_local = new(bPosList);
            }

            if (mRefHighlight == null || mRefHighlight.Disposed || prog == null || prog.Disposed)
            {
                InitShaderProgram();
            }

            Vec3d plPos = plr.CameraPos + plr.CameraPosOffset;
            double[] camOrigin = rpi.CameraMatrixOrigin;

            // arm shader program
            prog.Use();
            prog.Uniform("colorIn", ColorUtil.WhiteArgbVec);
            prog.UniformMatrix("projectionMatrix", rpi.CurrentProjectionMatrix);
            prog.Uniform("origin", Vec3f.Zero);

            // draw bounding box
            DrawBoundingBox(plPos, camOrigin);

            // draw highlight boxes
            foreach (BlockPos bp in bPosList_local)
            {
                HighlightBlock(bp?.ToVec3d(), plPos, camOrigin);
            }

            // stop shader program
            prog.Stop();

            // reset line width to default value
            rpi.LineWidth = 1.6f;
        }

        // draws bounding box as "grand" box in which the player is shown the current search shell (respect upper/lower bounds)
        private void DrawBoundingBox(Vec3d plPos, double[] camOrigin)
        {
            int shellSize_temp;
            lock (shellSizeLock)
            {
                shellSize_temp = shellSize;
            }
            if (shellSize_temp >= 0)
            {
                // TODO: move mesh building into SetShellSize() and only do translation here
                double shellDiameter = shellSize_temp * 2 + 1;
                Vec3d scale = new(shellDiameter, shellDiameter, shellDiameter);
                // rendering is based on player position, to translate render target-position from world frame into render frame
                Vec3d posDiff = searchOrigin - 0.5 * scale;
                Vec3d maxWorldPos = new(capi.World.BlockAccessor.MapSizeX, capi.World.BlockAccessor.MapSizeY,
                    capi.World.BlockAccessor.MapSizeZ);

                // only search downwards from players head position (player == search origin); excluding 0.5 added later
                if (CommandSystem.IsDownwardsSearch) maxWorldPos.Y = searchOrigin.Y + 2.0 - 0.5;

                // clamp to valid coordinates (lower bound)
                posDiff.X = Math.Max(0, posDiff.X);
                posDiff.Y = Math.Max(0, posDiff.Y);
                posDiff.Z = Math.Max(0, posDiff.Z);

                // clamp scale if would exceed max world size (upper bound)
                if (posDiff.X + scale.X > maxWorldPos.X) scale.X = maxWorldPos.X - posDiff.X;
                if (posDiff.Y + scale.Y > maxWorldPos.Y) scale.Y = maxWorldPos.Y - posDiff.Y;
                if (posDiff.Z + scale.Z > maxWorldPos.Z) scale.Z = maxWorldPos.Z - posDiff.Z;
                posDiff -= plPos;

                // correct for half block offset
                posDiff.Add(0.5);

                double[] modelMat_h = Mat4d.Create();
                Mat4d.Translate(modelMat_h, camOrigin, posDiff.X, posDiff.Y, posDiff.Z);
                Mat4d.Scale(modelMat_h, scale.X, scale.Y, scale.Z);
                prog.UniformMatrix("modelViewMatrix", Array.ConvertAll(modelMat_h, x => (float)x));
                rpi.LineWidth = getLineWidth(shellSize_temp, 2); // reduce line width with shell size (implying a more distance bounding box)
                rpi.RenderMesh(mRefBoundingBox);

                // draw smaller box indicating search origin
                double originBlockSize = 0.5;
                Vec3d originPosDiff = searchOrigin - plPos + new Vec3d(originBlockSize / 2, originBlockSize / 2, originBlockSize / 2);
                double[] modelMat_origin = Mat4d.Create();
                Mat4d.Translate(modelMat_origin, camOrigin, originPosDiff.X, originPosDiff.Y, originPosDiff.Z);
                Mat4d.Scale(modelMat_origin, originBlockSize, originBlockSize, originBlockSize);
                prog.UniformMatrix("modelViewMatrix", Array.ConvertAll(modelMat_origin, x => (float)x));
                rpi.LineWidth = getLineWidth((float)originPosDiff.Length(), 4); // reduce line width with distance to player
                rpi.RenderMesh(mRefBoundingBox);
            }
        }

        private void HighlightBlock(Vec3d bPos, Vec3d plPos, double[] camOrigin)
        {
            if (bPos == null) return;
            double[] modelMat_h = Mat4d.Create();
            // rendering is based on player position, to translate render target-position from world frame into render frame
            Vec3d posDiff = bPos - plPos;
            Mat4d.Translate(modelMat_h, camOrigin, posDiff.X, posDiff.Y, posDiff.Z);

            prog.UniformMatrix("modelViewMatrix", Array.ConvertAll(modelMat_h, x => (float)x));
            rpi.LineWidth = getLineWidth((float)posDiff.Length()); // reduce line width with distance to player
            rpi.RenderMesh(mRefHighlight);
        }

        public override void Dispose()
        {
            bPosList.Clear();
            mRefBoundingBox?.Dispose();
            shellSize = -1;
            searchOrigin.Mul(0.0);
            mRefHighlight?.Dispose();
            prog?.Dispose();

            base.Dispose();
        }
    }
}
