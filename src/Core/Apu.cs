using System;
using NesEmulator.Audio;

namespace NesEmulator.Core
{
    public class Apu
    {
        private static readonly byte[][] DutyTable = new byte[][]
        {
            new byte[] { 0, 1, 0, 0, 0, 0, 0, 0 }, // 12.5%
            new byte[] { 0, 1, 1, 0, 0, 0, 0, 0 }, // 25%
            new byte[] { 0, 1, 1, 1, 1, 0, 0, 0 }, // 50%
            new byte[] { 1, 0, 0, 1, 1, 1, 1, 1 }  // 75%
        };

        private static readonly byte[] TriangleTable = new byte[]
        {
            15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15
        };

        private static readonly ushort[] NoisePeriodTable = new ushort[]
        {
            4, 8, 16, 32, 64, 96, 128, 160, 202, 254, 380, 508, 762, 1016, 2034, 4068
        };

        private static readonly byte[] LengthTable = new byte[]
        {
            10, 254, 20, 2, 40, 4, 80, 6, 160, 8, 60, 10, 14, 12, 26, 14,
            12, 16, 24, 18, 48, 20, 96, 22, 192, 24, 72, 26, 16, 28, 32, 30
        };

        // Pulse 1
        private bool _p1Enabled;
        private byte _p1Duty;
        private byte _p1DutyPos;
        private ushort _p1Timer;
        private ushort _p1Period;
        private byte _p1Length;
        private bool _p1LengthHalt;
        private bool _p1ConstantVol;
        private byte _p1ParamVol;
        private byte _p1Decay;
        private byte _p1EnvDivider;
        private bool _p1EnvStart;
        private bool _p1SweepEnabled;
        private byte _p1SweepPeriod;
        private bool _p1SweepNegate;
        private byte _p1SweepShift;
        private byte _p1SweepDivider;
        private bool _p1SweepReload;

        // Pulse 2
        private bool _p2Enabled;
        private byte _p2Duty;
        private byte _p2DutyPos;
        private ushort _p2Timer;
        private ushort _p2Period;
        private byte _p2Length;
        private bool _p2LengthHalt;
        private bool _p2ConstantVol;
        private byte _p2ParamVol;
        private byte _p2Decay;
        private byte _p2EnvDivider;
        private bool _p2EnvStart;
        private bool _p2SweepEnabled;
        private byte _p2SweepPeriod;
        private bool _p2SweepNegate;
        private byte _p2SweepShift;
        private byte _p2SweepDivider;
        private bool _p2SweepReload;

        // Triangle
        private bool _triEnabled;
        private byte _triStep;
        private ushort _triTimer;
        private ushort _triPeriod;
        private byte _triLength;
        private bool _triControl;
        private byte _triLinearReload;
        private byte _triLinearCounter;
        private bool _triLinearReloadFlag;

        // Noise
        private bool _noiseEnabled;
        private ushort _noiseShift = 1;
        private ushort _noiseTimer;
        private ushort _noisePeriod = 4;
        private byte _noiseLength;
        private bool _noiseLengthHalt;
        private bool _noiseConstantVol;
        private byte _noiseParamVol;
        private byte _noiseDecay;
        private byte _noiseEnvDivider;
        private bool _noiseEnvStart;
        private bool _noiseMode;

        // Frame Sequencer
        private int _frameSeqStep;
        private int _frameSeqCycles;
        private bool _frameSeqMode5;
        private bool _frameIrqEnabled;
        private bool _frameIrq;

        // Timing & Downsampling
        private const int CpuClock = 1789773;
        private const int SampleRate = 44100;
        private int _sampleAccumulator;

        // DC High-pass filter
        private float _prevRaw;
        private float _filtered;

        public WaveOutPlayer Player { get; set; }

