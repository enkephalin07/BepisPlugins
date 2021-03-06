﻿using UnityEngine;
using UnityEngine.SceneManagement;
using UnityStandardAssets.ImageEffects;

//code by essu
namespace alphaShot
{
    public class AlphaShot2 : MonoBehaviour
    {
        private Material matBlackout = null;
        private Material matMask = null;
        private int col = Shader.PropertyToID("_TargetColour");

        void Awake()
        {
            var abd = Screencap.Properties.Resources.blackout;
            var ab = AssetBundle.LoadFromMemory(abd);
            matBlackout = new Material(ab.LoadAsset<Shader>("assets/blackout.shader"));
            matBlackout.SetColor(col, Color.black);
            matMask = new Material(ab.LoadAsset<Shader>("assets/alphamask.shader"));
            ab.Unload(false);
        }

        public byte[] Capture(int ResolutionX, int ResolutionY, int DownscalingRate, bool Transparent)
        {
            Texture2D fullSizeCapture = null;
            int newWidth = ResolutionX * DownscalingRate;
            int newHeight = ResolutionY * DownscalingRate;

            var currentScene = SceneManager.GetActiveScene().name;
            if (Transparent && (currentScene == "CustomScene" || currentScene == "Studio"))
                fullSizeCapture = CaptureAlpha(newWidth, newHeight);
            else
                fullSizeCapture = CaptureOpaque(newWidth, newHeight);

            byte[] ret = null;
            if (DownscalingRate > 1)
            {
                var pixels = ScaleUnityTexture.ScaleLanczos(fullSizeCapture.GetPixels32(), fullSizeCapture.width, ResolutionX, ResolutionY);
                GameObject.Destroy(fullSizeCapture);
                var texture2D4 = new Texture2D(ResolutionX, ResolutionY, TextureFormat.ARGB32, false);
                texture2D4.SetPixels32(pixels);
                ret = texture2D4.EncodeToPNG();
                GameObject.Destroy(texture2D4);
            }
            else
            {
                ret = fullSizeCapture.EncodeToPNG();
                GameObject.Destroy(fullSizeCapture);
            }
            return ret;
        }

        private Texture2D CaptureOpaque(int ResolutionX, int ResolutionY)
        {
            var renderCam = Camera.main;
            var tt = renderCam.targetTexture;
            var rta = RenderTexture.active;

            var rt = RenderTexture.GetTemporary(ResolutionX, ResolutionY, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default, 1);
            var ss = new Texture2D(ResolutionX, ResolutionY, TextureFormat.RGB24, false);
            var rect = renderCam.rect;

            renderCam.targetTexture = rt;
            renderCam.rect = new Rect(0, 0, 1, 1);
            renderCam.Render();
            renderCam.rect = rect;
            RenderTexture.active = rt;
            ss.ReadPixels(new Rect(0, 0, ResolutionX, ResolutionY), 0, 0);
            renderCam.targetTexture = tt;
            RenderTexture.active = rta;
            RenderTexture.ReleaseTemporary(rt);

            return ss;
        }

        private Texture2D CaptureAlpha(int ResolutionX, int ResolutionY)
        {
            var main = Camera.main;

            var baf = main.GetComponent<BloomAndFlares>();
            var baf_e = baf?.enabled;
            if (baf) baf.enabled = false;

            var vig = main.GetComponent<VignetteAndChromaticAberration>();
            var vig_e = vig?.enabled;
            if (vig) vig.enabled = false;

            var ace = main.GetComponent<AmplifyColorEffect>();
            var ace_e = ace?.enabled;
            if (ace) ace.enabled = false;

            var texture2D = PerformCapture(ResolutionX, ResolutionY, true);
            if (baf) baf.enabled = baf_e.Value;
            if (vig) vig.enabled = vig_e.Value;
            if (ace) ace.enabled = ace_e.Value;

            var texture2D2 = PerformCapture(ResolutionX, ResolutionY, false);

            var rt = RenderTexture.GetTemporary(texture2D.width, texture2D.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 1);

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(false, true, new Color(0, 0, 0, 0));
            matMask.SetTexture("_Mask", texture2D);
            Graphics.Blit(texture2D2, rt, matMask);
            GameObject.Destroy(texture2D);
            GameObject.Destroy(texture2D2);
            var texture2D3 = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false);
            texture2D3.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);


            return texture2D3;
        }

        public Texture2D PerformCapture(int ResolutionX, int ResolutionY, bool CaptureMask)
        {
            var renderCam = Camera.main;
            var targetTexture = renderCam.targetTexture;
            var rta = RenderTexture.active;
            var rect = renderCam.rect;
            var backgroundColor = renderCam.backgroundColor;
            var clearFlags = renderCam.clearFlags;
            var t2d = new Texture2D(ResolutionX, ResolutionY, TextureFormat.RGB24, false);
            var rt_temp = RenderTexture.GetTemporary(ResolutionX, ResolutionY, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default, 1);

            renderCam.clearFlags = CameraClearFlags.SolidColor;
            renderCam.targetTexture = rt_temp;
            renderCam.rect = new Rect(0, 0, 1, 1);

            var lc = Shader.GetGlobalColor(ChaShader._LineColorG);
            if (CaptureMask)
            {
                Shader.SetGlobalColor(ChaShader._LineColorG, new Color(.5f, .5f, .5f, 0f));
                GL.Clear(true, true, Color.white);
                renderCam.backgroundColor = Color.white;
                renderCam.renderingPath = RenderingPath.VertexLit;
                renderCam.RenderWithShader(matBlackout.shader, null);
                Shader.SetGlobalColor(ChaShader._LineColorG, lc);
            }
            else
            {
                renderCam.backgroundColor = Color.black;
                renderCam.renderingPath = RenderingPath.Forward;
                renderCam.Render();
            }
            renderCam.targetTexture = targetTexture;
            renderCam.rect = rect;

            RenderTexture.active = rt_temp;
            t2d.ReadPixels(new Rect(0f, 0f, ResolutionX, ResolutionY), 0, 0);
            t2d.Apply();
            RenderTexture.active = rta;
            renderCam.backgroundColor = backgroundColor;
            renderCam.clearFlags = clearFlags;
            RenderTexture.ReleaseTemporary(rt_temp);

            return t2d;
        }
    }
}