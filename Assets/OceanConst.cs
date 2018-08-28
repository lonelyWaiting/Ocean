using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OceanSystem
{
    public static class OceanConst
    {
        public const int KERNEL_UPDATE_SPECTRUM = 0;
        public const string SHADER_H0           = "H0";
        public const string SHADER_OMEGA        = "Omega";
        public const string SHADER_HK           = "HK";
        public const string SHADER_DX           = "Dx";
        public const string SHADER_DY           = "Dy";
        public const string SHADER_DIMENSION    = "Dimension";
        public const string SHADER_CURRENT_TIME = "curTime";
        public const int    THREAD_GROUP        = 16;

        public const int BUF2TEX_KERNEL            = 0;
        public const string BUF2TEX_INPUT_BUFFER   = "InputBuffer";
        public const string BUF2TEX_OUTPUT_TEXTURE = "outputTex";
        public const string BUF2TEX_DIMENSION      = "Dimension";
        public const int BUF2TEX_THREAD_NUM        = 16;

        public const string FFT_SHADER_THREAD_COUNT = "thread_count";
        public const string FFT_SHADER_ISTRIDE      = "istride";
        public const string FFT_SHADER_OSTRIDE      = "ostride";
        public const string FFT_SHADER_PSTRIDE      = "pstride";
        public const string FFT_SHADER_PHASE_BASE   = "phase_base";
        public const string FFT_SHADER_SRC          = "src";
        public const string FFT_SHADER_DST          = "dst";

    }
}