        public void Write(ushort addr, byte val)
        {
            switch (addr)
            {
                // Pulse 1
                case 0x4000:
                    _p1Duty = (byte)((val >> 6) & 3);
                    _p1LengthHalt = (val & 0x20) != 0;
                    _p1ConstantVol = (val & 0x10) != 0;
                    _p1ParamVol = (byte)(val & 0x0F);
                    break;
                case 0x4001:
                    _p1SweepEnabled = (val & 0x80) != 0;
                    _p1SweepPeriod = (byte)((val >> 4) & 7);
                    _p1SweepNegate = (val & 0x08) != 0;
                    _p1SweepShift = (byte)(val & 7);
                    _p1SweepReload = true;
                    break;
                case 0x4002:
                    _p1Period = (ushort)((_p1Period & 0x0700) | val);
                    break;
                case 0x4003:
                    _p1Period = (ushort)((_p1Period & 0x00FF) | ((val & 0x07) << 8));
                    if (_p1Enabled)
                        _p1Length = LengthTable[(val >> 3) & 0x1F];
                    _p1DutyPos = 0;
                    _p1EnvStart = true;
                    break;

                // Pulse 2
                case 0x4004:
                    _p2Duty = (byte)((val >> 6) & 3);
                    _p2LengthHalt = (val & 0x20) != 0;
                    _p2ConstantVol = (val & 0x10) != 0;
                    _p2ParamVol = (byte)(val & 0x0F);
                    break;
                case 0x4005:
                    _p2SweepEnabled = (val & 0x80) != 0;
                    _p2SweepPeriod = (byte)((val >> 4) & 7);
                    _p2SweepNegate = (val & 0x08) != 0;
                    _p2SweepShift = (byte)(val & 7);
                    _p2SweepReload = true;
                    break;
                case 0x4006:
                    _p2Period = (ushort)((_p2Period & 0x0700) | val);
                    break;
                case 0x4007:
                    _p2Period = (ushort)((_p2Period & 0x00FF) | ((val & 0x07) << 8));
                    if (_p2Enabled)
                        _p2Length = LengthTable[(val >> 3) & 0x1F];
                    _p2DutyPos = 0;
                    _p2EnvStart = true;
                    break;

                // Triangle
                case 0x4008:
                    _triControl = (val & 0x80) != 0;
                    _triLinearReload = (byte)(val & 0x7F);
                    break;
                case 0x400A:
                    _triPeriod = (ushort)((_triPeriod & 0x0700) | val);
                    break;
                case 0x400B:
                    _triPeriod = (ushort)((_triPeriod & 0x00FF) | ((val & 0x07) << 8));
                    if (_triEnabled)
                        _triLength = LengthTable[(val >> 3) & 0x1F];
                    _triLinearReloadFlag = true;
                    break;

                // Noise
                case 0x400C:
                    _noiseLengthHalt = (val & 0x20) != 0;
                    _noiseConstantVol = (val & 0x10) != 0;
                    _noiseParamVol = (byte)(val & 0x0F);
                    break;
                case 0x400E:
                    _noiseMode = (val & 0x80) != 0;
                    _noisePeriod = NoisePeriodTable[val & 0x0F];
                    break;
                case 0x400F:
                    if (_noiseEnabled)
                        _noiseLength = LengthTable[(val >> 3) & 0x1F];
                    _noiseEnvStart = true;
                    break;

                // Status
                case 0x4015:
                    _p1Enabled = (val & 1) != 0;
                    if (!_p1Enabled) _p1Length = 0;

                    _p2Enabled = (val & 2) != 0;
                    if (!_p2Enabled) _p2Length = 0;

                    _triEnabled = (val & 4) != 0;
                    if (!_triEnabled) _triLength = 0;

                    _noiseEnabled = (val & 8) != 0;
                    if (!_noiseEnabled) _noiseLength = 0;
                    break;

                // Frame Counter
                case 0x4017:
                    _frameSeqMode5 = (val & 0x80) != 0;
                    _frameIrqEnabled = (val & 0x40) == 0;
                    if (!_frameIrqEnabled) _frameIrq = false;
                    _frameSeqCycles = 0;
                    _frameSeqStep = 0;
                    if (_frameSeqMode5)
                    {
                        ClockQuarterFrame();
                        ClockHalfFrame();
                    }
                    break;
            }
        }

        public byte Read(ushort addr)
        {
            if (addr == 0x4015)
            {
                byte res = 0;
                if (_p1Length > 0) res |= 1;
                if (_p2Length > 0) res |= 2;
                if (_triLength > 0) res |= 4;
                if (_noiseLength > 0) res |= 8;
                if (_frameIrq) res |= 0x40;
                _frameIrq = false;
                return res;
            }
            return 0;
        }

