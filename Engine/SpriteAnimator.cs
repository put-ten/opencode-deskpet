namespace DeskPet.Engine;

public class SpriteAnimator
{
    public SpriteSheet Sheet { get; set; }
    public int CurrentFrame { get; set; }
    public int FrameDelay { get; set; }
    public bool IsPlaying { get; private set; }
    public bool Loop { get; set; }
    public event Action? AnimationComplete;

    private int _elapsed;
    private int _startFrame;
    private int _endFrame;

    public SpriteAnimator(SpriteSheet sheet, int frameDelay = 200, bool loop = true)
    {
        Sheet = sheet;
        FrameDelay = frameDelay;
        Loop = loop;
        CurrentFrame = 0;
        _startFrame = 0;
        _endFrame = sheet.FrameCount - 1;
    }

    public void Play(int startFrame = 0, int? endFrame = null)
    {
        _startFrame = startFrame;
        _endFrame = endFrame ?? (Sheet.FrameCount - 1);
        CurrentFrame = _startFrame;
        _elapsed = 0;
        IsPlaying = true;
    }

    public void Stop()
    {
        IsPlaying = false;
        CurrentFrame = _startFrame;
        _elapsed = 0;
    }

    public void Pause() => IsPlaying = false;

    public void Resume() => IsPlaying = true;

    public void Update(int deltaMs)
    {
        if (!IsPlaying) return;
        _elapsed += deltaMs;
        if (_elapsed < FrameDelay) return;
        _elapsed -= FrameDelay;
        CurrentFrame++;
        if (CurrentFrame > _endFrame)
        {
            if (Loop)
                CurrentFrame = _startFrame;
            else
            {
                CurrentFrame = _endFrame;
                IsPlaying = false;
                AnimationComplete?.Invoke();
            }
        }
    }
}
