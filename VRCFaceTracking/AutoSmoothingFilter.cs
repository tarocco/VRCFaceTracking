using System;

public struct AutoSmoothingFilter
{
    float _PriorValue1;
    float _PriorValue2;
    float _PriorRate;

    public void Initialize(float sample)
    {
        _PriorValue1 = sample;
        _PriorValue2 = sample;
        _PriorRate = 0f;
    }

    public static float LerpUnconstrained(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    public float Process(float sample, float speed, float decay)
    {
        var delta = sample - _PriorValue1;
        var rate = speed * Math.Abs(delta);
        _PriorRate = Math.Max(_PriorRate * decay, rate);
        var prior_rate_01 = Math.Min(Math.Max(0f, _PriorRate), 1f);
        _PriorValue1 = LerpUnconstrained(_PriorValue1, sample, prior_rate_01);
        _PriorValue2 = LerpUnconstrained(_PriorValue2, _PriorValue1, prior_rate_01);
        return _PriorValue2;
    }
}