        public void Clock(int cpuCycles)
        {
            for (int i = 0; i < cpuCycles; i++)
            {
                // Frame sequencer (~240 Hz)
                _frameSeqCycles++;
                if (_frameSeqCycles >= 7457)
                {
                    _frameSeqCycles = 0;
                    ClockFrameSequencer();
                }

                // Pulse 1 timer (clocks every 2 CPU cycles)
                if (_p1Timer == 0)
                {
                    _p1Timer = (ushort)(_p1Period * 2 + 1);
                    _p1DutyPos = (byte)((_p1DutyPos + 1) & 7);
                }
                else
                {
                    _p1Timer--;
                }

                // Pulse 2 timer
                if (_p2Timer == 0)
                {
                    _p2Timer = (ushort)(_p2Period * 2 + 1);
                    _p2DutyPos = (byte)((_p2DutyPos + 1) & 7);
                }
                else
                {
                    _p2Timer--;
                }

                // Triangle timer (clocks every CPU cycle)
                if (_triTimer == 0)
                {
                    _triTimer = _triPeriod;
                    if (_triLength > 0 && _triLinearCounter > 0 && _triPeriod > 2)
                    {
                        _triStep = (byte)((_triStep + 1) & 31);
                    }
                }
                else
                {
                    _triTimer--;
                }

                // Noise timer
                if (_noiseTimer == 0)
                {
                    _noiseTimer = _noisePeriod;
                    ushort feedback = (ushort)((_noiseShift & 1) ^ ((_noiseShift >> (_noiseMode ? 6 : 1)) & 1));
                    _noiseShift = (ushort)((_noiseShift >> 1) | (feedback << 14));
                }
                else
                {
                    _noiseTimer--;
                }
            }

            // Downsampling to 44.1kHz
            _sampleAccumulator += cpuCycles * SampleRate;
            while (_sampleAccumulator >= CpuClock)
            {
                _sampleAccumulator -= CpuClock;

                if (Player != null)
                {
                    short sample = MixSample();
                    Player.PlaySample(sample);
                }
            }
        }

        private void ClockFrameSequencer()
        {
            if (!_frameSeqMode5)
            {
                // 4-step sequence
                switch (_frameSeqStep)
                {
                    case 0:
                        ClockQuarterFrame();
                        break;
                    case 1:
                        ClockQuarterFrame();
                        ClockHalfFrame();
                        break;
                    case 2:
                        ClockQuarterFrame();
                        break;
                    case 3:
                        ClockQuarterFrame();
                        ClockHalfFrame();
                        if (_frameIrqEnabled) _frameIrq = true;
                        break;
                }
                _frameSeqStep = (_frameSeqStep + 1) & 3;
            }
            else
            {
                // 5-step sequence
                switch (_frameSeqStep)
                {
                    case 0:
                        ClockQuarterFrame();
                        break;
                    case 1:
                        ClockQuarterFrame();
                        ClockHalfFrame();
                        break;
                    case 2:
                        ClockQuarterFrame();
                        break;
                    case 3:
                        // No-op
                        break;
                    case 4:
                        ClockQuarterFrame();
                        ClockHalfFrame();
                        break;
                }
                _frameSeqStep = (_frameSeqStep + 1) % 5;
            }
        }

        private void ClockQuarterFrame()
        {
            // Clock Envelopes
            ClockEnvelope(ref _p1EnvStart, ref _p1EnvDivider, _p1ParamVol, ref _p1Decay, _p1LengthHalt);
            ClockEnvelope(ref _p2EnvStart, ref _p2EnvDivider, _p2ParamVol, ref _p2Decay, _p2LengthHalt);
            ClockEnvelope(ref _noiseEnvStart, ref _noiseEnvDivider, _noiseParamVol, ref _noiseDecay, _noiseLengthHalt);

            // Triangle Linear Counter
            if (_triLinearReloadFlag)
            {
                _triLinearCounter = _triLinearReload;
            }
            else if (_triLinearCounter > 0)
            {
                _triLinearCounter--;
            }

            if (!_triControl)
            {
                _triLinearReloadFlag = false;
            }
        }

