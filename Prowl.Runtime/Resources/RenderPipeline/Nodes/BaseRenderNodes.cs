﻿using Prowl.Runtime.NodeSystem;
using Raylib_cs;
using System;
using static Prowl.Runtime.MonoBehaviour;

namespace Prowl.Runtime.Resources.RenderPipeline
{

    // 1. Move Buffers to exist on the Camera's, the camera will then be responsible for their width/height and clearing unused ones after X frames
    // 2. 

    [DisallowMultipleNodes]
    public class CameraNode : Node
    {
        public override string Title => "Camera";
        public override float Width => 50;

        [Output] public GBuffer CameraOutput;

        public override object GetValue(NodePort port)
        {
            return Camera.Current.gBuffer;
        }
    }

    public abstract class RenderPassNode : Node
    {
        public abstract override string Title { get; }
        public abstract override float Width { get; }

        [Output] public RenderTexture OutputRT;
        public bool Clear = true;

        protected RenderTexture renderRT;
        long lastRenderedFrame = -1;
        Camera lastRenderedCam = null;

        public override object GetValue(NodePort port)
        {
            // If we already rendered this frame return that instead
            if (lastRenderedFrame == Time.frameCount && lastRenderedCam == Camera.Current)
                return renderRT;

            var gbuffer = Camera.Current.gBuffer;

            if (renderRT == null || (gbuffer.Width != renderRT.Width || gbuffer.Height != renderRT.Height))
            {
                renderRT?.Dispose();
                PixelFormat[] formats = [PixelFormat.PIXELFORMAT_UNCOMPRESSED_R32G32B32];
                renderRT = new RenderTexture(gbuffer.Width, gbuffer.Height, 1, false, formats);
            }

            Render();

            lastRenderedFrame = Time.frameCount;
            lastRenderedCam = Camera.Current;

            return renderRT;
        }

        public abstract void Render();
    }

    public class PBRDeferredNode : RenderPassNode
    {
        public override string Title => "PBR Deferred Pass";
        public override float Width => 100;

        public override void Render()
        {
            renderRT.Begin();
            if(Clear) Raylib.ClearBackground(Color.clear);
            Rlgl.rlDisableDepthTest();
            Rlgl.rlSetCullFace(0); // Cull the front faces for the lighting pass
            Camera.Current.RenderAllOfOrder(RenderingOrder.Lighting);
            Rlgl.rlEnableDepthTest();
            Rlgl.rlSetCullFace(1);
            renderRT.End();
        }
    }

    public class PostPBRDeferredNode : RenderPassNode
    {
        public override string Title => "Post PBR Deferred Pass";
        public override float Width => 100;

        [Input(ShowBackingValue.Never)] public RenderTexture LightingRT;

        Material? CombineShader = null;

        public override void Render()
        {
            var gbuffer = Camera.Current.gBuffer;
            var lighting = GetInputValue<RenderTexture>("LightingRT");

            CombineShader ??= new(Shader.Find("Defaults/GBuffercombine.shader"));
            CombineShader.SetTexture("gAlbedoAO", gbuffer.AlbedoAO);
            CombineShader.SetTexture("gLighting", lighting.InternalTextures[0]);

            Graphics.Blit(renderRT, CombineShader, 0, Clear);
        }
    }

    public class DepthOfFieldNode : RenderPassNode
    {
        public override string Title => "Depth Of Field";
        public override float Width => 125;

        [Input(ShowBackingValue.Never)] public RenderTexture RenderTexture;

        public float FocusStrength = 150f;
        public float Quality = 0.05f;
        public int BlurRadius = 10;

        Material Mat;

        public override void Render()
        {
            var gbuffer = Camera.Current.gBuffer;
            var rt = GetInputValue<RenderTexture>("RenderTexture");
            if (rt == null) return;

            Mat ??= new Material(Shader.Find("Defaults/DOF.shader"));
            Mat.SetTexture("gCombined", rt.InternalTextures[0]);
            Mat.SetTexture("gDepth", gbuffer.Depth);

            Mat.SetFloat("u_Quality", Math.Clamp(Quality, 0.0f, 0.9f));
            Mat.SetFloat("u_BlurRadius", Math.Clamp(BlurRadius, 2, 40));
            Mat.SetFloat("u_FocusStrength", FocusStrength);

            Graphics.Blit(renderRT, Mat, 0, true);
        }
    }

    public class AcesTonemappingNode : RenderPassNode
    {
        public override string Title => "Aces Tonemapping Pass";
        public override float Width => 100;

        [Input(ShowBackingValue.Never)] public RenderTexture RenderTexture;

        public float Contrast = 1.1f;
        public float Saturation = 1.2f;
        public bool UseACES = true;
        public bool UseGammaCorrection = true;

        Material? AcesMat = null;

        public override void Render()
        {
            var rt = GetInputValue<RenderTexture>("RenderTexture");
            if (rt == null) return;

            AcesMat ??= new(Shader.Find("Defaults/AcesTonemapper.shader"));
            AcesMat.SetTexture("gAlbedo", rt.InternalTextures[0]);
            AcesMat.SetFloat("Contrast", Math.Clamp(Contrast, 0, 2));
            AcesMat.SetFloat("Saturation", Math.Clamp(Saturation, 0, 2));

            if (UseACES) AcesMat.EnableKeyword("ACESTONEMAP");
            else AcesMat.DisableKeyword("ACESTONEMAP");
            if (UseGammaCorrection) AcesMat.EnableKeyword("GAMMACORRECTION");
            else AcesMat.DisableKeyword("GAMMACORRECTION");

            Graphics.Blit(renderRT, AcesMat, 0, Clear);
        }
    }

    [DisallowMultipleNodes]
    public class OutputNode : Node
    {
        public override string Title => "Output";
        public override float Width => 125;

        [Input] public RenderTexture RenderTexture;

        public override object GetValue(NodePort port)
        {
            return GetInputValue<RenderTexture>("RenderTexture");
        }
    }
}