namespace godot_raytraced_audio;

public class AnimatedFloat
{
    float last;
    public float dest;
    bool ease;

    Stopwatch watch;
    int time;

    public AnimatedFloat(float initial, int time, bool ease = false)
    {
        last = dest = initial;
        watch = new();
        this.time = time;
        this.ease = ease;
    }

    public float GetNext() => dest;

    public float ProgressEase
    {
        get
        {
            var x = ProgressLinear;
            return x < 0.5f ? 4 * x * x * x : 1 - MathF.Pow(-2 * x + 2, 3) / 2;
        }
    }

    public float ProgressLinear
    {
        get
        {
            var x = watch.ElapsedMilliseconds / (float)time;
            return Math.Min(1, x);
        }
    }

    float CurrentProgress => ease ? ProgressEase : ProgressLinear;

    public float Value
    {
        get
        {
            return Lerp(last, dest, CurrentProgress);
        }
        set
        {
            if (dest == value)
                return;

            last = Value;
            dest = value;

            watch.Restart();
        }
    }

    public void Force()
    {
        last = dest;
    }

    public void SetTime(int time)
    {
        this.time = time;
    }

    internal static float Lerp(float a, float b, float c) => a + c * (b - a);
}
