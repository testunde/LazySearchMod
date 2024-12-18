using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using System.Collections.Generic;
using Vintagestory.Client.NoObf;

namespace LazySearch
{
    public class BlockPosRenderer : ModSystem, IRenderer
    {
        private ICoreClientAPI capi;
        private WireframeCube wFC = null;
        private static readonly List<BlockPos> bPosList = new();
        private readonly Vec4f frameColor = new(1f, 1f, 0f, 1f);

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
            wFC = WireframeCube.CreateCenterOriginCube(capi);
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

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            IClientPlayer plr = capi.World.Player;
            if (plr.Entity.Properties.Client.Renderer is not EntityShapeRenderer rend) return;

            // create copy of list as otherwise it could be modified during iteration
            List<BlockPos> bPosList_local;
            lock (bPosList)
            {
                bPosList_local = new(bPosList);
            }

            // render last added blocks first
            // TODO: reorder depending on momentary distance to player (might not be needed for single-color meshes)
            bPosList_local.Reverse();

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
            float[] modelMat_h = Mat4f.Create();
            IRenderAPI rpi = capi.Render;
            // rending is based on player position, to translate render target-position from world frame into render frame
            Vec3f posDiff = bPos.ToVec3f() - pEnt.Pos.XYZFloat;
            Mat4f.Translate(modelMat_h, rpi.CameraMatrixOriginf, posDiff.X, posDiff.Y, posDiff.Z);
            Mat4f.Translate(modelMat_h, modelMat_h, 0.5f, 0.5f, 0.5f); // offset by 0.5 since block coordinates are off-set

            wFC.Render(capi, new Matrixf(modelMat_h), 1.6f, frameColor);

            // rpi.CameraMatrixOriginf;
            // Mat4f.RotateY(modelMat_h, modelMat_h, yaw);
            // Mat4f.
            // Matrixf a = new(rpi.CameraMatrixOriginf);
            // // a.
            // wFC.Render(capi, a, 1.6f, frameColor);

        }

        public override void Dispose()
        {
            wFC?.Dispose();
        }


    }
}
