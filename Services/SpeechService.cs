using System.Speech.Synthesis;

namespace NetNotifier.Services;

public class SpeechService : IDisposable
{
    private readonly SpeechSynthesizer _synth;

    public SpeechService()
    {
        _synth = new SpeechSynthesizer();
        _synth.Rate = -2;
        _synth.Volume = 100;
    }

    public void SpeakAsync(string text)
    {
        try
        {
            _synth.SpeakAsyncCancelAll();
            _synth.SpeakAsync(text);
        }
        catch
        {
            // Silent fail, matches old app behavior.
        }
    }

    public void Dispose() => _synth.Dispose();
}
