using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using System.Collections.Generic;

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
            MeshData data = LineMeshUtil.GetCube(frameColor);
            data.Scale(new Vec3f(1f, 1f, 1f), 0.5f, 0.5f, 0.5f);
            data.Flags = new int[data.VerticesCount];
            for (int i = 0; i < data.Flags.Length; i++)
            {
                data.Flags[i] = 256;
            }
            data.Flags = new int[24];
            mRef = rpi.UploadMesh(data);

            prog = rpi.GetEngineShader(EnumShaderProgram.Wireframe);
            prog.Use();
            prog.Uniform("origin", Vec3f.Zero);
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            IClientPlayer plr = capi.World.Player;
            if (plr.Entity.Properties.Client.Renderer is not EntityShapeRenderer) return;

            // create copy of list as otherwise it could be modified during iteration
            List<BlockPos> bPosList_local;
            lock (bPosList)
            {
                bPosList_local = new(bPosList);
            }

            if (mRef == null || prog == null)
            {
                InitShaderProgram();
            }

            prog.Use();
            prog.Uniform("colorIn", ColorUtil.WhiteArgbVec);

            Vec3f plPos = plr.Entity.Pos.XYZFloat;
            float[] camOrigin = rpi.CameraMatrixOriginf;
            foreach (BlockPos bp in bPosList_local)
            {
                HighlightBlock(bp?.ToVec3f(), plPos, camOrigin);
            }

            prog.Stop();

            // reset line width to default value
            rpi.LineWidth = 1.6f;
        }

        private void HighlightBlock(Vec3f bPos, Vec3f plPos, float[] camOrigin)
        {
            if (bPos == null) return;
            float[] modelMat_h = Mat4f.Create();
            // rendering is based on player position, to translate render target-position from world frame into render frame
            Vec3f posDiff = bPos - plPos;
            Mat4f.Translate(modelMat_h, camOrigin, posDiff.X, posDiff.Y, posDiff.Z);

            prog.UniformMatrix("modelViewMatrix", modelMat_h);
            rpi.LineWidth = float.Max(1.6f * float.Pow(2f, 5f) / (posDiff.Length() + 0.01f), 1.6f * 0.25f); // reduce line width with distance
            rpi.RenderMesh(mRef);
        }

        public override void Dispose()
        {
            mRef?.Dispose();
            prog?.Dispose();
        }


    }
}
