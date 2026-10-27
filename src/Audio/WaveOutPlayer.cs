using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace NesEmulator.Audio
{
    public class WaveOutPlayer : IDisposable
    {
        private const int SampleRate = 44100;
        private const int Channels = 1;
        private const int BitsPerSample = 16;
        private const int BufferCount = 4;
        private const int BufferSamples = 1024;
        private const int BufferSizeBytes = BufferSamples * 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct WaveFormatEx
        {
            public ushort wFormatTag;
            public ushort nChannels;
            public uint nSamplesPerSec;
            public uint nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WaveHdr
        {
            public IntPtr lpData;
            public uint dwBufferLength;
            public uint dwBytesRecorded;
            public IntPtr dwUser;
            public uint dwFlags;
            public uint dwLoops;
            public IntPtr lpNext;
            public IntPtr reserved;
        }

        [DllImport("winmm.dll")]
        private static extern int waveOutOpen(out IntPtr hWaveOut, int uDeviceID, ref WaveFormatEx lpFormat, IntPtr dwCallback, IntPtr dwInstance, int dwFlags);

        [DllImport("winmm.dll")]
        private static extern int waveOutPrepareHeader(IntPtr hWaveOut, ref WaveHdr lpWaveOutHdr, int uSize);

        [DllImport("winmm.dll")]
        private static extern int waveOutWrite(IntPtr hWaveOut, ref WaveHdr lpWaveOutHdr, int uSize);

        [DllImport("winmm.dll")]
        private static extern int waveOutUnprepareHeader(IntPtr hWaveOut, ref WaveHdr lpWaveOutHdr, int uSize);

        [DllImport("winmm.dll")]
        private static extern int waveOutReset(IntPtr hWaveOut);

        [DllImport("winmm.dll")]
        private static extern int waveOutClose(IntPtr hWaveOut);

        private IntPtr _hWaveOut = IntPtr.Zero;
        private readonly IntPtr[] _bufferPtrs = new IntPtr[BufferCount];
        private readonly WaveHdr[] _headers = new WaveHdr[BufferCount];
        private readonly short[] _currentBuffer = new short[BufferSamples];
        private int _sampleIndex = 0;
        private int _currentBufferId = 0;
        private bool _isInitialized = false;

        public bool Muted { get; set; }

        public WaveOutPlayer()
        {
            try
            {
                WaveFormatEx format = new WaveFormatEx();
                format.wFormatTag = 1; // PCM
                format.nChannels = Channels;
                format.nSamplesPerSec = SampleRate;
                format.wBitsPerSample = BitsPerSample;
                format.nBlockAlign = (ushort)(Channels * (BitsPerSample / 8));
                format.nAvgBytesPerSec = format.nSamplesPerSec * format.nBlockAlign;
                format.cbSize = 0;

                int result = waveOutOpen(out _hWaveOut, -1, ref format, IntPtr.Zero, IntPtr.Zero, 0);
                if (result != 0)
                {
                    _hWaveOut = IntPtr.Zero;
                    return;
                }

                for (int i = 0; i < BufferCount; i++)
                {
                    _bufferPtrs[i] = Marshal.AllocHGlobal(BufferSizeBytes);
                    _headers[i] = new WaveHdr
                    {
                        lpData = _bufferPtrs[i],
                        dwBufferLength = (uint)BufferSizeBytes,
                        dwFlags = 0
                    };
                    waveOutPrepareHeader(_hWaveOut, ref _headers[i], Marshal.SizeOf(typeof(WaveHdr)));
                }

                _isInitialized = true;
            }
            catch
            {
                _isInitialized = false;
            }
        }

        public void PlaySample(short sample)
        {
            if (!_isInitialized)
                return;

            _currentBuffer[_sampleIndex++] = Muted ? (short)0 : sample;
            if (_sampleIndex >= BufferSamples)
            {
                FlushBuffer();
                _sampleIndex = 0;
            }
        }

        private void FlushBuffer()
        {
            if (!_isInitialized || _hWaveOut == IntPtr.Zero)
                return;

            int id = _currentBufferId;
            Marshal.Copy(_currentBuffer, 0, _headers[id].lpData, BufferSamples);
            waveOutWrite(_hWaveOut, ref _headers[id], Marshal.SizeOf(typeof(WaveHdr)));
            _currentBufferId = (_currentBufferId + 1) % BufferCount;
        }

        public void Dispose()
        {
            if (_hWaveOut != IntPtr.Zero)
            {
                waveOutReset(_hWaveOut);
                for (int i = 0; i < BufferCount; i++)
                {
                    if (_headers[i].lpData != IntPtr.Zero)
                    {
                        waveOutUnprepareHeader(_hWaveOut, ref _headers[i], Marshal.SizeOf(typeof(WaveHdr)));
                        Marshal.FreeHGlobal(_bufferPtrs[i]);
                    }
                }
                waveOutClose(_hWaveOut);
                _hWaveOut = IntPtr.Zero;
            }
            _isInitialized = false;
        }
    }
}