        private void ClockHalfFrame()
        {
            // Clock Length Counters
            if (!_p1LengthHalt && _p1Length > 0) _p1Length--;
            if (!_p2LengthHalt && _p2Length > 0) _p2Length--;
            if (!_triControl && _triLength > 0) _triLength--;
            if (!_noiseLengthHalt && _noiseLength > 0) _noiseLength--;

            // Pulse 1 Sweep
            ClockSweep(ref _p1Period, _p1SweepEnabled, _p1SweepPeriod, _p1SweepNegate, _p1SweepShift, ref _p1SweepDivider, ref _p1SweepReload, true);

            // Pulse 2 Sweep
            ClockSweep(ref _p2Period, _p2SweepEnabled, _p2SweepPeriod, _p2SweepNegate, _p2SweepShift, ref _p2SweepDivider, ref _p2SweepReload, false);
        }

        private void ClockEnvelope(ref bool start, ref byte divider, byte paramVol, ref byte decay, bool loop)
        {
            if (start)
            {
                start = false;
                decay = 15;
                divider = paramVol;
            }
            else
            {
                if (divider == 0)
                {
                    divider = paramVol;
                    if (decay > 0)
                    {
                        decay--;
                    }
                    else if (loop)
                    {
                        decay = 15;
                    }
                }
                else
                {
                    divider--;
                }
            }
        }

        private void ClockSweep(ref ushort period, bool enabled, byte sweepPeriod, bool negate, byte shift, ref byte divider, ref bool reload, bool isChannel1)
        {
            if (divider == 0 && enabled && shift > 0 && period >= 8 && period <= 0x7FF)
            {
                int change = period >> shift;
                if (negate)
                {
                    period = (ushort)(period - change - (isChannel1 ? 1 : 0));
                }
                else
                {
                    period = (ushort)(period + change);
                }
            }

            if (divider == 0 || reload)
            {
                divider = sweepPeriod;
                reload = false;
            }
            else
            {
                divider--;
            }
        }

        private short MixSample()
        {
            int p1 = 0;
            if (_p1Enabled && _p1Length > 0 && _p1Period >= 8 && _p1Period <= 0x7FF)
            {
                if (DutyTable[_p1Duty][_p1DutyPos] == 1)
                {
                    p1 = _p1ConstantVol ? _p1ParamVol : _p1Decay;
                }
            }

            int p2 = 0;
            if (_p2Enabled && _p2Length > 0 && _p2Period >= 8 && _p2Period <= 0x7FF)
            {
                if (DutyTable[_p2Duty][_p2DutyPos] == 1)
                {
                    p2 = _p2ConstantVol ? _p2ParamVol : _p2Decay;
                }
            }

            int tri = 0;
            if (_triEnabled && _triLength > 0 && _triLinearCounter > 0 && _triPeriod > 2)
            {
                tri = TriangleTable[_triStep];
            }

            int noise = 0;
            if (_noiseEnabled && _noiseLength > 0 && (_noiseShift & 1) == 0)
            {
                noise = _noiseConstantVol ? _noiseParamVol : _noiseDecay;
            }

            // Authentic NES Non-linear Mixing
            float pulseOut = 0f;
            if (p1 + p2 > 0)
            {
                pulseOut = 95.88f / ((8128f / (p1 + p2)) + 100f);
            }

            float tndSum = (tri / 8227f) + (noise / 12241f);
            float tndOut = 0f;
            if (tndSum > 0f)
            {
                tndOut = 159.79f / ((1f / tndSum) + 100f);
            }

            float raw = (pulseOut + tndOut) * 2.2f;

            // High-pass DC Blocker Filter (eliminates pops, clicks, and DC buzzing)
            _filtered = _filtered * 0.995f + (raw - _prevRaw);
            _prevRaw = raw;

            int output = (int)(_filtered * 30000f);
            if (output > 32767) output = 32767;
            if (output < -32768) output = -32768;

            return (short)output;
        }

        public void Reset()
        {
            _p1Enabled = _p2Enabled = _triEnabled = _noiseEnabled = false;
            _p1Length = _p2Length = _triLength = _noiseLength = 0;
            _noiseShift = 1;
            _sampleAccumulator = 0;
            _filtered = 0f;
            _prevRaw = 0f;
        }
    }
}
