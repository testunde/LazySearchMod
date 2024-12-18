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
        private MeshRef mRef = null;
        private IStandardShaderProgram prog = null;
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
            IRenderAPI rpi = capi.Render;

            MeshData data = LineMeshUtil.GetCube(frameColor);
            data.Scale(new Vec3f(1f, 1f, 1f), 0.5f, 0.5f, 0.5f);
            // data.Flags = new int[data.VerticesCount];
            // for (int i = 0; i < data.Flags.Length; i++)
            // {
            //     data.Flags[i] = 256;
            // }
            // data.Flags = new int[24];
            // for (int l = 0; l < 24; l += 4)
            // {
            //     BlockFacing face = BlockFacing.ALLFACES[l / 6];
            //     data.Flags[l] = face.NormalPackedFlags;
            //     data.Flags[l + 1] = data.Flags[l];
            //     data.Flags[l + 2] = data.Flags[l];
            //     data.Flags[l + 3] = data.Flags[l];
            // }
            // byte[] rgba = data.GetRgba();
            // for (int i = 0; i < rgba.Length; i += 4)
            // {
            //     // set color to purple
            //     rgba[i + 0] = ColorUtil.ColorR(frameColor); // red
            //     rgba[i + 1] = ColorUtil.ColorG(frameColor); // green
            //     rgba[i + 2] = ColorUtil.ColorB(frameColor); // blue
            //     rgba[i + 3] = ColorUtil.ColorA(frameColor); // alpha
            // }
            // data.SetRgba(rgba);
            mRef = rpi.UploadMesh(data);

            prog = rpi.StandardShader;
            prog.Use();
            prog.ViewMatrix = rpi.CameraMatrixOriginf;

            // no lighting effects
            // Vec4f c = ColorUtil.ToRGBAVec4f(frameColor);//new(255f, 255f, 255f, 255f);
            // prog.RgbaLightIn = c;
            // prog.RgbaFogIn = c;
            // Vec3f asd = new();
            // _ = ColorUtil.ToRGBVec3f(frameColor, ref asd);
            // prog.RgbaAmbientIn = asd;//new Vec3f(255f, 255f, 255f);
            // prog.RgbaGlowIn = c;
            // prog.RgbaTint = c;
            // prog.FogMinIn = float.MaxValue;
            // prog.FogDensityIn = 0f;

            prog.Compile();
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
            Vec3f plPos = plr.Entity.Pos.XYZFloat;
            foreach (BlockPos bp in bPosList_local)
            {
                HighlightBlock(bp?.ToVec3f(), plPos);
            }
            prog.Stop();

            // reset line width to default value
            capi.Render.LineWidth = 1.6f;
        }

        private void HighlightBlock(Vec3f bPos, Vec3f plPos)
        {
            if (bPos == null) return;
            float[] modelMat_h = Mat4f.Create();
            // rendering is based on player position, to translate render target-position from world frame into render frame
            Vec3f posDiff = bPos - plPos;
            Mat4f.Translate(modelMat_h, modelMat_h, posDiff.X, posDiff.Y, posDiff.Z);

            prog.ModelMatrix = modelMat_h;
            capi.Render.LineWidth = float.Max(1.6f * float.Pow(2f, 6f) / (posDiff.Length() + 0.01f), 1.6f * 0.5f); // reduce line width with distance
            capi.Render.RenderMesh(mRef);
        }

        public override void Dispose()
        {
            mRef?.Dispose();
            prog?.Dispose();
        }


    }
}
