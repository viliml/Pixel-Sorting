using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PixelSorter : MonoBehaviour {
    public ComputeShader pixelSorter;

    public Texture image;

    public bool useImage = false;

    public bool animate = false;

    [Range(0.0f, 0.5f)]
    public float lowThreshold = 0.2f;
    
    [Range(0.5f, 1.0f)]
    public float highThreshold = 0.8f;

    public bool debugMask = false;

    [Range(-50, 50)]
    public int maskRandomOffset = 0;

    [Range(0, 30)]
    public float animationSpeed = 0;

    public bool debugSpans = false;

    public bool visualizeSpans = false;

    [Range(0, 1080)]
    public int maxSpanLength = 1080;

    [Range(0, 512)]
    public int maxRandomSpanOffset = 0;

    public bool debugSorting = false;

    public enum SortMode {
        Lightness = 0,
        Saturation,
        Hue,
        Intensity
    } public SortMode sortBy;

    public bool horizontalSorting = false;

    public bool reverseSorting = false;

    private float animatedLowThreshold = 0.5f;
    private float animatedHighThreshold = 0.5f;
    private int direction = 1;

    private int createMaskPass, testSelectionSortPass, testBitonicSortPass, testCustomSortPass, clearBufferPass, indentifySpansPass, visualizeSpansPass, rgbToHslPass, pixelSortPass, hsltoRgbPass, compositePass;

    private RenderTexture maskTex, spanTex, colorTex, hslTex, sortedTex;

    private ComputeBuffer testBuffer, sortedTestBuffer;

    void RegenerateRenderTextures() {
        maskTex = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
        maskTex.enableRandomWrite = true;
        maskTex.Create();

        spanTex = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
        spanTex.enableRandomWrite = true;
        spanTex.Create();

        colorTex = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        colorTex.enableRandomWrite = true;
        colorTex.Create();

        hslTex = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        hslTex.enableRandomWrite = true;
        hslTex.Create();

        sortedTex = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        sortedTex.enableRandomWrite = true;
        sortedTex.Create();
    }

    void OnEnable() {
        RegenerateRenderTextures();

        createMaskPass = pixelSorter.FindKernel("CS_CreateMask");
        testSelectionSortPass = pixelSorter.FindKernel("CS_TestSelectionSort");
        testBitonicSortPass = pixelSorter.FindKernel("CS_TestBitonicSort");
        testCustomSortPass = pixelSorter.FindKernel("CS_TestCustomSort");
        clearBufferPass = pixelSorter.FindKernel("CS_ClearBuffer");
        indentifySpansPass = pixelSorter.FindKernel("CS_IdentifySpans");
        visualizeSpansPass = pixelSorter.FindKernel("CS_VisualizeSpans");
        rgbToHslPass = pixelSorter.FindKernel("CS_RGBtoHSL");
        pixelSortPass = pixelSorter.FindKernel("CS_PixelSort");
        hsltoRgbPass = pixelSorter.FindKernel("CS_HSLtoRGB");
        compositePass = pixelSorter.FindKernel("CS_Composite");

        /*
        testBuffer = new ComputeBuffer(16, 4);
        sortedTestBuffer = new ComputeBuffer(16, 4);

        int[] testArray = {6, 14, 1, 15, 4, 13, 16, 11, 5, 2, 10, 12, 8, 7, 3, 9};

        testBuffer.SetData(testArray);

        pixelSorter.SetBuffer(testCustomSortPass, "_NumberBuffer", testBuffer);
        pixelSorter.SetBuffer(testCustomSortPass, "_SortedNumberBuffer", sortedTestBuffer);

        pixelSorter.Dispatch(testCustomSortPass, 1, 1, 1);

        sortedTestBuffer.GetData(testArray);

        string outString = "";
        for (int i = 0; i < 16; ++i) {
            outString += testArray[i].ToString() + " ";
        }

        Debug.Log(outString);
        */
    }

    void Update() {
        if (Screen.width != maskTex.width)
            RegenerateRenderTextures();

        if (animate) {
            animatedHighThreshold += 0.15f * Time.deltaTime * direction;
            animatedLowThreshold += -0.15f * Time.deltaTime * direction;

            if (animatedHighThreshold < 0.5f || 1.0f < animatedHighThreshold) direction *= -1;
        }
    }

    void OnDisable() {
        //testBuffer.Release();
        //sortedTestBuffer.Release();
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination) {
        Graphics.Blit(useImage ? image : source, colorTex);

        pixelSorter.SetFloat("_LowThreshold", animate ? animatedLowThreshold : lowThreshold);
        pixelSorter.SetFloat("_HighThreshold", animate ? animatedHighThreshold : highThreshold);
        pixelSorter.SetFloat("_FrameTime", Time.time);
        pixelSorter.SetFloat("_AnimationSpeed", animationSpeed);
        pixelSorter.SetInt("_BufferWidth", Screen.width);
        pixelSorter.SetInt("_BufferHeight", Screen.height);
        pixelSorter.SetInt("_FrameCount", Time.frameCount);
        pixelSorter.SetInt("_SpanLimit", maxSpanLength);
        pixelSorter.SetInt("_MaxRandomOffset", maxRandomSpanOffset);
        pixelSorter.SetInt("_MaskRandomOffset", maskRandomOffset);
        pixelSorter.SetInt("_ReverseSorting", reverseSorting ? 1 : 0);
        pixelSorter.SetInt("_HorizontalSorting", horizontalSorting ? 1 : 0);
        pixelSorter.SetInt("_SortBy", (int)sortBy);
        pixelSorter.SetTexture(createMaskPass, "_Mask", maskTex);
        pixelSorter.SetTexture(createMaskPass, "_ColorTex", colorTex);

        pixelSorter.Dispatch(createMaskPass, Mathf.CeilToInt(Screen.width / 8.0f), Mathf.CeilToInt(Screen.height / 8.0f), 1);

        pixelSorter.SetTexture(clearBufferPass, "_ClearBuffer", spanTex);
        pixelSorter.Dispatch(clearBufferPass, Mathf.CeilToInt(Screen.width / 8.0f), Mathf.CeilToInt(Screen.height / 8.0f), 1);

        pixelSorter.SetTexture(indentifySpansPass, "_SpanBuffer", spanTex);
        pixelSorter.SetTexture(indentifySpansPass, "_Mask", maskTex);

        pixelSorter.Dispatch(indentifySpansPass, horizontalSorting ? 1 : Screen.width, horizontalSorting ? Screen.height : 1, 1);

        pixelSorter.SetTexture(clearBufferPass, "_ClearBuffer", sortedTex);
        pixelSorter.Dispatch(clearBufferPass, Mathf.CeilToInt(Screen.width / 8.0f), Mathf.CeilToInt(Screen.height / 8.0f), 1);

        if (visualizeSpans) {
            pixelSorter.SetTexture(visualizeSpansPass, "_ColorBuffer", colorTex);
            pixelSorter.SetTexture(visualizeSpansPass, "_SortedBuffer", sortedTex);
            pixelSorter.SetTexture(visualizeSpansPass, "_SpanBuffer", spanTex);

            pixelSorter.Dispatch(visualizeSpansPass, Mathf.CeilToInt(Screen.width / 8.0f), Mathf.CeilToInt(Screen.height / 8.0f), 1); 
        } else {
            pixelSorter.SetTexture(rgbToHslPass, "_ColorBuffer", colorTex);
            pixelSorter.SetTexture(rgbToHslPass, "_HSLBuffer", hslTex);

            pixelSorter.Dispatch(rgbToHslPass, Mathf.CeilToInt(Screen.width / 8.0f), Mathf.CeilToInt(Screen.height / 8.0f), 1); 

            pixelSorter.SetTexture(pixelSortPass, "_HSLBuffer", hslTex);
            pixelSorter.SetTexture(pixelSortPass, "_SortedBuffer", sortedTex);
            pixelSorter.SetTexture(pixelSortPass, "_SpanBuffer", spanTex);

            pixelSorter.Dispatch(pixelSortPass, Screen.width, Screen.height, 1);

            pixelSorter.SetTexture(hsltoRgbPass, "_SortedBuffer", sortedTex);

            pixelSorter.Dispatch(hsltoRgbPass, Mathf.CeilToInt(Screen.width / 8.0f), Mathf.CeilToInt(Screen.height / 8.0f), 1); 
            
            pixelSorter.SetTexture(compositePass, "_Mask", maskTex);
            pixelSorter.SetTexture(compositePass, "_ColorBuffer", colorTex);
            pixelSorter.SetTexture(compositePass, "_SortedBuffer", sortedTex);
            
            if (!debugSorting)
                pixelSorter.Dispatch(compositePass, Mathf.CeilToInt(Screen.width / 8.0f), Mathf.CeilToInt(Screen.height / 8.0f), 1); 
        }

        if (debugMask)
            Graphics.Blit(maskTex, destination);
        else if (debugSpans)
            Graphics.Blit(spanTex, destination);
        else
            Graphics.Blit(sortedTex, destination);
    }
}
