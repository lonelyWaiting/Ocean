using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OceanSystem;

public class BufferVisualization {

    private RenderTexture mTarget    = null;
    private ComputeShader mBuf2TexCS = null;
    private int mSize;

    public BufferVisualization(ComputeShader buf2Tex, int size)
    {
        mSize = size;

        mTarget                   = new RenderTexture(mSize, mSize, 0, RenderTextureFormat.ARGBFloat);
        mTarget.filterMode        = FilterMode.Bilinear;
        mTarget.wrapMode          = TextureWrapMode.Clamp;
        mTarget.enableRandomWrite = true;
        mTarget.Create();

        mBuf2TexCS = buf2Tex;
    }

    public void Visualization(ComputeBuffer srcBuffer, RenderTexture dst)
    {
        if (srcBuffer == null) return;

        mBuf2TexCS.SetBuffer(OceanConst.BUF2TEX_KERNEL, OceanConst.BUF2TEX_INPUT_BUFFER, srcBuffer);
        mBuf2TexCS.SetTexture(OceanConst.BUF2TEX_KERNEL, OceanConst.BUF2TEX_OUTPUT_TEXTURE, mTarget);
        mBuf2TexCS.SetInt(OceanConst.BUF2TEX_DIMENSION, mSize);
        mTarget.DiscardContents();
        mBuf2TexCS.Dispatch(OceanConst.BUF2TEX_KERNEL, mSize / OceanConst.BUF2TEX_THREAD_NUM, mSize / OceanConst.BUF2TEX_THREAD_NUM, 1);

        Graphics.Blit(mTarget, dst);
    }

    ~BufferVisualization()
    {
        if (mTarget != null) mTarget.Release();
    }
}
