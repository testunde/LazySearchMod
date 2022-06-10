using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using System.Collections.Generic;

namespace LazySearch
{
    public class BlockPosRenderer : ModSystem, IRenderer
    {
        ICoreClientAPI capi;

        MeshRef bMR = null;
        static List<BlockPos> bPosList = new List<BlockPos>();
        int bTextureId;
        float[] viewMatrix = new float[16];

        public double RenderOrder
        {
            get { return 1; }
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
            this.capi = api;
            api.Event.RegisterRenderer(this, EnumRenderStage.AfterBlit);
        }

        public static void plotCoord(BlockPos bp)
        {
            bPosList.Add(bp.Copy());
        }

        public static void clearBlockPosList()
        {
            bPosList.Clear();
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            IClientPlayer plr = capi.World.Player;
            EntityShapeRenderer rend = plr.Entity.Properties.Client.Renderer as EntityShapeRenderer;
            if (rend == null) return;

            // create copy of list as otherwise it could be modified during iteration
            List<BlockPos> bPosList_local = new List<BlockPos>(bPosList);
            foreach (BlockPos bp in bPosList_local)
            {
                if (bp != null)
                {
                    HighlightBlock(plr.Entity, rend, bp);
                }
            }
        }

        private void HighlightBlock(EntityPlayer pEnt, EntityShapeRenderer rend, BlockPos bPos)
        {
            IRenderAPI rpi = capi.Render;

            if (bMR == null)
            {
                MeshData bMD = CubeMeshUtil.GetCube();
                bMD.Scale(new Vec3f(0, 0, 0), 0.5f, 0.5f, 0.5f);
                CubeMeshUtil.SetXyzFacesAndPacketNormals(bMD);
                bMD.Rgba2Static = true;
                bMD.RgbaStatic = true;
                byte[] rgba = bMD.GetRgba();
                for (int i = 0; i < rgba.Length; i += 4)
                {
                    // set color to purple
                    rgba[i + 0] = 255;
                    rgba[i + 1] = 0;
                    rgba[i + 2] = 255;
                    rgba[i + 3] = 255; // alpha
                }
                bMD.SetRgba(rgba);
                bMR = capi.Render.UploadMesh(bMD);
                bTextureId = capi.BlockTextureAtlas.Positions[0].atlasTextureId;
                viewMatrix = rpi.CameraMatrixOriginf;
            }

            float[] modelMat_h = Mat4f.Create();
            // rending is based on player position, to translate render target-position from world frame into render frame
            Vec3f posDiff = bPos.ToVec3f() - pEnt.Pos.XYZFloat;
            Mat4f.Translate(modelMat_h, modelMat_h, posDiff.X, posDiff.Y, posDiff.Z);
            Mat4f.Translate(modelMat_h, modelMat_h, 0.5f, 0.5f, 0.5f); // offset by 0.5 since block coordinates are off-set

            IStandardShaderProgram prog = rpi.PreparedStandardShader(bPos.X, bPos.Y, bPos.Z);
            // no lighting effects
            prog.FogDensityIn = 0.0f;
            prog.RgbaLightIn = new Vec4f(255f, 255f, 255f, 255f);
            prog.RgbaFogIn = new Vec4f(255f, 255f, 255f, 255f);
            prog.RgbaAmbientIn = new Vec3f(255f, 255f, 255f);
            prog.RgbaGlowIn = new Vec4f(255f, 255f, 255f, 255f);
            prog.RgbaTint = new Vec4f(255f, 255f, 255f, 255f);

            prog.Tex2D = bTextureId;
            prog.AlphaTest = 0.001f;
            prog.ModelMatrix = modelMat_h;
            prog.ViewMatrix = viewMatrix;

            capi.Render.RenderMesh(bMR);

            prog.Stop();
        }

        public override void Dispose()
        {
            bMR?.Dispose();
        }


    }
}
