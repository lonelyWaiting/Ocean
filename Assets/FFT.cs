using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OceanSystem;

public class FFT {
    private ComputeBuffer mTempBuffer       = null;
    private ComputeBuffer mBitReverseBuffer = null;
    private ComputeShader mRadix2FFT        = null;
    public int mSize = 0;

    private int bitReverse(int i, int N)
    {
        int dst = 0;
        while ((N >> 1) != 0)
        {
            dst = (dst << 1) + (i & 1);
            i >>= 1;
            N >>= 1;
        }
        return dst;
    }

    public FFT(ComputeShader fft, int _size)
    {
        mSize      = _size;
        mRadix2FFT = fft;

        mBitReverseBuffer  = new ComputeBuffer(mSize, sizeof(int));
        int[] bit_reverse = new int[mSize];
        for (int i = 0; i < mSize; i++)
        {
            bit_reverse[i] = bitReverse(i, mSize);
        }
        mBitReverseBuffer.SetData(bit_reverse);

        mTempBuffer = new ComputeBuffer(mSize * mSize, sizeof(float) * 2);
    }

    private void SwapBuffer(ref ComputeBuffer A, ref ComputeBuffer B)
    {
        ComputeBuffer temp = A;
        A = B;
        B = temp;
    }

    public void EvaluteFFT(ComputeBuffer srcBuffer, ref ComputeBuffer dstBuffer)
    {
        if (mTempBuffer == null || mBitReverseBuffer == null || dstBuffer == null || mRadix2FFT == null) return;

        ComputeBuffer[] swapBuffer = new ComputeBuffer[2];
        swapBuffer[0] = dstBuffer;
        swapBuffer[1] = mTempBuffer;

        int interation = (int)(Mathf.Log(mSize) / Mathf.Log(2));
        int thread_count = mSize * mSize / 2;
        int thread_group = thread_count / OceanConst.RADIX2FFT_THREAD_NUM;
        for (int i = 0; i < interation; i++)
        {
            mRadix2FFT.SetInt("thread_count", thread_count);
            mRadix2FFT.SetInt("istride", thread_count / (1 << i));
            mRadix2FFT.SetInt("bit_count", i);
            mRadix2FFT.SetInt("N", mSize);
            mRadix2FFT.SetBuffer(0, "input", i == 0 ? srcBuffer : swapBuffer[0]);
            mRadix2FFT.SetBuffer(0, "output", swapBuffer[1]);
            mRadix2FFT.Dispatch(0, thread_group, 1, 1);

            SwapBuffer(ref swapBuffer[0], ref swapBuffer[1]);
        }

        // bit reverse
        {
            mRadix2FFT.SetInt("thread_count", thread_count);
            mRadix2FFT.SetInt("istride", thread_count);
            mRadix2FFT.SetInt("N", mSize);
            mRadix2FFT.SetBuffer(3, "bit_reverse", mBitReverseBuffer);
            mRadix2FFT.SetBuffer(3, "input", swapBuffer[0]);
            mRadix2FFT.SetBuffer(3, "output", swapBuffer[1]);
            mRadix2FFT.Dispatch(3, thread_group, 1, 1);

            SwapBuffer(ref swapBuffer[0], ref swapBuffer[1]);
        }

        // transpose
        {
            mRadix2FFT.SetInt("thread_count", thread_count);
            mRadix2FFT.SetInt("N", mSize);
            mRadix2FFT.SetBuffer(1, "input", swapBuffer[0]);
            mRadix2FFT.SetBuffer(1, "output", swapBuffer[1]);
            mRadix2FFT.Dispatch(1, thread_group, 1, 1);

            SwapBuffer(ref swapBuffer[0], ref swapBuffer[1]);
        }

        for (int i = 0; i < interation; i++)
        {
            mRadix2FFT.SetInt("thread_count", thread_count);
            mRadix2FFT.SetInt("istride", thread_count / (1 << i));
            mRadix2FFT.SetInt("bit_count", i);
            mRadix2FFT.SetInt("N", mSize);
            mRadix2FFT.SetBuffer(0, "input", swapBuffer[0]);
            mRadix2FFT.SetBuffer(0, "output", swapBuffer[1]);
            mRadix2FFT.Dispatch(0, thread_group, 1, 1);

            SwapBuffer(ref swapBuffer[0], ref swapBuffer[1]);
        }

        // bit reverse
        {
            mRadix2FFT.SetInt("thread_count", thread_count);
            mRadix2FFT.SetInt("istride", thread_count);
            mRadix2FFT.SetInt("N", mSize);
            mRadix2FFT.SetBuffer(3, "bit_reverse", mBitReverseBuffer);
            mRadix2FFT.SetBuffer(3, "input", swapBuffer[0]);
            mRadix2FFT.SetBuffer(3, "output", swapBuffer[1]);
            mRadix2FFT.Dispatch(3, thread_group, 1, 1);

            SwapBuffer(ref swapBuffer[0], ref swapBuffer[1]);
        }

        // transpose
        {
            mRadix2FFT.SetInt("thread_count", thread_count);
            mRadix2FFT.SetInt("N", mSize);
            mRadix2FFT.SetBuffer(1, "input", swapBuffer[0]);
            mRadix2FFT.SetBuffer(1, "output", swapBuffer[1]);
            mRadix2FFT.Dispatch(1, thread_group, 1, 1);

            SwapBuffer(ref swapBuffer[0], ref swapBuffer[1]);
        }

        if (dstBuffer != swapBuffer[0])
        {
            mRadix2FFT.SetInt("thread_count", thread_count);
            mRadix2FFT.SetInt("istride", thread_count);
            mRadix2FFT.SetBuffer(2, "input", swapBuffer[0]);
            mRadix2FFT.SetBuffer(2, "output", dstBuffer);
            mRadix2FFT.Dispatch(2, thread_group, 1, 1);
        }
    }

