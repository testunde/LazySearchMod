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
        private MeshRef mRef = null;
        private IShaderProgram prog = null;
        private static readonly List<BlockPos> bPosList = new();
        private readonly int frameColor = ColorUtil.ToRgba(255, 255, 0, 255);

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

        public static void ClearBlockPosList()
        {
            lock (bPosList)
            {
                bPosList.Clear();
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
            if (mRef != null && !mRef.Disposed) mRef.Dispose();
            MeshData data = LineMeshUtil.GetCube(frameColor);
            data.Scale(new Vec3f(1f, 1f, 1f), 0.5f, 0.5f, 0.5f);
            data.Flags = new int[data.VerticesCount];
            for (int i = 0; i < data.Flags.Length; i++)
            {
                data.Flags[i] = 256;
            }
            data.Flags = new int[24];
            mRef = rpi.UploadMesh(data);

            if (prog != null && !prog.Disposed) prog.Dispose();
            prog = rpi.GetEngineShader(EnumShaderProgram.Wireframe);
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

            if (mRef == null || mRef.Disposed || prog == null || prog.Disposed)
            {
                InitShaderProgram();
            }

            prog.Use();
            prog.Uniform("colorIn", ColorUtil.WhiteArgbVec);
            prog.UniformMatrix("projectionMatrix", rpi.CurrentProjectionMatrix);
            prog.Uniform("origin", Vec3f.Zero);

            Vec3d plPos = plr.Pos.XYZ;
            double[] camOrigin = rpi.CameraMatrixOrigin;
            foreach (BlockPos bp in bPosList_local)
            {
                HighlightBlock(bp?.ToVec3d(), plPos, camOrigin);
            }

            // TODO: also render "grand" box in which the player is shown the current search shell (respect upper/lower bounds)

            prog.Stop();

            // reset line width to default value
            rpi.LineWidth = 1.6f;
        }

        private void HighlightBlock(Vec3d bPos, Vec3d plPos, double[] camOrigin)
        {
            if (bPos == null) return;
            double[] modelMat_h = Mat4d.Create();
            // rendering is based on player position, to translate render target-position from world frame into render frame
            Vec3d posDiff = bPos - plPos;
            Mat4d.Translate(modelMat_h, camOrigin, posDiff.X, posDiff.Y, posDiff.Z);

            prog.UniformMatrix("modelViewMatrix", Array.ConvertAll(modelMat_h, x => (float)x));
            rpi.LineWidth = float.Max(1.6f * float.Pow(2f, 5f) / (((float)posDiff.Length()) + 0.01f), 1.6f * 0.25f); // reduce line width with distance
            rpi.RenderMesh(mRef);
        }

        public override void Dispose()
        {
            bPosList.Clear();
            mRef?.Dispose();
            prog?.Dispose();
            base.Dispose();
        }


    }
}
