using RiftAccess.Audio;
using RiftAccess.Core;

namespace RiftAccess.Gameplay
{
    /// <summary>
    /// A short pattern at 120 beats per minute played with the player's current cue settings: each lane cue, then a
    /// click on the beat where the key would be pressed. Lets players hear the lead and lane sounds outside a song.
    /// </summary>
    internal static class CueDemo
    {
        private const float Beat = 0.5f;

        public static void Play()
        {
            CuePlayer.CancelAllPending();
            float lead = ModConfig.LeadBeats.Value;
            float start = 0.6f + (lead + ModConfig.CountIn.Value) * Beat;
            // (hit beat, lane): left, middle, right, middle, then left and right together.
            var hits = new[] { (0f, 0), (1f, 1), (2f, 2), (3f, 1), (4f, 0), (4f, 2) };
            float vol = ModConfig.CueVolume.Value / 100f;
            float width = ModConfig.PanWidth.Value / 100f;
            foreach (var (beat, lane) in hits)
            {
                string side = !ModConfig.LanePitch.Value ? "mid" : lane == 0 ? "left" : lane == 2 ? "right" : "mid";
                float pan = (lane - 1) * width;
                float hitTime = start + beat * Beat;
                CuePlayer.Play("tap_" + side, hitTime - lead * Beat, pan, vol);
                for (int k = 1; k <= ModConfig.CountIn.Value; k++)
                    CuePlayer.Play("tap_" + side, hitTime - (lead + k) * Beat, pan, vol * 0.5f);
                CuePlayer.Play("click", hitTime, pan, vol * 0.6f);
            }
        }
    }
}