    public void EvaluteFFTCPU(ComputeBuffer srcBuffer, ref ComputeBuffer dstBuffer)
    {
        if (srcBuffer == null || dstBuffer == null) return;

        Vector2[] srcData = new Vector2[mSize * mSize];
        srcBuffer.GetData(srcData);

        int count = mSize * mSize / 2;

        // 列FFT
        int log_2_N = (int)(Mathf.Log(mSize) / Mathf.Log(2));

        for (int i = 0; i < log_2_N; i++)
        {
            int istride = mSize * mSize / (2 << i);

            for (int k = 0; k < count; k++)
            {
                int mod = k & (istride - 1);
                // 如果为8FFT，这里应该为<<3
                int addr = ((k - mod) << 1) + mod;

                // fetch complex number
                Vector2 t = srcData[addr];
                Vector2 u = srcData[addr + istride];

                int w = (addr - mod) / (istride << 1);
                w = bitReverse(w, 1 << i);

                Vector2 W = new Vector2(Mathf.Cos(2 * Mathf.PI * w / (2 << i)), Mathf.Sin(2 * Mathf.PI * w / (2 << i)));

                srcData[addr].x           = t.x + W.x * u.x - W.y * u.y;
                srcData[addr].y           = t.y + W.x * u.y + W.y * u.x;
                srcData[addr + istride].x = t.x - W.x * u.x + W.y * u.y;
                srcData[addr + istride].y = t.y - W.x * u.y - W.y * u.x;
            }
        }

        Vector2[] temp = new Vector2[mSize * mSize];
	    for (int i = 0; i< mSize; i++)
	    {
		    for (int j = 0; j< mSize; j++)
		    {
			    temp[i * mSize + j] = srcData[bitReverse(i, mSize) * mSize + j];
		    }
	    }

	    // 转置
	    for (int i = 0; i< mSize; i++)
	    {
		    for (int j = 0; j< mSize; j++)
		    {
                srcData[i * mSize + j] = temp[j * mSize + i];
		    }
	    }

	    // 列行列式
	    for (int i = 0; i<log_2_N; i++)
	    {
		    int istride = mSize * mSize / (2 << i);

		    for (int k = 0; k<count; k++)
		    {
			    int mod = k & (istride - 1);
                // 如果为8FFT，这里应该为<<3
                int addr = ((k - mod) << 1) + mod;

                // fetch complex number
                Vector2 t = srcData[addr];
                Vector2 u = srcData[addr + istride];

                int w = (addr - mod) / (istride << 1);
                w = bitReverse(w, 1 << i);

                Vector2 W = new Vector2(Mathf.Cos(2 * Mathf.PI * w / (2 << i)), Mathf.Sin(2 * Mathf.PI * w / (2 << i)));
                srcData[addr].x           = t.x + W.x * u.x - W.y * u.y;
                srcData[addr].y           = t.y + W.x * u.y + W.y * u.x;
                srcData[addr + istride].x = t.x - W.x * u.x + W.y * u.y;
                srcData[addr + istride].y = t.y - W.x * u.y - W.y * u.x;

            }
	    }

	    for (int i = 0; i< mSize; i++)
	    {
		    for (int j = 0; j< mSize; j++)
		    {
			    temp[i * mSize + j] = srcData[bitReverse(i, mSize) * mSize + j];
		    }
	    }

	    // 转置
	    for (int i = 0; i < mSize; i++)
	    {
		    for (int j = 0; j < mSize; j++)
		    {
                srcData[i * mSize + j] = temp[j * mSize + i];
		    }
	    }

        dstBuffer.SetData(srcData);
    }

    public void Cleanup()
    {
        if (mTempBuffer != null) mTempBuffer.Release();
        if (mBitReverseBuffer != null) mBitReverseBuffer.Release();
    }
}